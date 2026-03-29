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

        var (rawPaths, skipped) = ScanForRawFiles(folderPath);
        TotalFiles = rawPaths.Count;
        SkippedFiles = skipped;

        if (rawPaths.Count == 0)
        {
            ErrorMessage = "文件夹中没有找到 RAW 文件";
            IsImporting = false;
            return;
        }

        var session = new CullingSession(folderPath) { TotalCount = rawPaths.Count };
        db.CullingSessions.Add(session);

        // Extract previews
        var previewResults = await RawPreviewExtractor.ExtractPreviewsAsync(
            rawPaths, 1024, 8,
            (done, total) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    PreviewProgress = (double)done / total);
            });

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // Create photos
        var photos = new List<Photo>();
        var failed = new List<string>();
        foreach (var path in rawPaths)
        {
            previewResults.TryGetValue(path, out var previewData);
            if (previewData == null)
                failed.Add(Path.GetFileName(path));

            var exif = Services.ExifReader.ReadExif(path);
            var photo = new Photo(path, Path.GetFileName(path), exif)
            {
                ThumbnailData = previewData,
                SessionId = session.Id
            };
            photos.Add(photo);
            db.Photos.Add(photo);
        }
        FailedFiles = failed;

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // AI scoring
        for (int i = 0; i < photos.Count; i++)
        {
            if (IsCancelled) { await CleanupSession(db, session); return; }

            var photo = photos[i];
            if (photo.ThumbnailData != null)
            {
                photo.AiScore = await Task.Run(() => _aiScorer.Score(photo.ThumbnailData));
            }
            AiProgress = (double)(i + 1) / photos.Count;
        }

        if (IsCancelled) { await CleanupSession(db, session); return; }

        // Auto-reject bottom 20%
        var scored = photos.Where(p => p.AiScore != null)
            .OrderBy(p => p.AiScore!.Overall)
            .ToList();
        var rejectCount = Math.Max(0, (int)(scored.Count * 0.2));
        foreach (var photo in scored.Take(rejectCount))
            photo.Status = CullStatus.Rejected;

        // Grouping
        GroupingProgress = 0.1;
        var groups = await PhotoGrouper.GroupPhotos(photos);
        GroupingProgress = 0.7;
        PhotoGrouper.AssignRecommendations(groups);
        GroupingProgress = 0.9;

        foreach (var group in groups)
        {
            group.SessionId = session.Id;
            foreach (var photo in group.Photos)
                photo.GroupId = group.Id;
            db.PhotoGroups.Add(group);
        }

        GroupingProgress = 1.0;
        await db.SaveChangesAsync();

        IsImporting = false;
        CompletedSession = session;
        IsComplete = true;
    }

    private async Task CleanupSession(PhotoCullDbContext db, CullingSession session)
    {
        db.CullingSessions.Remove(session);
        await db.SaveChangesAsync();
        IsImporting = false;
    }
}
