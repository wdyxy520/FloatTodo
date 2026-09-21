using System;
using FloatTodo.Core.Services;
using Xunit;

namespace FloatTodo.Core.Tests;

public class UpdateCheckerTests
{
    private const string SampleReleaseJson = """
    {
        "tag_name": "v0.3.0",
        "name": "FloatTodo v0.3.0",
        "body": "- 新增自动更新功能\n- 优化贴边动画",
        "html_url": "https://github.com/wdyxy520/FloatTodo/releases/tag/v0.3.0",
        "assets": [
            {
                "name": "FloatTodo-Portable-0.3.0.zip",
                "browser_download_url": "https://github.com/wdyxy520/FloatTodo/releases/download/v0.3.0/FloatTodo-Portable-0.3.0.zip"
            },
            {
                "name": "FloatTodo-Setup-0.3.0.exe",
                "browser_download_url": "https://github.com/wdyxy520/FloatTodo/releases/download/v0.3.0/FloatTodo-Setup-0.3.0.exe"
            }
        ]
    }
    """;

    [Fact]
    public void ParseRelease_DetectsNewerVersion_AndBuildsMirrorUrl()
    {
        var currentVersion = new Version(0, 2, 0);
        var result = UpdateChecker.ParseRelease(SampleReleaseJson, currentVersion);

        Assert.True(result.HasUpdate);
        Assert.Equal("0.3.0", result.LatestVersion);
        Assert.Equal("0.2.0", result.CurrentVersion);
        Assert.Contains("FloatTodo-Setup-0.3.0.exe", result.InstallerDownloadUrl);
        Assert.StartsWith("https://ghproxy.net/", result.MirrorDownloadUrl);
        Assert.Contains("FloatTodo-Setup-0.3.0.exe", result.MirrorDownloadUrl);
        Assert.Equal("https://github.com/wdyxy520/FloatTodo/releases/tag/v0.3.0", result.ReleaseUrl);
    }

    [Fact]
    public void ParseRelease_ReturnsFalse_WhenVersionIsSame()
    {
        var currentVersion = new Version(0, 3, 0);
        var result = UpdateChecker.ParseRelease(SampleReleaseJson, currentVersion);

        Assert.False(result.HasUpdate);
        Assert.Equal("0.3.0", result.LatestVersion);
    }

    [Fact]
    public void ParseRelease_ReturnsFalse_WhenCurrentVersionIsNewer()
    {
        var currentVersion = new Version(0, 4, 0);
        var result = UpdateChecker.ParseRelease(SampleReleaseJson, currentVersion);

        Assert.False(result.HasUpdate);
    }

    [Fact]
    public void ParseRelease_HandlesInvalidJsonGracefully()
    {
        var currentVersion = new Version(0, 2, 0);
        var result = UpdateChecker.ParseRelease("invalid json", currentVersion);

        Assert.False(result.HasUpdate);
        Assert.NotNull(result.ErrorMessage);
    }
}
