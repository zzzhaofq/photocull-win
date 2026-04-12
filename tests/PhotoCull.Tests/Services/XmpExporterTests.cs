using System.IO;
using PhotoCull.Models;
using PhotoCull.Services;
using Xunit;

namespace PhotoCull.Tests.Services;

public class XmpExporterTests
{
    [Fact]
    public void GenerateXmpSelected()
    {
        var xml = XmpExporter.GenerateXmp(5, ExportLabel.Green);
        Assert.Contains("xmp:Rating=\"5\"", xml);
        Assert.Contains("xmp:Label=\"Green\"", xml);
        Assert.Contains("<?xpacket begin=", xml);
        Assert.Contains("<?xpacket end=", xml);
    }

    [Fact]
    public void GenerateXmpRejected()
    {
        var xml = XmpExporter.GenerateXmp(1, ExportLabel.Red);
        Assert.Contains("xmp:Rating=\"1\"", xml);
        Assert.Contains("xmp:Label=\"Red\"", xml);
    }

    [Fact]
    public void RatingClamping()
    {
        var xmlHigh = XmpExporter.GenerateXmp(10, ExportLabel.Green);
        Assert.Contains("xmp:Rating=\"5\"", xmlHigh);
        var xmlLow = XmpExporter.GenerateXmp(-3, ExportLabel.Red);
        Assert.Contains("xmp:Rating=\"0\"", xmlLow);
    }

    [Fact]
    public void WriteSidecarCreatesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var rawPath = Path.Combine(tempDir, "DSC_001.RAF");
            File.WriteAllText(rawPath, "");

            XmpExporter.WriteSidecar(rawPath, tempDir, 5, ExportLabel.Green);

            var xmpPath = Path.Combine(tempDir, "DSC_001.xmp");
            Assert.True(File.Exists(xmpPath));

            var content = File.ReadAllText(xmpPath);
            Assert.Contains("xmp:Rating=\"5\"", content);
            Assert.Contains("xmp:Label=\"Green\"", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
