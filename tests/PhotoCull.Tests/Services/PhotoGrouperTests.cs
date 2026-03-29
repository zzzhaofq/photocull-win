using PhotoCull.Models;
using PhotoCull.Services;
using Xunit;

namespace PhotoCull.Tests.Services;

public class PhotoGrouperTests
{
    private static Photo MakePhoto(string name, DateTime date)
    {
        var exif = new PhotoExif { CaptureDate = date };
        return new Photo($"/test/{name}", name, exif);
    }

    [Fact]
    public async Task BurstGrouping()
    {
        var baseTime = DateTime.UtcNow;
        var photos = new List<Photo>
        {
            MakePhoto("001.RAF", baseTime),
            MakePhoto("002.RAF", baseTime.AddSeconds(0.5)),
            MakePhoto("003.RAF", baseTime.AddSeconds(1.0)),
            MakePhoto("004.RAF", baseTime.AddSeconds(10.0)),
        };
        var groups = await PhotoGrouper.GroupPhotos(photos);
        var burstGroups = groups.Where(g => g.GroupType == GroupType.Burst).ToList();
        Assert.Single(burstGroups);
        Assert.Equal(3, burstGroups[0].Photos.Count);
    }

    [Fact]
    public async Task SinglePhotosGetOwnGroup()
    {
        var baseTime = DateTime.UtcNow;
        var photos = new List<Photo>
        {
            MakePhoto("001.RAF", baseTime),
            MakePhoto("002.RAF", baseTime.AddSeconds(60)),
        };
        var groups = await PhotoGrouper.GroupPhotos(photos);
        var singles = groups.Where(g => g.GroupType == GroupType.Single).ToList();
        Assert.Equal(2, singles.Count);
    }

    [Fact]
    public async Task EmptyInputReturnsEmpty()
    {
        var groups = await PhotoGrouper.GroupPhotos(new List<Photo>());
        Assert.Empty(groups);
    }

    [Fact]
    public void AssignRecommendations()
    {
        var group = new PhotoGroup(GroupType.Burst);
        var p1 = new Photo("/a.RAF", "a.RAF") { AiScore = new AiScore { Overall = 60, Sharpness = 70, Exposure = 50, Composition = 60 } };
        var p2 = new Photo("/b.RAF", "b.RAF") { AiScore = new AiScore { Overall = 85, Sharpness = 90, Exposure = 80, Composition = 85 } };
        group.Photos = new List<Photo> { p1, p2 };
        PhotoGrouper.AssignRecommendations(new List<PhotoGroup> { group });
        Assert.Equal(p2.Id, group.RecommendedPhotoId);
    }
}
