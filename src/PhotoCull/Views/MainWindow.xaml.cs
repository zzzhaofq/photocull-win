using System.Windows;
using System.Windows.Controls;
using PhotoCull.Models;
using PhotoCull.ViewModels;

namespace PhotoCull.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private int? _filterRating;

    // View instances (created once)
    private readonly LicenseView _licenseView;
    private readonly ImportView _importView;
    private readonly QuickCullView _quickCullView;
    private readonly GroupPickView _groupPickView;
    private readonly ExportView _exportView;

    // Rating filter button/count references
    private Button[] _filterButtons = Array.Empty<Button>();
    private TextBlock[] _filterCountTexts = Array.Empty<TextBlock>();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        _licenseView = new LicenseView(_vm);
        _importView = new ImportView(_vm);
        _quickCullView = new QuickCullView(_vm);
        _groupPickView = new GroupPickView(_vm);
        _exportView = new ExportView(_vm);

        _filterButtons = new[] { FilterAll, FilterNoStar, Filter1Star, Filter2Star, Filter3Star, Filter4Star, Filter5Star };
        _filterCountTexts = new[] { FilterAllCount, FilterNoStarCount, Filter1StarCount, Filter2StarCount, Filter3StarCount, Filter4StarCount, Filter5StarCount };

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPhase))
                SwitchView();
            if (e.PropertyName == nameof(MainViewModel.InspectedPhoto))
                Inspector.SetPhoto(_vm.InspectedPhoto);
        };

        _vm.CullingVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(CullingViewModel.RejectedCount) or
                nameof(CullingViewModel.GroupPickSelectedCount) or
                nameof(CullingViewModel.RatingCounts) or
                nameof(CullingViewModel.ActiveTab))
            {
                UpdateStats();
                UpdateRatingFilter();
            }
        };

        SwitchView();
        UpdateStats();
    }

    private void SwitchView()
    {
        _filterRating = null;
        _quickCullView.SetFilterRating(null);
        _groupPickView.SetFilterRating(null);

        MainContent.Content = _vm.CurrentPhase switch
        {
            AppPhase.License => _licenseView,
            AppPhase.Import => _importView,
            AppPhase.QuickCull => _quickCullView,
            AppPhase.GroupPick => _groupPickView,
            AppPhase.Export => _exportView,
            _ => _licenseView
        };

        // Show rating filter only in QuickCull and GroupPick phases
        RatingFilterPanel.Visibility = _vm.CurrentPhase is AppPhase.QuickCull or AppPhase.GroupPick
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Hide inspector by default in GroupPick
        if (_vm.CurrentPhase == AppPhase.GroupPick)
            _vm.IsInspectorVisible = false;

        UpdateStats();
        UpdateRatingFilter();
    }

    private void UpdateStats()
    {
        var session = _vm.CullingVm.Session;
        if (session != null)
        {
            StatsTotal.Text = $"总计: {session.TotalCount}";
            StatsRejected.Text = $"淘汰: {_vm.CullingVm.RejectedCount}";
            StatsSelected.Text = $"保留: {_vm.CullingVm.GroupPickSelectedCount}";
        }
        else
        {
            StatsTotal.Text = "总计: -";
            StatsRejected.Text = "淘汰: -";
            StatsSelected.Text = "保留: -";
        }
    }

    private void OnPhaseClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsLicenseValid) return;
        if (sender is Button btn && btn.Tag is string tag)
        {
            var phase = tag switch
            {
                "Import" => AppPhase.Import,
                "QuickCull" => AppPhase.QuickCull,
                "GroupPick" => AppPhase.GroupPick,
                "Export" => AppPhase.Export,
                _ => _vm.CurrentPhase
            };
            _vm.NavigateToPhase(phase);
        }
    }

    private void OnRatingFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString();

        int? newRating;
        if (string.IsNullOrEmpty(tag))
            newRating = null; // "全部照片" button
        else
            newRating = int.Parse(tag);

        // Toggle: if already active, clear filter
        _filterRating = _filterRating == newRating ? null : newRating;

        _quickCullView.SetFilterRating(_filterRating);
        _groupPickView.SetFilterRating(_filterRating);
        UpdateRatingFilterHighlight();
    }

    // Cached brush for rating filter highlight
    private static readonly System.Windows.Media.SolidColorBrush RatingHighlightBrush;

    static MainWindow()
    {
        RatingHighlightBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(38, 33, 150, 243));
        RatingHighlightBrush.Freeze();
    }

    private void UpdateRatingFilter()
    {
        if (RatingFilterPanel.Visibility != Visibility.Visible) return;

        var cvm = _vm.CullingVm;
        var photos = _vm.CurrentPhase == AppPhase.QuickCull
            ? (cvm.ActiveTab == QuickCullTab.Rejected ? cvm.RejectedPhotos : cvm.KeptPhotos)
            : cvm.KeptPhotos;

        var total = photos.Count;
        var counts = new int[6]; // 0=no star, 1-5=star rating
        foreach (var photo in photos)
        {
            var r = Math.Max(0, Math.Min(5, photo.Rating));
            counts[r]++;
        }

        // Update count labels: index 0=All, 1=NoStar, 2-6=1-5 stars
        _filterCountTexts[0].Text = total.ToString();
        _filterCountTexts[1].Text = counts[0].ToString();
        for (int i = 1; i <= 5; i++)
            _filterCountTexts[i + 1].Text = counts[i].ToString();

        UpdateRatingFilterHighlight();
    }

    private void UpdateRatingFilterHighlight()
    {
        var transparentBrush = System.Windows.Media.Brushes.Transparent;

        // Index mapping: 0=All(null), 1=NoStar(0), 2=1star, 3=2star, 4=3star, 5=4star, 6=5star
        for (int i = 0; i < _filterButtons.Length; i++)
        {
            int? buttonRating = i == 0 ? null : (i - 1);
            _filterButtons[i].Background = _filterRating == buttonRating ? RatingHighlightBrush : transparentBrush;
        }
    }
}
