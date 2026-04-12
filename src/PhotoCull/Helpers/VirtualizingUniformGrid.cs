using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PhotoCull.Helpers;

/// <summary>
/// A virtualizing panel that lays out items in a uniform grid (wrap) pattern.
/// Unlike WrapPanel, this panel supports UI virtualization by implementing IScrollInfo.
/// Items are laid out in rows, each item having the same width (ItemWidth) and height (ItemHeight).
/// </summary>
public class VirtualizingUniformGrid : VirtualizingPanel, IScrollInfo
{
    #region Dependency Properties

    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(VirtualizingUniformGrid),
            new FrameworkPropertyMetadata(300.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(VirtualizingUniformGrid),
            new FrameworkPropertyMetadata(280.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    #endregion

    #region Layout Calculations

    private int _columnsCount = 1;
    private int _rowCount;
    private int _totalItems;

    private int CalculateColumns(double availableWidth)
    {
        if (ItemWidth <= 0) return 1;
        return Math.Max(1, (int)(availableWidth / ItemWidth));
    }

    private int GetFirstVisibleRow()
    {
        return (int)(_offset.Y / ItemHeight);
    }

    private int GetLastVisibleRow(double viewportHeight)
    {
        return (int)((_offset.Y + viewportHeight) / ItemHeight);
    }

    #endregion

    #region Measure / Arrange

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateScrollInfo(availableSize);

        var generator = ItemContainerGenerator;
        if (generator == null) return availableSize;

        _totalItems = GetItemCount();
        _columnsCount = CalculateColumns(availableSize.Width);
        _rowCount = (_totalItems + _columnsCount - 1) / _columnsCount;

        if (_totalItems == 0) return availableSize;

        // Determine visible range
        var firstVisibleRow = GetFirstVisibleRow();
        var lastVisibleRow = GetLastVisibleRow(availableSize.Height);

        var firstIndex = Math.Max(0, firstVisibleRow * _columnsCount);
        var lastIndex = Math.Min(_totalItems - 1, (lastVisibleRow + 1) * _columnsCount - 1);

        // Add buffer rows for smooth scrolling
        var bufferRows = 2;
        firstIndex = Math.Max(0, firstIndex - bufferRows * _columnsCount);
        lastIndex = Math.Min(_totalItems - 1, lastIndex + bufferRows * _columnsCount);

        // Generate and measure visible items
        var startPos = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = (startPos.Offset == 0) ? startPos.Index : startPos.Index + 1;

        using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
        {
            for (int itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                var child = generator.GenerateNext(out bool isNewlyRealized) as UIElement;
                if (child == null) continue;

                if (isNewlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                        AddInternalChild(child);
                    else
                        InsertInternalChild(childIndex, child);

                    generator.PrepareItemContainer(child);
                }

                child.Measure(new Size(ItemWidth, ItemHeight));
            }
        }

        // Remove items outside the visible range
        CleanupItems(firstIndex, lastIndex);

        // Total extent height
        var totalHeight = _rowCount * ItemHeight;
        _extent = new Size(availableSize.Width, totalHeight);
        _viewport = availableSize;

        ScrollOwner?.InvalidateScrollInfo();

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            var generator = ItemContainerGenerator;
            if (generator == null) continue;

            var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (itemIndex < 0) continue;

            var row = itemIndex / _columnsCount;
            var col = itemIndex % _columnsCount;

            var x = col * ItemWidth;
            var y = row * ItemHeight - _offset.Y;

            child.Arrange(new Rect(x, y, ItemWidth, ItemHeight));
        }

        return finalSize;
    }

    private void CleanupItems(int firstVisibleIndex, int lastVisibleIndex)
    {
        var generator = ItemContainerGenerator;
        if (generator == null) return;

        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var pos = new GeneratorPosition(i, 0);
            var itemIndex = generator.IndexFromGeneratorPosition(pos);

            if (itemIndex < firstVisibleIndex || itemIndex > lastVisibleIndex)
            {
                generator.Remove(pos, 1);
                RemoveInternalChildRange(i, 1);
            }
        }
    }

    private int GetItemCount()
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        return itemsControl?.Items.Count ?? 0;
    }

    #endregion

    #region IScrollInfo

    private ScrollViewer? _scrollOwner;
    private Size _extent = new(0, 0);
    private Size _viewport = new(0, 0);
    private Point _offset = new(0, 0);

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }

    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;

    public ScrollViewer? ScrollOwner
    {
        get => _scrollOwner;
        set => _scrollOwner = value;
    }

    public void LineUp() => SetVerticalOffset(_offset.Y - ItemHeight * 0.5);
    public void LineDown() => SetVerticalOffset(_offset.Y + ItemHeight * 0.5);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageUp() => SetVerticalOffset(_offset.Y - _viewport.Height);
    public void PageDown() => SetVerticalOffset(_offset.Y + _viewport.Height);
    public void MouseWheelUp() => SetVerticalOffset(_offset.Y - ItemHeight);
    public void MouseWheelDown() => SetVerticalOffset(_offset.Y + ItemHeight);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }

    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        offset = Math.Max(0, Math.Min(offset, _extent.Height - _viewport.Height));
        if (Math.Abs(offset - _offset.Y) < 0.5) return;
        _offset.Y = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(System.Windows.Media.Visual visual, Rect rectangle)
    {
        if (visual is UIElement child)
        {
            var generator = ItemContainerGenerator;
            if (generator != null)
            {
                var index = InternalChildren.IndexOf(child);
                if (index >= 0)
                {
                    var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(index, 0));
                    if (itemIndex >= 0)
                    {
                        var row = itemIndex / _columnsCount;
                        var itemTop = row * ItemHeight;
                        var itemBottom = itemTop + ItemHeight;

                        if (itemTop < _offset.Y)
                            SetVerticalOffset(itemTop);
                        else if (itemBottom > _offset.Y + _viewport.Height)
                            SetVerticalOffset(itemBottom - _viewport.Height);
                    }
                }
            }
        }
        return rectangle;
    }

    private void UpdateScrollInfo(Size availableSize)
    {
        _viewport = availableSize;
    }

    #endregion
}
