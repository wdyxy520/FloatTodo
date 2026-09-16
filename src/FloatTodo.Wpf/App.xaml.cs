using System.Windows;
using FloatTodo.Wpf.Themes;

namespace FloatTodo.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeHelper.ApplyTheme(Resources);
        ThemeHelper.InitializeThemeMonitoring();
    }
}
