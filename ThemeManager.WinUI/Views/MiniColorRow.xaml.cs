using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// Tiny read-only row showing a colour swatch + label + hex.
/// Used in the VibePage result card to display all 8 generated tokens.
/// </summary>
public sealed partial class MiniColorRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string),
            typeof(MiniColorRow), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty HexProperty =
        DependencyProperty.Register(nameof(Hex), typeof(string),
            typeof(MiniColorRow), new PropertyMetadata("#000000", OnHexChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Hex
    {
        get => (string)GetValue(HexProperty);
        set => SetValue(HexProperty, value);
    }
    
    public MiniColorRow() => InitializeComponent();

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniColorRow r)
            r.LabelText.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniColorRow r && e.NewValue is string hex)
        {
            r.HexText.Text = hex;
            try { r.SwatchBorder.Background = new SolidColorBrush(App.HexToColor(hex)); }
            catch { /* ignore bad hex during rapid binding */ }
        }
    }
}
