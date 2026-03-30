using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoCull.Data;
using PhotoCull.Models;
using PhotoCull.Services;
using PhotoCull.Services.LibRaw;

namespace PhotoCull.ViewModels;

public partial class ImportViewModel : ObservableObject
{
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private bool _isCancelled;
    [ObservableProperty] private double _previewProgress;
    [ObservableProperty] private double _aiProgress;
    [ObservableProperty] private double _groupingProgress;
    [ObservableProperty] private int _totalFiles;
    [ObservableProperty] private int _skippedFiles;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private CullingSession? _completedSession;
    [ObservableProperty] private List<string> _failedFiles = new();

    private readonly AiScorer _aiScorer = new();
    private CancellationTokenSource? _cts;

    // Throttle UI progress updates to avoid flooding the dispatcher
    private DateTime _lastPreviewProgressUpdate = DateTime.MinValue;
    private DateTime _lastAiProgressUpdate = DateTime.MinValue;
    private static readonly TimeSpan ProgressThrottle = TimeSpan.FromMilliseconds(50);

    public (List<string> rawPaths, int skippedCount) ScanForRawFiles(string folderPath)
    {
        var rawPaths = new List<string>();
        int skipped = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                if (RawPreviewExtractor.IsSupportedFile(file))
                    rawPaths.Add(file);
                else
                    skipped++;
            }
        }
        catch
        {
            // ignore access errors
        }

        return (rawPaths, skipped);
    }

    public void CancelImport()
    {
        IsCancelled = true;
        _cts?.Cancel();
    }

    public async Task ImportFolderAsync(string folderPath, PhotoCullDbContext db)
    {
        IsImporting = true;
        IsCancelled = false;
        IsComplete = false;
        ErrorMessage = null;
        PreviewProgress = 0;
        AiProgress = 0;
        GroupingProgress = 0;
        SkippedFiles = 0;
        FailedFiles = new List<string>();
        _cts = new CancellationTokenSource();

        // Scan files on background thread to avoid blocking UI
        var (rawPaths, skipped) = await Task.Run(() => ScanForRawFiles(folderPath)).ConfigureAwait(false);

        // Update UI on dispatcher (non-blocking)
        UpdateOnDispatcher(() =>
        {
            TotalFiles = rawPaths.Count;
            SkippedFiles = skipped;
        });

        if (rawPaths.Count == 0)
        {
            UpdateOnDispatcher(() =>
            {
                ErrorMessage = "文件夹中没有找到 RAW 文件";
                IsImporting = false;
            });
            return;
        }

        var session = new CullingSession(folderPath) { TotalCount = rawPaths.Count };
        db.CullingSessions.Add(session);

        // Extract previews (already parallel internally)
        var previewResults = await RawPreviewExtractor.ExtractPreviewsAsync(
            rawPaths, 1024, 8,
            (done, total) =>
            {
                var now = DateTime.UtcNow;
                if (now - _lastPreviewProgressUpdate < ProgressThrottle && done < total) return;
                _lastPreviewProgressUpdate = now;
                UpdateOnDispatcher(() => PreviewProgress = (double)done / total);
            },
            _cts.Token).ConfigureAwait(false);

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // Read EXIF in parallel on background threads
        var exifConcurrency = Math.Max(4, Environment.ProcessorCount);
        var exifResults = new PhotoExif[rawPaths.Count];
        using (var exifSemaphore = new SemaphoreSlim(exifConcurrency))
        {
            var exifTasks = rawPaths.Select(async (path, index) =>
            {
                await exifSemaphore.WaitAsync(_cts.Token).ConfigureAwait(false);
                try
                {
                    exifResults[index] = await Task.Run(() => ExifReader.ReadExif(path), _cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    exifSemaphore.Release();
                }
            });
            await Task.WhenAll(exifTasks).ConfigureAwait(false);
        }

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // Create photos (fast, just object creation)
        var photos = new List<Photo>(rawPaths.Count);
        var failed = new List<string>();
        for (int i = 0; i < rawPaths.Count; i++)
        {
            var path = rawPaths[i];
            previewResults.TryGetValue(path, out var previewData);
            if (previewData == null)
                failed.Add(Path.GetFileName(path));

            var photo = new Photo(path, Path.GetFileName(path), exifResults[i])
            {
                ThumbnailData = previewData,
                SessionId = session.Id
            };
            photos.Add(photo);
        }

        // Batch add all photos to EF context at once
        db.Photos.AddRange(photos);

        UpdateOnDispatcher(() => FailedFiles = failed);

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // AI scoring (parallel, matching Mac version's concurrent queue)
        var aiConcurrency = Math.Max(4, Environment.ProcessorCount);
        using var aiSemaphore = new SemaphoreSlim(aiConcurrency);
        var aiCompleted = 0;
        var aiTotal = photos.Count;

        var aiTasks = photos.Select(async photo =>
        {
            if (IsCancelled) return;
            await aiSemaphore.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                if (IsCancelled) return;
                if (photo.ThumbnailData != null)
                {
                    photo.AiScore = await Task.Run(() => _aiScorer.Score(photo.ThumbnailData), _cts.Token).ConfigureAwait(false);
                }
                var done = Interlocked.Increment(ref aiCompleted);
                var now = DateTime.UtcNow;
                if (now - _lastAiProgressUpdate >= ProgressThrottle || done >= aiTotal)
                {
                    _lastAiProgressUpdate = now;
                    UpdateOnDispatcher(() => AiProgress = (double)done / aiTotal);
                }
            }
            finally
            {
                aiSemaphore.Release();
            }
        });

        await Task.WhenAll(aiTasks).ConfigureAwait(false);

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // Auto-reject bottom 20%
        var scored = photos.Where(p => p.AiScore != null)
            .OrderBy(p => p.AiScore!.Overall)
            .ToList();
        var rejectCount = Math.Max(0, (int)(scored.Count * 0.2));
        foreach (var photo in scored.Take(rejectCount))
            photo.Status = CullStatus.Rejected;

        // Grouping
        UpdateOnDispatcher(() => GroupingProgress = 0.1);
        var groups = await PhotoGrouper.GroupPhotos(photos).ConfigureAwait(false);
        UpdateOnDispatcher(() => GroupingProgress = 0.7);
        PhotoGrouper.AssignRecommendations(groups);
        UpdateOnDispatcher(() => GroupingProgress = 0.9);

        foreach (var group in groups)
        {
            group.SessionId = session.Id;
            foreach (var photo in group.Photos)
                photo.GroupId = group.Id;
        }

        // Batch add all groups
        db.PhotoGroups.AddRange(groups);

        UpdateOnDispatcher(() => GroupingProgress = 1.0);
        await db.SaveChangesAsync(_cts.Token).ConfigureAwait(false);

        UpdateOnDispatcher(() =>
        {
            IsImporting = false;
            CompletedSession = session;
            IsComplete = true;
        });
    }

    private async Task CleanupSession(PhotoCullDbContext db, CullingSession session)
    {
        db.CullingSessions.Remove(session);
        await db.SaveChangesAsync().ConfigureAwait(false);
        UpdateOnDispatcher(() => IsImporting = false);
    }

    /// <summary>
    /// Non-blocking UI update via BeginInvoke (avoids deadlocks and UI freezes).
    /// </summary>
    private static void UpdateOnDispatcher(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }
        dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
    }
}
