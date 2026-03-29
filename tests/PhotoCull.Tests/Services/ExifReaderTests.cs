using PhotoCull.Services;
using Xunit;

namespace PhotoCull.Tests.Services;

public class ExifReaderTests
{
    [Fact]
    public void ReadExifFromNonexistentFile()
    {
        var exif = ExifReader.ReadExif(@"C:\nonexistent\photo.RAF");
        Assert.Null(exif.Aperture);
        Assert.Null(exif.ShutterSpeed);
        Assert.Null(exif.Iso);
        Assert.Null(exif.CaptureDate);
    }

    [Fact]
    public void ParseExifDateValid()
    {
        var date = ExifReader.ParseExifDate("2024:03:11 14:30:00");
        Assert.NotNull(date);
        Assert.Equal(2024, date.Value.Year);
        Assert.Equal(3, date.Value.Month);
        Assert.Equal(11, date.Value.Day);
    }

    [Fact]
    public void ParseExifDateInvalid()
    {
        var date = ExifReader.ParseExifDate("not-a-date");
        Assert.Null(date);
    }

    [Fact]
    public void FormatShutterSpeedFast()
    {
        var result = ExifReader.FormatShutterSpeed(1.0 / 500.0);
        Assert.Equal("1/500", result);
    }

    [Fact]
    public void FormatShutterSpeedSlow()
    {
        var result = ExifReader.FormatShutterSpeed(2.0);
        Assert.Equal("2.0s", result);
    }
}
