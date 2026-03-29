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
    private double? _savedVerticalOffset;

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
        _savedVerticalOffset = ScrollHost.VerticalOffset;
    }

    public void RestoreScrollPosition()
    {
        if (_savedVerticalOffset.HasValue)
        {
            ScrollHost.ScrollToVerticalOffset(_savedVerticalOffset.Value);
            _savedVerticalOffset = null;
        }
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

    public void UpdateSelectionVisuals()
    {
        // Walk visual tree to update borders
        if (PhotoItems.ItemsSource == null) return;
        foreach (var item in PhotoItems.ItemsSource)
        {
            if (item is Photo photo)
            {
                var container = PhotoItems.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null) continue;
                var border = FindChild<System.Windows.Controls.Border>(container);
                if (border != null)
                {
                    border.BorderBrush = _selectedIds.Contains(photo.Id)
                        ? new SolidColorBrush(Color.FromRgb(33, 150, 243))
                        : Brushes.Transparent;
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
