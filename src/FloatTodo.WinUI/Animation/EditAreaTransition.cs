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
        {
            area.Height = expanded ? double.NaN : 0;
            area.Opacity = expanded ? 1 : 0;
            area.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        area.Height = current;
        var duration = TimeSpan.FromMilliseconds(240);
        var easing = new CircleEase { EasingMode = EasingMode.EaseOut };

        var storyboard = new Storyboard();
        var heightAnim = new DoubleAnimation
        {
            From = current,
            To = target,
            Duration = new(duration),
            EnableDependentAnimation = true,
            EasingFunction = easing
        };
        Storyboard.SetTarget(heightAnim, area);
        Storyboard.SetTargetProperty(heightAnim, "Height");
        storyboard.Children.Add(heightAnim);

        var opacityAnim = new DoubleAnimation
        {
            From = area.Opacity,
            To = expanded ? 1 : 0,
            Duration = new(expanded ? duration : TimeSpan.FromMilliseconds(160)),
            EasingFunction = easing
        };
        Storyboard.SetTarget(opacityAnim, area);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        storyboard.Children.Add(opacityAnim);

        _active = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (_active != storyboard) return;
            _active = null;
            storyboard.Stop();
            area.Height = expanded ? double.NaN : 0;
            area.Opacity = expanded ? 1 : 0;
            area.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        };
        storyboard.Begin();
    }
    public void Stop() { _active?.Stop(); _active = null; _expanded = null; }
}
