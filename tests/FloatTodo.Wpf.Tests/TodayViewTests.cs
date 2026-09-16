using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FloatTodo.Core.Models;
using FloatTodo.Core.Services;
using FloatTodo.Wpf.Controls;
using Xunit;

namespace FloatTodo.Wpf.Tests;

public sealed class TodayViewTests
{
    [Fact]
    public void Today_KeyboardAndCardActions_PersistAndRefresh()
    {
        RunSta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "FloatTodoTests", Guid.NewGuid().ToString("N"));
            try
            {
                var path = Path.Combine(directory, "todos.json");
                var service = new TodoService(path);
                var view = new TodayView(service);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var input = (TextBox)view.FindName("QuickInput");
                var list = (ItemsControl)view.FindName("TaskList");
                input.Text = "  First  ";
                PressKey(input, Key.Enter);
                Assert.Equal("", input.Text);
                Assert.Equal("First", Assert.Single(service.GetItems()).Text);

                input.Text = "Discard";
                PressKey(input, Key.Escape);
                Assert.Empty(input.Text);
                Assert.Single(service.GetItems());
                input.Text = "Second";
                PressKey(input, Key.Enter);
                Layout(view);

                Button Action(ItemsControl items, string hint) => Descendants<Button>(items).First(button => (string)button.ToolTip == hint);
                void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                TextBox Editor(ItemsControl items) => Descendants<TextBox>(items).First(box => box.Name == "InlineEditor" && box.Visibility == Visibility.Visible);


                Click(Action(list, "编辑任务"));
                Layout(view);
                var editor = Editor(list);
                Assert.Equal("First", editor.Text);
                Assert.Empty(input.Text);
                editor.SetCurrentValue(TextBox.TextProperty, "Cancelled edit");
                PressKey(editor, Key.Escape);
                Assert.Equal("First", service.GetItems()[0].Text);
                Click(Action(list, "编辑任务"));
                Layout(view);
                editor = Editor(list);
                editor.SetCurrentValue(TextBox.TextProperty, "Edited");
                Assert.Equal("Edited", ((EntryRow)editor.DataContext).Draft);
                PressKey(editor, Key.Enter);
                Layout(view);
                Assert.Equal("Edited", new TodoService(path).GetItems()[0].Text);

                // Clicking row text completes exactly once, while editor/button interactions do not.
                var text = Descendants<TextBlock>(list).First(block => block.Name == "EntryText");
                text.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = Mouse.MouseUpEvent });
                PumpDispatcher();
                Layout(view);
                Assert.Equal(new[] { "Second", "Edited" }, list.Items.Cast<EntryRow>().Select(item => item.Text));
                Assert.True(new TodoService(path).GetItems()[1].IsCompleted);

                var restore = Descendants<CheckBox>(list).Last();
                restore.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                restore.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Layout(view);
                Assert.Equal("Edited", ((EntryRow)list.Items[0]).Text);
                Assert.False(service.GetItems()[0].IsCompleted);

                Click(Action(list, "编辑任务"));
                Layout(view);
                editor = Editor(list);
                editor.SetCurrentValue(TextBox.TextProperty, "Draft survives other additions");
                input.Text = "Third";
                PressKey(input, Key.Enter);
                Assert.Equal("Draft survives other additions", Editor(list).Text);
                editor.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = Mouse.MouseUpEvent });
                Assert.False(service.GetItems()[0].IsCompleted);
                PressKey(editor, Key.Escape);
                Layout(view);
                Click(Action(list, "转为笔记"));
                Layout(view);
                Assert.Equal(2, service.GetItems().Count);
                Assert.Equal("Edited", Assert.Single(service.GetNotes()).Text);
                var notes = (ItemsControl)view.FindName("NoteList");
                var noteInput = (TextBox)view.FindName("NoteInput");
                noteInput.Text = "Note line 1\nNote line 2";
                PressKey(noteInput, Key.Enter);
                Layout(view);
                Assert.Empty(noteInput.Text);
                Assert.Equal(2, service.GetNotes().Count);
                Click(Action(notes, "编辑笔记"));
                Layout(view);
                var noteEditor = Editor(notes);
                noteEditor.SetCurrentValue(TextBox.TextProperty, "笔记修改\n多行内容");
                RenderPreview(view);
                PressKey(noteEditor, Key.Enter);
                Layout(view);
                Assert.Equal("笔记修改\n多行内容", service.GetNotes()[0].Text);
                Click(Action(notes, "转为待办"));
                Layout(view);
                Assert.Equal("笔记修改\n多行内容", service.GetItems().Last().Text);
                Assert.False(service.GetItems().Last().IsCompleted);
                Click(Action(notes, "删除笔记"));
                Assert.Empty(service.GetNotes());
                Click(Action(list, "删除任务"));
                Layout(view);
                Assert.Equal(2, new TodoService(path).GetItems().Count);
                Directory.CreateDirectory(path + ".tmp");
                input.Text = "Retain failed entry";
                PressKey(input, Key.Enter);
                Assert.Equal("Retain failed entry", input.Text);
                Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("ErrorText")).Visibility);
                Assert.Equal(2, service.GetItems().Count);
                Click(Action(list, "编辑任务"));
                Layout(view);
                editor = Editor(list);
                editor.SetCurrentValue(TextBox.TextProperty, "Keep failed inline draft");
                PressKey(editor, Key.Enter);
                Assert.True(((EntryRow)editor.DataContext).IsEditing);
                Assert.Equal("Keep failed inline draft", editor.Text);
                Assert.Equal("Third", service.GetItems()[0].Text);
                PressKey(editor, Key.Escape);
                Click(Action(list, "转为笔记"));
                Assert.Equal(2, service.GetItems().Count);
                Assert.Empty(service.GetNotes());
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(230, 700));
        view.Arrange(new Rect(0, 0, 230, 700));
        view.UpdateLayout();
    }

    private static void RenderPreview(FrameworkElement view)
    {
        var directory = Environment.GetEnvironmentVariable("FLOATTODO_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        {
            FloatTodo.Wpf.Themes.ThemeHelper.ApplyTheme(view.Resources, theme, false);
            var host = new Border
            {
                Width = 260, Background = (Brush)view.Resources["WindowBackgroundBrush"], Padding = new Thickness(10),
                Child = view
            };
            host.Measure(new Size(260, double.PositiveInfinity));
            host.Arrange(new Rect(new Point(), host.DesiredSize));
            host.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(260, (int)Math.Ceiling(host.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, $"compact-{theme}.png"));
            encoder.Save(stream);
            host.Child = null;
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void PressKey(UIElement element, Key key) => element.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, new TestSource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                action();
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF interaction test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TestSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}





