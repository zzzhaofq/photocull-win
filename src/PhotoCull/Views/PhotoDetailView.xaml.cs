using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PhotoCull.Models;

namespace PhotoCull.Views;

public partial class PhotoDetailView : UserControl
{
    public event Action? CloseRequested;

    private bool _isDragging;
    private Point _lastPos;

    public PhotoDetailView()
    {
        InitializeComponent();
    }

    public void SetPhoto(Photo? photo)
    {
        if (photo?.ThumbnailData == null)
        {
            DetailImage.Source = null;
            return;
        }

        try
        {
            var image = new BitmapImage();
            using var ms = new System.IO.MemoryStream(photo.ThumbnailData);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            DetailImage.Source = image;
        }
        catch { DetailImage.Source = null; }

        // Reset transform
        ScaleXform.ScaleX = 1;
        ScaleXform.ScaleY = 1;
        TranslateXform.X = 0;
        TranslateXform.Y = 0;
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
