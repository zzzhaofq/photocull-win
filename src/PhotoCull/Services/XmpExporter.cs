using System.IO;
using PhotoCull.Models;

namespace PhotoCull.Services;

public static class XmpExporter
{
    public static string GenerateXmp(int rating, ExportLabel label)
    {
        var clampedRating = Math.Max(0, Math.Min(5, rating));
        return $"""
        <?xpacket begin="{'\uFEFF'}" id="W5M0MpCehiHzreSzNTczkc9d"?>
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
          <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about=""
              xmlns:xmp="http://ns.adobe.com/xap/1.0/"
              xmlns:xmpMM="http://ns.adobe.com/xap/1.0/mm/"
              xmp:Rating="{clampedRating}"
              xmp:Label="{label}">
            </rdf:Description>
          </rdf:RDF>
        </x:xmpmeta>
        <?xpacket end="w"?>
        """;
    }

    public static void WriteSidecar(string rawFilePath, string outputDir, int rating, ExportLabel label)
    {
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(rawFilePath);
        var xmpPath = Path.Combine(outputDir, $"{fileNameWithoutExt}.xmp");
        var content = GenerateXmp(rating, label);
        File.WriteAllText(xmpPath, content, System.Text.Encoding.UTF8);
    }

    public static void ExportSession(
        IReadOnlyList<Photo> photos,
        string outputDir,
        Action<int, int>? onProgress = null)
    {
        Directory.CreateDirectory(outputDir);

        for (int i = 0; i < photos.Count; i++)
        {
            var photo = photos[i];
            switch (photo.Status)
            {
                case CullStatus.Selected:
                {
                    var rating = photo.Rating > 0 ? photo.Rating : 5;
                    WriteSidecar(photo.FilePath, outputDir, rating, ExportLabel.Green);
                    break;
                }
                case CullStatus.Rejected:
                {
                    var rating = photo.Rating > 0 ? photo.Rating : 1;
                    WriteSidecar(photo.FilePath, outputDir, rating, ExportLabel.Red);
                    break;
                }
                case CullStatus.Unreviewed:
                {
                    if (photo.Rating > 0)
                        WriteSidecar(photo.FilePath, outputDir, photo.Rating, ExportLabel.Yellow);
                    break;
                }
            }
            onProgress?.Invoke(i + 1, photos.Count);
        }
    }
}
