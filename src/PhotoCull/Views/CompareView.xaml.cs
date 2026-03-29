using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PhotoCull.Models;

namespace PhotoCull.Views;

public partial class CompareView : UserControl
{
    public event Action? CloseRequested;
    public event Action<Photo>? PhotoSelected;

    private Guid? _selectedId;

    public CompareView()
    {
        InitializeComponent();
    }

    public void SetPhotos(IEnumerable<Photo> photos)
    {
        CompareItems.ItemsSource = photos;
        _selectedId = null;
        var count = photos.Count();
        var columns = count <= 2 ? 2 : 3;
        if (CompareItems.ItemsPanel?.LoadContent() is UniformGrid ug)
            ug.Columns = columns;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void OnComparePhotoClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Photo photo)
        {
            _selectedId = photo.Id;
            UpdateSelectionVisual();
            PhotoSelected?.Invoke(photo);
        }
    }

    private void UpdateSelectionVisual()
    {
        // Walk items to update borders
        foreach (var item in CompareItems.Items)
        {
            if (item is Photo photo)
            {
                var container = CompareItems.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null) continue;
                var border = FindChild<Border>(container);
                if (border != null)
                {
                    border.BorderBrush = photo.Id == _selectedId
                        ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
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
