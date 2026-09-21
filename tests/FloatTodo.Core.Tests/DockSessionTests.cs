using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using Xunit;

namespace FloatTodo.Core.Tests;

public class DockSessionTests
{
    [Fact]
    public void OldHideCompletionCannotHideAReopenedPanel()
    {
        var session = new DockSession();
        PanelTransition latest = default;
        session.TransitionRequested += t => latest = t;
        session.SetSide(DockSide.Right); session.Complete(latest);
        session.RequestVisible(false); var hide = latest;
        session.RequestVisible(true); var show = latest;
        Assert.False(session.Complete(hide));
        Assert.Equal(PanelState.Showing, session.State);
        Assert.True(session.Complete(show));
        Assert.Equal(PanelState.DockedVisible, session.State);
    }
    [Fact]
    public void DragInvalidatesAnimationsAndDetachingDisablesSlide()
    {
        var session = new DockSession();
        PanelTransition latest = default;
        session.TransitionRequested += t => latest = t;
        session.SetSide(DockSide.Left);
        session.RequestVisible(false); var hide = latest;
        session.BeginDrag();
        Assert.False(session.Complete(hide));
        Assert.False(session.Complete(latest));
        Assert.Equal(PanelState.Dragging, session.State);
        session.SetSide(DockSide.None);
        Assert.False(latest.Animate);
        Assert.True(session.Complete(latest));
        Assert.Equal(PanelState.Detached, session.State);
    }
}
