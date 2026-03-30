using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PhotoCull.Models;

namespace PhotoCull.Views;

public partial class PhotoGridView : UserControl
{
    public static readonly DependencyProperty ThumbnailWidthProperty =
        DependencyProperty.Register(nameof(ThumbnailWidth), typeof(double), typeof(PhotoGridView),
            new PropertyMetadata(300.0));

    public static readonly DependencyProperty ThumbnailHeightProperty =
        DependencyProperty.Register(nameof(ThumbnailHeight), typeof(double), typeof(PhotoGridView),
            new PropertyMetadata(225.0));

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

    public event Action<Photo, MouseEventArgs>? PhotoClicked;
    public event Action<Photo, MouseButtonEventArgs>? PhotoRightClicked;
    public event Action<Photo>? PhotoDoubleClicked;

    private HashSet<Guid> _selectedIds = new();

    // Cache selection brush to avoid creating new instances each time
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

    public void SaveScrollPosition()
    {
        // With ListBox, scroll position is managed automatically
    }

    public void RestoreScrollPosition()
    {
        // With ListBox, scroll position is managed automatically
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
        // Let the parent handle selection via PhotoClicked events
    }

    public void UpdateSelectionVisuals()
    {
        // Walk only visible containers (much faster with virtualization)
        if (PhotoItems.ItemsSource == null) return;
        foreach (var item in PhotoItems.ItemsSource)
        {
            if (item is Photo photo)
            {
                var container = PhotoItems.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container == null) continue; // Not materialized - skip (virtualized away)
                var border = FindChild<Border>(container);
                if (border != null)
                {
                    border.BorderBrush = _selectedIds.Contains(photo.Id)
                        ? SelectedBrush
                        : TransparentBrush;
                }
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
