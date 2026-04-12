using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoCull.Helpers;
using PhotoCull.Models;

namespace PhotoCull.Views;

public partial class PhotoGridView : UserControl
{
    // ThumbnailWidth/Height = the Image display area; CellWidth/Height = including info bar + margins
    public static readonly DependencyProperty ThumbnailWidthProperty =
        DependencyProperty.Register(nameof(ThumbnailWidth), typeof(double), typeof(PhotoGridView),
            new PropertyMetadata(300.0, OnThumbnailSizeChanged));

    public static readonly DependencyProperty ThumbnailHeightProperty =
        DependencyProperty.Register(nameof(ThumbnailHeight), typeof(double), typeof(PhotoGridView),
            new PropertyMetadata(225.0, OnThumbnailSizeChanged));

    public static readonly DependencyProperty ThumbnailCellWidthProperty =
        DependencyProperty.Register(nameof(ThumbnailCellWidth), typeof(double), typeof(PhotoGridView),
            new PropertyMetadata(308.0)); // width + margins

    public static readonly DependencyProperty ThumbnailCellHeightProperty =
        DependencyProperty.Register(nameof(ThumbnailCellHeight), typeof(double), typeof(PhotoGridView),
            new PropertyMetadata(270.0)); // height + info bar + margins

    public double ThumbnailWidth
    {
        get => (double)GetValue(ThumbnailWidthProperty);
        set => SetValue(ThumbnailWidthProperty, value);
    }

    public double ThumbnailHeight
    {
        get => (double)GetValue(ThumbnailHeightProperty);
        set => SetValue(ThumbnailHeightProperty, value);
    }

    public double ThumbnailCellWidth
    {
        get => (double)GetValue(ThumbnailCellWidthProperty);
        set => SetValue(ThumbnailCellWidthProperty, value);
    }

    public double ThumbnailCellHeight
    {
        get => (double)GetValue(ThumbnailCellHeightProperty);
        set => SetValue(ThumbnailCellHeightProperty, value);
    }

    public event Action<Photo, MouseEventArgs>? PhotoClicked;
    public event Action<Photo, MouseButtonEventArgs>? PhotoRightClicked;
    public event Action<Photo>? PhotoDoubleClicked;

    private HashSet<Guid> _selectedIds = new();

    // Cache selection brushes
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromRgb(33, 150, 243));
    private static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    static PhotoGridView()
    {
        SelectedBrush.Freeze();
    }

    public PhotoGridView()
    {
        InitializeComponent();
    }

    private static void OnThumbnailSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhotoGridView grid)
        {
            grid.ThumbnailCellWidth = grid.ThumbnailWidth + 8; // 4px margin each side
            grid.ThumbnailCellHeight = grid.ThumbnailHeight + 42; // info bar + margins
        }
    }

    public void SetPhotos(IEnumerable<Photo> photos)
    {
        PhotoItems.ItemsSource = photos;
    }

    public void UpdateThumbnailSize(double width)
    {
        ThumbnailWidth = width;
        ThumbnailHeight = width * 0.75; // 4:3 ratio
    }

    public void SetSelectedIds(HashSet<Guid> ids)
    {
        _selectedIds = ids;
        UpdateSelectionVisuals();
    }

    public void SaveScrollPosition() { /* managed by virtualizing panel */ }
    public void RestoreScrollPosition() { /* managed by virtualizing panel */ }

    /// <summary>
    /// Called when a thumbnail Image element is loaded/recycled.
    /// Uses ThumbnailCache to avoid repeated JPEG decoding.
    /// </summary>
    private void OnThumbImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image img) return;
        if (img.Tag is not Photo photo) return;

        // Use ThumbnailCache for deduplication
        var cached = ThumbnailCache.Shared.Thumbnail(photo.Id.ToString(), photo.ThumbnailData);
        img.Source = cached;

        // Set MaxHeight to ThumbnailHeight for consistent grid layout
        img.MaxHeight = ThumbnailHeight;
        img.MaxWidth = ThumbnailWidth;
    }

    private void OnPhotoClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Photo photo)
        {
            if (e.ClickCount == 2)
            {
                PhotoDoubleClicked?.Invoke(photo);
                return;
            }
            PhotoClicked?.Invoke(photo, e);
        }
    }

    private void OnPhotoRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Photo photo)
        {
            PhotoRightClicked?.Invoke(photo, e);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Parent handles selection via PhotoClicked events
    }

    public void UpdateSelectionVisuals()
    {
        // Only walk materialized containers (much faster with virtualization)
        if (PhotoItems.ItemsSource == null) return;
        for (int i = 0; i < PhotoItems.Items.Count; i++)
        {
            var container = PhotoItems.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue; // Not materialized - virtualized away
            var photo = PhotoItems.Items[i] as Photo;
            if (photo == null) continue;
            var border = FindChild<Border>(container);
            if (border != null)
            {
                border.BorderBrush = _selectedIds.Contains(photo.Id)
                    ? SelectedBrush
                    : TransparentBrush;
            }
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var found = FindChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
