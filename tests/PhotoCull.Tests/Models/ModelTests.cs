using PhotoCull.Models;
using Xunit;

namespace PhotoCull.Tests.Models;

public class ModelTests
{
    [Fact]
    public void PhotoInitialization()
    {
        var photo = new Photo("/test/DSC_001.RAF", "DSC_001.RAF");
        Assert.Equal(CullStatus.Unreviewed, photo.Status);
        Assert.Equal("DSC_001.RAF", photo.FileName);
        Assert.Null(photo.ThumbnailData);
        Assert.Null(photo.AiScore);
    }

    [Fact]
    public void PhotoGroupInitialization()
    {
        var group = new PhotoGroup(GroupType.Burst);
        Assert.Equal(GroupType.Burst, group.GroupType);
        Assert.Empty(group.Photos);
        Assert.False(group.IsReviewed);
        Assert.Empty(group.SelectedPhotoIds);
    }

    [Fact]
    public void CullingSessionInitialization()
    {
        var session = new CullingSession("/test/photos");
        Assert.Equal("/test/photos", session.FolderPath);
        Assert.Equal(Round.QuickCull, session.CurrentRound);
        Assert.Equal(0, session.TotalCount);
        Assert.Empty(session.Photos);
        Assert.Empty(session.Groups);
    }

    [Fact]
    public void PhotoExifSerializable()
    {
        var exif = new PhotoExif
        {
            Aperture = 2.8,
            ShutterSpeed = "1/500",
            Iso = 400,
            FocalLength = 56.0,
            CaptureDate = DateTime.UtcNow,
            CameraModel = "X-T5",
            LensModel = "XF56mmF1.2"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(exif);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<PhotoExif>(json);
        Assert.NotNull(decoded);
        Assert.Equal(exif.Aperture, decoded.Aperture);
        Assert.Equal(exif.ShutterSpeed, decoded.ShutterSpeed);
        Assert.Equal(exif.Iso, decoded.Iso);
        Assert.Equal(exif.FocalLength, decoded.FocalLength);
        Assert.Equal(exif.CameraModel, decoded.CameraModel);
        Assert.Equal(exif.LensModel, decoded.LensModel);
    }

    [Fact]
    public void AiScoreSerializable()
    {
        var score = new AiScore { Overall = 85.0, Sharpness = 92.0, Exposure = 78.0, Composition = 80.0, FaceQuality = 70.0 };
        var json = System.Text.Json.JsonSerializer.Serialize(score);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<AiScore>(json);
        Assert.NotNull(decoded);
        Assert.Equal(score.Overall, decoded.Overall);
        Assert.Equal(score.Sharpness, decoded.Sharpness);
        Assert.Equal(score.Exposure, decoded.Exposure);
        Assert.Equal(score.Composition, decoded.Composition);
        Assert.Equal(score.FaceQuality, decoded.FaceQuality);
    }
}
