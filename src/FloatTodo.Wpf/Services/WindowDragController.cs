using System;
using System.Windows;
using System.Windows.Input;
using FloatTodo.Core.Windowing;
using FloatTodo.Wpf.Interop;

namespace FloatTodo.Wpf.Services;

public sealed class WindowDragController
{
    private readonly UIElement _dragElement;
    private readonly Window _window;

    private bool _isPressed;
    private bool _isDragging;
    private Point _pressStartScreenPoint;
    private PixelPoint _cursorAnchorOffset;

    public bool IsDragging => _isDragging;

    public event Action<PixelPoint, PixelPoint>? DragStarted; // (currentCursor, anchorOffset)
    public event Action<PixelPoint, PixelPoint>? DragMoved;   // (currentCursor, anchorOffset)
    public event Action? DragCompleted;
    public event Action? DragCanceled;

    public WindowDragController(UIElement dragElement, Window window)
    {
        _dragElement = dragElement;
        _window = window;

        _dragElement.MouseLeftButtonDown += OnMouseLeftButtonDown;
        _dragElement.MouseMove += OnMouseMove;
        _dragElement.MouseLeftButtonUp += OnMouseLeftButtonUp;
        _dragElement.LostMouseCapture += OnLostMouseCapture;
        _window.KeyDown += OnWindowKeyDown;
        _window.Deactivated += OnWindowDeactivated;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        _isPressed = true;
        _isDragging = false;
        _pressStartScreenPoint = _dragElement.PointToScreen(e.GetPosition(_dragElement));

        NativeMethods.GetCursorPos(out var pt);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int winLeftPx = (int)Math.Round(_window.Left * mi.DpiScaleX);
        int winTopPx = (int)Math.Round(_window.Top * mi.DpiScaleY);

        _cursorAnchorOffset = new PixelPoint(pt.X - winLeftPx, pt.Y - winTopPx);
        _dragElement.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPressed) return;

        Point currentScreenPoint = _dragElement.PointToScreen(e.GetPosition(_dragElement));
        NativeMethods.GetCursorPos(out var pt);
        var currentCursor = new PixelPoint(pt.X, pt.Y);

        if (!_isDragging)
        {
            double dx = Math.Abs(currentScreenPoint.X - _pressStartScreenPoint.X);
            double dy = Math.Abs(currentScreenPoint.Y - _pressStartScreenPoint.Y);

            if (dx >= SystemParameters.MinimumHorizontalDragDistance ||
                dy >= SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                DragStarted?.Invoke(currentCursor, _cursorAnchorOffset);
            }
        }

        if (_isDragging)
        {
            DragMoved?.Invoke(currentCursor, _cursorAnchorOffset);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPressed)
        {
            CompleteDrag();
        }
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isPressed)
        {
            CompleteDrag();
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isDragging)
        {
            CancelDrag();
            e.Handled = true;
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_isDragging)
        {
            CompleteDrag();
        }
    }

    private void CompleteDrag()
    {
        if (!_isPressed) return;

        bool wasDragging = _isDragging;
        _isPressed = false;
        _isDragging = false;
        _dragElement.ReleaseMouseCapture();

        if (wasDragging)
        {
            DragCompleted?.Invoke();
        }
    }

    public void CancelDrag()
    {
        if (!_isPressed) return;

        bool wasDragging = _isDragging;
        _isPressed = false;
        _isDragging = false;
        _dragElement.ReleaseMouseCapture();

        if (wasDragging)
        {
            DragCanceled?.Invoke();
        }
    }
}
