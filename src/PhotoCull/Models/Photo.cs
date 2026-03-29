namespace PhotoCull.Models;

public class PhotoExif
{
    public double? Aperture { get; set; }
    public string? ShutterSpeed { get; set; }
    public int? Iso { get; set; }
    public double? FocalLength { get; set; }
    public DateTime? CaptureDate { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
}

public class AiScore
{
    public double Overall { get; set; }
    public double Sharpness { get; set; }
    public double Exposure { get; set; }
    public double Composition { get; set; }
    public double? FaceQuality { get; set; }
}

public class Photo
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public byte[]? ThumbnailData { get; set; }

    // EXIF stored as JSON column
    public PhotoExif Exif { get; set; } = new();

    // AI Score stored as JSON column
    public AiScore? AiScore { get; set; }

    public CullStatus Status { get; set; } = CullStatus.Unreviewed;
    public int Rating { get; set; }

    public Guid? GroupId { get; set; }
    public PhotoGroup? Group { get; set; }

    public Guid? SessionId { get; set; }
    public CullingSession? Session { get; set; }

    public DateTime CreatedAt { get; set; }

    public Photo()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public Photo(string filePath, string fileName, PhotoExif? exif = null) : this()
    {
        FilePath = filePath;
        FileName = fileName;
        if (exif != null) Exif = exif;
    }
}
