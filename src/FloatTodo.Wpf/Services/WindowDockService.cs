using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using FloatTodo.Wpf.Interop;

namespace FloatTodo.Wpf.Services;

public enum OperationState
{
    Idle,
    Dragging,
    SystemMoving,
    Resizing
}

public enum PresentationState
{
    Visible,
    Showing,
    Hiding,
    Hidden
}

public sealed class WindowDockService : IDisposable
{
    private readonly Window _window;
    private WindowDragController? _dragController;

    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _hoverShowTimer;
    private readonly Stopwatch _animStopwatch = new();

    private double _animStartX;
    private double _animTargetX;
    private double _animDurationMs = 180;
    private int _animVersion;

    private PixelRect _dragStartRect;
    private DockSide _dragStartDockSide;
    private MonitorDescriptor? _hostMonitor;

    public const double VisibleEdgeWidth = 6.0;

    public OperationState CurrentOperationState { get; private set; } = OperationState.Idle;
    public PresentationState CurrentPresentationState { get; private set; } = PresentationState.Visible;
    public DockSide CurrentDockSide { get; private set; } = DockSide.None;

    public DockState CurrentDockState =>
        CurrentDockSide == DockSide.None
            ? DockState.Floating
            : (CurrentPresentationState == PresentationState.Hidden
                ? DockState.DockedHidden
                : DockState.DockedVisible);

    public bool AutoHideEnabled { get; set; } = true;
    public Func<bool>? IsAutoHideSuspended { get; set; }

    public event Action<DockState>? DockStateChanged;
    public event Action<DockSide>? DockSideChanged;

    public WindowDockService(Window window, UIElement? dragElement = null)
    {
        _window = window;

        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _autoHideTimer.Tick += OnAutoHideTick;

        _hoverShowTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _hoverShowTimer.Tick += OnHoverShowTick;

        _window.SourceInitialized += OnSourceInitialized;

        if (dragElement != null)
        {
            AttachDragElement(dragElement);
        }
    }

    public void AttachDragElement(UIElement dragElement)
    {
        if (_dragController != null) return;

        _dragController = new WindowDragController(dragElement, _window);
        _dragController.DragStarted += OnDragStarted;
        _dragController.DragMoved += OnDragMoved;
        _dragController.DragCompleted += OnDragCompleted;
        _dragController.DragCanceled += OnDragCanceled;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(_window);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);

        var monitors = MonitorHelper.GetAllMonitors(_window);
        var curInfo = MonitorHelper.GetCurrentMonitorInfo(_window);
        NativeMethods.GetCursorPos(out var pt);
        _hostMonitor = DockGeometry.SelectTargetMonitor(
            new PixelPoint(pt.X, pt.Y),
            new PixelRect(curInfo.PhysicalWorkArea.Left, curInfo.PhysicalWorkArea.Top, curInfo.PhysicalWorkArea.Left + 320, curInfo.PhysicalWorkArea.Top + 560),
            monitors);
    }

    private void OnDragStarted(PixelPoint currentCursor, PixelPoint anchorOffset)
    {
        CurrentOperationState = OperationState.Dragging;
        _autoHideTimer.Stop();
        _hoverShowTimer.Stop();
        StopAnimation();

        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int winLeft = (int)Math.Round(_window.Left * mi.DpiScaleX);
        int winTop = (int)Math.Round(_window.Top * mi.DpiScaleY);
        int winWidth = (int)Math.Round(_window.ActualWidth * mi.DpiScaleX);
        int winHeight = (int)Math.Round(_window.ActualHeight * mi.DpiScaleY);

        _dragStartRect = PixelRect.FromXYWH(winLeft, winTop, winWidth, winHeight);
        _dragStartDockSide = CurrentDockSide;
    }

    private void OnDragMoved(PixelPoint currentCursor, PixelPoint anchorOffset)
    {
        if (CurrentOperationState != OperationState.Dragging) return;

        var allMonitors = MonitorHelper.GetAllMonitors(_window);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int winWidth = (int)Math.Round(_window.ActualWidth * mi.DpiScaleX);
        int winHeight = (int)Math.Round(_window.ActualHeight * mi.DpiScaleY);

        var candidateRect = new PixelRect(
            currentCursor.X - anchorOffset.X,
            currentCursor.Y - anchorOffset.Y,
            currentCursor.X - anchorOffset.X + winWidth,
            currentCursor.Y - anchorOffset.Y + winHeight
        );

        _hostMonitor = DockGeometry.SelectTargetMonitor(currentCursor, candidateRect, allMonitors);

        var placement = DockGeometry.CalculateDockPlacement(
            currentCursor,
            anchorOffset,
            winWidth,
            winHeight,
            _hostMonitor,
            CurrentDockSide
        );

        if (CurrentDockSide != placement.TargetDockSide)
        {
            CurrentDockSide = placement.TargetDockSide;
            DockSideChanged?.Invoke(CurrentDockSide);
            DockStateChanged?.Invoke(CurrentDockState);
        }

        var helper = new WindowInteropHelper(_window);
        NativeMethods.SetWindowPos(
            helper.Handle,
            IntPtr.Zero,
            placement.TargetRect.Left,
            placement.TargetRect.Top,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE
        );
    }

    private void OnDragCompleted()
    {
        if (CurrentOperationState != OperationState.Dragging) return;

        CurrentOperationState = OperationState.Idle;

        var helper = new WindowInteropHelper(_window);
        if (NativeMethods.GetWindowRect(helper.Handle, out var rect))
        {
            var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
            _window.Left = rect.Left / mi.DpiScaleX;
            _window.Top = rect.Top / mi.DpiScaleY;
        }

        DockStateChanged?.Invoke(CurrentDockState);
        DockSideChanged?.Invoke(CurrentDockSide);

        EvaluateAutoHide();
    }

    private void OnDragCanceled()
    {
        if (CurrentOperationState != OperationState.Dragging) return;

        CurrentOperationState = OperationState.Idle;
        CurrentDockSide = _dragStartDockSide;

        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        var helper = new WindowInteropHelper(_window);
        NativeMethods.SetWindowPos(
            helper.Handle,
            IntPtr.Zero,
            _dragStartRect.Left,
            _dragStartRect.Top,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE
        );

        _window.Left = _dragStartRect.Left / mi.DpiScaleX;
        _window.Top = _dragStartRect.Top / mi.DpiScaleY;

        DockStateChanged?.Invoke(CurrentDockState);
        DockSideChanged?.Invoke(CurrentDockSide);

        EvaluateAutoHide();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            // 0. When hidden at edge, intercept WM_NCHITTEST so exposed handle acts as client area
            case NativeMethods.WM_NCHITTEST:
            {
                if (CurrentPresentationState == PresentationState.Hidden)
                {
                    handled = true;
                    return new IntPtr(NativeMethods.HTCLIENT);
                }
                break;
            }

            // Immediately wake up on mouse movement over exposed strip
            case NativeMethods.WM_MOUSEMOVE:
            case NativeMethods.WM_NCMOUSEMOVE:
            {
                if (CurrentPresentationState == PresentationState.Hidden)
                {
                    _autoHideTimer.Stop();
                    _hoverShowTimer.Stop();
                    _hoverShowTimer.Start();
                }
                break;
            }

            // 1. Hard ceiling to completely prevent Windows Aero Snap / split-screen
            case NativeMethods.WM_GETMINMAXINFO:
            {
                var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
                var mi = MonitorHelper.GetCurrentMonitorInfo(_window);

                mmi.ptMinTrackSize.X = (int)Math.Round(_window.MinWidth * mi.DpiScaleX);
                mmi.ptMaxTrackSize.X = (int)Math.Round(_window.MaxWidth * mi.DpiScaleX);
                mmi.ptMinTrackSize.Y = (int)Math.Round(_window.MinHeight * mi.DpiScaleY);

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
                break;
            }

            // 2. Disable double click titlebar to maximize
            case NativeMethods.WM_SYSCOMMAND:
            {
                int sc = wParam.ToInt32() & 0xFFF0;
                if (sc == NativeMethods.SC_MAXIMIZE)
                {
                    handled = true;
                }
                break;
            }

            // 3. System resizing lifecycle
            case NativeMethods.WM_ENTERSIZEMOVE:
            {
                if (CurrentOperationState == OperationState.Idle)
                {
                    CurrentOperationState = OperationState.Resizing;
                    _autoHideTimer.Stop();
                    _hoverShowTimer.Stop();
                    StopAnimation();
                }
                break;
            }

            // 4. Pin docked edge during native sizing to prevent jumps and race conditions
            case NativeMethods.WM_SIZING:
            {
                var rect = Marshal.PtrToStructure<NativeMethods.RECT>(lParam);
                var mi = MonitorHelper.GetCurrentMonitorInfo(_window);

                if (CurrentDockSide == DockSide.Right)
                {
                    rect.Right = mi.PhysicalWorkArea.Right;
                    Marshal.StructureToPtr(rect, lParam, true);
                    handled = true;
                }
                else if (CurrentDockSide == DockSide.Left)
                {
                    rect.Left = mi.PhysicalWorkArea.Left;
                    Marshal.StructureToPtr(rect, lParam, true);
                    handled = true;
                }
                break;
            }

            case NativeMethods.WM_EXITSIZEMOVE:
            {
                if (CurrentOperationState == OperationState.Resizing)
                {
                    CurrentOperationState = OperationState.Idle;

                    var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
                    if (NativeMethods.GetWindowRect(hwnd, out var rect))
                    {
                        _window.Left = rect.Left / mi.DpiScaleX;
                        _window.Top = rect.Top / mi.DpiScaleY;
                        _window.Width = rect.Width / mi.DpiScaleX;
                        _window.Height = rect.Height / mi.DpiScaleY;
                    }

                    EvaluateAutoHide();
                }
                break;
            }

            // 5. Monitor topology / resolution changes
            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_SETTINGCHANGE:
            {
                StopAnimation();
                EnsureVisibleAfterTopologyChange();
                break;
            }
        }
        return IntPtr.Zero;
    }

    private void EnsureVisibleAfterTopologyChange()
    {
        var allMonitors = MonitorHelper.GetAllMonitors(_window);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);

        int winLeft = (int)Math.Round(_window.Left * mi.DpiScaleX);
        int winTop = (int)Math.Round(_window.Top * mi.DpiScaleY);
        int winWidth = (int)Math.Round(_window.ActualWidth * mi.DpiScaleX);
        int winHeight = (int)Math.Round(_window.ActualHeight * mi.DpiScaleY);
        var curRect = PixelRect.FromXYWH(winLeft, winTop, winWidth, winHeight);

        _hostMonitor = DockGeometry.SelectTargetMonitor(new PixelPoint(winLeft, winTop), curRect, allMonitors);

        // If outside monitor work area, pull back to visible work area
        if (curRect.Left > _hostMonitor.WorkArea.Right || curRect.Right < _hostMonitor.WorkArea.Left ||
            curRect.Top > _hostMonitor.WorkArea.Bottom || curRect.Bottom < _hostMonitor.WorkArea.Top)
        {
            _window.Left = _hostMonitor.WorkArea.Right / mi.DpiScaleX - _window.ActualWidth;
            _window.Top = _hostMonitor.WorkArea.Top / mi.DpiScaleY;
            CurrentDockSide = DockSide.None;
            CurrentPresentationState = PresentationState.Visible;
            DockSideChanged?.Invoke(CurrentDockSide);
            DockStateChanged?.Invoke(CurrentDockState);
        }
    }

    public void InitializePlacement(double left, double top, DockSide dockSide)
    {
        var allMonitors = MonitorHelper.GetAllMonitors(_window);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);

        int winLeft = (int)Math.Round(left * mi.DpiScaleX);
        int winTop = (int)Math.Round(top * mi.DpiScaleY);
        int winWidth = (int)Math.Round((_window.ActualWidth > 0 ? _window.ActualWidth : _window.Width) * mi.DpiScaleX);
        int winHeight = (int)Math.Round((_window.ActualHeight > 0 ? _window.ActualHeight : _window.Height) * mi.DpiScaleY);
        var curRect = PixelRect.FromXYWH(winLeft, winTop, winWidth, winHeight);

        _hostMonitor = DockGeometry.SelectTargetMonitor(new PixelPoint(winLeft, winTop), curRect, allMonitors);

        CurrentDockSide = dockSide;
        CurrentPresentationState = PresentationState.Visible;
        DockSideChanged?.Invoke(CurrentDockSide);
        DockStateChanged?.Invoke(CurrentDockState);
    }

    public void RequestDockState(DockState targetState)
    {
        if (CurrentDockSide == DockSide.None)
        {
            CurrentPresentationState = PresentationState.Visible;
            DockStateChanged?.Invoke(CurrentDockState);
            return;
        }

        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        double targetX = _window.Left;

        if (CurrentDockSide == DockSide.Right)
        {
            targetX = targetState switch
            {
                DockState.DockedHidden => mi.WorkArea.Right - VisibleEdgeWidth,
                _ => mi.WorkArea.Right - _window.ActualWidth
            };
        }
        else if (CurrentDockSide == DockSide.Left)
        {
            targetX = targetState switch
            {
                DockState.DockedHidden => mi.WorkArea.Left - _window.ActualWidth + VisibleEdgeWidth,
                _ => mi.WorkArea.Left
            };
        }

        StartSlideAnimation(targetX, targetState == DockState.DockedHidden ? PresentationState.Hidden : PresentationState.Visible);
    }

    private void StartSlideAnimation(double targetX, PresentationState targetPresentation)
    {
        StopAnimation();

        _animStartX = _window.Left;
        _animTargetX = targetX;
        _animDurationMs = 180;
        _animVersion++;

        if (targetPresentation == PresentationState.Visible)
        {
            CurrentPresentationState = PresentationState.Showing;
            DockStateChanged?.Invoke(CurrentDockState);
        }
        else
        {
            CurrentPresentationState = PresentationState.Hiding;
        }

        _animStopwatch.Restart();
        CompositionTarget.Rendering += OnVsyncRenderingFrame;
    }

    private void StopAnimation()
    {
        CompositionTarget.Rendering -= OnVsyncRenderingFrame;
        _animStopwatch.Stop();
    }

    private void OnVsyncRenderingFrame(object? sender, EventArgs e)
    {
        double elapsed = _animStopwatch.Elapsed.TotalMilliseconds;
        double progress = Math.Clamp(elapsed / _animDurationMs, 0.0, 1.0);

        bool isHiding = CurrentPresentationState == PresentationState.Hiding;
        double eased = isHiding
            ? Math.Pow(progress, 3)                // EaseIn
            : 1.0 - Math.Pow(1.0 - progress, 3);    // EaseOut

        double currentX = _animStartX + (_animTargetX - _animStartX) * eased;

        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int physX = (int)Math.Round(currentX * mi.DpiScaleX);
        int physY = (int)Math.Round(_window.Top * mi.DpiScaleY);

        var helper = new WindowInteropHelper(_window);
        NativeMethods.SetWindowPos(
            helper.Handle,
            IntPtr.Zero,
            physX,
            physY,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE
        );

        if (progress >= 1.0)
        {
            StopAnimation();

            _window.Left = _animTargetX;
            CurrentPresentationState = isHiding ? PresentationState.Hidden : PresentationState.Visible;
            DockStateChanged?.Invoke(CurrentDockState);
        }
    }

    public void OnWindowMouseEnter()
    {
        _autoHideTimer.Stop();
        if (CurrentPresentationState == PresentationState.Hidden)
        {
            _hoverShowTimer.Stop();
            _hoverShowTimer.Start();
        }
    }

    public void OnWindowMouseLeave()
    {
        if (CurrentOperationState == OperationState.Idle &&
            CurrentDockSide != DockSide.None &&
            AutoHideEnabled &&
            CurrentPresentationState == PresentationState.Visible)
        {
            EvaluateAutoHide();
        }
    }

    public void OnEdgeHandleMouseEnter() => OnWindowMouseEnter();
    public void OnEdgeHandleMouseLeave() => OnWindowMouseLeave();

    private void OnHoverShowTick(object? sender, EventArgs e)
    {
        _hoverShowTimer.Stop();

        if (CurrentPresentationState != PresentationState.Hidden) return;

        // Re-verify that cursor is indeed on the window's physical handle
        NativeMethods.GetCursorPos(out var pt);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int winLeftPx = (int)Math.Round(_window.Left * mi.DpiScaleX);
        int winTopPx = (int)Math.Round(_window.Top * mi.DpiScaleY);
        int winRightPx = (int)Math.Round((_window.Left + _window.ActualWidth) * mi.DpiScaleX);
        int winBottomPx = (int)Math.Round((_window.Top + _window.ActualHeight) * mi.DpiScaleY);

        if (pt.X >= winLeftPx && pt.X <= winRightPx && pt.Y >= winTopPx && pt.Y <= winBottomPx)
        {
            RequestDockState(DockState.DockedVisible);
        }
    }

    private void OnAutoHideTick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();

        if (CurrentOperationState != OperationState.Idle ||
            !AutoHideEnabled ||
            CurrentDockSide == DockSide.None ||
            CurrentPresentationState != PresentationState.Visible)
        {
            return;
        }

        // Keep keyboard entry visible even when the pointer leaves the panel.
        if (IsAutoHideSuspended?.Invoke() == true)
        {
            _autoHideTimer.Start();
            return;
        }

        // Double check: if mouse physically inside window, do not hide!
        NativeMethods.GetCursorPos(out var pt);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int winLeftPx = (int)Math.Round(_window.Left * mi.DpiScaleX);
        int winTopPx = (int)Math.Round(_window.Top * mi.DpiScaleY);
        int winRightPx = (int)Math.Round((_window.Left + _window.ActualWidth) * mi.DpiScaleX);
        int winBottomPx = (int)Math.Round((_window.Top + _window.ActualHeight) * mi.DpiScaleY);

        if (pt.X >= winLeftPx && pt.X <= winRightPx && pt.Y >= winTopPx && pt.Y <= winBottomPx)
        {
            return;
        }

        // Check hiding safety
        var allMonitors = MonitorHelper.GetAllMonitors(_window);
        var expandedRect = PixelRect.FromXYWH(winLeftPx, winTopPx, winRightPx - winLeftPx, winBottomPx - winTopPx);
        int visibleEdgePx = (int)Math.Round(VisibleEdgeWidth * mi.DpiScaleX);

        if (!DockGeometry.IsSafeToHide(CurrentDockSide, expandedRect, _hostMonitor ?? allMonitors[0], allMonitors, visibleEdgePx))
        {
            return;
        }

        RequestDockState(DockState.DockedHidden);
    }

    private void EvaluateAutoHide()
    {
        if (CurrentOperationState != OperationState.Idle ||
            !AutoHideEnabled ||
            CurrentDockSide == DockSide.None ||
            CurrentPresentationState != PresentationState.Visible)
        {
            return;
        }

        var allMonitors = MonitorHelper.GetAllMonitors(_window);
        var mi = MonitorHelper.GetCurrentMonitorInfo(_window);
        int winLeftPx = (int)Math.Round(_window.Left * mi.DpiScaleX);
        int winTopPx = (int)Math.Round(_window.Top * mi.DpiScaleY);
        int winWidthPx = (int)Math.Round(_window.ActualWidth * mi.DpiScaleX);
        int winHeightPx = (int)Math.Round(_window.ActualHeight * mi.DpiScaleY);
        var expandedRect = PixelRect.FromXYWH(winLeftPx, winTopPx, winWidthPx, winHeightPx);

        int visibleEdgePx = (int)Math.Round(VisibleEdgeWidth * mi.DpiScaleX);
        if (!DockGeometry.IsSafeToHide(CurrentDockSide, expandedRect, _hostMonitor ?? allMonitors[0], allMonitors, visibleEdgePx))
        {
            return;
        }

        _autoHideTimer.Stop();
        _autoHideTimer.Start();
    }

    public void Dispose()
    {
        _autoHideTimer.Stop();
        _hoverShowTimer.Stop();
        StopAnimation();

        var helper = new WindowInteropHelper(_window);
        if (helper.Handle != IntPtr.Zero)
        {
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.RemoveHook(WndProc);
        }
    }
}
