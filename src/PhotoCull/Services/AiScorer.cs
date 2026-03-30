using System.IO;
using OpenCvSharp;
using PhotoCull.Models;

namespace PhotoCull.Services;

public class AiScorer
{
    private static readonly Lazy<CascadeClassifier> FaceClassifier = new(() =>
    {
        var cascadePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "haarcascade_frontalface_default.xml");
        if (File.Exists(cascadePath))
            return new CascadeClassifier(cascadePath);

        // Try to find from OpenCvSharp data directory
        var openCvData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "haarcascade_frontalface_default.xml");
        if (File.Exists(openCvData))
            return new CascadeClassifier(openCvData);

        return new CascadeClassifier();
    });

    public AiScore Score(byte[] imageData)
    {
        Mat? image = null;
        Mat? gray = null;
        try
        {
            image = Cv2.ImDecode(imageData, ImreadModes.Color);
            if (image.Empty())
                return new AiScore { Overall = 50, Sharpness = 50, Exposure = 50, Composition = 50 };

            // Pre-compute grayscale once and reuse across all measurements
            gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            var sharpness = MeasureSharpness(gray);
            var exposure = MeasureExposure(gray);
            var composition = 50.0;

            var hasFace = DetectFaces(gray, out var faceArea);
            double? faceQuality = null;
            double overall;

            if (hasFace)
            {
                faceQuality = MeasureFaceQuality(image, faceArea);
                overall = sharpness * 0.40 + exposure * 0.25 + composition * 0.20 + (faceQuality ?? 50) * 0.15;
            }
            else
            {
                overall = sharpness * 0.47 + exposure * 0.29 + composition * 0.24;
            }

            return new AiScore
            {
                Overall = Clamp(overall),
                Sharpness = Clamp(sharpness),
                Exposure = Clamp(exposure),
                Composition = Clamp(composition),
                FaceQuality = faceQuality.HasValue ? Clamp(faceQuality.Value) : null
            };
        }
        catch
        {
            return new AiScore { Overall = 50, Sharpness = 50, Exposure = 50, Composition = 50 };
        }
        finally
        {
            gray?.Dispose();
            image?.Dispose();
        }
    }

    private static double MeasureSharpness(Mat gray)
    {
        using var laplacian = new Mat();
        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);

        Cv2.MeanStdDev(laplacian, out var mean, out var stddev);
        var variance = stddev.Val0 * stddev.Val0;

        return Math.Min(100, Math.Max(0, variance / 10.0));
    }

    private static double MeasureExposure(Mat gray)
    {
        // Resize to max 256x256 for speed
        using var small = new Mat();
        if (gray.Width > 256 || gray.Height > 256)
        {
            var scale = 256.0 / Math.Max(gray.Width, gray.Height);
            Cv2.Resize(gray, small, new Size(), scale, scale);
        }
        else
        {
            gray.CopyTo(small);
        }

        // Calculate histogram
        using var hist = new Mat();
        Cv2.CalcHist(new[] { small }, new[] { 0 }, null, hist, 1, new[] { 256 }, new[] { new Rangef(0, 256) });

        var totalPixels = small.Rows * small.Cols;
        var histogram = new float[256];
        for (int i = 0; i < 256; i++)
            histogram[i] = hist.At<float>(i);

        // Shadow clip (bins 0-4)
        var shadowClip = 0f;
        for (int i = 0; i < 5; i++) shadowClip += histogram[i];

        // Highlight clip (bins 251-255)
        var highlightClip = 0f;
        for (int i = 251; i < 256; i++) highlightClip += histogram[i];

        var clipRatio = (shadowClip + highlightClip) / totalPixels;

        // Mean brightness
        var meanBrightness = 0.0;
        for (int i = 0; i < 256; i++)
            meanBrightness += i * histogram[i];
        meanBrightness /= totalPixels;

        var brightnessScore = 100.0 - Math.Abs(meanBrightness - 128.0) / 128.0 * 60.0;
        var clipPenalty = Math.Min(50, clipRatio * 500);

        return Clamp(brightnessScore - clipPenalty);
    }

    private static bool DetectFaces(Mat gray, out Rect faceArea)
    {
        faceArea = default;
        var classifier = FaceClassifier.Value;
        if (classifier.Empty()) return false;

        var faces = classifier.DetectMultiScale(gray, 1.1, 3, HaarDetectionTypes.ScaleImage, new Size(30, 30));
        if (faces.Length > 0)
        {
            faceArea = faces.OrderByDescending(f => f.Width * f.Height).First();
            return true;
        }
        return false;
    }

    private static double MeasureFaceQuality(Mat image, Rect faceArea)
    {
        // Score based on face size relative to image
        var imageArea = (double)(image.Width * image.Height);
        var faceRatio = (faceArea.Width * faceArea.Height) / imageArea;

        // Ideal face ratio is around 5-15% of image area
        var score = faceRatio switch
        {
            < 0.01 => 30.0,
            < 0.05 => 50.0 + faceRatio / 0.05 * 20.0,
            < 0.15 => 80.0,
            < 0.30 => 70.0,
            _ => 50.0
        };

        return Clamp(score);
    }

    private static double Clamp(double value)
    {
        return Math.Min(100, Math.Max(0, value));
    }
}
