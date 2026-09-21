using FloatTodo.WinUI.Interop;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;

namespace FloatTodo.WinUI.Windowing;

/// <summary>
/// Transparent HWND host. The themed panel surface is XAML and moves with its children.
/// A window-level Mica brush would stay stationary while the panel animates.
/// </summary>
internal sealed class TransparentWindowBackdrop(nint hwnd) : SystemBackdrop
{
    private Windows.UI.Composition.Compositor? _compositor;
    private Windows.UI.Composition.CompositionColorBrush? _brush;
    private WindowMessageHook? _hook;
    private static Windows.System.DispatcherQueueController? _systemQueue;
    [StructLayout(LayoutKind.Sequential)]
    private struct QueueOptions { public int Size, ThreadType, Apartment; }
    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(QueueOptions options, out nint controller);

    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        // This host uses a constant transparent brush, not a system material controller.
        // It has no theme/activation policy to update. In particular, do not forward
        // theme notifications with an unavailable target to the base implementation.
        // The panel's separate Mica/Acrylic backdrop owns its own theme policy.
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        base.OnTargetConnected(target, root);
        // WinUI's Microsoft.UI dispatcher does not initialize the Windows.System dispatcher
        // required by the OS compositor behind ICompositionSupportsSystemBackdrop.
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is null)
        {
            var options = new QueueOptions { Size = Marshal.SizeOf<QueueOptions>(), ThreadType = 2, Apartment = 2 };
            Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out var pointer));
            try { _systemQueue = WinRT.MarshalInterface<Windows.System.DispatcherQueueController>.FromAbi(pointer); }
            finally { Marshal.Release(pointer); }
        }
        _compositor = new();
        _brush = _compositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
        target.SystemBackdrop = _brush;
        ConfigureFrame();
        _hook = new(hwnd)
        {
            Filter = (message, wParam, _) =>
            {
                if (message == 0x14) { ClearBackground((nint)wParam); return 1; } // WM_ERASEBKGND
                if (message == 0x31E) ConfigureFrame(); // WM_DWMCOMPOSITIONCHANGED
                return null;
            }
        };
        var dc = NativeMethods.GetDC(hwnd);
        try { ClearBackground(dc); }
        finally { NativeMethods.ReleaseDC(hwnd, dc); }
    }

    private void ConfigureFrame()
    {
        var margins = new NativeMethods.Margins();
        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        var region = NativeMethods.CreateRectRgn(-2, -2, -1, -1);
        try
        {
            var blur = new NativeMethods.BlurBehind { Flags = 3, Enable = 1, Region = region };
            NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref blur);
        }
        finally { NativeMethods.DeleteObject(region); }
        // No stationary DWM rounding or frame around the animated XAML surface.
        uint corner = 1, border = 0xFFFFFFFE;
        NativeMethods.DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
        NativeMethods.DwmSetWindowAttribute(hwnd, 34, ref border, 4);
    }

    private void ClearBackground(nint dc)
    {
        if (NativeMethods.GetClientRect(hwnd, out var rect))
            NativeMethods.FillRect(dc, ref rect, NativeMethods.GetStockObject(4));
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        _hook?.Dispose();
        _hook = null;
        target.SystemBackdrop = null;
        _brush?.Dispose();
        _compositor?.Dispose();
        _brush = null;
        _compositor = null;
        base.OnTargetDisconnected(target);
    }
}
