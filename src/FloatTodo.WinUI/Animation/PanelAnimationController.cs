using System.Numerics;
using FloatTodo.Core.Models;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace FloatTodo.WinUI.Animation;

internal sealed class PanelAnimationController
{
    private readonly FrameworkElement _surface;
    private readonly Visual _visual;
    private readonly UISettings _uiSettings = new();

    public PanelAnimationController(FrameworkElement surface)
    {
        _surface = surface;
        ElementCompositionPreview.SetIsTranslationEnabled(surface, true);
        _visual = ElementCompositionPreview.GetElementVisual(surface);
    }

    private float Outside(DockSide side) => (float)(_surface.ActualWidth + 1) * (side == DockSide.Left ? -1 : 1);

    public void SetHidden(DockSide side)
    {
        _visual.StopAnimation("Translation.X");
        _visual.StopAnimation("Translation.Y");
        _visual.Opacity = 1;
        var translation = side == DockSide.Top
            ? new Vector3(0, -(float)(_surface.ActualHeight + 1), 0)
            : new Vector3(Outside(side), 0, 0);
        _visual.Properties.InsertVector3("Translation", translation);
    }

    public void Run(bool show, DockSide side, bool animate, Action completed)
    {
        if (!animate || !_uiSettings.AnimationsEnabled)
        {
            _visual.StopAnimation("Translation.X");
            _visual.StopAnimation("Translation.Y");
            _visual.Opacity = show ? 1 : 0;
            _visual.Properties.InsertVector3("Translation", show ? Vector3.Zero : (side == DockSide.Top ? new(0, -(float)(_surface.ActualHeight + 1), 0) : new(Outside(side), 0, 0)));
            completed();
            return;
        }
        // The background, outline, caption and content are all descendants of this visual.
        // The native host remains transparent and stationary; its rectangle never animates.
        _visual.Opacity = 1;
        var compositor = _visual.Compositor;
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        var easing = compositor.CreateCubicBezierEasingFunction(new(.2f, 0), new(.2f, 1));
        var move = compositor.CreateScalarKeyFrameAnimation();
        move.Duration = TimeSpan.FromMilliseconds(show ? 220 : 200);
        move.StopBehavior = AnimationStopBehavior.LeaveCurrentValue;
        move.InsertExpressionKeyFrame(0, "this.StartingValue");

        string prop;
        if (side == DockSide.Top)
        {
            _visual.StopAnimation("Translation.X");
            prop = "Translation.Y";
            move.InsertKeyFrame(1, show ? 0 : -(float)(_surface.ActualHeight + 1), easing);
        }
        else
        {
            _visual.StopAnimation("Translation.Y");
            prop = "Translation.X";
            move.InsertKeyFrame(1, show ? 0 : Outside(side), easing);
        }

        batch.Completed += (_, _) =>
        {
            if (show)
            {
                _visual.Properties.InsertVector3("Translation", Vector3.Zero);
            }
            completed();
            batch.Dispose(); move.Dispose(); easing.Dispose();
        };
        _visual.StartAnimation(prop, move);
        batch.End();
    }
}
