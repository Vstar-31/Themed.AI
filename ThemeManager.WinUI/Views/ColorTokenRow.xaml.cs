using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// A self-contained row control for editing a single palette hex token.
///
/// Features:
///   • Colored swatch button → opens a ColorPicker flyout.
///   • Hex TextBox with validation (accepts #RRGGBB).
///   • Reset button to restore the default hex value.
///   • <see cref="ColorChanged"/> event for parent notification.
///   • <see cref="HexValue"/> DependencyProperty — bind to the ViewModel property.
/// </summary>
public sealed partial class ColorTokenRow : UserControl
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string),
            typeof(ColorTokenRow), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty HexValueProperty =
        DependencyProperty.Register(nameof(HexValue), typeof(string),
            typeof(ColorTokenRow), new PropertyMetadata("#000000", OnHexValueChanged));

    public static readonly DependencyProperty DefaultHexProperty =
        DependencyProperty.Register(nameof(DefaultHex), typeof(string),
            typeof(ColorTokenRow), new PropertyMetadata("#000000"));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// The hex color string (#RRGGBB). Two-way bind this to the ViewModel property.
    /// </summary>
    public string HexValue
    {
        get => (string)GetValue(HexValueProperty);
        set => SetValue(HexValueProperty, value);
    }

    /// <summary>The factory-default hex used by the reset button.</summary>
    public string DefaultHex
    {
        get => (string)GetValue(DefaultHexProperty);
        set => SetValue(DefaultHexProperty, value);
    }

    // ── Event ─────────────────────────────────────────────────────────────────
    /// <summary>Fires after the user confirms a valid color change.</summary>
    public event EventHandler<string>? ColorChanged;

    // ── Guard flags to prevent circular updates ───────────────────────────────
    private bool _updatingFromCode;

    public ColorTokenRow()
    {
        InitializeComponent();
    }

    // ── Property change callbacks ─────────────────────────────────────────────

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorTokenRow row)
            row.LabelText.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnHexValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorTokenRow row && e.NewValue is string hex)
            row.SyncFromHex(hex);
    }

    // ── Internal sync ─────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the swatch, TextBox, and ColorPicker to reflect the given hex string.
    /// Skips re-entrant calls caused by the picker itself changing HexValue.
    /// </summary>
    private void SyncFromHex(string hex)
    {
        if (_updatingFromCode) return;
        _updatingFromCode = true;
        try
        {
            if (!TryParseHex(hex, out var color)) return;

            // Swatch background
            SwatchButton.Background = new SolidColorBrush(color);

            // Hex TextBox
            if (HexBox.Text != hex)
                HexBox.Text = hex;

            // ColorPicker (only if flyout is open to avoid unnecessary work)
            if (PickerFlyout.IsOpen)
                InlinePicker.Color = color;
        }
        finally
        {
            _updatingFromCode = false;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        // Seed the picker with the current color before opening.
        if (TryParseHex(HexValue, out var c))
            InlinePicker.Color = c;
    }

    private void InlinePicker_ColorChanged(
        ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_updatingFromCode) return;
        var hex = ColorToHex(args.NewColor);
        PushChange(hex);
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingFromCode) return;
        var raw = HexBox.Text?.Trim() ?? string.Empty;
        if (!raw.StartsWith('#')) raw = "#" + raw;
        if (TryParseHex(raw, out _))
            PushChange(raw.ToUpperInvariant());
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        PushChange(DefaultHex);
    }

    /// <summary>
    /// Commits a validated hex value: updates HexValue DP, swatch, and fires the event.
    /// </summary>
    private void PushChange(string hex)
    {
        _updatingFromCode = true;
        try
        {
            HexValue = hex;
            if (TryParseHex(hex, out var color))
                SwatchButton.Background = new SolidColorBrush(color);
            if (HexBox.Text != hex)
                HexBox.Text = hex;
        }
        finally
        {
            _updatingFromCode = false;
        }
        ColorChanged?.Invoke(this, hex);
    }

    // ── Color parsing helpers ─────────────────────────────────────────────────

    private static bool TryParseHex(string hex, out Color color)
    {
        color = Colors.Black;
        try
        {
            hex = ThemeManager.Core.Models.CozyTheme.NormalizeHex(hex ?? string.Empty).TrimStart('#');
            if (hex.Length == 6)
            {
                color = Color.FromArgb(0xFF,
                    Convert.ToByte(hex[0..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
                return true;
            }
        }
        catch { }
        return false;
    }

    private static string ColorToHex(Color c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
