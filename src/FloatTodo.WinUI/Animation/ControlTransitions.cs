using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace FloatTodo.WinUI.Animation;

internal static class ControlTransitions
{
    // Keep text and surfaces opaque and stable. Animate only layout displacement.
    public static void Attach(FrameworkElement element)
    {
        void Apply()
        {
            try
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;
                var offset = compositor.CreateVector3KeyFrameAnimation();
                var easing = compositor.CreateCubicBezierEasingFunction(new(.1f, .9f), new(.2f, 1f));
                offset.Target = "Offset";
                offset.Duration = TimeSpan.FromMilliseconds(240);
                offset.InsertExpressionKeyFrame(0, "this.StartingValue");
                offset.InsertExpressionKeyFrame(1, "this.FinalValue", easing);
                var animations = compositor.CreateImplicitAnimationCollection();
                animations["Offset"] = offset;
                visual.ImplicitAnimations = animations;
            }
            catch
            {
            }
        }

        if (element.IsLoaded)
        {
            Apply();
        }
        else
        {
            element.Loaded += (_, _) => Apply();
        }
        element.Unloaded += (_, _) =>
        {
            try { ElementCompositionPreview.GetElementVisual(element).ImplicitAnimations = null; } catch { }
        };
    }
}
