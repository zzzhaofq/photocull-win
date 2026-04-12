using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoCull.Models;
using PhotoCull.Services;

namespace PhotoCull.ViewModels;

public partial class ExportViewModel : ObservableObject
{
    [ObservableProperty] private bool _exportToFolder = true;
    [ObservableProperty] private bool _exportXmp;
    [ObservableProperty] private string? _targetFolderPath;
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private double _exportProgress;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private int _exportedCount;
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private long _copiedBytes;
    [ObservableProperty] private string _currentFileName = string.Empty;
    [ObservableProperty] private bool _moveInsteadOfCopy;
    [ObservableProperty] private bool _exportFileList;
    [ObservableProperty] private int _minExportRating;
    [ObservableProperty] private string _defaultFolderName = string.Empty;

    private CullingSession? _session;

    // Cached computed lists to avoid repeated LINQ scans
    private List<Photo>? _cachedSelectedPhotos;
    private List<Photo>? _cachedRejectedPhotos;
    private List<Photo>? _cachedUnreviewedPhotos;
    private int[]? _cachedRatingCounts;

    private void InvalidateExportCaches()
    {
        _cachedSelectedPhotos = null;
        _cachedRejectedPhotos = null;
        _cachedUnreviewedPhotos = null;
        _cachedRatingCounts = null;
    }

    public CullingSession? Session => _session;

    public List<Photo> SelectedPhotos
    {
        get
        {
            _cachedSelectedPhotos ??= _session?.Photos.Where(p => p.Status == CullStatus.Selected).ToList() ?? new();
            return _cachedSelectedPhotos;
        }
    }

    public List<Photo> RejectedPhotos
    {
        get
        {
            _cachedRejectedPhotos ??= _session?.Photos.Where(p => p.Status == CullStatus.Rejected).ToList() ?? new();
            return _cachedRejectedPhotos;
        }
    }

    public List<Photo> UnreviewedPhotos
    {
        get
        {
            _cachedUnreviewedPhotos ??= _session?.Photos.Where(p => p.Status == CullStatus.Unreviewed).ToList() ?? new();
            return _cachedUnreviewedPhotos;
        }
    }

    public int TotalPhotos => _session?.TotalCount ?? 0;

    public List<Photo> FilteredSelectedPhotos
    {
        get
        {
            if (MinExportRating > 0)
                return SelectedPhotos.Where(p => p.Rating >= MinExportRating).ToList();
            return SelectedPhotos;
        }
    }

    public int[] RatingCounts
    {
        get
        {
            if (_cachedRatingCounts == null)
            {
                var counts = new int[6];
                foreach (var photo in SelectedPhotos)
                {
                    var r = Math.Max(0, Math.Min(5, photo.Rating));
                    counts[r]++;
                }
                _cachedRatingCounts = counts;
            }
            return _cachedRatingCounts;
        }
    }

    public void RejectPhoto(Photo photo) { photo.Status = CullStatus.Rejected; InvalidateExportCaches(); }
    public void RestorePhoto(Photo photo) { photo.Status = CullStatus.Selected; InvalidateExportCaches(); }

    public void LoadSession(CullingSession session)
    {
        _session = session;
        InvalidateExportCaches();
        IsComplete = false;
        ErrorMessage = null;
        ExportProgress = 0;
        TotalBytes = 0;
        CopiedBytes = 0;
        DefaultFolderName = $"{DateTime.Now:yyyyMMdd}-photoCull";
        OnPropertyChanged(nameof(Session));
        OnPropertyChanged(nameof(SelectedPhotos));
        OnPropertyChanged(nameof(RejectedPhotos));
        OnPropertyChanged(nameof(UnreviewedPhotos));
        OnPropertyChanged(nameof(TotalPhotos));
        OnPropertyChanged(nameof(FilteredSelectedPhotos));
        OnPropertyChanged(nameof(RatingCounts));
    }

    private string UniqueDestination(string fileName, string directory)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var dest = Path.Combine(directory, fileName);
        int counter = 1;
        while (File.Exists(dest))
        {
            var newName = string.IsNullOrEmpty(ext)
                ? $"{baseName} ({counter})"
                : $"{baseName} ({counter}){ext}";
            dest = Path.Combine(directory, newName);
            counter++;
        }
        return dest;
    }

    public async Task ExportAsync()
    {
        if (_session == null) return;

        IsExporting = true;
        IsComplete = false;
        ErrorMessage = null;
        ExportProgress = 0;
        ExportedCount = 0;
        TotalBytes = 0;
        CopiedBytes = 0;
        CurrentFileName = string.Empty;

        var selected = FilteredSelectedPhotos;

        try
        {
            if (ExportToFolder && !string.IsNullOrEmpty(TargetFolderPath))
            {
                Directory.CreateDirectory(TargetFolderPath);

                // Calculate total bytes
                long calculatedTotal = 0;
                foreach (var photo in selected)
                {
                    try { calculatedTotal += new FileInfo(photo.FilePath).Length; }
                    catch { }
                }
                TotalBytes = calculatedTotal;

                // Check disk space
                var driveInfo = new DriveInfo(Path.GetPathRoot(TargetFolderPath)!);
                if (driveInfo.AvailableFreeSpace < TotalBytes)
                {
                    ErrorMessage = $"目标磁盘空间不足（需要 {TotalBytes / 1_000_000} MB）";
                    IsExporting = false;
                    return;
                }

                var exportedFileNames = new List<string>();

                for (int i = 0; i < selected.Count; i++)
                {
                    var photo = selected[i];
                    CurrentFileName = photo.FileName;
                    var source = photo.FilePath;
                    var dest = UniqueDestination(photo.FileName, TargetFolderPath);

                    await Task.Run(() =>
                    {
                        if (MoveInsteadOfCopy)
                            File.Move(source, dest);
                        else
                            File.Copy(source, dest);
                    });

                    exportedFileNames.Add(Path.GetFileName(dest));
                    try { CopiedBytes += new FileInfo(dest).Length; }
                    catch { }
                    ExportedCount = i + 1;
                    ExportProgress = (double)ExportedCount / selected.Count;
                }

                // Write file list if requested
                if (ExportFileList)
                {
                    var listContent = string.Join("\n", exportedFileNames);
                    var listPath = Path.Combine(TargetFolderPath, "file_list.txt");
                    await File.WriteAllTextAsync(listPath, listContent);
                }
            }

            if (ExportXmp && !string.IsNullOrEmpty(TargetFolderPath))
            {
                var xmpDir = Path.Combine(TargetFolderPath, "XMP");
                await Task.Run(() =>
                    XmpExporter.ExportSession(_session.Photos, xmpDir));
            }

            _session.IsCompleted = true;
            IsComplete = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"导出失败: {ex.Message}";
        }

        IsExporting = false;
    }
}
