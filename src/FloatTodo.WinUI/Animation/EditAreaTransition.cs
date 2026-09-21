using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace FloatTodo.WinUI.Animation;

// Animate the small action area in layout so the card and its neighbours share
// one continuous height. The text is never faded, copied or scaled.
internal sealed class EditAreaTransition(FrameworkElement area)
{
    private Storyboard? _active;
    private bool? _expanded;
    public void SetExpanded(bool expanded)
    {
        if (_expanded == expanded) return;
        var first = _expanded is null; _expanded = expanded;
        var current = area.ActualHeight;
        _active?.Stop(); _active = null;
        area.IsHitTestVisible = expanded;
        area.Visibility = Visibility.Visible;
        area.Height = double.NaN;
        area.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
        var target = expanded ? area.DesiredSize.Height : 0;
        if (first || !area.IsLoaded || !new UISettings().AnimationsEnabled)
        { area.Height = expanded ? double.NaN : 0; area.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed; return; }
        area.Height = current;
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = current, To = target, Duration = new(TimeSpan.FromMilliseconds(170)),
            EnableDependentAnimation = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, area); Storyboard.SetTargetProperty(animation, "Height");
        storyboard.Children.Add(animation); _active = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (_active != storyboard) return;
            _active = null; storyboard.Stop();
            area.Height = expanded ? double.NaN : 0;
            area.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        };
        storyboard.Begin();
    }
    public void Stop() { _active?.Stop(); _active = null; _expanded = null; }
}
