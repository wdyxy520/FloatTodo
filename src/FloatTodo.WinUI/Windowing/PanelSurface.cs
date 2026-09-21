using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace FloatTodo.WinUI.Windowing;

public sealed class PanelSurface : Grid
{
    private readonly Dictionary<InputSystemCursorShape, InputCursor> _cursors = [];
    public PanelSurface()
    {
        PointerMoved += UpdateCursor;
        ProtectedCursor = Cursor(InputSystemCursorShape.Arrow);
        PointerExited += (_, _) => ProtectedCursor = Cursor(InputSystemCursorShape.Arrow);
    }
    private InputCursor Cursor(InputSystemCursorShape shape)
    {
        if (!_cursors.TryGetValue(shape, out var cursor)) _cursors[shape] = cursor = InputSystemCursor.Create(shape);
        return cursor;
    }
    private void UpdateCursor(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this).Position;
        var left = p.X < 6; var right = p.X > ActualWidth - 6;
        var top = p.Y < 6; var bottom = p.Y > ActualHeight - 6;
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


