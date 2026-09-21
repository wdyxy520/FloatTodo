using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using FloatTodo.Core.Services;

namespace FloatTodo.WinUI.Shell;

public static class UpdateService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FloatTodo-WinUI-App");
    }

    public static string GetCurrentVersionString()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}" : "0.2.0";
    }

    public static UpdateCheckResult? CachedResult { get; private set; }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var currentVersionStr = GetCurrentVersionString();
        Version.TryParse(currentVersionStr, out var currentVersion);
        currentVersion ??= new Version(0, 2, 0);

        try
        {
            var json = await _httpClient.GetStringAsync(UpdateChecker.DefaultApiUrl);
            var result = UpdateChecker.ParseRelease(json, currentVersion);
            CachedResult = result;
            return result;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                HasUpdate: false,
                LatestVersion: string.Empty,
                CurrentVersion: currentVersionStr,
                Changelog: string.Empty,
                ReleaseUrl: "https://github.com/wdyxy520/FloatTodo/releases",
                InstallerDownloadUrl: null,
                MirrorDownloadUrl: null,
                ErrorMessage: ex.Message
            );
        }
    }

    public static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 忽略启动异常
        }
    }
}
