using PhotoCull.Models;
using PhotoCull.ViewModels;
using Xunit;

namespace PhotoCull.Tests.ViewModels;

public class CullingViewModelTests
{
    private static (CullingSession session, List<Photo> photos) MakeSession()
    {
        var session = new CullingSession("/test");
        var baseTime = DateTime.UtcNow;
        var photos = Enumerable.Range(0, 5).Select(i =>
        {
            var exif = new PhotoExif { CaptureDate = baseTime.AddSeconds(i * 10) };
            return new Photo($"/test/{i}.RAF", $"{i}.RAF", exif);
        }).ToList();
        session.Photos = photos;
        session.TotalCount = photos.Count;
        return (session, photos);
    }

    private static (CullingSession session, List<Photo> photos) MakeSessionWithScores()
    {
        var session = new CullingSession("/test");
        var baseTime = DateTime.UtcNow;
        var scores = new[] { 20.0, 25.0, 50.0, 70.0, 90.0 };
        var photos = Enumerable.Range(0, 5).Select(i =>
        {
            var exif = new PhotoExif { CaptureDate = baseTime.AddSeconds(i * 10) };
            var p = new Photo($"/test/{i}.RAF", $"{i}.RAF", exif)
            {
                AiScore = new AiScore { Overall = scores[i], Sharpness = scores[i], Exposure = scores[i], Composition = scores[i] }
            };
            if (scores[i] < 30) p.Status = CullStatus.Rejected;
            return p;
        }).ToList();
        session.Photos = photos;
        session.TotalCount = photos.Count;
        return (session, photos);
    }

    [Fact]
    public void LoadSession()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        vm.LoadSession(session);
        Assert.Equal(5, vm.PhotosForCull.Count);
        Assert.Equal(0, vm.CurrentPhotoIndex);
        Assert.Equal(photos.First().Id, vm.SelectedPhotoId);
    }

    [Fact]
    public void RejectMovesForward()
    {
        var vm = new CullingViewModel();
        var (session, _) = MakeSession();
        vm.LoadSession(session);
        vm.RejectCurrent();
        Assert.Equal(1, vm.CurrentPhotoIndex);
        Assert.Equal(1, vm.RejectedCount);
    }

    [Fact]
    public void UndoRestoresStatus()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        vm.LoadSession(session);
        var firstPhoto = photos[0];
        Assert.Equal(CullStatus.Unreviewed, firstPhoto.Status);
        vm.RejectCurrent();
        Assert.Equal(CullStatus.Rejected, firstPhoto.Status);
        vm.Undo();
        Assert.Equal(CullStatus.Unreviewed, firstPhoto.Status);
    }

    [Fact]
    public void RedoReappliesChange()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        vm.LoadSession(session);
        var firstPhoto = photos[0];
        vm.RejectCurrent();
        vm.Undo();
        Assert.Equal(CullStatus.Unreviewed, firstPhoto.Status);
        vm.Redo();
        Assert.Equal(CullStatus.Rejected, firstPhoto.Status);
    }

    [Fact]
    public void Navigation()
    {
        var vm = new CullingViewModel();
        var (session, _) = MakeSession();
        vm.LoadSession(session);
        vm.MoveNext();
        Assert.Equal(1, vm.CurrentPhotoIndex);
        vm.MovePrevious();
        Assert.Equal(0, vm.CurrentPhotoIndex);
        vm.MovePrevious(); // should stay at 0
        Assert.Equal(0, vm.CurrentPhotoIndex);
    }

    [Fact]
    public void EnterCompareRequires2to10()
    {
        var vm = new CullingViewModel();
        vm.EnterCompare(new List<Guid> { Guid.NewGuid() });
        Assert.False(vm.IsComparing);
        vm.EnterCompare(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        Assert.True(vm.IsComparing);
        Assert.Equal(2, vm.ComparePhotoIds.Count);
        vm.ExitCompare();
        Assert.False(vm.IsComparing);
        // 10 should work
        vm.EnterCompare(Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList());
        Assert.True(vm.IsComparing);
        vm.ExitCompare();
        // 11 should not
        vm.EnterCompare(Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList());
        Assert.False(vm.IsComparing);
    }

    [Fact]
    public void RejectedAndKeptPhotosPartition()
    {
        var vm = new CullingViewModel();
        var (session, _) = MakeSessionWithScores();
        vm.LoadSession(session);
        Assert.Equal(2, vm.RejectedPhotos.Count);
        Assert.Equal(3, vm.KeptPhotos.Count);
        Assert.True(vm.RejectedPhotos.All(p => p.Status == CullStatus.Rejected));
        Assert.True(vm.KeptPhotos.All(p => p.Status != CullStatus.Rejected));
    }

    [Fact]
    public void LoadSessionDefaultsToKeptTab()
    {
        var vm = new CullingViewModel();
        var (session, _) = MakeSessionWithScores();
        vm.LoadSession(session);
        Assert.Equal(QuickCullTab.Kept, vm.ActiveTab);
        var firstKept = vm.KeptPhotos.FirstOrDefault();
        Assert.Equal(firstKept?.Id, vm.SelectedPhotoId);
    }

    [Fact]
    public void MoveToKeptChangesStatus()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSessionWithScores();
        vm.LoadSession(session);
        var rejectedPhoto = photos[0];
        Assert.Equal(CullStatus.Rejected, rejectedPhoto.Status);
        vm.MoveToKept(rejectedPhoto.Id);
        Assert.Equal(CullStatus.Unreviewed, rejectedPhoto.Status);
        Assert.Single(vm.RejectedPhotos);
        Assert.Equal(4, vm.KeptPhotos.Count);
    }

    [Fact]
    public void MoveToRejectedChangesStatus()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSessionWithScores();
        vm.LoadSession(session);
        var keptPhoto = photos[2]; // score 50
        Assert.Equal(CullStatus.Unreviewed, keptPhoto.Status);
        vm.MoveToRejected(keptPhoto.Id);
        Assert.Equal(CullStatus.Rejected, keptPhoto.Status);
        Assert.Equal(3, vm.RejectedPhotos.Count);
        Assert.Equal(2, vm.KeptPhotos.Count);
    }

    [Fact]
    public void MoveToKeptIsUndoable()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSessionWithScores();
        vm.LoadSession(session);
        var rejectedPhoto = photos[0];
        vm.MoveToKept(rejectedPhoto.Id);
        Assert.Equal(CullStatus.Unreviewed, rejectedPhoto.Status);
        vm.Undo();
        Assert.Equal(CullStatus.Rejected, rejectedPhoto.Status);
    }

    [Fact]
    public void MoveToRejectedIsUndoable()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSessionWithScores();
        vm.LoadSession(session);
        var keptPhoto = photos[2];
        vm.MoveToRejected(keptPhoto.Id);
        Assert.Equal(CullStatus.Rejected, keptPhoto.Status);
        vm.Undo();
        Assert.Equal(CullStatus.Unreviewed, keptPhoto.Status);
    }

    [Fact]
    public void SetRatingUpdatesPhoto()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        vm.LoadSession(session);
        var photo = photos[0];
        Assert.Equal(0, photo.Rating);
        vm.SetRating(3, photo.Id);
        Assert.Equal(3, photo.Rating);
    }

    [Fact]
    public void SetRatingClampsTo0Through5()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        vm.LoadSession(session);
        vm.SetRating(7, photos[0].Id);
        Assert.Equal(5, photos[0].Rating);
        vm.SetRating(-1, photos[1].Id);
        Assert.Equal(0, photos[1].Rating);
    }

    [Fact]
    public void SetRatingIsUndoable()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        vm.LoadSession(session);
        var photo = photos[0];
        vm.SetRating(4, photo.Id);
        Assert.Equal(4, photo.Rating);
        vm.Undo();
        Assert.Equal(0, photo.Rating);
    }

    [Fact]
    public void ActiveGroupsExcludesFullyRejectedGroups()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        var group1 = new PhotoGroup(GroupType.Burst) { Photos = new List<Photo> { photos[0], photos[1] } };
        var group2 = new PhotoGroup(GroupType.Scene) { Photos = new List<Photo> { photos[2], photos[3], photos[4] } };
        session.Groups = new List<PhotoGroup> { group1, group2 };
        vm.LoadSession(session);

        photos[0].Status = CullStatus.Rejected;
        photos[1].Status = CullStatus.Rejected;
        Assert.Single(vm.ActiveGroups);
        Assert.Equal(group2.Id, vm.ActiveGroups[0].Id);
    }

    [Fact]
    public void GroupPickOnlyShowsKeptPhotos()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSession();
        var group = new PhotoGroup(GroupType.Scene) { Photos = photos };
        session.Groups = new List<PhotoGroup> { group };
        vm.LoadSession(session);

        photos[0].Status = CullStatus.Rejected;
        photos[1].Status = CullStatus.Rejected;
        vm.TransitionToGroupPick();
        Assert.Equal(3, vm.CurrentGroupPhotos.Count);
        Assert.True(vm.CurrentGroupPhotos.All(p => p.Status != CullStatus.Rejected));
    }

    [Fact]
    public void BackToQuickCullRestoresState()
    {
        var vm = new CullingViewModel();
        var (session, _) = MakeSessionWithScores();
        session.Groups = new List<PhotoGroup>();
        vm.LoadSession(session);

        vm.TransitionToGroupPick();
        Assert.Equal(Round.GroupPick, session.CurrentRound);

        vm.BackToQuickCull();
        Assert.Equal(Round.QuickCull, session.CurrentRound);
        Assert.Equal(QuickCullTab.Kept, vm.ActiveTab);
        Assert.Equal(vm.KeptPhotos.FirstOrDefault()?.Id, vm.SelectedPhotoId);
    }

    [Fact]
    public void GroupPickStatsAreCorrect()
    {
        var vm = new CullingViewModel();
        var (session, photos) = MakeSessionWithScores();
        session.Groups = new List<PhotoGroup>();
        vm.LoadSession(session);
        // 2 rejected, 3 kept
        Assert.Equal(3, vm.GroupPickTotalPhotos);
        Assert.Equal(0, vm.GroupPickSelectedCount);
        photos[2].Status = CullStatus.Selected;
        vm.RecalcCounts();
        Assert.Equal(1, vm.GroupPickSelectedCount);
    }
}
