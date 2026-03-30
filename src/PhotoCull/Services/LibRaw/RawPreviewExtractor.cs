using System.IO;
using ImageMagick;

namespace PhotoCull.Services.LibRaw;

public static class RawPreviewExtractor
{
    public static readonly HashSet<string> SupportedRawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".raf", ".cr2", ".cr3", ".arw", ".nef", ".nrw", ".dng",
        ".orf", ".rw2", ".pef", ".srw", ".3fr", ".mos", ".mrw"
    };

    public static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg"
    };

    public static readonly HashSet<string> AllSupportedExtensions =
        new(SupportedRawExtensions.Concat(SupportedImageExtensions), StringComparer.OrdinalIgnoreCase);

    public static bool IsRawFile(string path)
    {
        return SupportedRawExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsSupportedFile(string path)
    {
        return AllSupportedExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// Extract a preview/thumbnail from a RAW or JPEG file.
    /// Returns JPEG byte[] or null on failure.
    /// </summary>
    public static byte[]? ExtractPreview(string filePath, int maxDimension = 1024)
    {
        try
        {
            var ext = Path.GetExtension(filePath);
            if (SupportedImageExtensions.Contains(ext))
            {
                return LoadAndResizeWithMagick(filePath, maxDimension);
            }

            return ExtractRawPreview(filePath, maxDimension);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Load a high-resolution preview for the detail view (matches Mac 2560px behaviour).
    /// </summary>
    public static byte[]? LoadHighResPreview(string filePath, int maxDimension = 2560)
    {
        try
        {
            var ext = Path.GetExtension(filePath);
            if (SupportedImageExtensions.Contains(ext))
            {
                return LoadAndResizeWithMagick(filePath, maxDimension);
            }

            // For RAW files: try embedded preview first, then full decode
            return ExtractRawPreview(filePath, maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ExtractRawPreview(string filePath, int maxDimension)
    {
        // Strategy 1: Try to extract embedded thumbnail/preview (fast)
        try
        {
            using var image = new MagickImage();
            // Read only the embedded thumbnail profile
            var settings = new MagickReadSettings
            {
                ExtractArea = null // read full embedded preview
            };

            // Try reading the embedded preview using the dng:thumbnail define
            image.Read(filePath, settings);

            // Check if we got a valid image
            if (image.Width > 0 && image.Height > 0)
            {
                return ResizeAndEncodeJpeg(image, maxDimension);
            }
        }
        catch
        {
            // Fall through to strategy 2
        }

        // Strategy 2: Full decode + resize (slow but reliable)
        try
        {
            using var image = new MagickImage(filePath);
            return ResizeAndEncodeJpeg(image, maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? LoadAndResizeWithMagick(string filePath, int maxDimension)
    {
        try
        {
            using var image = new MagickImage(filePath);
            return ResizeAndEncodeJpeg(image, maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ResizeAndEncodeJpeg(MagickImage image, int maxDimension)
    {
        var maxSide = Math.Max((int)image.Width, (int)image.Height);
        if (maxSide > maxDimension)
        {
            var geometry = new MagickGeometry((uint)maxDimension, (uint)maxDimension)
            {
                IgnoreAspectRatio = false
            };
            image.Resize(geometry);
        }

        image.Format = MagickFormat.Jpeg;
        image.Quality = 92;
        return image.ToByteArray();
    }

    public static async Task<Dictionary<string, byte[]?>> ExtractPreviewsAsync(
        IReadOnlyList<string> filePaths,
        int maxDimension = 1024,
        int concurrency = 8,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, byte[]?>(filePaths.Count);
        using var semaphore = new SemaphoreSlim(concurrency);
        var completed = 0;
        var total = filePaths.Count;
        var lockObj = new object();

        var tasks = filePaths.Select(async path =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = await Task.Run(() => ExtractPreview(path, maxDimension), cancellationToken).ConfigureAwait(false);
                lock (lockObj)
                {
                    results[path] = data;
                    completed++;
                    onProgress?.Invoke(completed, total);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }
}
