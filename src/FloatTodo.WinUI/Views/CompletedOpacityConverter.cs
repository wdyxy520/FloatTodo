using Microsoft.UI.Xaml.Data;

namespace FloatTodo.WinUI.Views;

public sealed class CompletedOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? 0.55 : 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
