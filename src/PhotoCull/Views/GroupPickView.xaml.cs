using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PhotoCull.Models;
using PhotoCull.ViewModels;

namespace PhotoCull.Views;

public partial class GroupPickView : UserControl
{
    private readonly MainViewModel _vm;
    private bool _isMergeMode;
    private int? _filterRating;

    // Cached frozen brushes
    private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush(76, 175, 80);
    private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush(117, 117, 117);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public GroupPickView(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;

        GroupGrid.PhotoClicked += OnGroupPhotoClicked;
        GroupGrid.PhotoRightClicked += OnGroupPhotoRightClicked;
        Compare.CloseRequested += () => _vm.CullingVm.ExitCompare();
        Compare.PhotoSelected += OnComparePhotoSelected;

        _vm.CullingVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(CullingViewModel.CurrentGroupIndex) or
                nameof(CullingViewModel.CurrentGroup) or
                nameof(CullingViewModel.CurrentGroupPhotos) or
                nameof(CullingViewModel.ActiveGroups) or
                nameof(CullingViewModel.ReviewedGroupCount))
            {
                RefreshGroupDetail();
                UpdateGroupCount();
            }
        };

        Loaded += (s, e) =>
        {
            Focus();
            RefreshGroupList();
            RefreshGroupDetail();
            UpdateGroupCount();
        };
    }

    private void RefreshGroupList()
    {
        GroupList.ItemsSource = null;
        GroupList.ItemsSource = _vm.CullingVm.ActiveGroups;
        if (_vm.CullingVm.CurrentGroupIndex >= 0 && _vm.CullingVm.CurrentGroupIndex < _vm.CullingVm.ActiveGroups.Count)
            GroupList.SelectedIndex = _vm.CullingVm.CurrentGroupIndex;
    }

    private void RefreshGroupDetail()
    {
        var photos = _vm.CullingVm.CurrentGroupPhotos;
        // AI 推荐照片置顶
        var group = _vm.CullingVm.CurrentGroup;
        if (group?.RecommendedPhotoId != null)
        {
            var recIdx = photos.FindIndex(p => p.Id == group.RecommendedPhotoId);
            if (recIdx > 0)
            {
                var rec = photos[recIdx];
                photos.RemoveAt(recIdx);
                photos.Insert(0, rec);
            }
        }
        if (_filterRating.HasValue)
            photos = photos.Where(p => p.Rating == _filterRating.Value).ToList();
        GroupGrid.SetPhotos(photos);

        // Highlight selected photos
        if (group?.RecommendedPhotoId != null)
        {
            var selectedIds = new HashSet<Guid>(group.SelectedPhotoIds);
            if (selectedIds.Count == 0 && group.RecommendedPhotoId.HasValue)
                selectedIds.Add(group.RecommendedPhotoId.Value);
            GroupGrid.SetSelectedIds(selectedIds);
        }

        // Show/hide clear selection button
        ClearSelectionBtn.Visibility = group != null && group.IsReviewed
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateButtonStyles();
    }

    public void SetFilterRating(int? rating)
    {
        _filterRating = rating;
        RefreshGroupDetail();
    }

    private void UpdateGroupCount()
    {
        var cvm = _vm.CullingVm;
        GroupCountText.Text = $"已审核 {cvm.ReviewedGroupCount}/{cvm.ActiveGroups.Count} 组";
    }

    private void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedIndex < 0) return;

        if (_isMergeMode)
        {
            var index = GroupList.SelectedIndex;
            var cvm = _vm.CullingVm;
            if (cvm.SelectedGroupIndices.Contains(index))
                cvm.SelectedGroupIndices.Remove(index);
            else
                cvm.SelectedGroupIndices.Add(index);
            UpdateMergeUI();
        }
        else
        {
            _vm.CullingVm.CurrentGroupIndex = GroupList.SelectedIndex;
            if (_vm.CullingVm.Session != null)
                _vm.CullingVm.Session.CurrentGroupIndex = GroupList.SelectedIndex;
            // Restore previous selection if the group was already reviewed
            var group = _vm.CullingVm.CurrentGroup;
            if (group != null && group.IsReviewed && group.SelectedPhotoIds.Count > 0)
                _vm.CullingVm.SelectedGroupPhotoIds = new HashSet<Guid>(group.SelectedPhotoIds);
            else
                _vm.CullingVm.SelectedGroupPhotoIds.Clear();
            RefreshGroupDetail();
        }
    }

    private void OnGroupPhotoClicked(Photo photo, MouseEventArgs e)
    {
        var cvm = _vm.CullingVm;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (cvm.SelectedGroupPhotoIds.Contains(photo.Id))
                cvm.SelectedGroupPhotoIds.Remove(photo.Id);
            else
                cvm.SelectedGroupPhotoIds.Add(photo.Id);
        }
        else
        {
            cvm.SelectedGroupPhotoIds.Clear();
            cvm.SelectedGroupPhotoIds.Add(photo.Id);
        }

        cvm.LastClickedGroupPhotoId = photo.Id;
        GroupGrid.SetSelectedIds(cvm.SelectedGroupPhotoIds);
        _vm.InspectedPhoto = photo;
        UpdateButtonStyles();
    }

    private void OnGroupPhotoRightClicked(Photo photo, MouseButtonEventArgs e)
    {
        var contextMenu = new ContextMenu();

        var removeItem = new MenuItem { Header = "从组中移除" };
        removeItem.Click += (s, args) =>
        {
            _vm.CullingVm.RemovePhotoFromCurrentGroup(photo.Id);
            RefreshGroupList();
            RefreshGroupDetail();
        };
        contextMenu.Items.Add(removeItem);

        // Move to other group submenu
        var moveMenu = new MenuItem { Header = "移入其他组" };
        foreach (var group in _vm.CullingVm.ActiveGroups)
        {
            if (group.Id == _vm.CullingVm.CurrentGroup?.Id) continue;
            var menuItem = new MenuItem
            {
                Header = $"{group.GroupType} ({group.Photos.Count} 张)",
                Tag = group.Id
            };
            menuItem.Click += (s, args) =>
            {
                _vm.CullingVm.MovePhotoToGroup(photo.Id, group.Id);
                RefreshGroupList();
                RefreshGroupDetail();
            };
            moveMenu.Items.Add(menuItem);
        }
        if (moveMenu.Items.Count > 0)
            contextMenu.Items.Add(moveMenu);

        contextMenu.IsOpen = true;
    }

    private void OnComparePhotoSelected(Photo photo)
    {
        _vm.InspectedPhoto = photo;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var cvm = _vm.CullingVm;
        e.Handled = true;

        switch (e.Key)
        {
            case Key.Enter:
                if (cvm.SelectedGroupPhotoIds.Count > 0)
                {
                    cvm.SelectPhotosInGroup(cvm.SelectedGroupPhotoIds);
                    cvm.MoveToNextGroup();
                }
                else
                {
                    cvm.AcceptRecommendation();
                }
                RefreshGroupList();
                RefreshGroupDetail();
                UpdateButtonStyles();
                break;

            case Key.Tab:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    cvm.MoveToPreviousGroup();
                else
                    cvm.MoveToNextGroup();
                RefreshGroupList();
                GroupList.SelectedIndex = cvm.CurrentGroupIndex;
                RefreshGroupDetail();
                break;

            case Key.C:
                TryEnterCompare();
                break;

            case Key.Escape:
                if (cvm.IsComparing)
                    cvm.ExitCompare();
                else
                    cvm.SelectedGroupPhotoIds.Clear();
                GroupGrid.SetSelectedIds(cvm.SelectedGroupPhotoIds);
                break;

            case Key.Left:
                cvm.MoveToPreviousGroup();
                RefreshGroupList();
                GroupList.SelectedIndex = cvm.CurrentGroupIndex;
                break;

            case Key.Right:
                cvm.MoveToNextGroup();
                RefreshGroupList();
                GroupList.SelectedIndex = cvm.CurrentGroupIndex;
                break;

            case Key.D1: case Key.NumPad1: SetRatingForGroupSelected(1); break;
            case Key.D2: case Key.NumPad2: SetRatingForGroupSelected(2); break;
            case Key.D3: case Key.NumPad3: SetRatingForGroupSelected(3); break;
            case Key.D4: case Key.NumPad4: SetRatingForGroupSelected(4); break;
            case Key.D5: case Key.NumPad5: SetRatingForGroupSelected(5); break;

            case Key.Z:
                cvm.Undo();
                RefreshGroupList();
                RefreshGroupDetail();
                break;

            default:
                e.Handled = false;
                break;
        }
    }

    private void SetRatingForGroupSelected(int rating)
    {
        var cvm = _vm.CullingVm;
        if (cvm.SelectedGroupPhotoIds.Count > 0)
            cvm.BatchSetRating(rating, cvm.SelectedGroupPhotoIds);
        else if (cvm.LastClickedGroupPhotoId.HasValue)
            cvm.SetRating(rating, cvm.LastClickedGroupPhotoId.Value);
    }

    private void TryEnterCompare()
    {
        var cvm = _vm.CullingVm;
        List<Guid> ids;
        if (cvm.SelectedGroupPhotoIds.Count >= 2)
        {
            ids = cvm.SelectedGroupPhotoIds.ToList();
        }
        else
        {
            ids = cvm.CurrentGroupPhotos.Select(p => p.Id).Take(10).ToList();
        }

        if (ids.Count >= 2)
        {
            cvm.EnterCompare(ids);
            var photos = cvm.PhotosForCull.Where(p => ids.Contains(p.Id)).ToList();
            Compare.SetPhotos(photos);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) { _vm.BackToQuickCull(); Focus(); }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        _vm.CullingVm.AcceptRecommendation();
        RefreshGroupList();
        RefreshGroupDetail();
        UpdateButtonStyles();
        Focus();
    }

    private void OnConfirmSelectionClick(object sender, RoutedEventArgs e)
    {
        var cvm = _vm.CullingVm;
        if (cvm.SelectedGroupPhotoIds.Count > 0)
        {
            cvm.SelectPhotosInGroup(cvm.SelectedGroupPhotoIds);
            cvm.MoveToNextGroup();
            RefreshGroupList();
            RefreshGroupDetail();
            UpdateButtonStyles();
        }
        Focus();
    }

    private void OnAcceptAllClick(object sender, RoutedEventArgs e)
    {
        _vm.CullingVm.AcceptAllRecommendations();
        RefreshGroupList();
        RefreshGroupDetail();
        Focus();
    }

    private void OnCompareClick(object sender, RoutedEventArgs e) { TryEnterCompare(); Focus(); }

    private void OnClearSelectionClick(object sender, RoutedEventArgs e)
    {
        _vm.CullingVm.ClearGroupSelection();
        RefreshGroupList();
        RefreshGroupDetail();
        Focus();
    }

    private void OnMergeClick(object sender, RoutedEventArgs e)
    {
        _isMergeMode = true;
        var cvm = _vm.CullingVm;
        cvm.SelectedGroupIndices.Clear();
        cvm.SelectedGroupIndices.Add(cvm.CurrentGroupIndex);
        UpdateMergeUI();
        Focus();
    }

    private void OnMergeCancelClick(object sender, RoutedEventArgs e)
    {
        ExitMergeMode();
        Focus();
    }

    private void OnMergeConfirmClick(object sender, RoutedEventArgs e)
    {
        _vm.CullingVm.MergeSelectedGroups();
        ExitMergeMode();
        RefreshGroupList();
        RefreshGroupDetail();
        Focus();
    }

    private void ExitMergeMode()
    {
        _isMergeMode = false;
        _vm.CullingVm.SelectedGroupIndices.Clear();
        UpdateMergeUI();
    }

    private void UpdateMergeUI()
    {
        var count = _vm.CullingVm.SelectedGroupIndices.Count;
        MergeBtn.Visibility = _isMergeMode ? Visibility.Collapsed : Visibility.Visible;
        MergeCancelBtn.Visibility = _isMergeMode ? Visibility.Visible : Visibility.Collapsed;
        MergeConfirmBtn.Visibility = _isMergeMode ? Visibility.Visible : Visibility.Collapsed;
        MergeConfirmBtn.IsEnabled = count >= 2;
        MergeCountText.Visibility = _isMergeMode ? Visibility.Visible : Visibility.Collapsed;
        MergeCountText.Text = $"已选 {count} 组";
    }

    private void UpdateButtonStyles()
    {
        var hasManual = _vm.CullingVm.SelectedGroupPhotoIds.Count > 0;
        AcceptBtn.Background = hasManual ? GrayBrush : GreenBrush;
        AcceptBtn.Content = hasManual ? "接受推荐" : "接受推荐 (Enter)";
        ConfirmBtn.Background = hasManual ? GreenBrush : GrayBrush;
        ConfirmBtn.Content = hasManual ? $"确认选择 ({_vm.CullingVm.SelectedGroupPhotoIds.Count}) (Enter)" : "确认选择";
        ConfirmBtn.IsEnabled = hasManual;
    }

    private void OnFinishClick(object sender, RoutedEventArgs e) => _vm.OnGroupPickComplete();

    private void OnGroupThumbnailSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        GroupGrid?.UpdateThumbnailSize(e.NewValue);
    }
}
