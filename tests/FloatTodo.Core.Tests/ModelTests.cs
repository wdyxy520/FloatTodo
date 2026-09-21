using FloatTodo.Core.Models;
using Xunit;

namespace FloatTodo.Core.Tests;

public class ModelTests
{
    [Fact]
    public void TodoItem_DefaultValues_AreCorrect()
    {
        var item = new TodoItem { Text = "Test Todo" };

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Test Todo", item.Text);
        Assert.False(item.IsCompleted);
        Assert.Equal(0, item.Order);
        Assert.True(item.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Snippet_DefaultValues_AreCorrect()
    {
        var snippet = new Snippet { Text = "Test Snippet" };

        Assert.NotEqual(Guid.Empty, snippet.Id);
        Assert.Equal("Test Snippet", snippet.Text);
        Assert.Equal(0, snippet.Order);
        Assert.True(snippet.CreatedAt <= DateTime.UtcNow);
        Assert.True(snippet.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal(320, settings.Window.Width);
        Assert.Equal(560, settings.Window.Height);
        Assert.Equal(DockSide.None, settings.Window.DockSide);
        Assert.Equal(DockState.Floating, settings.Window.DockState);
        Assert.Equal(ViewState.Expanded, settings.Window.ViewState);
        Assert.True(settings.Window.Topmost);
        Assert.True(settings.Window.AutoHide);
        Assert.True(settings.Appearance.UseMica);
        Assert.Equal(AppTheme.System, settings.Appearance.Theme);
    }

    [Theory]
    [InlineData(AppTheme.System)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void AppTheme_ValuesAreDefined(AppTheme theme)
    {
        Assert.True(Enum.IsDefined(typeof(AppTheme), theme));
    }

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

    [Theory]
    [InlineData(DockSide.None)]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    public void DockSide_ValuesAreDefined(DockSide side)
    {
        Assert.True(Enum.IsDefined(typeof(DockSide), side));
    }

    [Theory]
    [InlineData(DockState.Floating)]
    [InlineData(DockState.DockedVisible)]
    [InlineData(DockState.DockedHidden)]
    public void DockState_ValuesAreDefined(DockState state)
    {
        Assert.True(Enum.IsDefined(typeof(DockState), state));
    }

    [Theory]
    [InlineData(ViewState.Expanded)]
    [InlineData(ViewState.Peek)]
    public void ViewState_ValuesAreDefined(ViewState state)
    {
        Assert.True(Enum.IsDefined(typeof(ViewState), state));
    }
}
