using System.ComponentModel;
using FloatTodo.Core.Models;
using FloatTodo.ViewModels;
using Xunit;

namespace FloatTodo.Core.Tests;

public class MainViewModelTests
{
    [Fact]
    public void ThemeChangeUpdatesSettingsAndFiresEvents()
    {
        var settings = new AppSettings();
        var main = new MainViewModel(new TodayViewModel([]), settings);
        var settingsChangedFired = false;
        var propertyChangedFired = false;

        main.SettingsChanged += () => settingsChangedFired = true;
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Theme)) propertyChangedFired = true;
        };

        main.Theme = AppTheme.Dark;

        Assert.True(settingsChangedFired);
        Assert.True(propertyChangedFired);
        Assert.Equal(AppTheme.Dark, settings.Appearance.Theme);
        Assert.Equal(AppTheme.Dark, main.Theme);
    }

    [Fact]
    public void BackdropChangeUpdatesSettingsAndFiresEvents()
    {
        var settings = new AppSettings();
        var main = new MainViewModel(new TodayViewModel([]), settings);
        var settingsChangedFired = false;
        var propertyChangedFired = false;

        main.SettingsChanged += () => settingsChangedFired = true;
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Backdrop)) propertyChangedFired = true;
        };

        main.Backdrop = BackdropType.Acrylic;

        Assert.True(settingsChangedFired);
        Assert.True(propertyChangedFired);
        Assert.Equal(BackdropType.Acrylic, settings.Appearance.Backdrop);
        Assert.True(settings.Appearance.UseMica);

        main.Backdrop = BackdropType.Solid;
        Assert.Equal(BackdropType.Solid, settings.Appearance.Backdrop);
        Assert.False(settings.Appearance.UseMica);
    }

    [Fact]
    public void PreviewOpacityClampsAndUpdatesSettings()
    {
        var settings = new AppSettings();
        var main = new MainViewModel(new TodayViewModel([]), settings);
        var settingsChangedCount = 0;

        main.SettingsChanged += () => settingsChangedCount++;

        main.PreviewOpacity = 0.85;
        Assert.Equal(0.85, main.PreviewOpacity, 3);
        Assert.Equal(0.85, settings.Appearance.PreviewOpacity, 3);
        Assert.Equal(1, settingsChangedCount);

        // Clamping below 0.4
        main.PreviewOpacity = 0.2;
        Assert.Equal(0.4, main.PreviewOpacity, 3);
        Assert.Equal(0.4, settings.Appearance.PreviewOpacity, 3);

        // Clamping above 1.0
        main.PreviewOpacity = 1.5;
        Assert.Equal(1.0, main.PreviewOpacity, 3);
        Assert.Equal(1.0, settings.Appearance.PreviewOpacity, 3);
    }

    [Fact]
    public void TopmostAndAutoHideUpdateSettings()
    {
        var settings = new AppSettings();
        var main = new MainViewModel(new TodayViewModel([]), settings);

        main.Topmost = false;
        Assert.False(settings.Window.Topmost);

        main.AutoHide = false;
        Assert.False(settings.Window.AutoHide);
    }

    [Fact]
    public void SettingsOpenStateChangesProperly()
    {
        var main = new MainViewModel(new TodayViewModel([]), new AppSettings());
        Assert.False(main.IsSettingsOpen);

        main.IsSettingsOpen = true;
        Assert.True(main.IsSettingsOpen);

        main.IsSettingsOpen = false;
        Assert.False(main.IsSettingsOpen);
    }

    [Fact]
    public void LanguageChangeUpdatesSettingsAndFiresEvents()
    {
        var settings = new AppSettings();
        var main = new MainViewModel(new TodayViewModel([]), settings);
        var settingsChangedFired = false;
        var propertyChangedFired = false;

        main.SettingsChanged += () => settingsChangedFired = true;
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Language)) propertyChangedFired = true;
        };

        main.Language = "en-US";

        Assert.True(settingsChangedFired);
        Assert.True(propertyChangedFired);
        Assert.Equal("en-US", settings.Language);
        Assert.Equal("en-US", main.Language);
    }
}
