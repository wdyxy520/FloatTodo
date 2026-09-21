using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using FloatTodo.WinUI.Animation;
using FloatTodo.WinUI.Interop;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
namespace FloatTodo.WinUI.Windowing;

internal sealed class DockController : IDisposable
{
    private readonly MainWindow _window; private readonly nint _hwnd; private readonly WindowPlacementService _placement; private readonly EdgeHandleController _edge = new();
    private readonly PanelAnimationController _animation; private readonly WindowMessageHook _hook; private readonly DispatcherQueueTimer _poll, _intent;
    private readonly DockSession _session = new(); private DateTimeOffset? _outside; private bool _hidden, _disposed, _explicitHide, _waitForPointerEntry;
    private readonly WindowDragController _drag;
    public DockSide Side => _session.Side;
    public bool IsInteracting => _drag.IsInteracting;
    public DockController(MainWindow window, nint hwnd)
    {
        _window = window; _hwnd = hwnd; _placement = new(window, hwnd); _animation = new(window.PanelRoot); _hook = new(hwnd);
        _placement.Restore(window.Settings.Window);
        _session.TransitionRequested += Transition;
        _session.SetSide(window.Settings.Window.DockSide);
        _drag = new(window, _placement, this);
        _intent = window.DispatcherQueue.CreateTimer(); _intent.IsRepeating = false; _intent.Interval = TimeSpan.FromMilliseconds(60);
        _intent.Tick += (_, _) => { NativeMethods.GetCursorPos(out var p); if (_edge.Contains(p.X, p.Y)) Show(); };
        _edge.Hovered += () => { if (!_intent.IsRunning) _intent.Start(); };
        _poll = window.DispatcherQueue.CreateTimer(); _poll.Interval = TimeSpan.FromMilliseconds(80); _poll.Tick += (_, _) => CheckPointer(); _poll.Start();
        window.PanelRoot.PointerEntered += (_, _) => { _waitForPointerEntry = false; if (!_explicitHide && _session.State == PanelState.Hiding) Show(); _outside = null; };
        _hook.Message += (msg, _, _) =>
        {
            if (msg == 0x231) { _session.BeginDrag(); _outside = null; }
            else if (msg == 0x232) { _session.SetSide(_placement.Snap()); Save(); }
            else if (msg == 0x2E0 || msg == 0x7E) window.DispatcherQueue.TryEnqueue(() => { if (_disposed) return; _placement.Place(Side); if (_hidden) _edge.Show(_placement.Bounds, Side); Save(); });
        };
    }
    public void Show(bool activate = false) { _explicitHide = false; if (activate) _waitForPointerEntry = true; _outside = null; _session.RequestVisible(true); if (activate) NativeMethods.SetForegroundWindow(_hwnd); }
    public void Hide(bool explicitRequest = true) { if (_window.IsPreviewMode) return; _waitForPointerEntry = false; _explicitHide = explicitRequest; _session.RequestVisible(false); }
    public void SuspendForPreview()
    {
        _intent.Stop(); _outside = null; _edge.Hide();
        _session.RequestVisible(true, animate: false);
    }
    public void ResumeFromPreview()
    {
        _outside = null; _waitForPointerEntry = true;
        _session.RequestVisible(true, animate: false);
    }
    public void AlignToEdge() => _placement.Place(Side);
    public void BeginDrag()
    {
        _intent.Stop(); _outside = null; _edge.Hide();
        _session.BeginDrag();
    }
    public void EndDrag(DockSide side)
    {
        _session.SetSide(side);
        _outside = null;
        Save();
    }
    public void SetSide(DockSide side) { _placement.Place(side); _session.SetSide(side); Save(); }
    private void CheckPointer()
    {
        if (_window.IsPreviewMode || _hidden || Side == DockSide.None || !_window.Settings.Window.AutoHide || _window.ViewModel.IsEditing || _window.IsDialogOpen || _session.State == PanelState.Dragging || IsInteracting) { _outside = null; return; }
        NativeMethods.GetCursorPos(out var p);
        if (_placement.Bounds.Contains(new(p.X, p.Y))) { _waitForPointerEntry = false; }
        if (_placement.Bounds.Contains(new(p.X, p.Y)) || _edge.Contains(p.X, p.Y)) { if (!_explicitHide && _session.State == PanelState.Hiding) Show(); _outside = null; return; }
        if (_waitForPointerEntry || _session.State == PanelState.Hiding) return;
        _outside ??= DateTimeOffset.UtcNow; if (DateTimeOffset.UtcNow - _outside > TimeSpan.FromMilliseconds(450)) Hide(false);
    }
    private void Transition(PanelTransition request)
    {
        if (_disposed) return;
        if (request.Visible)
        {
            if (_hidden) { _placement.Place(Side); _animation.SetHidden(Side); _window.AppWindow.Show(false); _hidden = false; }
            // Keep the hit-testable edge until the main window has submitted its next XAML frame.
            EventHandler<object>? firstFrame = null;
            firstFrame = (_, _) => { CompositionTarget.Rendering -= firstFrame; if (!_disposed && _session.DesiredVisible) _edge.Hide(); };
            CompositionTarget.Rendering += firstFrame;
        }
        else { _edge.Show(_placement.Bounds, Side); }
        _animation.Run(request.Visible, Side, request.Animate, () =>
        {
            if (_disposed || !_session.Complete(request)) return;
            if (!request.Visible) { _window.AppWindow.Hide(); _hidden = true; }
            else _edge.Hide();
            Save();
        });
    }
    private void Save() { _placement.Save(_window.Settings.Window, Side); _window.Settings.Window.DockState = _hidden ? DockState.DockedHidden : Side == DockSide.None ? DockState.Floating : DockState.DockedVisible; _window.SaveSettings(); }
    public void Dispose() { _disposed = true; _drag.Dispose(); _poll.Stop(); _intent.Stop(); _hook.Dispose(); _edge.Dispose(); }
}






