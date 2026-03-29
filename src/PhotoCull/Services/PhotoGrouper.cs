using OpenCvSharp;
using PhotoCull.Models;

namespace PhotoCull.Services;

public static class PhotoGrouper
{
    public const double BurstTimeThreshold = 3.0;   // seconds
    public const double SceneTimeWindow = 60.0;      // seconds
    public const double SceneCorrelationThreshold = 0.7;

    public static async Task<List<PhotoGroup>> GroupPhotos(List<Photo> photos)
    {
        if (photos.Count == 0) return new List<PhotoGroup>();

        var sorted = photos
            .OrderBy(p => p.Exif.CaptureDate ?? DateTime.MinValue)
            .ToList();

        // Step 1: Burst grouping (< 3 seconds apart)
        var burstGroups = new List<List<Photo>>();
        var currentBurst = new List<Photo> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var lastDate = currentBurst[^1].Exif.CaptureDate;
            var currentDate = sorted[i].Exif.CaptureDate;

            if (lastDate.HasValue && currentDate.HasValue &&
                (currentDate.Value - lastDate.Value).TotalSeconds < BurstTimeThreshold)
            {
                currentBurst.Add(sorted[i]);
            }
            else
            {
                if (currentBurst.Count > 1)
                    burstGroups.Add(new List<Photo>(currentBurst));
                currentBurst = new List<Photo> { sorted[i] };
            }
        }
        if (currentBurst.Count > 1)
            burstGroups.Add(currentBurst);

        // Step 2: Scene grouping on remaining photos
        var burstPhotoIds = new HashSet<Guid>(burstGroups.SelectMany(g => g.Select(p => p.Id)));
        var remaining = sorted.Where(p => !burstPhotoIds.Contains(p.Id)).ToList();
        var sceneGroups = await Task.Run(() => GroupByScene(remaining));

        // Step 3: Build PhotoGroup objects
        var groups = new List<PhotoGroup>();

        foreach (var burst in burstGroups)
        {
            var group = new PhotoGroup(GroupType.Burst);
            group.Photos.AddRange(burst);
            groups.Add(group);
        }

        foreach (var scene in sceneGroups)
        {
            var group = new PhotoGroup(GroupType.Scene);
            group.Photos.AddRange(scene);
            groups.Add(group);
        }

        // Step 4: Single groups for unassigned photos
        var groupedIds = new HashSet<Guid>(groups.SelectMany(g => g.Photos.Select(p => p.Id)));
        foreach (var photo in sorted.Where(p => !groupedIds.Contains(p.Id)))
        {
            var group = new PhotoGroup(GroupType.Single);
            group.Photos.Add(photo);
            groups.Add(group);
        }

        return groups;
    }

    private static List<List<Photo>> GroupByScene(List<Photo> photos)
    {
        if (photos.Count <= 1) return new List<List<Photo>>();

        // Compute color histograms for all photos
        var histograms = new List<(Photo Photo, Mat? Histogram)>();
        foreach (var photo in photos)
        {
            var hist = ComputeColorHistogram(photo.ThumbnailData);
            histograms.Add((photo, hist));
        }

        var groups = new List<List<Photo>>();
        var assigned = new HashSet<Guid>();

        for (int i = 0; i < histograms.Count; i++)
        {
            var (photo, hist) = histograms[i];
            if (assigned.Contains(photo.Id)) continue;
            if (hist == null) continue;

            var currentGroup = new List<Photo> { photo };
            assigned.Add(photo.Id);

            for (int j = i + 1; j < histograms.Count; j++)
            {
                var (candidate, candidateHist) = histograms[j];
                if (assigned.Contains(candidate.Id)) continue;
                if (candidateHist == null) continue;

                // Time window check
                if (photo.Exif.CaptureDate.HasValue && candidate.Exif.CaptureDate.HasValue)
                {
                    var timeDiff = Math.Abs((candidate.Exif.CaptureDate.Value - photo.Exif.CaptureDate.Value).TotalSeconds);
                    if (timeDiff > SceneTimeWindow) break;
                }

                // Compare histograms
                var correlation = Cv2.CompareHist(hist, candidateHist, HistCompMethods.Correl);
                if (correlation > SceneCorrelationThreshold)
                {
                    currentGroup.Add(candidate);
                    assigned.Add(candidate.Id);
                }
            }

            if (currentGroup.Count > 1)
                groups.Add(currentGroup);
        }

        // Dispose histograms
        foreach (var (_, hist) in histograms)
            hist?.Dispose();

        return groups;
    }

    private static Mat? ComputeColorHistogram(byte[]? imageData)
    {
        if (imageData == null || imageData.Length == 0) return null;

        try
        {
            using var image = Cv2.ImDecode(imageData, ImreadModes.Color);
            if (image.Empty()) return null;

            using var hsv = new Mat();
            Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);

            var hBins = 30;
            var sBins = 32;
            var histSize = new[] { hBins, sBins };
            var ranges = new[] { new Rangef(0, 180), new Rangef(0, 256) };
            var channels = new[] { 0, 1 };

            var hist = new Mat();
            Cv2.CalcHist(new[] { hsv }, channels, null, hist, 2, histSize, ranges);
            Cv2.Normalize(hist, hist, 0, 1, NormTypes.MinMax);

            return hist;
        }
        catch
        {
            return null;
        }
    }

    public static void AssignRecommendations(List<PhotoGroup> groups)
    {
        foreach (var group in groups)
        {
            if (group.Photos.Count == 0) continue;
            var best = group.Photos
                .OrderByDescending(p => p.AiScore?.Overall ?? 0)
                .First();
            group.RecommendedPhotoId = best.Id;
        }
    }
}
