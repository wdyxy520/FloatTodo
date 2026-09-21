using FloatTodo.WinUI.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;

namespace FloatTodo.WinUI.Windowing;

// A separate owned input island keeps Restore clickable while the main HWND
// passes mouse input to other processes (HTTRANSPARENT alone cannot do that).
internal sealed class PreviewInputController : IDisposable
{
    private readonly MainWindow _main;
    private readonly nint _hwnd;
    private readonly Window _restore = new();
    private readonly Button _button;
    private readonly nint _buttonHwnd;
    private long _addedStyles;
    private bool _enabled;
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    public PreviewInputController(MainWindow main, Action restore)
    {
        _main = main; _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(main);
        _restore.Title = Shell.Loc.Get("PreviewExitTitle");
        _restore.AppWindow.IsShownInSwitchers = false;
        var presenter = (OverlappedPresenter)_restore.AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = false;
        presenter.IsMaximizable = false; presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true;
        _button = new() { Content = new FontIcon { Glyph = "\uE890", FontSize = 13 }, MinWidth = 0, MinHeight = 0, Padding = new(0), CornerRadius = new(6), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        _button.Click += (_, _) => restore();
        ToolTipService.SetToolTip(_button, Shell.Loc.Get("PreviewExitTooltip"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_button, Shell.Loc.Get("PreviewExitName"));
        _button.PointerEntered += (_, _) => _button.Opacity = 1;
        _button.PointerExited += (_, _) => _button.Opacity = Math.Clamp(main.Settings.Appearance.PreviewOpacity, .4, 1);
        _restore.Content = _button;
        _buttonHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_restore);
        NativeMethods.SetWindowLongPtr(_buttonHwnd, -8, _hwnd); // Owned, not a child HWND.
        var ex = NativeMethods.GetWindowLongPtr(_buttonHwnd, -20).ToInt64();
        NativeMethods.SetWindowLongPtr(_buttonHwnd, -20, (nint)((ex | 0x08000080L) & ~0x40000L));
        var style = NativeMethods.GetWindowLongPtr(_buttonHwnd, -16).ToInt64();
        NativeMethods.SetWindowLongPtr(_buttonHwnd, -16, (nint)(style & ~0xC40000L));
        NativeMethods.SetWindowPos(_buttonHwnd, 0, 0, 0, 0, 0, 0x37);
    }
    public void Enable()
    {
        if (_enabled) return;
        var current = NativeMethods.GetWindowLongPtr(_hwnd, -20).ToInt64();
        const long mask = 0x08080020L; // NOACTIVATE | LAYERED | TRANSPARENT
        _addedStyles = mask & ~current;
        NativeMethods.SetWindowLongPtr(_hwnd, -20, (nint)(current | mask));
        if (!SetLayeredWindowAttributes(_hwnd, 0, 255, 2))
        {
            NativeMethods.SetWindowLongPtr(_hwnd, -20, (nint)current);
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        _enabled = true;
        Position(); _restore.AppWindow.Show(false);
        NativeMethods.SetWindowPos(_buttonHwnd, -1, 0, 0, 0, 0, 0x13);
    }
    public void Position()
    {
        NativeMethods.GetWindowRect(_hwnd, out var r);
        var dpi = NativeMethods.GetDpiForWindow(_hwnd) / 96.0;
        var size = (int)Math.Round(30 * dpi);
        var marginX = (int)Math.Round(6 * dpi);
        var marginY = (int)Math.Round(3 * dpi);
        _restore.AppWindow.MoveAndResize(new(r.Right - size - marginX, r.Top + marginY, size, size));
        var input = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(_restore.AppWindow.Id);
        input.ClearAllRegionRects();
        input.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough, [new(0, 0, size, size)]);
        _button.RequestedTheme = _main.PanelRoot.ActualTheme;
        _button.Opacity = Math.Clamp(_main.Settings.Appearance.PreviewOpacity, .4, 1);
    }
    public void Disable()
    {
        if (_enabled)
        {
            var current = NativeMethods.GetWindowLongPtr(_hwnd, -20).ToInt64();
            NativeMethods.SetWindowLongPtr(_hwnd, -20, (nint)(current & ~_addedStyles));
            NativeMethods.SetWindowPos(_hwnd, 0, 0, 0, 0, 0, 0x37);
            _enabled = false;
        }
        _restore.AppWindow.Hide();
    }
    public void Dispose() { Disable(); _restore.Close(); }
}
