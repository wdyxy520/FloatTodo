using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FloatTodo.Wpf.Interop;

internal readonly record struct MonitorAreaInfo(
    Rect WorkArea, 
    Rect MonitorArea, 
    NativeMethods.RECT PhysicalWorkArea,
    NativeMethods.RECT PhysicalMonitorArea,
    double DpiScaleX, 
    double DpiScaleY);

internal static class MonitorHelper
{
    public static MonitorAreaInfo GetCurrentMonitorInfo(Window window)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.EnsureHandle();

        var mi = new NativeMethods.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            var workArea = new Rect(
                mi.rcWork.Left / dpiX,
                mi.rcWork.Top / dpiY,
                mi.rcWork.Width / dpiX,
                mi.rcWork.Height / dpiY);

            var monitorArea = new Rect(
                mi.rcMonitor.Left / dpiX,
                mi.rcMonitor.Top / dpiY,
                mi.rcMonitor.Width / dpiX,
                mi.rcMonitor.Height / dpiY);

            return new MonitorAreaInfo(workArea, monitorArea, mi.rcWork, mi.rcMonitor, dpiX, dpiY);
        }

        // Fallback to WPF SystemParameters
        var fallbackArea = SystemParameters.WorkArea;
        var fallbackRect = new NativeMethods.RECT
        {
            Left = (int)fallbackArea.Left,
            Top = (int)fallbackArea.Top,
            Right = (int)fallbackArea.Right,
            Bottom = (int)fallbackArea.Bottom
        };
        return new MonitorAreaInfo(fallbackArea, fallbackArea, fallbackRect, fallbackRect, 1.0, 1.0);
    }

    public static IReadOnlyList<FloatTodo.Core.Windowing.MonitorDescriptor> GetAllMonitors(Window window)
    {
        var list = new List<FloatTodo.Core.Windowing.MonitorDescriptor>();
        var dpi = VisualTreeHelper.GetDpi(window);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            if (NativeMethods.GetMonitorInfo(hMon, ref mi))
            {
                list.Add(new FloatTodo.Core.Windowing.MonitorDescriptor
                {
                    Handle = hMon,
                    MonitorArea = new FloatTodo.Core.Windowing.PixelRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom),
                    WorkArea = new FloatTodo.Core.Windowing.PixelRect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom),
                    DpiScaleX = dpiX,
                    DpiScaleY = dpiY,
                    IsPrimary = (mi.dwFlags & 1) != 0
                });
            }
            return true;
        }, IntPtr.Zero);

        if (list.Count == 0)
        {
            var fallback = GetCurrentMonitorInfo(window);
            list.Add(new FloatTodo.Core.Windowing.MonitorDescriptor
            {
                Handle = new IntPtr(1),
                MonitorArea = new FloatTodo.Core.Windowing.PixelRect(fallback.PhysicalMonitorArea.Left, fallback.PhysicalMonitorArea.Top, fallback.PhysicalMonitorArea.Right, fallback.PhysicalMonitorArea.Bottom),
                WorkArea = new FloatTodo.Core.Windowing.PixelRect(fallback.PhysicalWorkArea.Left, fallback.PhysicalWorkArea.Top, fallback.PhysicalWorkArea.Right, fallback.PhysicalWorkArea.Bottom),
                DpiScaleX = fallback.DpiScaleX,
                DpiScaleY = fallback.DpiScaleY,
                IsPrimary = true
            });
        }

        return list;
    }
}
