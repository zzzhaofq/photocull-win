using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PhotoCull.Models;

namespace PhotoCull.Views;

public partial class InspectorPanel : UserControl
{
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

        // Thumbnail
        if (photo.ThumbnailData != null)
        {
            try
            {
                var image = new BitmapImage();
                using var ms = new System.IO.MemoryStream(photo.ThumbnailData);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                ThumbImage.Source = image;
            }
            catch { ThumbImage.Source = null; }
        }
        else
        {
            ThumbImage.Source = null;
        }

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

        // Status
        StatusText.Text = photo.Status switch
        {
            CullStatus.Selected => "已选",
            CullStatus.Rejected => "已淘汰",
            _ => "未审核"
        };
        StatusText.Foreground = photo.Status switch
        {
            CullStatus.Selected => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
            CullStatus.Rejected => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)),
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158))
        };

        var stars = photo.Rating > 0 ? new string('★', photo.Rating) + new string('☆', 5 - photo.Rating) : "☆☆☆☆☆";
        RatingText.Text = stars;
    }
}
