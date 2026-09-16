using System.Windows;
using System.Windows.Media.Animation;

namespace FloatTodo.Wpf.Interop;

public static class WindowHelper
{
    public static readonly DependencyProperty WindowLeftProperty = DependencyProperty.RegisterAttached(
        "WindowLeft",
        typeof(double),
        typeof(WindowHelper),
        new PropertyMetadata(0.0, OnWindowLeftChanged));

    public static double GetWindowLeft(DependencyObject obj) => (double)obj.GetValue(WindowLeftProperty);
    public static void SetWindowLeft(DependencyObject obj, double value) => obj.SetValue(WindowLeftProperty, value);

    private static void OnWindowLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window && !double.IsNaN((double)e.NewValue))
        {
            window.Left = (double)e.NewValue;
        }
    }

    public static readonly DependencyProperty WindowTopProperty = DependencyProperty.RegisterAttached(
        "WindowTop",
        typeof(double),
        typeof(WindowHelper),
        new PropertyMetadata(0.0, OnWindowTopChanged));

    public static double GetWindowTop(DependencyObject obj) => (double)obj.GetValue(WindowTopProperty);
    public static void SetWindowTop(DependencyObject obj, double value) => obj.SetValue(WindowTopProperty, value);

    private static void OnWindowTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window && !double.IsNaN((double)e.NewValue))
        {
            window.Top = (double)e.NewValue;
        }
    }

    public static void AnimateLeft(Window window, double toLeft, double durationMs = 200, Action? onCompleted = null)
    {
        SetWindowLeft(window, window.Left);

        var animation = new DoubleAnimation
        {
            From = window.Left,
            To = toLeft,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (onCompleted != null)
        {
            animation.Completed += (_, _) => onCompleted();
        }

        window.BeginAnimation(WindowLeftProperty, animation);
    }
}
