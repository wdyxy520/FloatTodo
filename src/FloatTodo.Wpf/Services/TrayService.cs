using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using FloatTodo.Wpf.Interop;

namespace FloatTodo.Wpf.Services;

public class TrayService : IDisposable
{
    private readonly Window _window;
    private NativeMethods.NOTIFYICONDATA _nid;
    private bool _isCreated;
    private System.Drawing.Icon? _trayIcon;

    private bool _isTopmost = true;
    private bool _isAutoHide = true;

    private ContextMenu? _contextMenu;
    private MenuItem? _menuTopmost;
    private MenuItem? _menuAutoHide;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action<bool>? TopmostToggled;
    public event Action<bool>? AutoHideToggled;
    public event Action? ExitRequested;

    public TrayService(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(_window);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);

        CreateTrayIcon(helper.Handle);
    }

    private void CreateTrayIcon(IntPtr hwnd)
    {
        IntPtr hIcon = IntPtr.Zero;

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                _trayIcon = new System.Drawing.Icon(stream, 32, 32);
                hIcon = _trayIcon.Handle;
            }
        }
        catch
        {
            // fallback if resource loading fails
        }

        if (hIcon == IntPtr.Zero)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    _trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    hIcon = _trayIcon?.Handle ?? IntPtr.Zero;
                }
            }
            catch
            {
                // fallback
            }
        }

        if (hIcon == IntPtr.Zero)
        {
            hIcon = NativeMethods.LoadIcon(IntPtr.Zero, NativeMethods.IDI_APPLICATION);
        }

        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
            hWnd = hwnd,
            uID = 1001,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = hIcon,
            szTip = "FloatTodo - Working Memory"
        };

        _isCreated = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_TRAYICON)
        {
            int action = lParam.ToInt32();
            if (action == NativeMethods.WM_LBUTTONUP)
            {
                OpenRequested?.Invoke();
                handled = true;
            }
            else if (action == NativeMethods.WM_RBUTTONUP)
            {
                _window.Dispatcher.Invoke(() => ShowFluentContextMenu(hwnd));
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void BuildContextMenu(IntPtr hwnd)
    {
        _contextMenu = new ContextMenu();

        // 1. 打开 FloatTodo (纯文本，不放图标)
        var openItem = new MenuItem { Header = "打开 FloatTodo" };
        openItem.Click += (_, _) => OpenRequested?.Invoke();
        _contextMenu.Items.Add(openItem);

        // 2. 偏好设置... (纯文本，不放图标)
        var settingsItem = new MenuItem { Header = "偏好设置..." };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        _contextMenu.Items.Add(settingsItem);

        // 分割线
        _contextMenu.Items.Add(new Separator());

        // 3. 保持置顶 (可勾选项)
        _menuTopmost = new MenuItem { Header = "保持置顶", IsCheckable = true, IsChecked = _isTopmost };
        _menuTopmost.Click += (_, _) =>
        {
            _isTopmost = !_isTopmost;
            _menuTopmost.IsChecked = _isTopmost;
            TopmostToggled?.Invoke(_isTopmost);
        };
        _contextMenu.Items.Add(_menuTopmost);

        // 4. 自动隐藏 (可勾选项)
        _menuAutoHide = new MenuItem { Header = "自动隐藏", IsCheckable = true, IsChecked = _isAutoHide };
        _menuAutoHide.Click += (_, _) =>
        {
            _isAutoHide = !_isAutoHide;
            _menuAutoHide.IsChecked = _isAutoHide;
            AutoHideToggled?.Invoke(_isAutoHide);
        };
        _contextMenu.Items.Add(_menuAutoHide);

        // 分割线
        _contextMenu.Items.Add(new Separator());

        // 5. 退出应用 (纯文本，不放图标)
        var exitItem = new MenuItem { Header = "退出应用" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        _contextMenu.Items.Add(exitItem);

        // 微软防失焦规范：关闭时发送 WM_NULL 释放消息管线
        _contextMenu.Closed += (_, _) =>
        {
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
        };
    }

    private void ShowFluentContextMenu(IntPtr hwnd)
    {
        if (_contextMenu == null)
        {
            BuildContextMenu(hwnd);
        }

        // 1. 设置前台窗口，使得外部点击能收到失焦并自动 Dismiss
        NativeMethods.SetForegroundWindow(hwnd);

        // 2. 更新选中状态
        if (_menuTopmost != null) _menuTopmost.IsChecked = _isTopmost;
        if (_menuAutoHide != null) _menuAutoHide.IsChecked = _isAutoHide;

        // 3. 弹出 ContextMenu 在鼠标当前物理位置
        _contextMenu!.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _contextMenu.IsOpen = true;
    }

    public void UpdateStates(bool topmost, bool autoHide, bool isSolid = false)
    {
        _isTopmost = topmost;
        _isAutoHide = autoHide;

        if (_menuTopmost != null) _menuTopmost.IsChecked = topmost;
        if (_menuAutoHide != null) _menuAutoHide.IsChecked = autoHide;
    }

    public void Dispose()
    {
        if (_isCreated)
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
            _isCreated = false;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
