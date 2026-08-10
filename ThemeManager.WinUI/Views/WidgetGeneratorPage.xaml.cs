using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class WidgetGeneratorPage : Page
{
    public WidgetGeneratorViewModel ViewModel { get; }

    public WidgetGeneratorPage()
    {
        InitializeComponent();
        ViewModel = new WidgetGeneratorViewModel(App.SkinManager);
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.GenerateAsync();

    private async void Chip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string chip)
            await ViewModel.UseChipAsync(chip);
    }

    private async void OpenEditorButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = await ViewModel.AcceptAndOpenEditorAsync();
        if (skin is not null)
            Frame.Navigate(typeof(SkinEditorPage), skin);
    }
}
