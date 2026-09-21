using FloatTodo.Core.Models;

namespace FloatTodo.Core.Windowing;

public enum PanelState { Detached, DockedVisible, DockedHidden, Showing, Hiding, Dragging }
public readonly record struct PanelTransition(long Revision, bool Visible, bool Animate);

/// <summary>UI-independent intent arbitration. Stale animation completions cannot commit a newer request.</summary>
public sealed class DockSession
{
    private long _revision;
    public DockSide Side { get; private set; }
    public PanelState State { get; private set; } = PanelState.Detached;
    public bool DesiredVisible { get; private set; } = true;
    public event Action<PanelTransition>? TransitionRequested;
    public void SetSide(DockSide side)
    {
        Side = side;
        RequestVisible(true, animate: false);
    }
    public void BeginDrag()
    {
        // Cancel any visual transition before entering drag; the native drag loop owns position.
        RequestVisible(true, animate: false);
        State = PanelState.Dragging;
    }
    public void RequestVisible(bool visible, bool animate = true)
    {
        DesiredVisible = visible;
        var revision = ++_revision;
        State = visible ? PanelState.Showing : PanelState.Hiding;
        TransitionRequested?.Invoke(new(revision, visible, animate && Side != DockSide.None));
    }
    public bool Complete(PanelTransition transition)
    {
        if (transition.Revision != _revision || State == PanelState.Dragging) return false;
        State = DesiredVisible
            ? Side == DockSide.None ? PanelState.Detached : PanelState.DockedVisible
            : PanelState.DockedHidden;
        return true;
    }
}
