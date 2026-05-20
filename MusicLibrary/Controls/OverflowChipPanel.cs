using System.Windows;
using System.Windows.Controls;

namespace MusicLibrary.Controls;

/// <summary>
/// Custom panel for tag chips in filter toolbar with overflow handling.
/// Convention: last child is the "More" pill ("+ N ещё ▾"), all others are chips.
///
/// Algorithm:
/// * Measure all chips and the More pill.
/// * Greedily fit chips left-to-right while sum <= available width.
/// * If any chip didn't fit (hidden chips exist), need to show More pill.
/// * Ensure visible chips + More pill fit; hide more chips from right if needed.
/// * Expose HiddenCount via DependencyProperty so XAML can bind the More pill text.
/// </summary>
public sealed class OverflowChipPanel : Panel
{
    public static readonly DependencyProperty HiddenCountProperty =
        DependencyProperty.Register(
            nameof(HiddenCount),
            typeof(int),
            typeof(OverflowChipPanel),
            new PropertyMetadata(0));

    public int HiddenCount
    {
        get => (int)GetValue(HiddenCountProperty);
        private set => SetValue(HiddenCountProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        int count = InternalChildren.Count;
        if (count == 0)
        {
            HiddenCount = 0;
            return new Size();
        }

        UIElement morePill = InternalChildren[count - 1];
        var infiniteSlot = new Size(double.PositiveInfinity, constraint.Height);

        // Measure all children to know their desired sizes.
        for (int i = 0; i < count; i++)
        {
            InternalChildren[i].Measure(infiniteSlot);
        }

        double availableWidth = double.IsInfinity(constraint.Width) ? double.MaxValue : constraint.Width;
        double morePillWidth = morePill.DesiredSize.Width;
        int numChips = count - 1;

        // Greedily fit chips left-to-right while sum <= available width.
        double used = 0;
        int lastVisibleChip = -1;
        for (int i = 0; i < numChips; i++)
        {
            double w = InternalChildren[i].DesiredSize.Width;
            if (used + w <= availableWidth)
            {
                used += w;
                lastVisibleChip = i;
            }
            else
            {
                break;
            }
        }

        int hiddenCount = numChips - (lastVisibleChip + 1);

        // If any chips were hidden, reserve space for the More pill — possibly
        // hiding more chips from the right until the More pill fits too.
        if (hiddenCount > 0)
        {
            while (lastVisibleChip >= 0 && used + morePillWidth > availableWidth)
            {
                used -= InternalChildren[lastVisibleChip].DesiredSize.Width;
                lastVisibleChip--;
            }
            hiddenCount = numChips - (lastVisibleChip + 1);
        }

        // Apply visibility.
        for (int i = 0; i < numChips; i++)
        {
            InternalChildren[i].Visibility = i <= lastVisibleChip ? Visibility.Visible : Visibility.Collapsed;
        }
        morePill.Visibility = hiddenCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        HiddenCount = hiddenCount;

        double totalWidth = used + (hiddenCount > 0 ? morePillWidth : 0);
        double totalHeight = 0;
        for (int i = 0; i < count; i++)
        {
            if (InternalChildren[i].Visibility == Visibility.Visible)
            {
                totalHeight = System.Math.Max(totalHeight, InternalChildren[i].DesiredSize.Height);
            }
        }

        return new Size(System.Math.Min(totalWidth, availableWidth), totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = InternalChildren.Count;
        double x = 0;
        for (int i = 0; i < count - 1; i++)
        {
            var c = InternalChildren[i];
            if (c.Visibility != Visibility.Visible) continue;
            c.Arrange(new Rect(x, 0, c.DesiredSize.Width, finalSize.Height));
            x += c.DesiredSize.Width;
        }
        if (count > 0)
        {
            var more = InternalChildren[count - 1];
            if (more.Visibility == Visibility.Visible)
            {
                more.Arrange(new Rect(x, 0, more.DesiredSize.Width, finalSize.Height));
            }
        }
        return finalSize;
    }
}
