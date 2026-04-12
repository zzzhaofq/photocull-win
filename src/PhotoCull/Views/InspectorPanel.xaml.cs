using System.Windows.Controls;
using System.Windows.Media;
using PhotoCull.Helpers;
using PhotoCull.Models;

namespace PhotoCull.Views;

public partial class InspectorPanel : UserControl
{
    // Cached frozen brushes
    private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush(76, 175, 80);
    private static readonly SolidColorBrush RedBrush = CreateFrozenBrush(244, 67, 54);
    private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush(158, 158, 158);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public InspectorPanel()
    {
        InitializeComponent();
    }

    public void SetPhoto(Photo? photo)
    {
        if (photo == null)
        {
            Root.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        Root.Visibility = System.Windows.Visibility.Visible;

        // Use ThumbnailCache instead of creating new BitmapImage every time
        ThumbImage.Source = ThumbnailCache.Shared.Thumbnail(photo.Id.ToString(), photo.ThumbnailData);

        // File info
        FileNameText.Text = photo.FileName;
        FilePathText.Text = photo.FilePath;

        // EXIF
        var exif = photo.Exif;
        CameraText.Text = $"机身: {exif.CameraModel ?? "-"}";
        LensText.Text = $"镜头: {exif.LensModel ?? "-"}";
        ApertureText.Text = $"光圈: {(exif.Aperture.HasValue ? $"f/{exif.Aperture:F1}" : "-")}";
        ShutterText.Text = $"快门: {exif.ShutterSpeed ?? "-"}";
        IsoText.Text = $"ISO: {(exif.Iso.HasValue ? exif.Iso.ToString() : "-")}";
        FocalText.Text = $"焦距: {(exif.FocalLength.HasValue ? $"{exif.FocalLength:F0}mm" : "-")}";
        DateText.Text = $"日期: {(exif.CaptureDate.HasValue ? exif.CaptureDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-")}";

        // AI Score
        var score = photo.AiScore;
        if (score != null)
        {
            OverallText.Text = $"综合: {score.Overall:F1}";
            SharpnessText.Text = $"锐度: {score.Sharpness:F1}";
            ExposureText.Text = $"曝光: {score.Exposure:F1}";
            CompositionText.Text = $"构图: {score.Composition:F1}";
            FaceText.Text = $"人脸: {(score.FaceQuality.HasValue ? $"{score.FaceQuality:F1}" : "-")}";
        }
        else
        {
            OverallText.Text = "综合: -";
            SharpnessText.Text = "锐度: -";
            ExposureText.Text = "曝光: -";
            CompositionText.Text = "构图: -";
            FaceText.Text = "人脸: -";
        }

        // Status - use cached brushes
        StatusText.Text = photo.Status switch
        {
            CullStatus.Selected => "已选",
            CullStatus.Rejected => "已淘汰",
            _ => "未审核"
        };
        StatusText.Foreground = photo.Status switch
        {
            CullStatus.Selected => GreenBrush,
            CullStatus.Rejected => RedBrush,
            _ => GrayBrush
        };

        var stars = photo.Rating > 0 ? new string('★', photo.Rating) + new string('☆', 5 - photo.Rating) : "☆☆☆☆☆";
        RatingText.Text = stars;
    }
}
