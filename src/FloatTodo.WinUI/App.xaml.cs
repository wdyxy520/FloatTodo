using FloatTodo.Core.Services;
using FloatTodo.ViewModels;
using FloatTodo.WinUI.Services;
using FloatTodo.WinUI.Shell;
namespace FloatTodo.WinUI;

public partial class App : Application
{
    private MainWindow? _window; private Shell.SingleInstanceService? _instance;
    public App() => InitializeComponent();
    protected override async void OnLaunched(LaunchActivatedEventArgs e)
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--data-dir");
            string? path = index >= 0 && index + 1 < args.Length ? Path.Combine(Path.GetFullPath(args[index + 1]), "todos.json") : null;
            var settingsPath = path is null ? null : Path.Combine(Path.GetDirectoryName(path)!, "settings.json");
            var settingsStore = new StorageService(settingsPath);
            var settings = settingsStore.LoadSettings();

            try
            {
                if (!string.IsNullOrEmpty(settings.Language))
                {
                    Loc.SetLanguageOverride(settings.Language);
                    var culture = new System.Globalization.CultureInfo(settings.Language);
                    System.Globalization.CultureInfo.CurrentCulture = culture;
                    System.Globalization.CultureInfo.CurrentUICulture = culture;
                    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
                    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to apply language override: {ex.Message}");
            }

            _instance = new Shell.SingleInstanceService(path ?? "FloatTodo.Default");
            if (!_instance.IsFirst)
            {
                _instance.Dispose();
                Exit();
                return;
            }

            MemoService service;
            try
            {
                service = await Task.Run(() => new MemoService(path));
            }
            catch (Exception ex)
            {
                var failure = new Window
                {
                    Title = Loc.Get("AppStartupError"),
                    Content = new TextBlock
                    {
                        Text = Loc.Get("AppCannotReadMemos") + "\n" + ex.Message,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20)
                    }
                };
                failure.Activate();
                return;
            }

            _window = new MainWindow(new TodayViewModel(service.GetMemos()), service, settingsPath);
            _instance.Listen(() => _window.DispatcherQueue.TryEnqueue(() => _window.ShowPanel()));
            _window.Closed += (_, _) => _instance.Dispose();
            _window.Activate();
        }
        catch (Exception error)
        {
            var failure = new Window
            {
                Title = Loc.Get("AppStartupError"),
                Content = new TextBlock
                {
                    Text = error.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                }
            };
            failure.Activate();
        }
    }
}
