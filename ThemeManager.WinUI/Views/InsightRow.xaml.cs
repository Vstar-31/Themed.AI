using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ThemeManager.WinUI.Views;

public sealed partial class InsightRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string),
            typeof(InsightRow), new PropertyMetadata(string.Empty,
                (d, e) => ((InsightRow)d).LabelText.Text = e.NewValue as string ?? string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string),
            typeof(InsightRow), new PropertyMetadata("—",
                (d, e) => ((InsightRow)d).ValueText.Text = e.NewValue as string ?? "—"));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public InsightRow() => InitializeComponent();
}
