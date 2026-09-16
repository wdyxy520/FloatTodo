using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FloatTodo.Core.Models;
using FloatTodo.Core.Services;
using FloatTodo.Core.Windowing;
using FloatTodo.Wpf.Interop;
using FloatTodo.Wpf.Services;
using FloatTodo.Wpf.Themes;

namespace FloatTodo.Wpf;

public partial class MainWindow : Window
{
    private readonly WindowDockService _dockService;
    private readonly TrayService _trayService;
    private readonly StorageService _storageService = new();

    private bool _isRealExit;
    private bool _isRestored;
    private BackdropType _currentBackdrop = BackdropType.Mica;
    private AppTheme _currentTheme = AppTheme.System;
    private double _lastNormalLeft;
    private double _lastNormalTop;

    public MainWindow()
    {
        InitializeComponent();

        _dockService = new WindowDockService(this, AppTitleBar.DragElement);
        _dockService.IsAutoHideSuspended = () => TodayTasks.IsInputActive;
        _trayService = new TrayService(this);

        AppTitleBar.PinToggled += OnPinToggled;
        AppTitleBar.SettingsRequested += ShowSettingsView;

        _dockService.DockStateChanged += OnDockStateChanged;
        _dockService.DockStateChanged += _ => SaveCurrentSettings();

        _dockService.DockSideChanged += OnDockSideChanged;
        _dockService.DockSideChanged += _ => SaveCurrentSettings();

        SizeChanged += (_, _) => SaveCurrentSettings();
        LocationChanged += (_, _) => SaveCurrentSettings();

        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.SettingsRequested += ShowSettingsView;
        _trayService.TopmostToggled += OnTrayTopmostToggled;
        _trayService.AutoHideToggled += OnTrayAutoHideToggled;
        _trayService.ExitRequested += OnTrayExitRequested;

        ThemeHelper.ThemeChanged += () =>
        {
            Dispatcher.Invoke(() => ApplyBackdrop(_currentBackdrop));
        };

        Loaded += OnLoaded;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // 1. ToolWindow style: clean resident tool window that does not occupy taskbar
        IntPtr exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        IntPtr newExStyle = new IntPtr(exStyle.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, newExStyle);

        // 2. Strip WS_MAXIMIZEBOX to completely disable system Aero Snap maximize
        IntPtr style = NativeMethods.GetWindowLongPtr(hwnd, -16);
        IntPtr newStyle = new IntPtr(style.ToInt64() & ~0x00010000);
        NativeMethods.SetWindowLongPtr(hwnd, -16, newStyle);

        // 3. Apply initial backdrop & immersive dark mode
        ApplyBackdrop(_currentBackdrop);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreSettings();
    }

    private void RestoreSettings()
    {
        var settings = _storageService.LoadSettings();

        // 1. Restore appearance / backdrop & theme
        _currentBackdrop = settings.Appearance.Backdrop;
        _currentTheme = settings.Appearance.Theme;
        switch (_currentTheme)
        {
            case AppTheme.Light:
                RadioThemeLight.IsChecked = true;
                break;
            case AppTheme.Dark:
                RadioThemeDark.IsChecked = true;
                break;
            case AppTheme.System:
            default:
                RadioThemeSystem.IsChecked = true;
                break;
        }
        ThemeHelper.ApplyTheme(Application.Current.Resources, _currentTheme);
        ApplyBackdrop(_currentBackdrop);

        // 2. Restore window behavior
        Topmost = settings.Window.Topmost;
        AppTitleBar.IsPinned = Topmost;
        ChkTopmost.IsChecked = Topmost;

        _dockService.AutoHideEnabled = settings.Window.AutoHide;
        ChkAutoHide.IsChecked = _dockService.AutoHideEnabled;

        // 3. Restore size & position
        Width = Math.Clamp(settings.Window.Width, 260, 560);
        Height = Math.Max(settings.Window.Height, 360);

        var allMonitors = MonitorHelper.GetAllMonitors(this);
        var primary = allMonitors.FirstOrDefault(m => m.IsPrimary) ?? allMonitors[0];
        var mi = MonitorHelper.GetCurrentMonitorInfo(this);

        double targetLeft = settings.Window.Left;
        double targetTop = settings.Window.Top;

        bool hasSavedPos = settings.Window.Left != 0 || settings.Window.Top != 0;
        DockSide savedDockSide = settings.Window.DockSide;

        // 若原先处于吸附状态，首次打开必须以完全展开状态（DockedVisible）呈现！
        if (savedDockSide == DockSide.Right)
        {
            targetLeft = primary.WorkArea.Right / mi.DpiScaleX - Width;
        }
        else if (savedDockSide == DockSide.Left)
        {
            targetLeft = primary.WorkArea.Left / mi.DpiScaleX;
        }
        else
        {
            // 自由悬浮位置有效性校验
            bool isInsideAnyMonitor = false;
            if (hasSavedPos)
            {
                int physLeft = (int)Math.Round(targetLeft * mi.DpiScaleX);
                int physTop = (int)Math.Round(targetTop * mi.DpiScaleY);
                int physRight = (int)Math.Round((targetLeft + Width) * mi.DpiScaleX);
                int physBottom = (int)Math.Round((targetTop + Height) * mi.DpiScaleY);
                var winRect = new PixelRect(physLeft, physTop, physRight, physBottom);

                foreach (var m in allMonitors)
                {
                    if (m.WorkArea.IntersectsWith(winRect))
                    {
                        isInsideAnyMonitor = true;
                        break;
                    }
                }
            }

            if (!hasSavedPos || !isInsideAnyMonitor)
            {
                targetLeft = primary.WorkArea.Right / mi.DpiScaleX - Width;
                targetTop = primary.WorkArea.Top / mi.DpiScaleY + 120;
                savedDockSide = DockSide.Right;
            }
        }

        Left = targetLeft;
        Top = targetTop;
        _lastNormalLeft = Left;
        _lastNormalTop = Top;

        // 初始化吸附控制器并同步指示UI
        _dockService.InitializePlacement(targetLeft, targetTop, savedDockSide);
        OnDockSideChanged(savedDockSide);
        OnDockStateChanged(DockState.DockedVisible);

        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);

        _isRestored = true;

        // 若开启了自动隐藏且为吸附状态，在就绪展示 3 秒后优雅滑出收起（若鼠标未停留）
        if (_dockService.AutoHideEnabled && savedDockSide != DockSide.None)
        {
            var startupDelayTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            startupDelayTimer.Tick += (s, args) =>
            {
                startupDelayTimer.Stop();
                NativeMethods.GetCursorPos(out var pt);
                int winLeftPx = (int)Math.Round(Left * mi.DpiScaleX);
                int winTopPx = (int)Math.Round(Top * mi.DpiScaleY);
                int winRightPx = (int)Math.Round((Left + Width) * mi.DpiScaleX);
                int winBottomPx = (int)Math.Round((Top + Height) * mi.DpiScaleY);

                bool isMouseOver = pt.X >= winLeftPx && pt.X <= winRightPx && pt.Y >= winTopPx && pt.Y <= winBottomPx;
                if (!isMouseOver && !TodayTasks.IsInputActive && _dockService.CurrentDockSide != DockSide.None && _dockService.AutoHideEnabled)
                {
                    _dockService.RequestDockState(DockState.DockedHidden);
                }
            };
            startupDelayTimer.Start();
        }
    }

    private void SaveCurrentSettings(bool immediate = false)
    {
        if (!_isRestored) return;

        // If currently docked and hidden off-screen, never save the temporary off-screen coordinate!
        double saveLeft = _dockService.CurrentDockState == DockState.DockedHidden ? _lastNormalLeft : Left;
        double saveTop = _dockService.CurrentDockState == DockState.DockedHidden ? _lastNormalTop : Top;

        if (_dockService.CurrentDockState != DockState.DockedHidden)
        {
            _lastNormalLeft = Left;
            _lastNormalTop = Top;
        }

        var settings = new AppSettings
        {
            Window = new WindowSettings
            {
                Left = saveLeft,
                Top = saveTop,
                Width = ActualWidth > 0 ? ActualWidth : Width,
                Height = ActualHeight > 0 ? ActualHeight : Height,
                Topmost = Topmost,
                AutoHide = _dockService.AutoHideEnabled,
                DockSide = _dockService.CurrentDockSide,
                DockState = _dockService.CurrentDockState
            },
            Appearance = new AppearanceSettings
            {
                Backdrop = _currentBackdrop,
                Theme = _currentTheme
            }
        };

        if (immediate)
        {
            _storageService.SaveSettingsImmediately(settings);
        }
        else
        {
            _storageService.SaveSettingsDebounced(settings);
        }
    }

    public void ApplyBackdrop(BackdropType type)
    {
        _currentBackdrop = type;
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero) return;

        bool isDark = ThemeHelper.IsDarkMode();

        // 1. Synchronize Windows 11 DWM Immersive Dark Mode attribute (20)
        int isDarkVal = isDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDarkVal, sizeof(int));

        // 2. Configure Backdrop type & Rounded corners
        int backdropType;
        int cornerPref;
        bool isTranslucent;

        switch (type)
        {
            case BackdropType.Mica:
                backdropType = NativeMethods.DWMSBT_MAINWINDOW;
                cornerPref = NativeMethods.DWMWCP_ROUND;
                isTranslucent = true;
                ExpandedContainer.CornerRadius = new CornerRadius(9);
                RadioMica.IsChecked = true;
                break;

            case BackdropType.MicaAlt:
                backdropType = NativeMethods.DWMSBT_TABBEDWINDOW;
                cornerPref = NativeMethods.DWMWCP_ROUND;
                isTranslucent = true;
                ExpandedContainer.CornerRadius = new CornerRadius(9);
                RadioMicaAlt.IsChecked = true;
                break;

            case BackdropType.Acrylic:
                backdropType = NativeMethods.DWMSBT_TRANSIENTWINDOW;
                cornerPref = NativeMethods.DWMWCP_ROUND;
                isTranslucent = true;
                ExpandedContainer.CornerRadius = new CornerRadius(9);
                RadioAcrylic.IsChecked = true;
                break;

            case BackdropType.Solid:
            default:
                backdropType = NativeMethods.DWMSBT_NONE;
                cornerPref = NativeMethods.DWMWCP_DONOTROUND;
                isTranslucent = false;
                ExpandedContainer.CornerRadius = new CornerRadius(0);
                RadioSolid.IsChecked = true;
                break;
        }

        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        ThemeHelper.ApplyTheme(Application.Current.Resources, isTranslucent);
        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);
    }

    public void ShowSettingsView()
    {
        if (Visibility != Visibility.Visible)
        {
            Show();
        }
        if (_dockService.CurrentDockState == DockState.DockedHidden)
        {
            _dockService.RequestDockState(DockState.DockedVisible);
        }

        MainContentGrid.Visibility = Visibility.Collapsed;
        SettingsContentGrid.Visibility = Visibility.Visible;
        Activate();
    }

    public void ShowMainView()
    {
        SettingsContentGrid.Visibility = Visibility.Collapsed;
        MainContentGrid.Visibility = Visibility.Visible;
    }

    private void OnBackToMainClick(object sender, RoutedEventArgs e)
    {
        ShowMainView();
    }

    private void OnThemeRadioChecked(object sender, RoutedEventArgs e)
    {
        if (!_isRestored) return;

        AppTheme selected = AppTheme.System;
        if (RadioThemeLight.IsChecked == true) selected = AppTheme.Light;
        else if (RadioThemeDark.IsChecked == true) selected = AppTheme.Dark;

        if (selected != _currentTheme)
        {
            _currentTheme = selected;
            ThemeHelper.ApplyTheme(Application.Current.Resources, _currentTheme);
            ApplyBackdrop(_currentBackdrop);
            SaveCurrentSettings();
        }
    }

    private void OnBackdropRadioChecked(object sender, RoutedEventArgs e)
    {
        if (!_isRestored) return;

        BackdropType selected = BackdropType.Mica;
        if (RadioMicaAlt.IsChecked == true) selected = BackdropType.MicaAlt;
        else if (RadioAcrylic.IsChecked == true) selected = BackdropType.Acrylic;
        else if (RadioSolid.IsChecked == true) selected = BackdropType.Solid;

        if (selected != _currentBackdrop)
        {
            ApplyBackdrop(selected);
            SaveCurrentSettings();
        }
    }

    private void OnSettingTopmostClick(object sender, RoutedEventArgs e)
    {
        Topmost = ChkTopmost.IsChecked == true;
        AppTitleBar.IsPinned = Topmost;
        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);
        SaveCurrentSettings();
    }

    private void OnSettingAutoHideClick(object sender, RoutedEventArgs e)
    {
        _dockService.AutoHideEnabled = ChkAutoHide.IsChecked == true;
        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);
        SaveCurrentSettings();
    }

    private void OnOpenDataDirClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "FloatTodo");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开目录: {ex.Message}", "FloatTodo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnPinToggled()
    {
        Topmost = AppTitleBar.IsPinned;
        ChkTopmost.IsChecked = Topmost;
        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);
        SaveCurrentSettings();

        if (Topmost && _dockService.CurrentDockState != DockState.DockedVisible)
        {
            _dockService.RequestDockState(DockState.DockedVisible);
        }
    }

    private void OnTrayOpenRequested()
    {
        if (Visibility != Visibility.Visible)
        {
            Show();
        }
        ShowMainView();
        _dockService.RequestDockState(DockState.DockedVisible);
        Activate();
    }

    private void OnTrayTopmostToggled(bool topmost)
    {
        Topmost = topmost;
        AppTitleBar.IsPinned = topmost;
        ChkTopmost.IsChecked = topmost;
        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);
        SaveCurrentSettings();
    }

    private void OnTrayAutoHideToggled(bool autoHide)
    {
        _dockService.AutoHideEnabled = autoHide;
        ChkAutoHide.IsChecked = autoHide;
        _trayService.UpdateStates(Topmost, _dockService.AutoHideEnabled, _currentBackdrop == BackdropType.Solid);
        SaveCurrentSettings();
    }

    private void OnTrayExitRequested()
    {
        _isRealExit = true;
        SaveCurrentSettings(immediate: true);
        _dockService.Dispose();
        _storageService.Dispose();
        _trayService.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isRealExit)
        {
            // Intercept closing: hide to tray instead of killing the process!
            e.Cancel = true;
            if (_dockService.CurrentDockSide != DockSide.None)
            {
                _dockService.RequestDockState(DockState.DockedHidden);
            }
            else
            {
                Hide();
            }
            return;
        }

        base.OnClosing(e);
    }

    private void OnDockStateChanged(DockState state)
    {
        Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case DockState.DockedHidden:
                    // Keep ExpandedContainer ALWAYS visible to eliminate flash and layout thrashing.
                    // Only display subtle edge indicator handle when docked and hidden.
                    if (_dockService.CurrentDockSide == DockSide.Right)
                    {
                        LeftHandle.Visibility = Visibility.Visible;
                        RightHandle.Visibility = Visibility.Collapsed;
                    }
                    else if (_dockService.CurrentDockSide == DockSide.Left)
                    {
                        RightHandle.Visibility = Visibility.Visible;
                        LeftHandle.Visibility = Visibility.Collapsed;
                    }
                    break;

                case DockState.DockedVisible:
                case DockState.Floating:
                default:
                    LeftHandle.Visibility = Visibility.Collapsed;
                    RightHandle.Visibility = Visibility.Collapsed;
                    break;
            }
        });
    }

    private void OnDockSideChanged(DockSide side)
    {
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        _dockService.OnWindowMouseEnter();
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        _dockService.OnWindowMouseLeave();
    }

    private void OnHandleMouseEnter(object sender, MouseEventArgs e)
    {
        _dockService.OnEdgeHandleMouseEnter();
    }

    private void OnHandleMouseLeave(object sender, MouseEventArgs e)
    {
        _dockService.OnEdgeHandleMouseLeave();
    }
}
