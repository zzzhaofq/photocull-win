using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PhotoCull.Helpers;
using PhotoCull.Models;
using PhotoCull.Services.LibRaw;

namespace PhotoCull.Views;

public partial class PhotoDetailView : UserControl
{
    public event Action? CloseRequested;

    private bool _isDragging;
    private Point _lastPos;
    private CancellationTokenSource? _hiResCts;

    public PhotoDetailView()
    {
        InitializeComponent();
    }

    public void SetPhoto(Photo? photo)
    {
        // Cancel any pending hi-res load
        _hiResCts?.Cancel();
        _hiResCts?.Dispose();
        _hiResCts = null;

        if (photo?.ThumbnailData == null)
        {
            DetailImage.Source = null;
            return;
        }

        // Show thumbnail immediately
        try
        {
            var thumbImage = ThumbnailCache.Shared.Thumbnail(photo.Id, photo.ThumbnailData);
            if (thumbImage != null)
            {
                DetailImage.Source = thumbImage;
            }
            else
            {
                var image = new BitmapImage();
                using var ms = new MemoryStream(photo.ThumbnailData);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                DetailImage.Source = image;
            }
        }
        catch { DetailImage.Source = null; }

        // Reset transform
        ScaleXform.ScaleX = 1;
        ScaleXform.ScaleY = 1;
        TranslateXform.X = 0;
        TranslateXform.Y = 0;

        // Async load 2560px hi-res preview
        var cts = new CancellationTokenSource();
        _hiResCts = cts;
        var photoId = photo.Id;
        var filePath = photo.FilePath;

        _ = LoadHiResAsync(photoId, filePath, cts.Token);
    }

    private async Task LoadHiResAsync(string photoId, string filePath, CancellationToken ct)
    {
        try
        {
            // Check cache first
            var cached = ThumbnailCache.Shared.GetHiRes(photoId);
            if (cached != null)
            {
                if (!ct.IsCancellationRequested)
                    DetailImage.Source = cached;
                return;
            }

            // Load on background thread
            var hiResData = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return RawPreviewExtractor.LoadHighResPreview(filePath, 2560);
            }, ct);

            if (ct.IsCancellationRequested) return;

            var hiResImage = ThumbnailCache.Shared.HiRes(photoId, hiResData);
            if (hiResImage != null && !ct.IsCancellationRequested)
            {
                DetailImage.Source = hiResImage;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when switching photos quickly
        }
        catch
        {
            // Keep thumbnail on failure
        }
    }

    /// <summary>
    /// Preload hi-res previews for neighboring photos (call with ±1 neighbors).
    /// </summary>
    public static void PreloadHiRes(IReadOnlyList<Photo> photos, int currentIndex)
    {
        var indices = new[] { currentIndex - 1, currentIndex + 1 };
        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= photos.Count) continue;
            var photo = photos[idx];
            if (photo.FilePath == null) continue;

            // Skip if already cached
            if (ThumbnailCache.Shared.GetHiRes(photo.Id) != null) continue;

            var id = photo.Id;
            var path = photo.FilePath;
            _ = Task.Run(() =>
            {
                var data = RawPreviewExtractor.LoadHighResPreview(path, 2560);
                if (data != null)
                {
                    // Create BitmapImage on a background thread then freeze
                    var image = new BitmapImage();
                    using var ms = new MemoryStream(data);
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                    ThumbnailCache.Shared.SetHiRes(image, id);
                }
            });
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = e.Delta > 0 ? 1.2 : 1 / 1.2;
        ScaleXform.ScaleX *= delta;
        ScaleXform.ScaleY *= delta;

        // Clamp zoom
        ScaleXform.ScaleX = Math.Max(0.1, Math.Min(10, ScaleXform.ScaleX));
        ScaleXform.ScaleY = Math.Max(0.1, Math.Min(10, ScaleXform.ScaleY));
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastPos = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(this);
        TranslateXform.X += pos.X - _lastPos.X;
        TranslateXform.Y += pos.Y - _lastPos.Y;
        _lastPos = pos;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }
}
