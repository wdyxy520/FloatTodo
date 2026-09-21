using Microsoft.Win32;
namespace FloatTodo.WinUI.Shell;

internal static class StartupService
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("FloatTodo") is string; } }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("FloatTodo", '"' + Environment.ProcessPath + '"'); else key.DeleteValue("FloatTodo", false);
    }
}
internal sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex; private readonly EventWaitHandle _signal; private RegisteredWaitHandle? _wait;
    public bool IsFirst { get; }
    public SingleInstanceService(string dataIdentity)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(dataIdentity.ToUpperInvariant())))[..24];
        _mutex = new Mutex(true, @"Local\FloatTodo-" + hash, out var created); IsFirst = created;
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\FloatTodo-Activate-" + hash);
        if (!created) _signal.Set();
    }
    public void Listen(Action activate) => _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => activate(), null, Timeout.Infinite, false);
    public void Dispose() { _wait?.Unregister(null); _signal.Dispose(); _mutex.Dispose(); }
}
