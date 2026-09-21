using Microsoft.UI.Xaml.Data;
namespace FloatTodo.WinUI.Views;

public sealed class VisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value switch { bool b => b, string s => !string.IsNullOrEmpty(s), _ => value is not null };
        return (isVisible != (parameter as string == "Invert")) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
