using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace FloatTodo.WinUI.Animation;

// Commit natural height once; only the compositor animates the action area.
// Collapse immediately so invisible controls cannot retain focus.
internal sealed class EditAreaTransition(FrameworkElement area)
{
    private readonly UISettings _settings = new();
    private bool? _expanded;

    public void SetExpanded(bool expanded)
    {
        if (_expanded == expanded) return;
        var animate = _expanded is not null && area.IsLoaded && _settings.AnimationsEnabled;
        _expanded = expanded;

        var visual = ElementCompositionPreview.GetElementVisual(area);
        visual.StopAnimation("Opacity");
        visual.Opacity = 1;
        area.Height = double.NaN;
        area.IsHitTestVisible = expanded;
        area.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        if (!expanded || !animate) return;

        using var reveal = visual.Compositor.CreateScalarKeyFrameAnimation();
        reveal.Duration = TimeSpan.FromMilliseconds(160);
        reveal.InsertKeyFrame(0, 0);
        reveal.InsertKeyFrame(1, 1);
        visual.StartAnimation("Opacity", reveal);
    }

    public void Stop()
    {
        var visual = ElementCompositionPreview.GetElementVisual(area);
        visual.StopAnimation("Opacity");
        visual.Opacity = 1;
        _expanded = null;
    }
}
