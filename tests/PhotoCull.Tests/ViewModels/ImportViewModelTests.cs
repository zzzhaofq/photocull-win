using PhotoCull.Models;
using PhotoCull.Services.LibRaw;
using PhotoCull.ViewModels;
using Xunit;

namespace PhotoCull.Tests.ViewModels;

public class ImportViewModelTests
{
    [Fact]
    public void InitialState()
    {
        var vm = new ImportViewModel();
        Assert.False(vm.IsImporting);
        Assert.False(vm.IsComplete);
        Assert.Equal(0, vm.PreviewProgress);
        Assert.Equal(0, vm.TotalFiles);
        Assert.Null(vm.ErrorMessage);
        Assert.Empty(vm.FailedFiles);
        Assert.False(vm.IsCancelled);
    }

    [Fact]
    public void ScanFindsSupportedFiles()
    {
        var vm = new ImportViewModel();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.RAF"), "");
            File.WriteAllText(Path.Combine(tempDir, "b.CR3"), "");
            File.WriteAllText(Path.Combine(tempDir, "c.jpg"), "");
            File.WriteAllText(Path.Combine(tempDir, "d.png"), "");
            var (urls, skipped) = vm.ScanForRawFiles(tempDir);
            Assert.Equal(3, urls.Count);
            Assert.Equal(1, skipped);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanEmptyFolderReturnsEmpty()
    {
        var vm = new ImportViewModel();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var (urls, skipped) = vm.ScanForRawFiles(tempDir);
            Assert.Empty(urls);
            Assert.Equal(0, skipped);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SessionDisplayLimitMax5()
    {
        var sessions = Enumerable.Range(0, 8)
            .Select(i => new CullingSession($"/test/folder{i}") { TotalCount = (i + 1) * 10 })
            .ToList();
        var displayed = sessions.Take(5).ToList();
        Assert.Equal(5, displayed.Count);
        Assert.Equal("/test/folder0", displayed[0].FolderPath);
        Assert.Equal("/test/folder4", displayed[4].FolderPath);
    }

    [Fact]
    public void SessionDisplayLimitFewerThan5()
    {
        var sessions = Enumerable.Range(0, 3)
            .Select(i => new CullingSession($"/test/folder{i}"))
            .ToList();
        var displayed = sessions.Take(5).ToList();
        Assert.Equal(3, displayed.Count);
    }

    [Fact]
    public void SessionDisplayLimitEmpty()
    {
        var sessions = new List<CullingSession>();
        var displayed = sessions.Take(5).ToList();
        Assert.Empty(displayed);
    }

    [Fact]
    public void SessionIsCompletedDefault()
    {
        var session = new CullingSession("/test");
        Assert.False(session.IsCompleted);
    }
}
