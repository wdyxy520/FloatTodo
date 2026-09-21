using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FloatTodo.Core.Services;

namespace FloatTodo.WinUI.Shell;

public enum UpdateStatus
{
    Idle,
    Checking,
    Downloading,
    ReadyToInstall,
    Failed
}

public static class UpdateService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static CancellationTokenSource? _downloadCts;

    public static event Action<UpdateStatus>? StatusChanged;
    public static event Action<double>? DownloadProgressChanged;

    public static UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
    public static double DownloadProgress { get; private set; }
    public static string? ReadyInstallerPath { get; private set; }
    public static UpdateCheckResult? CachedResult { get; private set; }

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FloatTodo-WinUI-App");
    }

    public static string GetCurrentVersionString()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}" : "0.3.2";
    }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var currentVersionStr = GetCurrentVersionString();
        Version.TryParse(currentVersionStr, out var currentVersion);
        currentVersion ??= new Version(0, 3, 2);

        SetStatus(UpdateStatus.Checking);

        try
        {
            var json = await _httpClient.GetStringAsync(UpdateChecker.DefaultApiUrl);
            var result = UpdateChecker.ParseRelease(json, currentVersion);
            CachedResult = result;

            if (result.HasUpdate)
            {
                var targetPath = GetTargetInstallerPath(result.LatestVersion);
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 1_000_000)
                {
                    ReadyInstallerPath = targetPath;
                    SetStatus(UpdateStatus.ReadyToInstall);
                }
                else
                {
                    SetStatus(UpdateStatus.Idle);
                }
            }
            else
            {
                SetStatus(UpdateStatus.Idle);
            }

            return result;
        }
        catch (Exception ex)
        {
            SetStatus(UpdateStatus.Failed);
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

    public static string GetTargetInstallerPath(string latestVersion)
    {
        var dir = Path.Combine(Path.GetTempPath(), "FloatTodo_Updates");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, $"FloatTodo-Setup-{latestVersion}.exe");
    }

    public static async Task<string?> DownloadUpdateAsync(UpdateCheckResult result, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(result.LatestVersion)) return null;

        var targetPath = GetTargetInstallerPath(result.LatestVersion);
        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 1_000_000)
        {
            ReadyInstallerPath = targetPath;
            SetStatus(UpdateStatus.ReadyToInstall);
            return targetPath;
        }

        _downloadCts?.Cancel();
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _downloadCts.Token;

        SetStatus(UpdateStatus.Downloading);
        SetProgress(0);

        var tempPath = targetPath + ".download";

        // 尝试下载的候选 URL 列表（首选国内镜像，次选官方源）
        var candidateUrls = new[] { result.MirrorDownloadUrl, result.InstallerDownloadUrl };

        foreach (var url in candidateUrls)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[16384];
                    long totalRead = 0;
                    int read;

                    while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read), token);
                        totalRead += read;

                        if (totalBytes > 0)
                        {
                            var pct = Math.Clamp((double)totalRead / totalBytes * 100.0, 0, 100);
                            SetProgress(pct);
                        }
                    }
                }

                if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 1_000_000)
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                    File.Move(tempPath, targetPath);
                    ReadyInstallerPath = targetPath;
                    SetProgress(100);
                    SetStatus(UpdateStatus.ReadyToInstall);
                    return targetPath;
                }
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                SetStatus(UpdateStatus.Idle);
                return null;
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                // 镜像失败后继续尝试下一个源
            }
        }

        SetStatus(UpdateStatus.Failed);
        return null;
    }

    public static void ApplyUpdateAndRestart(string? installerPath = null)
    {
        installerPath ??= ReadyInstallerPath;
        if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath)) return;

        try
        {
            var exePath = Environment.ProcessPath ?? "";
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start /wait \"\" \"{installerPath}\" /SILENT && start \"\" \"{exePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
        catch
        {
            OpenUrl(installerPath);
        }
    }

    public static void ApplyUpdateOnExit(string? installerPath = null)
    {
        installerPath ??= ReadyInstallerPath;
        if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /NORESTART",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // 退出时不阻断
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

    private static void SetStatus(UpdateStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    private static void SetProgress(double progress)
    {
        DownloadProgress = progress;
        DownloadProgressChanged?.Invoke(progress);
    }
}
