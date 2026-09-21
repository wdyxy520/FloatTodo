using System.Runtime.InteropServices;
using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using FloatTodo.WinUI.Interop;

namespace FloatTodo.WinUI.Windowing;

internal sealed class MonitorService
{
    public MonitorDescriptor ForWindow(nint hwnd) => Read(NativeMethods.MonitorFromWindow(hwnd, 2));

    public IReadOnlyList<MonitorDescriptor> All()
    {
        var monitors = new List<MonitorDescriptor>();
        NativeMethods.MonitorEnumProc callback = (nint monitor, nint dc, ref NativeMethods.Rect rect, nint data) =>
        {
            monitors.Add(Read(monitor));
            return true;
        };
        NativeMethods.EnumDisplayMonitors(0, 0, callback, 0);
        return monitors;
    }

    private static MonitorDescriptor Read(nint handle)
    {
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(handle, ref info)) throw new InvalidOperationException("无法读取显示器工作区");
        var dpi = NativeMethods.GetDpiForMonitor(handle, 0, out var x, out var y) == 0 ? x / 96.0 : 1;
        return new()
        {
            Handle = handle,
            MonitorArea = new(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom),
            WorkArea = new(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom),
            DpiScaleX = dpi, DpiScaleY = dpi, IsPrimary = (info.Flags & 1) != 0
        };
    }
}

internal sealed class WindowPlacementService(MainWindow window, nint hwnd)
{
    private readonly MonitorService _monitors = new();
    private IReadOnlyList<MonitorDescriptor>? _dragMonitors;
    private int _lastLeft = int.MinValue;
    private int _lastTop = int.MinValue;

    public void BeginPointerDrag()
    {
        _dragMonitors = _monitors.All();
        var bounds = Bounds;
        _lastLeft = bounds.Left;
        _lastTop = bounds.Top;
    }

    public void EndPointerDrag()
    {
        _dragMonitors = null;
        _lastLeft = int.MinValue;
        _lastTop = int.MinValue;
    }

    public PixelRect Bounds
    {
        get { NativeMethods.GetWindowRect(hwnd, out var r); return new(r.Left, r.Top, r.Right, r.Bottom); }
    }
    public MonitorDescriptor Monitor => _monitors.ForWindow(hwnd);

    public DockSide MoveDuringDrag(PixelPoint cursor, PixelPoint anchor, DockSide currentSide, int width, int height)
    {
        var candidate = PixelRect.FromXYWH(cursor.X - anchor.X, cursor.Y - anchor.Y, width, height);
        var monitor = DockGeometry.SelectTargetMonitor(cursor, candidate, (_dragMonitors ?? _monitors.All()));
        // 16 DIP capture and 28 DIP release hysteresis.
        var placement = DockGeometry.CalculateDockPlacement(cursor, anchor, width, height, monitor, currentSide);
        MoveFast(placement.TargetRect.Left, placement.TargetRect.Top);
        return placement.TargetDockSide;
    }

    public DockSide MoveDuringDrag(PixelPoint cursor, PixelPoint anchor, DockSide currentSide)
    {
        var bounds = Bounds;
        return MoveDuringDrag(cursor, anchor, currentSide, bounds.Width, bounds.Height);
    }

    public DockSide Snap()
    {
        var bounds = Bounds;
        NativeMethods.GetCursorPos(out var cursor);
        return MoveDuringDrag(new(cursor.X, cursor.Y), new(cursor.X - bounds.Left, cursor.Y - bounds.Top), DockSide.None, bounds.Width, bounds.Height);
    }

    public void MoveFast(int left, int top)
    {
        if (_lastLeft != left || _lastTop != top)
        {
            _lastLeft = left;
            _lastTop = top;
            NativeMethods.SetWindowPos(hwnd, 0, left, top, 0, 0, 0x15);
        }
    }

    public void Move(PixelRect rect)
    {
        var current = Bounds;
        if (current.Left != rect.Left || current.Top != rect.Top)
        {
            _lastLeft = rect.Left;
            _lastTop = rect.Top;
            NativeMethods.SetWindowPos(hwnd, 0, rect.Left, rect.Top, 0, 0, 0x15);
        }
    }

    public void Place(DockSide side)
    {
        var r = Bounds;
        var m = Monitor;
        var x = side == DockSide.Left ? m.WorkArea.Left : side == DockSide.Right ? m.WorkArea.Right - r.Width : Math.Clamp(r.Left, m.WorkArea.Left, Math.Max(m.WorkArea.Left, m.WorkArea.Right - r.Width));
        var y = side == DockSide.Top ? m.WorkArea.Top : Math.Clamp(r.Top, m.WorkArea.Top, Math.Max(m.WorkArea.Top, m.WorkArea.Bottom - r.Height));
        Move(PixelRect.FromXYWH(x, y, r.Width, r.Height));
    }

    public void Restore(WindowSettings settings)
    {
        var dpi = Monitor.DpiScaleX;
        window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            (int)Math.Round(settings.Left * dpi), (int)Math.Round(settings.Top * dpi),
            (int)Math.Round(Math.Clamp(settings.Width, 280, 1000) * dpi),
            (int)Math.Round(Math.Clamp(settings.Height, 220, 1600) * dpi)));
        Place(settings.DockSide);
    }

    public void Save(WindowSettings settings, DockSide side)
    {
        var bounds = Bounds;
        var dpi = Monitor.DpiScaleX;
        settings.Left = bounds.Left / dpi;
        settings.Top = bounds.Top / dpi;
        settings.Width = bounds.Width / dpi;
        settings.Height = bounds.Height / dpi;
        settings.DockSide = side;
    }
}

