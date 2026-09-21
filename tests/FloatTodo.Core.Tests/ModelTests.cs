using FloatTodo.Core.Models;
using Xunit;

namespace FloatTodo.Core.Tests;

public class ModelTests
{
    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.System)]
    public void AppSettings_ThemeSerialization_PreservesValue(AppTheme expectedTheme)
    {
        var original = new AppSettings
        {
            Appearance = new AppearanceSettings { Theme = expectedTheme }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(expectedTheme, deserialized.Appearance.Theme);
    }
}
