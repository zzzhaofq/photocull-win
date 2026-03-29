using CommunityToolkit.Mvvm.ComponentModel;
using PhotoCull.Helpers;
using PhotoCull.Models;

namespace PhotoCull.ViewModels;

public partial class CullingViewModel : ObservableObject
{
    [ObservableProperty] private CullingSession? _session;
    [ObservableProperty] private int _currentPhotoIndex;
    [ObservableProperty] private Guid? _selectedPhotoId;
    [ObservableProperty] private bool _isZoomed;
    [ObservableProperty] private List<Photo> _photosForCull = new();
    [ObservableProperty] private List<PhotoGroup> _groups = new();
    [ObservableProperty] private int _currentGroupIndex;
    [ObservableProperty] private bool _isComparing;
    [ObservableProperty] private List<Guid> _comparePhotoIds = new();
    [ObservableProperty] private QuickCullTab _activeTab = QuickCullTab.Kept;
    [ObservableProperty] private Guid? _groupPreviewPhotoId;
    [ObservableProperty] private int _rejectedCount;
    [ObservableProperty] private int _groupPickSelectedCount;
    [ObservableProperty] private int[] _ratingCounts = new int[6];
    [ObservableProperty] private bool _didAcceptAllRecommendations;

    public HashSet<Guid> SelectedPhotoIds { get; set; } = new();
    public Guid? LastClickedPhotoId { get; set; }
    public HashSet<Guid> SelectedGroupPhotoIds { get; set; } = new();
    public Guid? LastClickedGroupPhotoId { get; set; }
    public HashSet<int> SelectedGroupIndices { get; set; } = new();

    public UndoRedoManager UndoManager { get; } = new();

    public string RejectedTabName => "AI 建议淘汰";
    public string KeptTabName => "AI 建议保留";

    public List<Photo> RejectedPhotos => PhotosForCull.Where(p => p.Status == CullStatus.Rejected).ToList();
    public List<Photo> KeptPhotos => PhotosForCull.Where(p => p.Status != CullStatus.Rejected).ToList();

    public int StarredPhotoCount => RatingCounts[1] + RatingCounts[2] + RatingCounts[3] + RatingCounts[4] + RatingCounts[5];
    public int GroupPickTotalPhotos => KeptPhotos.Count;

    public Photo? CurrentPhoto =>
        CurrentPhotoIndex >= 0 && CurrentPhotoIndex < PhotosForCull.Count
            ? PhotosForCull[CurrentPhotoIndex]
            : null;

    public List<PhotoGroup> ActiveGroups =>
        Groups.Where(g => g.Photos.Any(p => p.Status != CullStatus.Rejected)).ToList();

    public PhotoGroup? CurrentGroup
    {
        get
        {
            var active = ActiveGroups;
            return CurrentGroupIndex >= 0 && CurrentGroupIndex < active.Count ? active[CurrentGroupIndex] : null;
        }
    }

    public List<Photo> CurrentGroupPhotos =>
        CurrentGroup?.Photos.Where(p => p.Status != CullStatus.Rejected).ToList() ?? new();

    public int ReviewedGroupCount => ActiveGroups.Count(g => g.IsReviewed);

    public void RecalcCounts()
    {
        RejectedCount = PhotosForCull.Count(p => p.Status == CullStatus.Rejected);
        GroupPickSelectedCount = PhotosForCull.Count(p => p.Status == CullStatus.Selected);
        var counts = new int[6];
        foreach (var photo in PhotosForCull)
        {
            var r = Math.Max(0, Math.Min(5, photo.Rating));
            counts[r]++;
        }
        RatingCounts = counts;
        UpdateSessionCounts();
        OnPropertyChanged(nameof(RejectedPhotos));
        OnPropertyChanged(nameof(KeptPhotos));
        OnPropertyChanged(nameof(StarredPhotoCount));
        OnPropertyChanged(nameof(GroupPickTotalPhotos));
        OnPropertyChanged(nameof(ActiveGroups));
        OnPropertyChanged(nameof(CurrentGroup));
        OnPropertyChanged(nameof(CurrentGroupPhotos));
        OnPropertyChanged(nameof(ReviewedGroupCount));
    }

    public void LoadSession(CullingSession session)
    {
        Session = session;
        CurrentPhotoIndex = session.CurrentPhotoIndex;
        PhotosForCull = session.Photos
            .OrderBy(p => p.Exif.CaptureDate ?? DateTime.MinValue)
            .ToList();
        Groups = session.Groups
            .OrderBy(g => g.Photos.FirstOrDefault()?.Exif.CaptureDate ?? DateTime.MinValue)
            .ToList();
        CurrentGroupIndex = session.CurrentGroupIndex;
        ActiveTab = QuickCullTab.Kept;
        UndoManager.Clear();
        RecalcCounts();

        var firstKept = KeptPhotos.FirstOrDefault();
        if (firstKept != null)
        {
            SelectedPhotoId = firstKept.Id;
        }
        else
        {
            var firstRejected = RejectedPhotos.FirstOrDefault();
            if (firstRejected != null)
            {
                ActiveTab = QuickCullTab.Rejected;
                SelectedPhotoId = firstRejected.Id;
            }
        }
    }

    public void RejectCurrent()
    {
        var photo = CurrentPhoto;
        if (photo == null) return;
        var previous = photo.Status;
        photo.Status = CullStatus.Rejected;
        RecalcCounts();
        UndoManager.RegisterUndo(
            () => { photo.Status = previous; RecalcCounts(); },
            () => { photo.Status = CullStatus.Rejected; RecalcCounts(); });
        MoveNext();
    }

    public void KeepCurrent()
    {
        var photo = CurrentPhoto;
        if (photo == null) return;
        var previous = photo.Status;
        photo.Status = CullStatus.Selected;
        RecalcCounts();
        UndoManager.RegisterUndo(
            () => { photo.Status = previous; RecalcCounts(); },
            () => { photo.Status = CullStatus.Selected; RecalcCounts(); });
        MoveNext();
    }

    public void MoveNext()
    {
        if (CurrentPhotoIndex < PhotosForCull.Count - 1)
        {
            CurrentPhotoIndex++;
            SelectedPhotoId = PhotosForCull[CurrentPhotoIndex].Id;
            if (Session != null) Session.CurrentPhotoIndex = CurrentPhotoIndex;
        }
    }

    public void MovePrevious()
    {
        if (CurrentPhotoIndex > 0)
        {
            CurrentPhotoIndex--;
            SelectedPhotoId = PhotosForCull[CurrentPhotoIndex].Id;
            if (Session != null) Session.CurrentPhotoIndex = CurrentPhotoIndex;
        }
    }

    public void ToggleZoom() => IsZoomed = !IsZoomed;

    public void Undo() => UndoManager.Undo();
    public void Redo() => UndoManager.Redo();

    public void SetRating(int rating, Guid photoId)
    {
        var clamped = Math.Max(0, Math.Min(5, rating));
        var photo = PhotosForCull.FirstOrDefault(p => p.Id == photoId);
        if (photo == null) return;
        var previous = photo.Rating;
        photo.Rating = clamped;
        var counts = RatingCounts.ToArray();
        counts[previous]--;
        counts[clamped]++;
        RatingCounts = counts;
        OnPropertyChanged(nameof(StarredPhotoCount));
        UndoManager.RegisterUndo(
            () =>
            {
                photo.Rating = previous;
                var c = RatingCounts.ToArray();
                c[clamped]--;
                c[previous]++;
                RatingCounts = c;
                OnPropertyChanged(nameof(StarredPhotoCount));
            },
            () =>
            {
                photo.Rating = clamped;
                var c = RatingCounts.ToArray();
                c[previous]--;
                c[clamped]++;
                RatingCounts = c;
                OnPropertyChanged(nameof(StarredPhotoCount));
            });
    }

    public void MoveToKept(Guid photoId)
    {
        var photo = PhotosForCull.FirstOrDefault(p => p.Id == photoId);
        if (photo == null || photo.Status != CullStatus.Rejected) return;
        var previous = photo.Status;
        photo.Status = CullStatus.Unreviewed;
        RecalcCounts();
        UndoManager.RegisterUndo(
            () => { photo.Status = previous; RecalcCounts(); },
            () => { photo.Status = CullStatus.Unreviewed; RecalcCounts(); });
    }

    public void MoveToRejected(Guid photoId)
    {
        var photo = PhotosForCull.FirstOrDefault(p => p.Id == photoId);
        if (photo == null || photo.Status == CullStatus.Rejected) return;
        var previous = photo.Status;
        photo.Status = CullStatus.Rejected;
        RecalcCounts();
        UndoManager.RegisterUndo(
            () => { photo.Status = previous; RecalcCounts(); },
            () => { photo.Status = CullStatus.Rejected; RecalcCounts(); });
    }

    // Batch operations
    public void BatchMoveToKept(IEnumerable<Guid> photoIds)
    {
        var photos = PhotosForCull.Where(p => photoIds.Contains(p.Id) && p.Status == CullStatus.Rejected).ToList();
        if (photos.Count == 0) return;
        var previousStates = photos.Select(p => (Photo: p, Previous: p.Status)).ToList();
        foreach (var p in photos) p.Status = CullStatus.Unreviewed;
        RecalcCounts();
        UndoManager.RegisterUndo(
            () => { foreach (var (photo, prev) in previousStates) photo.Status = prev; RecalcCounts(); },
            () => { foreach (var p in photos) p.Status = CullStatus.Unreviewed; RecalcCounts(); });
    }

    public void BatchMoveToRejected(IEnumerable<Guid> photoIds)
    {
        var photos = PhotosForCull.Where(p => photoIds.Contains(p.Id) && p.Status != CullStatus.Rejected).ToList();
        if (photos.Count == 0) return;
        var previousStates = photos.Select(p => (Photo: p, Previous: p.Status)).ToList();
        foreach (var p in photos) p.Status = CullStatus.Rejected;
        RecalcCounts();
        UndoManager.RegisterUndo(
            () => { foreach (var (photo, prev) in previousStates) photo.Status = prev; RecalcCounts(); },
            () => { foreach (var p in photos) p.Status = CullStatus.Rejected; RecalcCounts(); });
    }

    public void BatchSetRating(int rating, IEnumerable<Guid> photoIds)
    {
        var clamped = Math.Max(0, Math.Min(5, rating));
        var photos = PhotosForCull.Where(p => photoIds.Contains(p.Id)).ToList();
        if (photos.Count == 0) return;
        var previousRatings = photos.Select(p => (Photo: p, Previous: p.Rating)).ToList();
        var counts = RatingCounts.ToArray();
        foreach (var p in photos) { counts[p.Rating]--; p.Rating = clamped; counts[clamped]++; }
        RatingCounts = counts;
        OnPropertyChanged(nameof(StarredPhotoCount));
        UndoManager.RegisterUndo(
            () =>
            {
                var c = RatingCounts.ToArray();
                foreach (var (photo, prev) in previousRatings) { c[photo.Rating]--; photo.Rating = prev; c[prev]++; }
                RatingCounts = c; OnPropertyChanged(nameof(StarredPhotoCount));
            },
            () =>
            {
                var c = RatingCounts.ToArray();
                foreach (var p in photos) { c[p.Rating]--; p.Rating = clamped; c[clamped]++; }
                RatingCounts = c; OnPropertyChanged(nameof(StarredPhotoCount));
            });
    }

    private void UpdateSessionCounts()
    {
        if (Session == null) return;
        Session.RejectedCount = RejectedCount;
        Session.SelectedCount = GroupPickSelectedCount;
    }

    public void TransitionToGroupPick()
    {
        if (Session != null) Session.CurrentRound = Round.GroupPick;
        CurrentGroupIndex = 0;
        if (Session != null) Session.CurrentGroupIndex = 0;
        RecalcCounts();

        var active = ActiveGroups;
        var firstUnreviewed = active.FindIndex(g => !g.IsReviewed);
        if (firstUnreviewed >= 0)
        {
            CurrentGroupIndex = firstUnreviewed;
            if (Session != null) Session.CurrentGroupIndex = firstUnreviewed;
        }
    }

    public void ResumeGroupPick()
    {
        if (Session != null) Session.CurrentRound = Round.GroupPick;
        var savedIndex = Session?.CurrentGroupIndex ?? 0;
        var active = ActiveGroups;
        CurrentGroupIndex = Math.Min(savedIndex, Math.Max(active.Count - 1, 0));
        SelectedGroupPhotoIds.Clear();
        RecalcCounts();
    }

    public void BackToQuickCull()
    {
        if (Session != null) Session.CurrentRound = Round.QuickCull;
        ActiveTab = QuickCullTab.Kept;
        RecalcCounts();
        var firstKept = KeptPhotos.FirstOrDefault();
        if (firstKept != null)
        {
            SelectedPhotoId = firstKept.Id;
        }
        else
        {
            var firstRejected = RejectedPhotos.FirstOrDefault();
            if (firstRejected != null)
            {
                ActiveTab = QuickCullTab.Rejected;
                SelectedPhotoId = firstRejected.Id;
            }
        }
    }

    public void AcceptRecommendation()
    {
        var group = CurrentGroup;
        if (group == null || !group.RecommendedPhotoId.HasValue) return;
        var recId = group.RecommendedPhotoId.Value;
        group.SelectedPhotoIds = new List<Guid> { recId };
        var photo = group.Photos.FirstOrDefault(p => p.Id == recId);
        if (photo != null) photo.Status = CullStatus.Selected;
        group.IsReviewed = true;
        RecalcCounts();
        MoveToNextGroup();
    }

    public void AcceptAllRecommendations()
    {
        foreach (var group in ActiveGroups.Where(g => !g.IsReviewed))
        {
            if (!group.RecommendedPhotoId.HasValue) continue;
            var recId = group.RecommendedPhotoId.Value;
            group.SelectedPhotoIds = new List<Guid> { recId };
            var photo = group.Photos.FirstOrDefault(p => p.Id == recId);
            if (photo != null) photo.Status = CullStatus.Selected;
            group.IsReviewed = true;
        }
        RecalcCounts();
        DidAcceptAllRecommendations = true;
        var active = ActiveGroups;
        if (active.Count > 0)
        {
            CurrentGroupIndex = active.Count - 1;
            if (Session != null) Session.CurrentGroupIndex = CurrentGroupIndex;
        }
    }

    public void UndoAllRecommendations()
    {
        foreach (var group in ActiveGroups.Where(g => g.IsReviewed))
        {
            if (group.RecommendedPhotoId.HasValue &&
                group.SelectedPhotoIds.Count == 1 &&
                group.SelectedPhotoIds[0] == group.RecommendedPhotoId.Value)
            {
                group.IsReviewed = false;
                group.SelectedPhotoIds = new List<Guid>();
                var photo = group.Photos.FirstOrDefault(p => p.Id == group.RecommendedPhotoId.Value);
                if (photo != null) photo.Status = CullStatus.Unreviewed;
            }
        }
        RecalcCounts();
        DidAcceptAllRecommendations = false;
        var active = ActiveGroups;
        var firstUnreviewed = active.FindIndex(g => !g.IsReviewed);
        if (firstUnreviewed >= 0)
        {
            CurrentGroupIndex = firstUnreviewed;
            if (Session != null) Session.CurrentGroupIndex = firstUnreviewed;
        }
        else
        {
            CurrentGroupIndex = 0;
            if (Session != null) Session.CurrentGroupIndex = 0;
        }
    }

    public void SelectPhotosInGroup(HashSet<Guid> ids)
    {
        var group = CurrentGroup;
        if (group == null) return;
        group.SelectedPhotoIds = ids.ToList();
        foreach (var photo in group.Photos)
            photo.Status = ids.Contains(photo.Id) ? CullStatus.Selected : CullStatus.Unreviewed;
        group.IsReviewed = true;
        RecalcCounts();
    }

    public void ClearGroupSelection()
    {
        var group = CurrentGroup;
        if (group == null) return;
        foreach (var photo in group.Photos)
        {
            if (group.SelectedPhotoIds.Contains(photo.Id))
                photo.Status = CullStatus.Unreviewed;
        }
        group.SelectedPhotoIds = new List<Guid>();
        group.IsReviewed = false;
        SelectedGroupPhotoIds.Clear();
        RecalcCounts();
    }

    public void MoveToNextGroup()
    {
        var active = ActiveGroups;
        if (CurrentGroupIndex < active.Count - 1)
        {
            CurrentGroupIndex++;
            if (Session != null) Session.CurrentGroupIndex = CurrentGroupIndex;
            SelectedGroupPhotoIds.Clear();
        }
        OnPropertyChanged(nameof(CurrentGroup));
        OnPropertyChanged(nameof(CurrentGroupPhotos));
    }

    public void MoveToPreviousGroup()
    {
        if (CurrentGroupIndex > 0)
        {
            CurrentGroupIndex--;
            if (Session != null) Session.CurrentGroupIndex = CurrentGroupIndex;
        }
        OnPropertyChanged(nameof(CurrentGroup));
        OnPropertyChanged(nameof(CurrentGroupPhotos));
    }

    public void EnterCompare(List<Guid> photoIds)
    {
        if (photoIds.Count < 2 || photoIds.Count > 10) return;
        ComparePhotoIds = photoIds;
        IsComparing = true;
    }

    public void ExitCompare()
    {
        IsComparing = false;
        ComparePhotoIds = new List<Guid>();
    }

    // Manual group operations
    public void MergeSelectedGroups()
    {
        var active = ActiveGroups;
        var sortedIndices = SelectedGroupIndices.OrderBy(i => i).ToList();
        if (sortedIndices.Count < 2) return;

        var targetGroup = active[sortedIndices[0]];
        foreach (var idx in sortedIndices.Skip(1).Reverse())
        {
            var sourceGroup = active[idx];
            foreach (var photo in sourceGroup.Photos)
            {
                photo.GroupId = targetGroup.Id;
                targetGroup.Photos.Add(photo);
            }
            sourceGroup.Photos.Clear();
            Groups.RemoveAll(g => g.Id == sourceGroup.Id);
        }

        targetGroup.IsReviewed = false;
        targetGroup.GroupType = GroupType.Scene;
        SelectedGroupIndices.Clear();

        var newIdx = ActiveGroups.FindIndex(g => g.Id == targetGroup.Id);
        if (newIdx >= 0)
        {
            CurrentGroupIndex = newIdx;
            if (Session != null) Session.CurrentGroupIndex = newIdx;
        }
        SelectedGroupPhotoIds.Clear();
        OnPropertyChanged(nameof(ActiveGroups));
        OnPropertyChanged(nameof(CurrentGroup));
        OnPropertyChanged(nameof(CurrentGroupPhotos));
    }

    public void RemovePhotoFromCurrentGroup(Guid photoId)
    {
        var group = CurrentGroup;
        if (group == null) return;
        var photo = group.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo == null) return;

        var newGroup = new PhotoGroup(GroupType.Single);
        newGroup.Photos.Add(photo);
        newGroup.SessionId = group.SessionId;
        photo.GroupId = newGroup.Id;
        group.Photos.Remove(photo);

        var insertIdx = Groups.FindIndex(g => g.Id == group.Id);
        if (insertIdx >= 0) Groups.Insert(insertIdx + 1, newGroup);

        SelectedGroupPhotoIds.Remove(photoId);
        var ids = group.SelectedPhotoIds;
        ids.Remove(photoId);
        group.SelectedPhotoIds = ids;

        OnPropertyChanged(nameof(ActiveGroups));
        OnPropertyChanged(nameof(CurrentGroupPhotos));
    }

    public void MovePhotoToGroup(Guid photoId, Guid targetGroupId)
    {
        var sourceGroup = CurrentGroup;
        if (sourceGroup == null) return;
        var photo = sourceGroup.Photos.FirstOrDefault(p => p.Id == photoId);
        var targetGroup = Groups.FirstOrDefault(g => g.Id == targetGroupId);
        if (photo == null || targetGroup == null || sourceGroup.Id == targetGroupId) return;

        sourceGroup.Photos.Remove(photo);
        targetGroup.Photos.Add(photo);
        photo.GroupId = targetGroup.Id;

        SelectedGroupPhotoIds.Remove(photoId);
        var ids = sourceGroup.SelectedPhotoIds;
        ids.Remove(photoId);
        sourceGroup.SelectedPhotoIds = ids;

        targetGroup.IsReviewed = false;
        OnPropertyChanged(nameof(ActiveGroups));
        OnPropertyChanged(nameof(CurrentGroupPhotos));
    }
}
