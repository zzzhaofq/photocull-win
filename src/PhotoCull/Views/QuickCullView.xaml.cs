using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PhotoCull.Models;
using PhotoCull.ViewModels;

namespace PhotoCull.Views;

public partial class QuickCullView : UserControl
{
    private readonly MainViewModel _vm;
    private int? _filterRating;

    // Cached brushes for tab visuals (avoid creating new ones on every update)
    private static readonly SolidColorBrush TabActiveBrush = CreateFrozenBrush(62, 62, 62);
    private static readonly SolidColorBrush TabInactiveBrush = CreateFrozenBrush(42, 42, 42);
    private static readonly SolidColorBrush RedTextBrush = CreateFrozenBrush(244, 67, 54);
    private static readonly SolidColorBrush GreenTextBrush = CreateFrozenBrush(76, 175, 80);
    private static readonly SolidColorBrush GrayTextBrush = CreateFrozenBrush(170, 170, 170);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public QuickCullView(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;

        Grid.PhotoClicked += OnGridPhotoClicked;
        Grid.PhotoDoubleClicked += OnGridPhotoDoubleClicked;
        Detail.CloseRequested += () =>
        {
            _vm.CullingVm.IsZoomed = false;
            Grid.RestoreScrollPosition();
        };

        _vm.CullingVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(CullingViewModel.ActiveTab) or
                nameof(CullingViewModel.RejectedCount) or
                nameof(CullingViewModel.PhotosForCull))
            {
                RefreshGrid();
            }
            if (e.PropertyName == nameof(CullingViewModel.SelectedPhotoId))
            {
                UpdateDetailView();
                UpdateInspector();
            }
        };

        Loaded += (s, e) =>
        {
            Focus();
            RefreshGrid();
        };
    }

    private void RefreshGrid()
    {
        var cvm = _vm.CullingVm;
        var photos = cvm.ActiveTab == QuickCullTab.Rejected ? cvm.RejectedPhotos : cvm.KeptPhotos;
        if (_filterRating.HasValue)
            photos = photos.Where(p => p.Rating == _filterRating.Value).ToList();
        Grid.SetPhotos(photos);
        UpdateTabVisuals();
        CountText.Text = $"{cvm.RejectedCount} 淘汰 / {cvm.KeptPhotos.Count} 保留";
    }

    public void SetFilterRating(int? rating)
    {
        _filterRating = rating;
        RefreshGrid();
    }

    private void UpdateTabVisuals()
    {
        var isRejected = _vm.CullingVm.ActiveTab == QuickCullTab.Rejected;
        RejectedTab.Background = isRejected ? TabActiveBrush : TabInactiveBrush;
        KeptTab.Background = !isRejected ? TabActiveBrush : TabInactiveBrush;

        RejectedTabText.Text = $"AI 建议淘汰 ({_vm.CullingVm.RejectedPhotos.Count})";
        RejectedTabText.Foreground = isRejected ? RedTextBrush : GrayTextBrush;

        KeptTabText.Text = $"AI 建议保留 ({_vm.CullingVm.KeptPhotos.Count})";
        KeptTabText.Foreground = !isRejected ? GreenTextBrush : GrayTextBrush;
    }

    private void UpdateDetailView()
    {
        var cvm = _vm.CullingVm;
        if (cvm.IsZoomed && cvm.SelectedPhotoId.HasValue)
        {
            var photo = cvm.FindPhotoById(cvm.SelectedPhotoId.Value);
            Detail.SetPhoto(photo);
        }
    }

    private void UpdateInspector()
    {
        var cvm = _vm.CullingVm;
        if (cvm.SelectedPhotoId.HasValue)
        {
            _vm.InspectedPhoto = cvm.FindPhotoById(cvm.SelectedPhotoId.Value);
        }
    }

    private void OnGridPhotoClicked(Photo photo, MouseEventArgs e)
    {
        var cvm = _vm.CullingVm;
        var me = e as MouseButtonEventArgs;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Toggle selection
            if (cvm.SelectedPhotoIds.Contains(photo.Id))
                cvm.SelectedPhotoIds.Remove(photo.Id);
            else
                cvm.SelectedPhotoIds.Add(photo.Id);
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && cvm.LastClickedPhotoId.HasValue)
        {
            // Range selection
            var photos = cvm.ActiveTab == QuickCullTab.Rejected ? cvm.RejectedPhotos : cvm.KeptPhotos;
            var startIdx = photos.FindIndex(p => p.Id == cvm.LastClickedPhotoId);
            var endIdx = photos.FindIndex(p => p.Id == photo.Id);
            if (startIdx >= 0 && endIdx >= 0)
            {
                var min = Math.Min(startIdx, endIdx);
                var max = Math.Max(startIdx, endIdx);
                for (int i = min; i <= max; i++)
                    cvm.SelectedPhotoIds.Add(photos[i].Id);
            }
        }
        else
        {
            cvm.SelectedPhotoIds.Clear();
            cvm.SelectedPhotoIds.Add(photo.Id);
        }

        cvm.LastClickedPhotoId = photo.Id;
        cvm.SelectedPhotoId = photo.Id;

        // Update currentPhotoIndex
        var allIdx = cvm.PhotosForCull.FindIndex(p => p.Id == photo.Id);
        if (allIdx >= 0) cvm.CurrentPhotoIndex = allIdx;

        Grid.SetSelectedIds(cvm.SelectedPhotoIds);
    }

    private void OnGridPhotoDoubleClicked(Photo photo)
    {
        Grid.SaveScrollPosition();
        _vm.CullingVm.IsZoomed = true;
        Detail.SetPhoto(photo);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var cvm = _vm.CullingVm;
        e.Handled = true;

        switch (e.Key)
        {
            case Key.X:
                if (cvm.SelectedPhotoIds.Count > 1)
                    cvm.BatchMoveToRejected(cvm.SelectedPhotoIds);
                else
                    cvm.RejectCurrent();
                RefreshGrid();
                break;

            case Key.P:
                if (cvm.SelectedPhotoIds.Count > 1)
                    cvm.BatchMoveToKept(cvm.SelectedPhotoIds);
                else
                    cvm.KeepCurrent();
                RefreshGrid();
                break;

            case Key.D1: case Key.NumPad1: SetRatingForSelected(1); break;
            case Key.D2: case Key.NumPad2: SetRatingForSelected(2); break;
            case Key.D3: case Key.NumPad3: SetRatingForSelected(3); break;
            case Key.D4: case Key.NumPad4: SetRatingForSelected(4); break;
            case Key.D5: case Key.NumPad5: SetRatingForSelected(5); break;

            case Key.Z:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    cvm.Redo();
                else
                    cvm.Undo();
                RefreshGrid();
                break;

            case Key.Space:
                if (!cvm.IsZoomed) Grid.SaveScrollPosition();
                cvm.ToggleZoom();
                if (cvm.IsZoomed) UpdateDetailView();
                else Grid.RestoreScrollPosition();
                break;

            case Key.Escape:
                if (cvm.IsZoomed)
                {
                    cvm.IsZoomed = false;
                    Grid.RestoreScrollPosition();
                }
                else
                    cvm.SelectedPhotoIds.Clear();
                Grid.SetSelectedIds(cvm.SelectedPhotoIds);
                break;

            case Key.Right:
                cvm.MoveNext();
                // Don't call RefreshGrid for simple navigation - just update selection
                UpdateNavigationSelection(cvm);
                break;

            case Key.Left:
                cvm.MovePrevious();
                UpdateNavigationSelection(cvm);
                break;

            case Key.Home:
                cvm.CurrentPhotoIndex = 0;
                if (cvm.PhotosForCull.Count > 0)
                    cvm.SelectedPhotoId = cvm.PhotosForCull[0].Id;
                break;

            case Key.End:
                if (cvm.PhotosForCull.Count > 0)
                {
                    cvm.CurrentPhotoIndex = cvm.PhotosForCull.Count - 1;
                    cvm.SelectedPhotoId = cvm.PhotosForCull[^1].Id;
                }
                break;

            case Key.Tab:
                cvm.ActiveTab = cvm.ActiveTab == QuickCullTab.Rejected ? QuickCullTab.Kept : QuickCullTab.Rejected;
                RefreshGrid();
                break;

            case Key.A:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    // Select all
                    var photos = cvm.ActiveTab == QuickCullTab.Rejected ? cvm.RejectedPhotos : cvm.KeptPhotos;
                    cvm.SelectedPhotoIds = new HashSet<Guid>(photos.Select(p => p.Id));
                    Grid.SetSelectedIds(cvm.SelectedPhotoIds);
                }
                break;

            case Key.Enter:
                _vm.OnQuickCullComplete();
                break;

            default:
                e.Handled = false;
                break;
        }
    }

    /// <summary>
    /// Lightweight navigation update - only updates selection visual without rebuilding entire grid.
    /// </summary>
    private void UpdateNavigationSelection(CullingViewModel cvm)
    {
        if (cvm.SelectedPhotoId.HasValue)
        {
            cvm.SelectedPhotoIds.Clear();
            cvm.SelectedPhotoIds.Add(cvm.SelectedPhotoId.Value);
            Grid.SetSelectedIds(cvm.SelectedPhotoIds);
        }
    }

    private void SetRatingForSelected(int rating)
    {
        var cvm = _vm.CullingVm;
        if (cvm.SelectedPhotoIds.Count > 1)
            cvm.BatchSetRating(rating, cvm.SelectedPhotoIds);
        else if (cvm.SelectedPhotoId.HasValue)
            cvm.SetRating(rating, cvm.SelectedPhotoId.Value);
        RefreshGrid();
    }

    private void OnRejectClick(object sender, RoutedEventArgs e) { _vm.CullingVm.RejectCurrent(); RefreshGrid(); Focus(); }
    private void OnKeepClick(object sender, RoutedEventArgs e) { _vm.CullingVm.KeepCurrent(); RefreshGrid(); Focus(); }
    private void OnUndoClick(object sender, RoutedEventArgs e) { _vm.CullingVm.Undo(); RefreshGrid(); Focus(); }

    private void OnRatingClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out var rating))
            SetRatingForSelected(rating);
        Focus();
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _vm.CullingVm.ActiveTab = tag == "Rejected" ? QuickCullTab.Rejected : QuickCullTab.Kept;
            RefreshGrid();
        }
        Focus();
    }

    private void OnNextPhaseClick(object sender, RoutedEventArgs e) => _vm.OnQuickCullComplete();

    private void OnThumbnailSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Grid?.UpdateThumbnailSize(e.NewValue);
    }
}
