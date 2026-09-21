using CommunityToolkit.Mvvm.ComponentModel;
using FloatTodo.Core.Models;

namespace FloatTodo.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public TodayViewModel Today { get; }
    public AppSettings Settings { get; }
    public event Action? SettingsChanged;
    [ObservableProperty] private bool isDialogOpen;
    [ObservableProperty] private bool isSettingsOpen;

    public MainViewModel(TodayViewModel today, AppSettings settings)
    {
        Today = today;
        Settings = settings;
    }

    public AppTheme Theme
    {
        get => Settings.Appearance.Theme;
        set
        {
            if (Theme == value) return;
            Settings.Appearance.Theme = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }

    public BackdropType Backdrop
    {
        get => Settings.Appearance.Backdrop;
        set
        {
            if (Backdrop == value) return;
            Settings.Appearance.Backdrop = value;
            Settings.Appearance.UseMica = value != BackdropType.Solid;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }

    public double PreviewOpacity
    {
        get => Settings.Appearance.PreviewOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.4, 1.0);
            if (Math.Abs(Settings.Appearance.PreviewOpacity - clamped) < 0.001) return;
            Settings.Appearance.PreviewOpacity = clamped;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }

    public bool Topmost
    {
        get => Settings.Window.Topmost;
        set
        {
            if (Topmost == value) return;
            Settings.Window.Topmost = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }

    public bool AutoHide
    {
        get => Settings.Window.AutoHide;
        set
        {
            if (AutoHide == value) return;
            Settings.Window.AutoHide = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }

    public bool AutoCheckUpdate
    {
        get => Settings.Update.AutoCheckUpdate;
        set
        {
            if (AutoCheckUpdate == value) return;
            Settings.Update.AutoCheckUpdate = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }

    public string Language
    {
        get => Settings.Language;
        set
        {
            if (Language == value) return;
            Settings.Language = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke();
        }
    }
}
