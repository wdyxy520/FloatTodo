using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace FloatTodo.WinUI.Windowing;

public sealed class PanelSurface : Grid
{
    public const double ResizeHandleThickness = 8.0;
    private readonly Dictionary<InputSystemCursorShape, InputCursor> _cursors = [];
    public bool IsCursorLocked { get; set; }

    public PanelSurface()
    {
        PointerMoved += UpdateCursor;
        ProtectedCursor = Cursor(InputSystemCursorShape.Arrow);
        PointerExited += (_, _) =>
        {
            if (!IsCursorLocked) ProtectedCursor = Cursor(InputSystemCursorShape.Arrow);
        };
    }
    private InputCursor Cursor(InputSystemCursorShape shape)
    {
        if (!_cursors.TryGetValue(shape, out var cursor)) _cursors[shape] = cursor = InputSystemCursor.Create(shape);
        return cursor;
    }
    public void ResetCursor()
    {
        ProtectedCursor = Cursor(InputSystemCursorShape.Arrow);
    }
    private void UpdateCursor(object sender, PointerRoutedEventArgs e)
    {
        if (IsCursorLocked) return;
        var p = e.GetCurrentPoint(this).Position;
        var border = ResizeHandleThickness;
        var left = p.X < border; var right = p.X > ActualWidth - border;
        var top = p.Y < border; var bottom = p.Y > ActualHeight - border;
        // Child text editors retain their text cursor inside the panel.
        var target = left && top || right && bottom ? Cursor(InputSystemCursorShape.SizeNorthwestSoutheast)
            : right && top || left && bottom ? Cursor(InputSystemCursorShape.SizeNortheastSouthwest)
            : left || right ? Cursor(InputSystemCursorShape.SizeWestEast)
            : top || bottom ? Cursor(InputSystemCursorShape.SizeNorthSouth) : Cursor(InputSystemCursorShape.Arrow);
        if (ProtectedCursor != target)
        {
            ProtectedCursor = target;
        }
    }
}


