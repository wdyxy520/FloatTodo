using System;
using FloatTodo.Core.Models;
using FloatTodo.ViewModels;
using FloatTodo.WinUI.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FloatTodo.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private MainViewModel? _main;
    private bool _isInitializing;
    private int _currentColMode = -1;
    private bool? _isCardInternalWide;

    private const double WideThreshold2Col = 520;
    private const double WideThreshold3Col = 860;
    private const double CardInternalWideThreshold = 300;

    private readonly ColumnDefinition _col0 = new() { Width = new GridLength(1, GridUnitType.Star) };
    private readonly ColumnDefinition _col1 = new() { Width = new GridLength(1, GridUnitType.Star) };
    private readonly ColumnDefinition _col2 = new() { Width = new GridLength(1, GridUnitType.Star) };

    public event Action? PeekPreviewRequested;

    public SettingsPage()
    {
        InitializeComponent();

        ActualThemeChanged += (_, _) => UpdatePreviewTheme();
        Loaded += (_, _) =>
        {
            UpdatePreviewTheme();
            UpdateAdaptiveLayout(ActualWidth);
        };
        SizeChanged += (_, e) => UpdateAdaptiveLayout(e.NewSize.Width);

        ThemeComboBox.SelectionChanged += OnThemeChanged;
        MaterialComboBox.SelectionChanged += OnMaterialChanged;
        PreviewOpacitySlider.ValueChanged += OnOpacityValueChanged;
        TopmostSwitch.Toggled += OnTopmostToggled;
        AutoHideSwitch.Toggled += OnAutoHideToggled;
        StartupSwitch.Toggled += OnStartupToggled;
        AutoCheckUpdateSwitch.Toggled += OnAutoCheckUpdateToggled;
        AutoDownloadUpdateSwitch.Toggled += OnAutoDownloadUpdateToggled;
        InstallOnExitSwitch.Toggled += OnInstallOnExitToggled;
        LanguageComboBox.SelectionChanged += OnLanguageChanged;
        PeekPreviewButton.Click += (_, _) => PeekPreviewRequested?.Invoke();
        RestartUpdateButton.Click += (_, _) => UpdateService.ApplyUpdateAndRestart();

        UpdateService.DownloadProgressChanged += OnDownloadProgressChanged;
        UpdateService.StatusChanged += OnUpdateStatusChanged;

        LanguageHint.Text = Loc.Get("LanguageRestart");
        VersionText.Text = $"FloatTodo v{UpdateService.GetCurrentVersionString()}";
        CheckUpdateButton.Click += OnCheckUpdateClicked;
    }

    public void Initialize(MainViewModel main)
    {
        _main = main;
        RefreshFromSettings();
    }

    public void RefreshFromSettings()
    {
        if (_main == null) return;
        _isInitializing = true;
        try
        {
            ThemeComboBox.SelectedIndex = Math.Clamp((int)_main.Theme, 0, 2);
            MaterialComboBox.SelectedIndex = Math.Clamp((int)_main.Backdrop, 0, 3);

            var opacityVal = Math.Clamp(_main.PreviewOpacity * 100, 40, 100);
            PreviewOpacitySlider.Value = opacityVal;
            OpacityPercentText.Text = $"{(int)opacityVal}%";
            MiniPreviewCard.Opacity = opacityVal / 100.0;

            TopmostSwitch.IsOn = _main.Topmost;
            AutoHideSwitch.IsOn = _main.AutoHide;
            StartupSwitch.IsOn = StartupService.IsEnabled;
            AutoCheckUpdateSwitch.IsOn = _main.AutoCheckUpdate;
            AutoDownloadUpdateSwitch.IsOn = _main.AutoDownloadUpdate;
            InstallOnExitSwitch.IsOn = _main.InstallOnExit;
            LanguageComboBox.SelectedIndex = _main.Language switch
            {
                "zh-CN" => 1,
                "en-US" => 2,
                _ => 0
            };

            RequestedTheme = _main.Theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            UpdatePreviewTheme();
            UpdateAdaptiveLayout(ActualWidth);

            if (UpdateService.CachedResult is { HasUpdate: true } cached)
            {
                DisplayUpdateResult(cached);
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public void UpdatePreviewTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;
        if (_main != null)
        {
            if (_main.Theme == AppTheme.Dark) isDark = true;
            else if (_main.Theme == AppTheme.Light) isDark = false;
        }

        if (isDark)
        {
            var grad = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 15, 23, 42), Offset = 0.0 });
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 30, 58, 138), Offset = 0.35 });
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 88, 28, 135), Offset = 0.75 });
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 180, 83, 9), Offset = 1.0 });
            WallpaperContainer.Background = grad;

            MiniPreviewCard.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 36, 36, 36));
            MiniPreviewCard.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255));

            PreviewTitleText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            PreviewCheckText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 230, 230));
            PreviewBulletDesc.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 230, 230));
            PreviewBulletText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 160));
        }
        else
        {
            var grad = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 59, 130, 246), Offset = 0.0 });
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 139, 92, 246), Offset = 0.45 });
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 236, 72, 153), Offset = 0.8 });
            grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 245, 158, 11), Offset = 1.0 });
            WallpaperContainer.Background = grad;

            MiniPreviewCard.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            MiniPreviewCard.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 0, 0));

            PreviewTitleText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 28, 28, 28));
            PreviewCheckText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 40, 40));
            PreviewBulletDesc.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 40, 40));
            PreviewBulletText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 120, 120));
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        if (ThemeComboBox.SelectedIndex >= 0)
        {
            _main.Theme = (AppTheme)ThemeComboBox.SelectedIndex;
            RequestedTheme = _main.Theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            UpdatePreviewTheme();
        }
    }

    private void OnMaterialChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        if (MaterialComboBox.SelectedIndex >= 0)
        {
            _main.Backdrop = (BackdropType)MaterialComboBox.SelectedIndex;
        }
    }

    private void OnOpacityValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        OpacityPercentText.Text = $"{(int)e.NewValue}%";
        MiniPreviewCard.Opacity = e.NewValue / 100.0;

        if (_isInitializing || _main == null) return;
        _main.PreviewOpacity = e.NewValue / 100.0;
    }

    private void OnTopmostToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        _main.Topmost = TopmostSwitch.IsOn;
    }

    private void OnAutoHideToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        _main.AutoHide = AutoHideSwitch.IsOn;
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        StartupService.SetEnabled(StartupSwitch.IsOn);
    }

    private void OnAutoCheckUpdateToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        _main.AutoCheckUpdate = AutoCheckUpdateSwitch.IsOn;
    }

    private void OnAutoDownloadUpdateToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        _main.AutoDownloadUpdate = AutoDownloadUpdateSwitch.IsOn;
    }

    private void OnInstallOnExitToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        _main.InstallOnExit = InstallOnExitSwitch.IsOn;
    }

    private void OnDownloadProgressChanged(double progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (UpdateService.Status == UpdateStatus.Downloading)
            {
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = progress;
                if (UpdateInfoBar.IsOpen)
                {
                    UpdateInfoBar.Message = Loc.Format("UpdateDownloadingStatus", (int)progress);
                }
            }
        });
    }

    private void OnUpdateStatusChanged(UpdateStatus status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (status)
            {
                case UpdateStatus.Downloading:
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    RestartUpdateButton.Visibility = Visibility.Collapsed;
                    if (UpdateInfoBar.IsOpen)
                    {
                        UpdateInfoBar.Message = Loc.Format("UpdateDownloadingStatus", (int)UpdateService.DownloadProgress);
                    }
                    break;
                case UpdateStatus.ReadyToInstall:
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    RestartUpdateButton.Visibility = Visibility.Visible;
                    UpdateInfoBar.Severity = InfoBarSeverity.Success;
                    UpdateInfoBar.Title = Loc.Get("UpdateFlyoutTitle");
                    UpdateInfoBar.Message = Loc.Get("UpdateReadyStatus");
                    UpdateInfoBar.IsOpen = true;
                    break;
                case UpdateStatus.Failed:
                case UpdateStatus.Idle:
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    break;
            }
        });
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _main == null) return;
        var selectedTag = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        _main.Language = selectedTag;
        Loc.SetLanguageOverride(selectedTag);
    }

    public void UpdateAdaptiveLayout(double width)
    {
        if (width <= 0) return;

        // 1. 控制区外层分栏调度（单列 / 双列 / 三列）
        int targetColMode = width >= WideThreshold3Col ? 3 : (width >= WideThreshold2Col ? 2 : 1);
        if (targetColMode != _currentColMode)
        {
            _currentColMode = targetColMode;
            ApplyColumnMode(targetColMode);
        }

        // 2. 卡片内部设置项调度（基于卡片可用宽度做左右排布或上下堆叠）
        double cardWidth = targetColMode switch
        {
            3 => (width - 24 - 32) / 3.0,
            2 => (width - 24 - 16) / 2.0,
            _ => width - 24.0
        };

        bool targetInternalWide = cardWidth >= CardInternalWideThreshold;
        if (_isCardInternalWide != targetInternalWide)
        {
            _isCardInternalWide = targetInternalWide;
            ApplyInternalLayout(targetInternalWide);
        }
    }

    private void ApplyColumnMode(int mode)
    {
        ContentGrid.ColumnDefinitions.Clear();
        switch (mode)
        {
            case 3: // 三列：外观材质、桌面预览、窗口行为三列一字排开
                ContentGrid.ColumnDefinitions.Add(_col0);
                ContentGrid.ColumnDefinitions.Add(_col1);
                ContentGrid.ColumnDefinitions.Add(_col2);

                Grid.SetRow(CardAppearance, 0);
                Grid.SetColumn(CardAppearance, 0);
                Grid.SetRowSpan(CardAppearance, 1);

                Grid.SetRow(CardPreview, 0);
                Grid.SetColumn(CardPreview, 1);
                Grid.SetRowSpan(CardPreview, 1);

                Grid.SetRow(CardBehavior, 0);
                Grid.SetColumn(CardBehavior, 2);
                Grid.SetRowSpan(CardBehavior, 1);

                Grid.SetRow(FooterSection, 1);
                Grid.SetColumn(FooterSection, 0);
                Grid.SetColumnSpan(FooterSection, 3);
                break;

            case 2: // 双列：左侧外观与行为，右侧桌面预览
                ContentGrid.ColumnDefinitions.Add(_col0);
                ContentGrid.ColumnDefinitions.Add(_col1);

                Grid.SetRow(CardAppearance, 0);
                Grid.SetColumn(CardAppearance, 0);
                Grid.SetRowSpan(CardAppearance, 1);

                Grid.SetRow(CardBehavior, 1);
                Grid.SetColumn(CardBehavior, 0);
                Grid.SetRowSpan(CardBehavior, 1);

                Grid.SetRow(CardPreview, 0);
                Grid.SetColumn(CardPreview, 1);
                Grid.SetRowSpan(CardPreview, 2);

                Grid.SetRow(FooterSection, 2);
                Grid.SetColumn(FooterSection, 0);
                Grid.SetColumnSpan(FooterSection, 2);
                break;

            default: // 1: 单列垂直流
                ContentGrid.ColumnDefinitions.Add(_col0);

                Grid.SetRow(CardAppearance, 0);
                Grid.SetColumn(CardAppearance, 0);
                Grid.SetRowSpan(CardAppearance, 1);

                Grid.SetRow(CardPreview, 1);
                Grid.SetColumn(CardPreview, 0);
                Grid.SetRowSpan(CardPreview, 1);

                Grid.SetRow(CardBehavior, 2);
                Grid.SetColumn(CardBehavior, 0);
                Grid.SetRowSpan(CardBehavior, 1);

                Grid.SetRow(FooterSection, 3);
                Grid.SetColumn(FooterSection, 0);
                Grid.SetColumnSpan(FooterSection, 1);
                break;
        }
    }

    private void ApplyInternalLayout(bool wide)
    {
        // 1. 外观主题
        SetRowLayout(ThemeTitle, ThemeComboBox, wide, controlWidth: 140);

        // 2. 背景材质
        SetRowLayout(MaterialTitle, MaterialComboBox, wide, controlWidth: 140);

        // 3. 保持置顶
        SetRowLayout(TopmostTitle, TopmostSwitch, wide);

        // 4. 贴边后自动收起
        SetRowLayout(AutoHideTitle, AutoHideSwitch, wide);

        // 5. 开机启动
        SetRowLayout(StartupTitle, StartupSwitch, wide);

        // 5.1 自动检查更新
        SetRowLayout(AutoCheckUpdateTitle, AutoCheckUpdateSwitch, wide);

        // 5.2 自动下载更新
        SetRowLayout(AutoDownloadUpdateTitle, AutoDownloadUpdateSwitch, wide);

        // 5.3 退出时自动安装更新
        SetRowLayout(InstallOnExitTitle, InstallOnExitSwitch, wide);

        // 5.4 语言设置
        SetRowLayout(LanguageLabelPanel, LanguageComboBox, wide, controlWidth: 160);

        // 6. 桌面预览不透明度
        ApplyOpacityRowLayout(wide);

        // 7. 试看实际桌面效果
        ApplyPeekRowLayout(wide);
    }

    private static void SetRowLayout(FrameworkElement label, FrameworkElement control, bool wide, double? controlWidth = null)
    {
        if (wide)
        {
            // 左右排布：标题在左，控件在右
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 1);
            label.Margin = new Thickness(0);

            Grid.SetRow(control, 0);
            Grid.SetColumn(control, 1);
            Grid.SetColumnSpan(control, 1);
            control.HorizontalAlignment = HorizontalAlignment.Right;
            if (controlWidth.HasValue)
            {
                control.Width = controlWidth.Value;
            }
            control.Margin = new Thickness(12, 0, 0, 0);
        }
        else
        {
            // 上下排布：标题在上，控件在下
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 2);
            label.Margin = new Thickness(0, 0, 0, 4);

            Grid.SetRow(control, 1);
            Grid.SetColumn(control, 0);
            Grid.SetColumnSpan(control, 2);
            control.HorizontalAlignment = control is ComboBox ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            if (controlWidth.HasValue)
            {
                control.Width = double.NaN;
            }
            control.Margin = new Thickness(0);
        }
    }

    private void ApplyOpacityRowLayout(bool wide)
    {
        if (wide)
        {
            // 左右排布：左边标题，中间滑块，右边百分比
            Grid.SetRow(OpacityTitle, 0);
            Grid.SetColumn(OpacityTitle, 0);
            Grid.SetColumnSpan(OpacityTitle, 1);
            OpacityTitle.Margin = new Thickness(0);

            Grid.SetRow(PreviewOpacitySlider, 0);
            Grid.SetColumn(PreviewOpacitySlider, 1);
            Grid.SetColumnSpan(PreviewOpacitySlider, 1);
            PreviewOpacitySlider.Width = 110;
            PreviewOpacitySlider.Margin = new Thickness(8, 0, 8, 0);
            PreviewOpacitySlider.HorizontalAlignment = HorizontalAlignment.Right;

            Grid.SetRow(OpacityPercentText, 0);
            Grid.SetColumn(OpacityPercentText, 2);
            Grid.SetColumnSpan(OpacityPercentText, 1);
        }
        else
        {
            // 上下排布：第一行标题在左、百分比在右；第二行滑块撑满整行
            Grid.SetRow(OpacityTitle, 0);
            Grid.SetColumn(OpacityTitle, 0);
            Grid.SetColumnSpan(OpacityTitle, 2);
            OpacityTitle.Margin = new Thickness(0);

            Grid.SetRow(OpacityPercentText, 0);
            Grid.SetColumn(OpacityPercentText, 2);
            Grid.SetColumnSpan(OpacityPercentText, 1);

            Grid.SetRow(PreviewOpacitySlider, 1);
            Grid.SetColumn(PreviewOpacitySlider, 0);
            Grid.SetColumnSpan(PreviewOpacitySlider, 3);
            PreviewOpacitySlider.Width = double.NaN;
            PreviewOpacitySlider.Margin = new Thickness(0, 2, 0, 0);
            PreviewOpacitySlider.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }

    private void ApplyPeekRowLayout(bool wide)
    {
        if (wide)
        {
            // 左右排布：左边提示文字，右边试看按钮
            Grid.SetRow(PeekTipText, 0);
            Grid.SetColumn(PeekTipText, 0);
            Grid.SetColumnSpan(PeekTipText, 1);
            PeekTipText.Margin = new Thickness(0);

            Grid.SetRow(PeekPreviewButton, 0);
            Grid.SetColumn(PeekPreviewButton, 1);
            Grid.SetColumnSpan(PeekPreviewButton, 1);
            PeekPreviewButton.HorizontalAlignment = HorizontalAlignment.Right;
            PeekPreviewButton.Margin = new Thickness(12, 0, 0, 0);
        }
        else
        {
            // 上下排布：第一行按钮撑满整行，第二行提示文字
            Grid.SetRow(PeekPreviewButton, 0);
            Grid.SetColumn(PeekPreviewButton, 0);
            Grid.SetColumnSpan(PeekPreviewButton, 2);
            PeekPreviewButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            PeekPreviewButton.Margin = new Thickness(0, 4, 0, 0);

            Grid.SetRow(PeekTipText, 1);
            Grid.SetColumn(PeekTipText, 0);
            Grid.SetColumnSpan(PeekTipText, 2);
            PeekTipText.Margin = new Thickness(0, 4, 0, 0);
        }
    }

    public void DisplayUpdateResult(FloatTodo.Core.Services.UpdateCheckResult result)
    {
        if (result.HasUpdate)
        {
            UpdateInfoBar.Severity = InfoBarSeverity.Success;
            UpdateInfoBar.Title = Loc.Format("UpdateNewVersion", result.LatestVersion);
            UpdateInfoBar.Message = string.IsNullOrWhiteSpace(result.Changelog) ? Loc.Get("UpdateChooseDownload") : result.Changelog.Trim();

            if (UpdateService.Status == UpdateStatus.ReadyToInstall)
            {
                RestartUpdateButton.Visibility = Visibility.Visible;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }
            else if (UpdateService.Status == UpdateStatus.Downloading)
            {
                RestartUpdateButton.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = UpdateService.DownloadProgress;
            }
            else
            {
                RestartUpdateButton.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }

            DownloadMirrorButton.Visibility = !string.IsNullOrEmpty(result.MirrorDownloadUrl) ? Visibility.Visible : Visibility.Collapsed;
            DownloadMirrorButton.Click -= OnMirrorDownloadClicked;
            DownloadMirrorButton.Click += OnMirrorDownloadClicked;

            DownloadOfficialButton.Visibility = !string.IsNullOrEmpty(result.InstallerDownloadUrl) ? Visibility.Visible : Visibility.Collapsed;
            DownloadOfficialButton.Click -= OnOfficialDownloadClicked;
            DownloadOfficialButton.Click += OnOfficialDownloadClicked;

            ViewReleaseButton.Visibility = !string.IsNullOrEmpty(result.ReleaseUrl) ? Visibility.Visible : Visibility.Collapsed;
            ViewReleaseButton.Click -= OnViewReleaseClicked;
            ViewReleaseButton.Click += OnViewReleaseClicked;

            void OnMirrorDownloadClicked(object s, RoutedEventArgs ev) => UpdateService.OpenUrl(result.MirrorDownloadUrl);
            void OnOfficialDownloadClicked(object s, RoutedEventArgs ev) => UpdateService.OpenUrl(result.InstallerDownloadUrl);
            void OnViewReleaseClicked(object s, RoutedEventArgs ev) => UpdateService.OpenUrl(result.ReleaseUrl);
        }
        else if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            UpdateInfoBar.Severity = InfoBarSeverity.Warning;
            UpdateInfoBar.Title = Loc.Get("UpdateConnectFailed");
            UpdateInfoBar.Message = Loc.Get("UpdateConnectError");
            RestartUpdateButton.Visibility = Visibility.Collapsed;
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            DownloadMirrorButton.Visibility = Visibility.Collapsed;
            DownloadOfficialButton.Visibility = Visibility.Collapsed;
            ViewReleaseButton.Visibility = Visibility.Visible;
            ViewReleaseButton.Click -= OnErrorReleaseClicked;
            ViewReleaseButton.Click += OnErrorReleaseClicked;
            void OnErrorReleaseClicked(object s, RoutedEventArgs ev) => UpdateService.OpenUrl(result.ReleaseUrl);
        }
        else
        {
            UpdateInfoBar.Severity = InfoBarSeverity.Informational;
            UpdateInfoBar.Title = Loc.Get("UpdateUpToDate");
            UpdateInfoBar.Message = Loc.Format("UpdateCurrentVersion", result.CurrentVersion);
            RestartUpdateButton.Visibility = Visibility.Collapsed;
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            DownloadMirrorButton.Visibility = Visibility.Collapsed;
            DownloadOfficialButton.Visibility = Visibility.Collapsed;
            ViewReleaseButton.Visibility = Visibility.Collapsed;
        }
        UpdateInfoBar.IsOpen = true;
    }

    private async void OnCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateProgressRing.Visibility = Visibility.Visible;
        UpdateProgressRing.IsActive = true;
        UpdateInfoBar.IsOpen = false;

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            DisplayUpdateResult(result);
        }
        finally
        {
            UpdateProgressRing.IsActive = false;
            UpdateProgressRing.Visibility = Visibility.Collapsed;
            CheckUpdateButton.IsEnabled = true;
        }
    }
}

