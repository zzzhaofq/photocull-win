using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Caching.Memory;

namespace PhotoCull.Helpers;

public sealed class ThumbnailCache
{
    public static ThumbnailCache Shared { get; } = new(500);

    private readonly MemoryCache _cache;

    public ThumbnailCache(int countLimit = 500)
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = countLimit
        });
    }

    public BitmapImage? GetImage(string key)
    {
        _cache.TryGetValue(key, out BitmapImage? image);
        return image;
    }

    public void SetImage(BitmapImage image, string key)
    {
        var options = new MemoryCacheEntryOptions { Size = 1 };
        _cache.Set(key, image, options);
    }

    public void RemoveImage(string key)
    {
        _cache.Remove(key);
    }

    public void Clear()
    {
        _cache.Compact(1.0);
    }

    public BitmapImage? Thumbnail(string photoId, byte[]? data)
    {
        var cached = GetImage(photoId);
        if (cached != null) return cached;

        if (data == null || data.Length == 0) return null;

        try
        {
            var image = new BitmapImage();
            using var ms = new System.IO.MemoryStream(data);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            SetImage(image, photoId);
            return image;
        }
        catch
        {
            return null;
        }
    }
}
