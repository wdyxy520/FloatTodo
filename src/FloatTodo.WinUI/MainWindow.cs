using FloatTodo.Core.Models;
using FloatTodo.Core.Services;
using FloatTodo.ViewModels;
using FloatTodo.WinUI.Services;
using FloatTodo.WinUI.Windowing;
using FloatTodo.WinUI.Interop;
using FloatTodo.WinUI.Shell;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

namespace FloatTodo.WinUI;

public sealed class MainWindow : Window
{
    public PanelSurface PanelRoot { get; } = new();
    public Grid DragHandle { get; } = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), IsTabStop = true };
    public Grid SettingsDragHandle { get; } = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), IsTabStop = true };
    public Grid CurrentDragHandle => _isSettingsActive ? SettingsDragHandle : DragHandle;
    private readonly Grid _host = new();
    private readonly SystemBackdropElement _material = new() { CornerRadius = new(10), IsHitTestVisible = false };
    private readonly Border _solid = new() { CornerRadius = new(10), IsHitTestVisible = false };
    private Microsoft.UI.Xaml.Controls.Primitives.ToggleButton? _pin;
    private Button? _settingsButton;
    private Button? _updateButton;
    private TextBlock? _updateFlyoutDesc;
    private readonly Grid _normalHeader;
    private readonly Grid _settingsHeader;
    private readonly MainPage _normalPage;
    private readonly Views.SettingsPage _settingsPage;
    private readonly DesktopPreviewView _previewView;
    private PreviewInputController? _previewInput;
    private long _modeRevision;
    public bool IsPreviewMode { get; private set; }
    private bool _isSettingsActive;
    private bool _previewFromSettings;
    private BackdropType? _materialKind;
    public MainViewModel Main { get; }
    public TodayViewModel ViewModel => Main.Today;
    public AppSettings Settings => Main.Settings;
    public bool IsDialogOpen => Main.IsDialogOpen;
    private readonly MemoSaveCoordinator _save;
    private readonly StorageService _settingsStore;
    private Shell.TrayIconService? _tray;
    private Shell.GlobalHotKeyService? _hotkey;
    private DockController? _dock;
    private bool _closing;

    public void ShowPanel() { ExitPreview(); _dock?.Show(true); }
    public void SaveSettings() => _settingsStore.SaveSettingsDebounced(Settings);

    private async Task EnterPreviewAsync(bool fromSettings = false)
    {
        if (IsPreviewMode || IsDialogOpen) return;
        _previewFromSettings = fromSettings;
        ViewModel.FinishEditingExcept();
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(PanelRoot.XamlRoot)) popup.IsOpen = false;
        var revision = ++_modeRevision;
        IsPreviewMode = true;
        PanelRoot.IsHitTestVisible = false;
        _dock?.SuspendForPreview();
        if (AppWindow.Presenter is OverlappedPresenter p) p.IsAlwaysOnTop = true;
        try
        {
            await FadeHostAsync(0, 100);
            if (revision != _modeRevision) return;
            SetPreviewLayout(true);
            await FadeHostAsync((float)Math.Clamp(Settings.Appearance.PreviewOpacity, .4, 1), 160);
            if (revision != _modeRevision) return;
            _previewInput ??= new(this, ShowPanel);
            _previewInput.Enable();
        }
        catch (Exception e)
        {
            if (revision != _modeRevision) return;
            ExitPreview();
            ViewModel.ErrorMessage = Loc.Get("PreviewError") + e.Message;
        }
    }

    private void ExitPreview()
    {
        if (!IsPreviewMode) return;
        ++_modeRevision;
        _previewInput?.Disable(); // Restore input before changing the visible page.
        IsPreviewMode = false;
        SetPreviewLayout(false);
        if (AppWindow.Presenter is OverlappedPresenter p) p.IsAlwaysOnTop = Main.Topmost;
        _dock?.ResumeFromPreview();
        _ = FadeHostAsync(1, 160);
    }

    private void SetPreviewLayout(bool preview)
    {
        if (preview)
        {
            _normalHeader.Visibility = Visibility.Collapsed;
            _normalPage.Visibility = Visibility.Collapsed;
            _settingsHeader.Visibility = Visibility.Collapsed;
            _settingsPage.Visibility = Visibility.Collapsed;
            _previewView.Visibility = Visibility.Visible;
        }
        else
        {
            _previewView.Visibility = Visibility.Collapsed;
            if (_previewFromSettings)
            {
                _settingsHeader.Visibility = Visibility.Visible;
                _settingsPage.Visibility = Visibility.Visible;
                _normalHeader.Visibility = Visibility.Collapsed;
                _normalPage.Visibility = Visibility.Collapsed;
                _settingsPage.RefreshFromSettings();
            }
            else
            {
                _normalHeader.Visibility = Visibility.Visible;
                _normalPage.Visibility = Visibility.Visible;
                _settingsHeader.Visibility = Visibility.Collapsed;
                _settingsPage.Visibility = Visibility.Collapsed;
            }
            _previewFromSettings = false;
        }
        // The host owns preview opacity; applying it here too squares the setting.
        _previewView.Opacity = 1;
        PanelRoot.IsHitTestVisible = !preview;
        PanelRoot.BorderThickness = new(preview ? 0 : 1);
        ApplyMaterial();
    }

    private Task FadeHostAsync(float opacity, int milliseconds)
    {
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_host);
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            visual.StopAnimation("Opacity");
            visual.Opacity = opacity;
            _host.Opacity = opacity;
            return Task.CompletedTask;
        }
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var batch = visual.Compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(milliseconds);
        animation.InsertExpressionKeyFrame(0, "this.StartingValue"); animation.InsertKeyFrame(1, opacity);
        batch.Completed += (_, _) =>
        {
            visual.Opacity = opacity;
            _host.Opacity = opacity;
            animation.Dispose();
            batch.Dispose();
            done.TrySetResult();
        };
        visual.StartAnimation("Opacity", animation); batch.End();
        return done.Task;
    }

    public MainWindow(TodayViewModel viewModel, MemoService memos, string? settingsPath)
    {
        Title = Loc.Get("WindowTitle");
        // Set before the first Activate/Show, not only after the content has loaded.
        AppWindow.IsShownInSwitchers = false;
        var appIcoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(appIcoPath))
        {
            AppWindow.SetIcon(appIcoPath);
        }
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            // Native resize borders reserve an invisible 8px inset, defeating flush docking.
            presenter.IsResizable = false;
        }
        _settingsStore = new(settingsPath);
        Main = new(viewModel, _settingsStore.LoadSettings());
        Main.SettingsChanged += () =>
        {
            ApplyAppearance();
            if (_pin is not null) _pin.IsChecked = Main.Topmost;
            if (AppWindow.Presenter is OverlappedPresenter p) p.IsAlwaysOnTop = IsPreviewMode || Main.Topmost;
            SaveSettings();
        };
        PanelRoot.Style = (Style)Application.Current.Resources["PanelSurface"];
        PanelRoot.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Grid.SetRowSpan(_material, 2); Grid.SetRowSpan(_solid, 2);
        PanelRoot.Children.Add(_solid); PanelRoot.Children.Add(_material);
        PanelRoot.ActualThemeChanged += (_, _) => UpdateSolidBackground();
        PanelRoot.RowDefinitions.Add(new() { Height = GridLength.Auto });
        PanelRoot.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        _normalHeader = CreateHeader(); PanelRoot.Children.Add(_normalHeader);
        _settingsHeader = CreateSettingsHeader(); PanelRoot.Children.Add(_settingsHeader);
        var page = _normalPage = new MainPage { DataContext = viewModel };
        Grid.SetRow(page, 1);
        PanelRoot.Children.Add(page);
        _settingsPage = new Views.SettingsPage();
        Grid.SetRow(_settingsPage, 1);
        _settingsPage.Visibility = Visibility.Collapsed;
        _settingsPage.Initialize(Main);
        _settingsPage.PeekPreviewRequested += async () => await EnterPreviewAsync(fromSettings: true);
        PanelRoot.Children.Add(_settingsPage);
        _previewView = new DesktopPreviewView { DataContext = viewModel, Visibility = Visibility.Collapsed };
        Grid.SetRowSpan(_previewView, 2); PanelRoot.Children.Add(_previewView);
        _host.Children.Add(PanelRoot);
        _host.SizeChanged += (_, e) =>
        {
            if (_host.Clip is RectangleGeometry rg)
                rg.Rect = new(0, 0, e.NewSize.Width, e.NewSize.Height);
            else
                _host.Clip = new RectangleGeometry { Rect = new(0, 0, e.NewSize.Width, e.NewSize.Height) };
        };
        Content = _host;
        PanelRoot.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OutsideCardPressed), true);
        PanelRoot.SizeChanged += (_, _) =>
        {
            if (_dock?.IsInteracting != true)
            {
                ConfigureInputRegions();
            }
        };
        AppWindow.Changed += (_, _) => { if (IsPreviewMode) _previewInput?.Position(); };
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)Settings.Window.Width, (int)Settings.Window.Height));
        if (AppWindow.Presenter is OverlappedPresenter topmost) topmost.IsAlwaysOnTop = Main.Topmost;
        SystemBackdrop = new TransparentWindowBackdrop(WinRT.Interop.WindowNative.GetWindowHandle(this));
        ConfigureToolWindow();
        ApplyAppearance();
        _save = new(memos, viewModel, DispatcherQueue);
        PanelRoot.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape && _isSettingsActive && !IsPreviewMode)
            {
                NavigateToMain();
                e.Handled = true;
            }
        };
        Closed += (_, _) =>
        {
            if (Main.InstallOnExit && !string.IsNullOrEmpty(UpdateService.ReadyInstallerPath))
            {
                UpdateService.ApplyUpdateOnExit();
            }
        };
        UpdateService.StatusChanged += _ =>
        {
            DispatcherQueue.TryEnqueue(UpdateTitleBarUpdateBadge);
        };
        Activated += async (_, e) =>
        {
            ConfigureToolWindow();
            if (!IsPreviewMode && e.WindowActivationState == WindowActivationState.Deactivated) { if (!Main.IsDialogOpen && !_isSettingsActive) ViewModel.FinishEditingExcept(); await _save.FlushAsync(); }
        };
        AppWindow.Closing += async (_, e) =>
        {
            if (_closing) return;
            e.Cancel = true;
            if (await _save.FlushAsync())
            {
                _closing = true;
                _settingsStore.SaveSettingsImmediately(Settings);
                DispatcherQueue.TryEnqueue(Close);
            }
        };
        PanelRoot.Loaded += (_, _) => InitializeShell();
        Closed += (_, _) =>
        {
            _modeRevision++; _previewInput?.Dispose(); _tray?.Dispose(); _hotkey?.Dispose(); _dock?.Dispose();
            _material.SystemBackdrop = null; SystemBackdrop = null;
            _settingsStore.Dispose();
        };
    }

    internal void ConfigureInputRegions()
    {
        var input = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        input.ClearAllRegionRects();
        input.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough,
            [new(0, 0, AppWindow.Size.Width, AppWindow.Size.Height)]);
    }

    private void OutsideCardPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isSettingsActive) return;
        DependencyObject? node = e.OriginalSource as DependencyObject;
        while (node is not null && node is not MemoCard) node = VisualTreeHelper.GetParent(node);
        ViewModel.FinishEditingExcept((node as MemoCard)?.DataContext as MemoViewModel);
    }
    private void ConfigureToolWindow()
    {
        AppWindow.IsShownInSwitchers = false;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var current = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
        var next = (current | 0x80L) & ~0x40000L; // TOOLWINDOW, never APPWINDOW
        var style = NativeMethods.GetWindowLongPtr(hwnd, -16).ToInt64();
        var borderless = style & ~0xC40000L; // No caption, dialog frame, or native sizing insets.
        if (current != next || style != borderless)
        {
            NativeMethods.SetWindowLongPtr(hwnd, -20, (nint)next);
            NativeMethods.SetWindowLongPtr(hwnd, -16, (nint)borderless);
            NativeMethods.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x37);
        }
    }

    public async void NavigateToSettings()
    {
        if (IsPreviewMode) ExitPreview();
        ViewModel.FinishEditingExcept();
        await _save.FlushAsync();
        _isSettingsActive = true;
        Main.IsSettingsOpen = true;
        _settingsPage.RefreshFromSettings();
        _normalHeader.Visibility = Visibility.Collapsed;
        _normalPage.Visibility = Visibility.Collapsed;
        _settingsHeader.Visibility = Visibility.Visible;
        _settingsPage.Visibility = Visibility.Visible;
        _previewView.Visibility = Visibility.Collapsed;
    }

    public void NavigateToMain()
    {
        _isSettingsActive = false;
        Main.IsSettingsOpen = false;
        _settingsHeader.Visibility = Visibility.Collapsed;
        _settingsPage.Visibility = Visibility.Collapsed;
        _normalHeader.Visibility = Visibility.Visible;
        _normalPage.Visibility = Visibility.Visible;
        _previewView.Visibility = Visibility.Collapsed;
    }

    private Grid CreateHeader()
    {
        var header = new Grid { Height = 38, Padding = new Thickness(6, 0, 6, 0) };
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        var addBtn = CaptionButton("\uE710", Loc.Get("BtnAddMemo"));
        addBtn.Click += (_, _) =>
        {
            ViewModel.NewTextCommand.Execute(null);
            _normalPage?.ScrollToTop();
        };

        var addFlyout = new MenuFlyout();
        var textItem = new MenuFlyoutItem
        {
            Text = Loc.Get("MenuAddTextMemo"),
            Icon = new FontIcon { Glyph = "\uE8C4" }
        };
        textItem.Click += (_, _) =>
        {
            ViewModel.NewTextCommand.Execute(null);
            _normalPage?.ScrollToTop();
        };
        var checklistItem = new MenuFlyoutItem
        {
            Text = Loc.Get("MenuAddChecklistMemo"),
            Icon = new FontIcon { Glyph = "\uE73A" }
        };
        checklistItem.Click += (_, _) =>
        {
            ViewModel.NewChecklistCommand.Execute(null);
            _normalPage?.ScrollToTop();
        };
        addFlyout.Items.Add(textItem);
        addFlyout.Items.Add(checklistItem);
        addBtn.ContextFlyout = addFlyout;

        Grid.SetColumn(addBtn, 0);
        header.Children.Add(addBtn);

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DragHandle, Loc.Get("DragMainHandle"));
        Grid.SetColumn(DragHandle, 1);
        header.Children.Add(DragHandle);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var updateBtn = _updateButton = CaptionButton("\uE896", Loc.Get("TitleBarUpdateReadyToolTip"));
        updateBtn.Visibility = Visibility.Collapsed;
        if (updateBtn.Content is FontIcon uIcon)
        {
            uIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 99, 38));
        }

        var updateFlyout = new Flyout();
        var flyoutStack = new StackPanel { Spacing = 8, Width = 230, Padding = new Thickness(4) };
        var flyoutHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        flyoutHeader.Children.Add(new FontIcon { Glyph = "\uE896", FontSize = 16, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 99, 38)) });
        flyoutHeader.Children.Add(new TextBlock { Text = Loc.Get("UpdateFlyoutTitle"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        flyoutStack.Children.Add(flyoutHeader);

        _updateFlyoutDesc = new TextBlock
        {
            Text = Loc.Format("UpdateFlyoutDesc", UpdateService.CachedResult?.LatestVersion ?? ""),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        flyoutStack.Children.Add(_updateFlyoutDesc);

        var flyoutGrid = new Grid { Margin = new Thickness(0, 4, 0, 0), ColumnSpacing = 8 };
        flyoutGrid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        flyoutGrid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });

        var restartBtn = new Button
        {
            Content = Loc.Get("UpdateFlyoutRestartButton"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            FontSize = 12
        };
        restartBtn.Click += (_, _) =>
        {
            updateFlyout.Hide();
            UpdateService.ApplyUpdateAndRestart();
        };
        var laterBtn = new Button
        {
            Content = Loc.Get("UpdateFlyoutLaterButton"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 12
        };
        laterBtn.Click += (_, _) => updateFlyout.Hide();

        Grid.SetColumn(restartBtn, 0);
        Grid.SetColumn(laterBtn, 1);
        flyoutGrid.Children.Add(restartBtn);
        flyoutGrid.Children.Add(laterBtn);
        flyoutStack.Children.Add(flyoutGrid);

        updateFlyout.Content = flyoutStack;
        updateBtn.Flyout = updateFlyout;

        var settings = _settingsButton = CaptionButton("\uE713", Loc.Get("BtnSettings"));
        settings.Click += (_, _) => NavigateToSettings();
        var hide = CaptionButton("\uE8BB", Loc.Get("BtnExit"), isClose: true);
        hide.Click += (_, _) => Close();
        _pin = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
        {
            Content = new FontIcon { Glyph = "\uE718", FontSize = 12 },
            Width = 30, Height = 30, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            IsChecked = Main.Topmost, VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(_pin, Loc.Get("BtnPin"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_pin, Loc.Get("BtnPin"));
        _pin.Click += (_, _) => Main.Topmost = _pin.IsChecked == true;
        actions.Children.Add(_pin);
        var preview = CaptionButton("\uE890", Loc.Get("BtnPreview"));
        preview.Click += async (_, _) => await EnterPreviewAsync();
        actions.Children.Add(preview);
        actions.Children.Add(updateBtn);
        actions.Children.Add(settings);
        actions.Children.Add(hide);
        Grid.SetColumn(actions, 2);
        header.Children.Add(actions);
        return header;
    }

    private Grid CreateSettingsHeader()
    {
        var header = new Grid { Height = 38, Padding = new Thickness(6, 0, 6, 0), Visibility = Visibility.Collapsed };
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        var backBtn = CaptionButton("\uE72B", Loc.Get("BtnBack"));
        backBtn.Click += (_, _) => NavigateToMain();
        header.Children.Add(backBtn);

        SettingsDragHandle.Children.Add(new TextBlock
        {
            Text = Loc.Get("SettingsTitle"), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0),
            IsHitTestVisible = false
        });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SettingsDragHandle, Loc.Get("DragSettingsHandle"));
        Grid.SetColumn(SettingsDragHandle, 1);
        header.Children.Add(SettingsDragHandle);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var hide = CaptionButton("\uE8BB", Loc.Get("BtnExit"), isClose: true);
        hide.Click += (_, _) => Close();
        actions.Children.Add(hide);
        Grid.SetColumn(actions, 2);
        header.Children.Add(actions);

        return header;
    }

    private static Button CaptionButton(string glyph, string label, bool isClose = false)
    {
        var icon = new FontIcon { Glyph = glyph, FontSize = 12 };
        var button = new Button
        {
            Content = icon, Width = 30, Height = 30, MinWidth = 0, MinHeight = 0,
            Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4)
        };
        ToolTipService.SetToolTip(button, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label);

        if (isClose)
        {
            button.PointerEntered += (s, e) =>
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28)); // #C42B1C
                icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            };
            button.PointerPressed += (s, e) =>
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 33, 21)); // #9E2115
                icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            };
            button.PointerReleased += (s, e) =>
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28));
                icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            };
            button.PointerExited += (s, e) =>
            {
                button.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                icon.ClearValue(IconElement.ForegroundProperty);
            };
        }
        else
        {
            void ApplyHover(bool isPressed)
            {
                var isDark = button.ActualTheme == ElementTheme.Dark;
                if (isPressed)
                {
                    button.Background = isDark
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(45, 255, 255, 255))
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(35, 0, 0, 0));
                }
                else
                {
                    button.Background = isDark
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(28, 255, 255, 255))
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(20, 0, 0, 0));
                }
            }

            button.PointerEntered += (s, e) => ApplyHover(false);
            button.PointerPressed += (s, e) => ApplyHover(true);
            button.PointerReleased += (s, e) => ApplyHover(false);
            button.PointerExited += (s, e) => button.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        return button;
    }

    private void UpdateTitleBarUpdateBadge()
    {
        if (_updateButton is null) return;
        var isReady = UpdateService.Status == UpdateStatus.ReadyToInstall;
        var isDownloading = UpdateService.Status == UpdateStatus.Downloading;

        if (isReady)
        {
            _updateButton.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(_updateButton, Loc.Get("TitleBarUpdateReadyToolTip"));
            if (_updateFlyoutDesc is not null)
            {
                _updateFlyoutDesc.Text = Loc.Format("UpdateFlyoutDesc", UpdateService.CachedResult?.LatestVersion ?? "");
            }
        }
        else if (isDownloading)
        {
            _updateButton.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(_updateButton, Loc.Get("TitleBarUpdateDownloadingToolTip"));
        }
        else
        {
            _updateButton.Visibility = Visibility.Collapsed;
        }
    }

    private void InitializeShell()
    {
        if (_dock is not null) return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ConfigureInputRegions();
        _dock = new(this, hwnd);
        _tray = new(hwnd);
        _tray.OpenRequested += ShowPanel;
        _tray.HideRequested += () => { ExitPreview(); _dock.Hide(); };
        _tray.PreviewRequested += async () => { if (IsPreviewMode) ShowPanel(); else await EnterPreviewAsync(); };
        _tray.IsPreview = () => IsPreviewMode;
        _tray.SettingsRequested += () => { ShowPanel(); NavigateToSettings(); };
        _tray.ExitRequested += Close;
        _hotkey = new(hwnd, ShowPanel);
        if (!_hotkey.IsRegistered) ViewModel.ErrorMessage = Loc.Get("HotkeyConflict");
        StartBackgroundUpdateCheck();
    }

    private void StartBackgroundUpdateCheck()
    {
        if (!Main.AutoCheckUpdate) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(3500);
            var result = await Shell.UpdateService.CheckForUpdatesAsync();
            if (result.HasUpdate)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isSettingsActive)
                    {
                        _settingsPage.DisplayUpdateResult(result);
                    }
                    if (_settingsButton is not null)
                    {
                        ToolTipService.SetToolTip(_settingsButton, Loc.Format("SettingsUpdateAvailable", result.LatestVersion));
                    }
                    UpdateTitleBarUpdateBadge();
                });

                if (Main.AutoDownloadUpdate)
                {
                    await Shell.UpdateService.DownloadUpdateAsync(result);
                }
            }
        });
    }

    public void ApplyAppearance()
    {
        var targetTheme = Settings.Appearance.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        _host.RequestedTheme = targetTheme;
        if (_settingsHeader is not null) _settingsHeader.RequestedTheme = targetTheme;
        if (_normalHeader is not null) _normalHeader.RequestedTheme = targetTheme;
        if (_settingsPage is not null)
        {
            _settingsPage.RequestedTheme = targetTheme;
            _settingsPage.UpdatePreviewTheme();
        }
        if (IsPreviewMode)
        {
            var pOpacity = Math.Clamp(Settings.Appearance.PreviewOpacity, .4, 1);
            _previewView.Opacity = 1;
            _host.Opacity = pOpacity;
            _previewInput?.Position();
        }
        else
        {
            _host.Opacity = 1;
        }
        ApplyMaterial();
    }

    private void UpdateSolidBackground()
    {
        var dark = PanelRoot.ActualTheme == ElementTheme.Dark;
        _solid.Background = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 32, 32, 32) : Windows.UI.Color.FromArgb(255, 245, 245, 245));
    }

    private void ApplyMaterial()
    {
        var a = Settings.Appearance;
        if (IsPreviewMode) { _material.Visibility = Visibility.Collapsed; _solid.Visibility = Visibility.Collapsed; return; }
        UpdateSolidBackground();
        var supported = a.Backdrop == BackdropType.Acrylic
            ? Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()
            : a.Backdrop != BackdropType.Solid && Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported();

        _solid.Visibility = supported ? Visibility.Collapsed : Visibility.Visible;
        _material.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        _solid.Opacity = _material.Opacity = 1;
        // Theme changes are handled by the built-in backdrop. Do not disconnect and
        // reconnect its target from inside ActualThemeChanged.
        var kind = supported ? a.Backdrop : BackdropType.Solid;
        if (_materialKind != kind)
        {
            _material.SystemBackdrop = kind switch
            {
                BackdropType.Mica => new MicaBackdrop(),
                BackdropType.MicaAlt => new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt },
                BackdropType.Acrylic => new DesktopAcrylicBackdrop(),
                _ => null
            };
            _materialKind = kind;
        }
    }
}



