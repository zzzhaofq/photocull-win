using PhotoCull.Services.LibRaw;
using Xunit;

namespace PhotoCull.Tests.Services;

public class RawPreviewExtractorTests
{
    [Fact]
    public void SupportedRawExtensions()
    {
        Assert.True(RawPreviewExtractor.IsRawFile("/test/photo.RAF"));
        Assert.True(RawPreviewExtractor.IsRawFile("/test/photo.raf"));
        Assert.True(RawPreviewExtractor.IsRawFile("/test/photo.CR3"));
        Assert.True(RawPreviewExtractor.IsRawFile("/test/photo.arw"));
        Assert.True(RawPreviewExtractor.IsRawFile("/test/photo.nef"));
        Assert.False(RawPreviewExtractor.IsRawFile("/test/photo.jpg"));
        Assert.False(RawPreviewExtractor.IsRawFile("/test/photo.png"));
        Assert.False(RawPreviewExtractor.IsRawFile("/test/document.pdf"));
    }

    [Fact]
    public void SupportedImageExtensions()
    {
        Assert.True(RawPreviewExtractor.IsSupportedFile("/test/photo.jpg"));
        Assert.True(RawPreviewExtractor.IsSupportedFile("/test/photo.JPG"));
        Assert.True(RawPreviewExtractor.IsSupportedFile("/test/photo.jpeg"));
        Assert.True(RawPreviewExtractor.IsSupportedFile("/test/photo.JPEG"));
        Assert.True(RawPreviewExtractor.IsSupportedFile("/test/photo.RAF"));
        Assert.False(RawPreviewExtractor.IsSupportedFile("/test/photo.png"));
        Assert.False(RawPreviewExtractor.IsSupportedFile("/test/document.pdf"));
    }

    [Fact]
    public void ExtractPreviewFromNonexistentFile()
    {
        var data = RawPreviewExtractor.ExtractPreview("/nonexistent/photo.RAF");
        Assert.Null(data);
    }
}
