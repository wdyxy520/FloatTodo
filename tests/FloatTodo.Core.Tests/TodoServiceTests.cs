using System.Text.Json;
using FloatTodo.Core.Services;
using Xunit;

namespace FloatTodo.Core.Tests;

public sealed class TodoServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FloatTodoTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "todos.json");

    [Fact]
    public void Crud_PersistsAcrossServiceInstances()
    {
        var service = new TodoService(FilePath);
        Assert.Empty(service.GetItems());
        service.Add("  第一项任务  ");
        var created = Assert.Single(new TodoService(FilePath).GetItems());
        Assert.Equal("第一项任务", created.Text);
        service.Edit(created.Id, "修改后的任务");
        service.SetCompleted(created.Id, true);
        var edited = Assert.Single(new TodoService(FilePath).GetItems());
        Assert.Equal(created.Id, edited.Id);
        Assert.Equal(created.CreatedAt, edited.CreatedAt);
        Assert.Equal("修改后的任务", edited.Text);
        Assert.True(edited.IsCompleted);
        service.Delete(created.Id);
        Assert.Empty(new TodoService(FilePath).GetItems());
    }

    [Fact]
    public void CompletedItemsSink_AndReopeningRestoresOriginalOrder()
    {
        var service = new TodoService(FilePath);
        service.Add("A");
        service.Add("B");
        var first = service.GetItems()[0];
        service.SetCompleted(first.Id, true);
        service.Add("C");
        Assert.Equal(new[] { "B", "C", "A" }, new TodoService(FilePath).GetItems().Select(item => item.Text));
        service.SetCompleted(first.Id, false);
        Assert.Equal(new[] { "A", "B", "C" }, service.GetItems().Select(item => item.Text));
    }

    [Fact]
    public void BlankInputAndMissingIds_DoNotChangeSavedData()
    {
        var service = new TodoService(FilePath);
        service.Add("Keep");
        var before = File.ReadAllText(FilePath);
        Assert.Throws<ArgumentException>(() => service.Add(" \t "));
        Assert.Throws<ArgumentException>(() => service.Edit(service.GetItems()[0].Id, " "));
        Assert.Throws<KeyNotFoundException>(() => service.Delete(Guid.NewGuid()));
        Assert.Equal(before, File.ReadAllText(FilePath));
    }

    [Fact]
    public void FailedWrite_DoesNotPublishMutationOrLoseExistingData()
    {
        var service = new TodoService(FilePath);
        service.Add("Keep");
        var id = service.GetItems()[0].Id;
        var before = File.ReadAllText(FilePath);
        Directory.CreateDirectory(FilePath + ".tmp");
        var exception = Record.Exception(() => service.Delete(id));
        Assert.True(exception is IOException or UnauthorizedAccessException);
        Assert.Single(service.GetItems());
        Assert.Equal(before, File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"Text\":\" \"}]")]
    public void CorruptData_IsReportedAndPreserved(string contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, contents);
        var exception = Record.Exception(() => new TodoService(FilePath));
        Assert.True(exception is JsonException or InvalidDataException);
        Assert.Equal(contents, File.ReadAllText(FilePath));
    }

    [Fact]
    public void ReturnedSnapshots_CannotMutateServiceState()
    {
        var service = new TodoService(FilePath);
        service.Add("Keep");
        service.GetItems()[0].Text = "Unsaved change";
        Assert.Equal("Keep", service.GetItems()[0].Text);
    }

    [Fact]
    public void NotesCrudAndConversions_PersistContentIdentityAndCreationTime()
    {
        var service = new TodoService(FilePath);
        service.AddNote("first line\nsecond line");
        var note = Assert.Single(service.GetNotes());
        service.EditNote(note.Id, "changed\n保留换行");
        var edited = Assert.Single(new TodoService(FilePath).GetNotes());
        Assert.True(edited.UpdatedAt >= note.UpdatedAt);
        service.ConvertToTodo(note.Id);
        var task = Assert.Single(new TodoService(FilePath).GetItems());
        Assert.Empty(new TodoService(FilePath).GetNotes());
        Assert.Equal(note.Id, task.Id);
        Assert.Equal(note.CreatedAt, task.CreatedAt);
        Assert.Equal(edited.Text, task.Text);
        Assert.False(task.IsCompleted);
        service.SetCompleted(task.Id, true);
        service.ConvertToNote(task.Id);
        Assert.Empty(new TodoService(FilePath).GetItems());
        Assert.Equal(edited.Text, Assert.Single(new TodoService(FilePath).GetNotes()).Text);
        service.ConvertToTodo(task.Id);
        Assert.False(Assert.Single(service.GetItems()).IsCompleted);
        service.ConvertToNote(task.Id);
        service.DeleteNote(task.Id);
        Assert.Empty(new TodoService(FilePath).GetNotes());
    }

    [Fact]
    public void LegacyArray_UpgradesOnlyOnSuccessfulSave()
    {
        Directory.CreateDirectory(_directory);
        var item = new FloatTodo.Core.Models.TodoItem { Text = "Legacy", IsCompleted = true, Order = 7 };
        string legacy = JsonSerializer.Serialize(new[] { item });
        File.WriteAllText(FilePath, legacy);
        var service = new TodoService(FilePath);
        Assert.Equal(legacy, File.ReadAllText(FilePath));
        service.AddNote("New note");
        var reloaded = new TodoService(FilePath);
        Assert.Equal(item.Id, Assert.Single(reloaded.GetItems()).Id);
        Assert.True(reloaded.GetItems()[0].IsCompleted);
        Assert.Single(reloaded.GetNotes());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedConversion_LeavesBothCollectionsAndDiskIntact(bool fromNote)
    {
        var service = new TodoService(FilePath);
        service.Add("Task");
        service.AddNote("Note");
        string before = File.ReadAllText(FilePath);
        Directory.CreateDirectory(FilePath + ".tmp");
        var error = Record.Exception(() =>
        {
            if (fromNote) service.ConvertToTodo(service.GetNotes()[0].Id);
            else service.ConvertToNote(service.GetItems()[0].Id);
        });
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("Task", Assert.Single(service.GetItems()).Text);
        Assert.Equal("Note", Assert.Single(service.GetNotes()).Text);
        Assert.Equal(before, File.ReadAllText(FilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
