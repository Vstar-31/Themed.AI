using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ThemeManager.WinUI.Views;

// ── HexToBrushConverter ──────────────────────────────────────────────────────
/// <summary>
/// Converts a "#RRGGBB" hex string to a <see cref="SolidColorBrush"/>.
/// Used in the live preview panel to bind ViewModel hex strings directly to
/// Background / Foreground properties.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex)
        {
            try { return new SolidColorBrush(App.HexToColor(hex)); }
            catch { }
        }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x7D, 0x5A, 0x44));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is SolidColorBrush b)
            return App.ColorToHex(b.Color);
        throw new NotSupportedException();
    }
}

// ── BoolToVisibilityConverter ────────────────────────────────────────────────
/// <summary>true → Visible, false → Collapsed</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

// ── InverseBoolConverter ─────────────────────────────────────────────────────
/// <summary>true → false, false → true. Used for IsBuiltIn → Delete IsEnabled.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is false || value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is false || value is not true;
}

// ── DoubleToStringConverter ───────────────────────────────────────────────────
/// <summary>Formats a double to two decimal places for Slider labels.</summary>
public sealed class DoubleToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is double d ? d.ToString("0.00") : "1.00";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => double.TryParse(value?.ToString(), out var d) ? d : 1.0;
}
