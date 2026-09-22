using Microsoft.UI.Xaml.Hosting;
using System.Runtime.CompilerServices;
using Windows.UI.ViewManagement;

namespace FloatTodo.WinUI.Animation;

internal static class ControlTransitions
{
    private static readonly ConditionalWeakTable<FrameworkElement, object> Attached = new();
    // Keep text and surfaces opaque and stable. Animate only layout displacement.
    public static void Attach(FrameworkElement element)
    {
        if (Attached.TryGetValue(element, out _)) return;
        Attached.Add(element, new object());

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

        element.Loaded += (_, _) => Apply();
        if (element.IsLoaded) Apply();
        element.Unloaded += (_, _) =>
        {
            try { ElementCompositionPreview.GetElementVisual(element).ImplicitAnimations = null; } catch { }
        };
    }
}
