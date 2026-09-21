using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using FloatTodo.WinUI.Interop;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;

namespace FloatTodo.WinUI.Windowing;

// XAML owns pointer capture; physical cursor deltas drive both moving and resizing.
// No native move/size loop is entered, so Windows Snap never resizes this panel.
internal sealed class WindowDragController : IDisposable
{
    private readonly MainWindow _window;
    private readonly WindowPlacementService _placement;
    private readonly DockController _dock;

    private readonly PointerEventHandler _press, _release, _canceled, _captureLost, _moved;
    private Microsoft.UI.Xaml.Input.Pointer? _pointer;
    private PixelRect _original;
    private PixelPoint _anchor, _start;
    private DockSide _side, _originalSide;
    private int _edges;
    private bool _dragging;
    private PixelPoint? _lastCursor;
    private double _dragDpi;

    public bool IsInteracting => _pointer is not null || _dragging;

    public WindowDragController(MainWindow window, WindowPlacementService placement, DockController dock)
    {
        _window = window; _placement = placement; _dock = dock;

        _press = Press;
        _release = Released;
        _canceled = Canceled;
        _captureLost = CaptureLost;
        _moved = Moved;
        window.PanelRoot.AddHandler(UIElement.PointerPressedEvent, _press, true);
        window.PanelRoot.AddHandler(UIElement.PointerReleasedEvent, _release, true);
        window.PanelRoot.AddHandler(UIElement.PointerCanceledEvent, _canceled, true);
        window.PanelRoot.AddHandler(UIElement.PointerCaptureLostEvent, _captureLost, true);
        window.PanelRoot.AddHandler(UIElement.PointerMovedEvent, _moved, true);
    }

    private void Press(object sender, PointerRoutedEventArgs e)
    {
        if (_window.IsPreviewMode || _pointer is not null || !e.GetCurrentPoint(_window.PanelRoot).Properties.IsLeftButtonPressed || _window.IsDialogOpen) return;
        var point = e.GetCurrentPoint(_window.PanelRoot).Position;
        var root = _window.PanelRoot;
        var border = PanelSurface.ResizeHandleThickness;
        _edges = (point.X < border ? 1 : point.X > root.ActualWidth - border ? 2 : 0)
            | (point.Y < border ? 4 : point.Y > root.ActualHeight - border ? 8 : 0);
        var handle = _window.CurrentDragHandle;
        var title = handle.TransformToVisual(root).TransformPoint(new(0, 0));
        var inTitle = point.X >= title.X && point.X < title.X + handle.ActualWidth
            && point.Y >= title.Y && point.Y < title.Y + handle.ActualHeight;

        if (_edges == 0 && inTitle)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(hwnd, 0x0112, 0xF012, 0);
            e.Handled = true;
            return;
        }

        if (_edges == 0) return;
        if (!root.CapturePointer(e.Pointer)) return;
        _pointer = e.Pointer;
        _window.PanelRoot.IsCursorLocked = true;
        NativeMethods.GetCursorPos(out var p);
        _original = _placement.Bounds; _originalSide = _side = _dock.Side;
        _start = new(p.X, p.Y); _anchor = new(p.X - _original.Left, p.Y - _original.Top);
        _lastCursor = _start; _dragDpi = _placement.Monitor.DpiScaleX;
        _placement.BeginPointerDrag(); e.Handled = true;
    }

    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer is null) return;
        Tick();
        e.Handled = true;
    }

    private void Tick()
    {
        if ((NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0) { Finish(true); return; }
        NativeMethods.GetCursorPos(out var p);
        var cursor = new PixelPoint(p.X, p.Y);
        if (_lastCursor == cursor) return;
        _lastCursor = cursor;
        var dx = p.X - _start.X; var dy = p.Y - _start.Y;
        var dpi = _dragDpi;
        if (!_dragging && Math.Abs(dx) < 3 * dpi && Math.Abs(dy) < 3 * dpi) return;
        if (!_dragging) { _dock.BeginDrag(); _dragging = true; }
        if (_edges == 0) return;

        var minW = (int)(280 * dpi); var minH = (int)(220 * dpi);
        var left = (_edges & 1) != 0 ? Math.Min(_original.Left + dx, _original.Right - minW) : _original.Left;
        var right = (_edges & 2) != 0 ? Math.Max(_original.Right + dx, _original.Left + minW) : _original.Right;
        var top = (_edges & 4) != 0 ? Math.Min(_original.Top + dy, _original.Bottom - minH) : _original.Top;
        var bottom = (_edges & 8) != 0 ? Math.Max(_original.Bottom + dy, _original.Top + minH) : _original.Bottom;

        // Screen edge snapping during resize if within threshold (16 DIP)
        var work = _placement.Monitor.WorkArea;
        var snapPx = (int)Math.Round(16.0 * dpi);
        if ((_edges & 1) != 0 && Math.Abs(left - work.Left) <= snapPx) left = work.Left;
        if ((_edges & 2) != 0 && Math.Abs(right - work.Right) <= snapPx) right = work.Right;
        if ((_edges & 4) != 0 && Math.Abs(top - work.Top) <= snapPx) top = work.Top;
        if ((_edges & 8) != 0 && Math.Abs(bottom - work.Bottom) <= snapPx) bottom = work.Bottom;

        _window.AppWindow.MoveAndResize(new(left, top, right - left, bottom - top));
    }

    private void Released(object sender, PointerRoutedEventArgs e) => Finish(false);
    private void Canceled(object sender, PointerRoutedEventArgs e) => Finish(false);
    private void CaptureLost(object sender, PointerRoutedEventArgs e) => Finish(false);
    private void Finish(bool cancel)
    {
        if (_pointer is null) return;
        _pointer = null;
        _window.PanelRoot.IsCursorLocked = false;
        _placement.EndPointerDrag();
        if (_dragging)
        {
            _dragging = false;
            if (cancel)
            {
                _window.AppWindow.MoveAndResize(new(_original.Left, _original.Top, _original.Width, _original.Height));
                _dock.EndDrag(_originalSide);
            }
            else if (_edges != 0)
            {
                var bounds = _placement.Bounds;
                var work = _placement.Monitor.WorkArea;
                if (bounds.Right >= work.Right - 2) _side = DockSide.Right;
                else if (bounds.Left <= work.Left + 2) _side = DockSide.Left;
                _dock.EndDrag(_side);
                if (_side != DockSide.None)
                {
                    _dock.AlignToEdge();
                }
            }
            else
            {
                _dock.EndDrag(_side);
            }
        }
        else if (_edges != 0)
        {
            _dock.AlignToEdge();
        }
        if (cancel)
        {
            try { _window.PanelRoot.ReleasePointerCaptures(); }
            catch { }
        }
        _window.ConfigureInputRegions();
    }
    public void Dispose()
    {
        Finish(false);
        _window.PanelRoot.RemoveHandler(UIElement.PointerPressedEvent, _press);
        _window.PanelRoot.RemoveHandler(UIElement.PointerReleasedEvent, _release);
        _window.PanelRoot.RemoveHandler(UIElement.PointerCanceledEvent, _canceled);
        _window.PanelRoot.RemoveHandler(UIElement.PointerCaptureLostEvent, _captureLost);
        _window.PanelRoot.RemoveHandler(UIElement.PointerMovedEvent, _moved);
    }
}



