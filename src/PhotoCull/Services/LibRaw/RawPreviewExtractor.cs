using System.Runtime.InteropServices;
using OpenCvSharp;

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

    public static byte[]? ExtractPreview(string filePath, int maxDimension = 1024)
    {
        var ext = Path.GetExtension(filePath);
        if (SupportedImageExtensions.Contains(ext))
        {
            return LoadAndResizeJpeg(filePath, maxDimension);
        }

        return ExtractRawPreview(filePath, maxDimension);
    }

    private static byte[]? ExtractRawPreview(string filePath, int maxDimension)
    {
        var handle = LibRawInterop.libraw_init(0);
        if (handle == IntPtr.Zero) return null;

        try
        {
            if (LibRawInterop.libraw_open_file(handle, filePath) != 0)
                return null;

            // Try embedded thumbnail first
            if (LibRawInterop.libraw_unpack_thumb(handle) == 0)
            {
                var thumbPtr = LibRawInterop.libraw_dcraw_make_mem_thumb(handle, out int thumbError);
                if (thumbPtr != IntPtr.Zero && thumbError == 0)
                {
                    try
                    {
                        var thumbData = ExtractImageData(thumbPtr);
                        if (thumbData != null)
                        {
                            var resized = ResizeJpegBytes(thumbData, maxDimension);
                            return resized ?? thumbData;
                        }
                    }
                    finally
                    {
                        LibRawInterop.libraw_dcraw_clear_mem(thumbPtr);
                    }
                }
            }

            // Fallback: full decode
            LibRawInterop.libraw_recycle(handle);
            if (LibRawInterop.libraw_open_file(handle, filePath) != 0)
                return null;

            if (LibRawInterop.libraw_unpack(handle) != 0)
                return null;

            if (LibRawInterop.libraw_dcraw_process(handle) != 0)
                return null;

            var imagePtr = LibRawInterop.libraw_dcraw_make_mem_image(handle, out int imgError);
            if (imagePtr == IntPtr.Zero || imgError != 0)
                return null;

            try
            {
                var header = Marshal.PtrToStructure<LibRawInterop.LibrawProcessedImage>(imagePtr);
                var dataOffset = Marshal.SizeOf<LibRawInterop.LibrawProcessedImage>();
                var pixelData = new byte[header.DataSize];
                Marshal.Copy(imagePtr + dataOffset, pixelData, 0, header.DataSize);

                return EncodeRawPixelsAndResize(pixelData, header.Width, header.Height, header.Colors, maxDimension);
            }
            finally
            {
                LibRawInterop.libraw_dcraw_clear_mem(imagePtr);
            }
        }
        finally
        {
            LibRawInterop.libraw_close(handle);
        }
    }

    private static byte[]? ExtractImageData(IntPtr processedImagePtr)
    {
        var header = Marshal.PtrToStructure<LibRawInterop.LibrawProcessedImage>(processedImagePtr);
        if (header.DataSize <= 0) return null;

        var dataOffset = Marshal.SizeOf<LibRawInterop.LibrawProcessedImage>();
        var data = new byte[header.DataSize];
        Marshal.Copy(processedImagePtr + dataOffset, data, 0, header.DataSize);

        // Type 1 = JPEG thumbnail
        if (header.Type == 1)
            return data;

        // Type 2 = bitmap data, needs encoding
        return EncodeRawPixelsAndResize(data, header.Width, header.Height, header.Colors, 1024);
    }

    private static byte[]? EncodeRawPixelsAndResize(byte[] pixelData, int width, int height, int colors, int maxDimension)
    {
        try
        {
            var matType = colors == 3 ? MatType.CV_8UC3 : MatType.CV_8UC4;
            using var mat = new Mat(height, width, matType, pixelData);

            // LibRaw outputs RGB, OpenCV uses BGR
            if (colors == 3)
                Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2BGR);

            return ResizeAndEncode(mat, maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? LoadAndResizeJpeg(string filePath, int maxDimension)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            return ResizeJpegBytes(bytes, maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ResizeJpegBytes(byte[] jpegBytes, int maxDimension)
    {
        try
        {
            using var image = Cv2.ImDecode(jpegBytes, ImreadModes.Color);
            if (image.Empty()) return jpegBytes;

            return ResizeAndEncode(image, maxDimension);
        }
        catch
        {
            return jpegBytes;
        }
    }

    private static byte[]? ResizeAndEncode(Mat image, int maxDimension)
    {
        var maxSide = Math.Max(image.Width, image.Height);
        if (maxSide <= maxDimension)
        {
            Cv2.ImEncode(".jpg", image, out var buf, new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 92) });
            return buf;
        }

        var scale = (double)maxDimension / maxSide;
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(), scale, scale, InterpolationFlags.Area);
        Cv2.ImEncode(".jpg", resized, out var encoded, new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 92) });
        return encoded;
    }

    public static async Task<Dictionary<string, byte[]?>> ExtractPreviewsAsync(
        IReadOnlyList<string> filePaths,
        int maxDimension = 1024,
        int concurrency = 8,
        Action<int, int>? onProgress = null)
    {
        var results = new Dictionary<string, byte[]?>(filePaths.Count);
        using var semaphore = new SemaphoreSlim(concurrency);
        var completed = 0;
        var total = filePaths.Count;
        var lockObj = new object();

        var tasks = filePaths.Select(async path =>
        {
            await semaphore.WaitAsync();
            try
            {
                var data = await Task.Run(() => ExtractPreview(path, maxDimension));
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

        await Task.WhenAll(tasks);
        return results;
    }
}
