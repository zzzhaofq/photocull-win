using PhotoCull.Models;
using PhotoCull.ViewModels;
using Xunit;

namespace PhotoCull.Tests.ViewModels;

public class ExportViewModelTests
{
    [Fact]
    public void InitialState()
    {
        var vm = new ExportViewModel();
        Assert.True(vm.ExportToFolder);
        Assert.False(vm.ExportXmp);
        Assert.False(vm.IsExporting);
        Assert.False(vm.IsComplete);
    }

    [Fact]
    public void SelectedPhotosFiltering()
    {
        var vm = new ExportViewModel();
        var session = new CullingSession("/test");
        var p1 = new Photo("/a.RAF", "a.RAF") { Status = CullStatus.Selected };
        var p2 = new Photo("/b.RAF", "b.RAF") { Status = CullStatus.Rejected };
        var p3 = new Photo("/c.RAF", "c.RAF") { Status = CullStatus.Selected };
        session.Photos = new List<Photo> { p1, p2, p3 };
        session.TotalCount = 3;
        vm.LoadSession(session);
        Assert.Equal(2, vm.SelectedPhotos.Count);
        Assert.Equal(3, vm.TotalPhotos);
    }
}
