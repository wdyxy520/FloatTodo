using System.Windows;
using System.Windows.Media;
using FloatTodo.Core.Models;
using Microsoft.Win32;

namespace FloatTodo.Wpf.Themes;

public static class ThemeHelper
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsDarkMode() => CurrentTheme switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => IsSystemDarkMode()
    };

    private static bool _isTranslucent = true;
    public static event Action? ThemeChanged;

    public static void ApplyTheme(ResourceDictionary resources, bool? isTranslucent)
        => ApplyTheme(resources, null, isTranslucent);

    public static void ApplyTheme(ResourceDictionary resources, AppTheme? theme = null, bool? isTranslucent = null)
    {
        if (theme.HasValue)
        {
            CurrentTheme = theme.Value;
        }

        if (isTranslucent.HasValue)
        {
            _isTranslucent = isTranslucent.Value;
        }

#pragma warning disable WPF0001
        if (Application.Current != null)
        {
            Application.Current.ThemeMode = CurrentTheme switch
            {
                AppTheme.Light => ThemeMode.Light,
                AppTheme.Dark => ThemeMode.Dark,
                _ => ThemeMode.System
            };
        }
#pragma warning restore WPF0001

        bool isDark = IsDarkMode();

        if (isDark)
        {
            // WinUI 3 Dark Palette
            // When translucent backdrop (Mica / MicaAlt / Acrylic) is enabled, WindowBackgroundBrush must be Transparent so the underlying DWM backdrop is visible!
            resources["WindowBackgroundBrush"] = _isTranslucent
                ? Brushes.Transparent
                : new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));

            resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
            resources["CardBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
            resources["HoverBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        }
        else
        {
            // WinUI 3 Light Palette
            resources["WindowBackgroundBrush"] = _isTranslucent
                ? Brushes.Transparent
                : new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));

            resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            resources["CardBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x5E, 0x5E, 0x5E));
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));
            resources["HoverBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x0C, 0x00, 0x00, 0x00));
        }
    }

    public static void InitializeThemeMonitoring()
    {
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (CurrentTheme == AppTheme.System)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ApplyTheme(Application.Current.Resources, _isTranslucent);
                    ThemeChanged?.Invoke();
                });
            }
        };
    }
}
