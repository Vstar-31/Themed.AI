using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using ThemeManager.WinUI.ViewModels;
using Windows.System;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibePage : Page
{
    public VibeGeneratorViewModel ViewModel { get; }

    private DispatcherTimer? _debounceTimer;
    private const int DebounceMs = 350;

    // Swatch border elements — same 5 from the XAML
    private Border[] SwatchBorders => [Swatch0, Swatch1, Swatch2, Swatch3, Swatch4];
    private TextBlock[] HexLabels  => [HexLabel0, HexLabel1, HexLabel2, HexLabel3, HexLabel4];

    public VibePage()
    {
        InitializeComponent();
        ViewModel = new VibeGeneratorViewModel(App.ThemeService);
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(VibeGeneratorViewModel.HasResult))
            {
                UpdateSwatchStrip();

                // Scroll the result card into view so the user can see swatches + Save/Edit buttons.
                if (ViewModel.HasResult)
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        ResultCard.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
                    });
                }
            }
        };

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            if (!string.IsNullOrWhiteSpace(ViewModel.VibeText))
            {
                var analysis = await Task.Run(
                    () => new ThemeManager.Core.NLP.VibeThemeGenerator()
                              .Explain(ViewModel.VibeText));
                ViewModel.Analysis = analysis;
            }
        };
    }

    // ── Global keyboard shortcut: Ctrl+G anywhere in the app → generate ───────

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Focus the input so the user can start typing immediately.
        VibeInput.Focus(FocusState.Programmatic);
    }

    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource
                       .GetKeyStateForCurrentThread(VirtualKey.Control);
        bool isCtrl = ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (isCtrl && e.Key == VirtualKey.G && ViewModel.CanGenerate)
        {
            e.Handled = true;
            await ViewModel.GenerateAsync();
        }
    }

    // ── Debounced text input (live insight panel) ─────────────────────────────

    private void VibeInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private async void VibeInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.CanGenerate)
        {
            e.Handled = true;
            await ViewModel.GenerateAsync();
        }
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        => await ViewModel.GenerateAsync();

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveGeneratedThemeAsync();
        if (ViewModel.GeneratedTheme != null)
        {
            var safeAccent = ThemeManager.Core.Models.CozyTheme.NormalizeHex(ViewModel.GeneratedTheme.AccentPrimary);
            await App.SystemIntegrator.ApplyAccentColorAsync(safeAccent);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GeneratedTheme is not null)
            Frame.Navigate(typeof(ThemeEditorPage), ViewModel.GeneratedTheme);
    }

    private async void RegenerateButton_Click(object sender, RoutedEventArgs e)
        => await ViewModel.RegenerateAsync();

    private void ResetButton_Click(object sender, RoutedEventArgs e)
        => ViewModel.Reset();

    private async void Chip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string chip)
            await ViewModel.UseChipAsync(chip);
    }

    private async void TrySuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        var chips = ViewModel.SuggestionChips;
        await ViewModel.UseChipAsync(chips[Random.Shared.Next(chips.Count)]);
    }

    // ── Swatch strip: color + stagger animation + click-to-copy ──────────────

    private void UpdateSwatchStrip()
    {
        var swatches = ViewModel.PreviewSwatches;
        var borders  = SwatchBorders;
        var labels   = HexLabels;

        for (int i = 0; i < borders.Length; i++)
        {
            if (i < swatches.Count)
            {
                var hex    = swatches[i];
                var border = borders[i];
                var label  = labels[i];

                border.Background = new SolidColorBrush(App.HexToColor(hex));
                label.Text        = hex;

                // Store hex for click-to-copy via Tag.
                border.Tag = hex;

                // Stagger animation: each swatch fades in 80 ms after the previous.
                AnimateSwatchIn(border, delayMs: i * 80);
            }
            else
            {
                borders[i].Background =
                    new SolidColorBrush(App.HexToColor("#E0D5C7"));
                labels[i].Text = string.Empty;
                borders[i].Tag = null;
            }
        }
    }

    /// <summary>Fade + scale-up stagger animation for each swatch reveal.</summary>
    private static void AnimateSwatchIn(Border border, int delayMs)
    {
        border.Opacity = 0;

        var storyboard  = new Storyboard();
        var fadeAnim    = new DoubleAnimation
        {
            From           = 0,
            To             = 1,
            Duration       = TimeSpan.FromMilliseconds(220),
            BeginTime      = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fadeAnim, border);
        Storyboard.SetTargetProperty(fadeAnim, "Opacity");
        storyboard.Children.Add(fadeAnim);
        storyboard.Begin();
    }

    // ── Swatch click → copy hex to clipboard ─────────────────────────────────

    private void Swatch_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as Border)?.Tag is string hex)
            ViewModel.CopyHexToClipboard(hex);
    }
}
