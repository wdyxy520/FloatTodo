using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FloatTodo.Core.Services;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string LatestVersion,
    string CurrentVersion,
    string Changelog,
    string ReleaseUrl,
    string? InstallerDownloadUrl,
    string? MirrorDownloadUrl,
    string? ErrorMessage = null
);

public static class UpdateChecker
{
    public const string DefaultApiUrl = "https://api.github.com/repos/wdyxy520/FloatTodo/releases/latest";
    public const string DefaultMirrorPrefix = "https://ghproxy.net/";

    public static UpdateCheckResult ParseRelease(string json, Version currentVersion, string mirrorPrefix = DefaultMirrorPrefix)
    {
        var currentVersionStr = $"{currentVersion.Major}.{currentVersion.Minor}.{Math.Max(0, currentVersion.Build)}";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
            var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? "" : "";

            var cleanTag = tagName.TrimStart('v', 'V');
            if (!Version.TryParse(cleanTag, out var latestVersion))
            {
                return new UpdateCheckResult(false, cleanTag, currentVersionStr, body, htmlUrl, null, null, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Unrecognized version format" : "未能识别的版本号格式");
            }

            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (asset.TryGetProperty("browser_download_url", out var dlProp))
                        {
                            installerUrl = dlProp.GetString();
                            if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                        }
                    }
                }
            }

            var hasUpdate = latestVersion > currentVersion;
            var mirrorUrl = !string.IsNullOrEmpty(installerUrl) ? mirrorPrefix + installerUrl : null;

            return new UpdateCheckResult(
                HasUpdate: hasUpdate,
                LatestVersion: cleanTag,
                CurrentVersion: currentVersionStr,
                Changelog: body,
                ReleaseUrl: htmlUrl,
                InstallerDownloadUrl: installerUrl,
                MirrorDownloadUrl: mirrorUrl
            );
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
}
