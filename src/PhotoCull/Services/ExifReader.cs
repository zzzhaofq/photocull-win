using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoCull.Models;
using System.Globalization;

namespace PhotoCull.Services;

public static class ExifReader
{
    private static readonly string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";

    public static PhotoExif ReadExif(string filePath)
    {
        var exif = new PhotoExif();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

            if (exifSubIfd != null)
            {
                // Aperture (FNumber)
                if (exifSubIfd.TryGetDouble(ExifDirectoryBase.TagFNumber, out var fNumber))
                    exif.Aperture = fNumber;

                // Shutter Speed (ExposureTime)
                if (exifSubIfd.TryGetDouble(ExifDirectoryBase.TagExposureTime, out var exposureTime))
                    exif.ShutterSpeed = FormatShutterSpeed(exposureTime);

                // ISO
                if (exifSubIfd.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso))
                    exif.Iso = iso;

                // Focal Length
                if (exifSubIfd.TryGetDouble(ExifDirectoryBase.TagFocalLength, out var focalLength))
                    exif.FocalLength = focalLength;

                // Capture Date
                if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var captureDate))
                    exif.CaptureDate = captureDate;
                else
                {
                    var dateString = exifSubIfd.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
                    if (dateString != null)
                        exif.CaptureDate = ParseExifDate(dateString);
                }

                // Lens Model
                var lensModel = exifSubIfd.GetDescription(ExifDirectoryBase.TagLensModel);
                if (!string.IsNullOrEmpty(lensModel))
                    exif.LensModel = lensModel;
            }

            if (exifIfd0 != null)
            {
                // Camera Model
                var model = exifIfd0.GetDescription(ExifDirectoryBase.TagModel);
                if (!string.IsNullOrEmpty(model))
                    exif.CameraModel = model;
            }
        }
        catch
        {
            // Return empty EXIF on failure
        }

        return exif;
    }

    public static DateTime? ParseExifDate(string dateString)
    {
        if (DateTime.TryParseExact(dateString, ExifDateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        return null;
    }

    public static string FormatShutterSpeed(double exposureTime)
    {
        if (exposureTime >= 1)
        {
            return $"{exposureTime:F1}s";
        }
        else
        {
            var denominator = (int)Math.Round(1.0 / exposureTime);
            return $"1/{denominator}";
        }
    }
}
