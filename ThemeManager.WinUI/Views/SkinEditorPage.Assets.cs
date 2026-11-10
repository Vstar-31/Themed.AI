using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ThemeManager.Core.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private readonly WidgetAssetLibrary _assets = new();
    private bool _assetPickerOpen;

    private async Task<string?> ImportImageAsync(CancellationToken token)
    {
        var picker = new FileOpenPicker();
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico" }) picker.FileTypeFilter.Add(extension);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        token.ThrowIfCancellationRequested();
        var source = WidgetAssetLibrary.Inspect(file.Path);
        if (!source.Available) throw new InvalidDataException(source.Status);
        await ValidateImageAsync(file);
        token.ThrowIfCancellationRequested();
        var imported = await _assets.ImportAsync(file.Path, token);
        await ValidateImageAsync(await StorageFile.GetFileFromPathAsync(imported.Path));
        token.ThrowIfCancellationRequested();
        return imported.Path;
    }

    private static async Task ValidateImageAsync(StorageFile file)
    {
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 || decoder.PixelWidth > 16384 || decoder.PixelHeight > 16384 ||
            (long)decoder.PixelWidth * decoder.PixelHeight > 32_000_000)
            throw new InvalidDataException("Choose an image up to 32 megapixels and 16,384 pixels per side.");
    }

    private async void ChooseImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_assetPickerOpen || ViewModel.SelectedMeter is not { IsImage: true } meter) return;
        _assetPickerOpen = true;
        try
        {
            var path = await ImportImageAsync(CancellationToken.None);
            if (path is not null && IsLoaded) ViewModel.ApplyImageAsset(path, meter.Definition.Id);
        }
        catch (Exception ex) when (IsAssetError(ex)) { ViewModel.StatusMessage = AssetError(ex); }
        finally { _assetPickerOpen = false; }
    }

    private static bool IsAssetError(Exception ex) => ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException
        or NotSupportedException or System.Runtime.InteropServices.COMException;
    private static string AssetError(Exception ex) => ex is InvalidDataException ? ex.Message :
        "Couldn't open this image. Check that the file is readable, supported, and that storage is available, then try again.";

    private async void BrowseAssets_Click(object sender, RoutedEventArgs args)
    {
        if (_assetPickerOpen) return;
        _assetPickerOpen = true;
        using var cancellation = new CancellationTokenSource();
        string? targetId = ViewModel.SelectedMeter is { IsImage: true } meter ? meter.Definition.Id : null;
        var rows = new ObservableCollection<AssetBrowserRow>();
        var all = new List<AssetBrowserRow>();
        var search = new TextBox { PlaceholderText = "Search images by filename", HorizontalAlignment = HorizontalAlignment.Stretch };
        var refresh = new Button { Content = "Refresh" };
        var message = new TextBlock { Text = "Loading images…", TextWrapping = TextWrapping.Wrap };
        var details = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = 3 };
        var list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Single, Height = 320 };
        // Virtualized rows decode small thumbnails only when their template requests the source.
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid Padding="4,6" ColumnSpacing="12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="96"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <Border Width="96" Height="64" CornerRadius="6" Background="{ThemeResource SubtleFillColorSecondaryBrush}">
                  <Image Source="{Binding Thumbnail}" Stretch="Uniform" AutomationProperties.Name="Image preview"/>
                </Border>
                <StackPanel Grid.Column="1" Spacing="4" VerticalAlignment="Center">
                  <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis"/>
                  <TextBlock Text="{Binding Status}" TextWrapping="Wrap" Opacity="0.7" FontSize="12"/>
                </StackPanel>
              </Grid>
            </DataTemplate>
            """);
        var top = new Grid { ColumnSpacing = 8 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(search); Grid.SetColumn(refresh, 1); top.Children.Add(refresh);
        var content = new StackPanel { Spacing = 10, MinWidth = 260, MaxWidth = 620 };
        content.Children.Add(new TextBlock { Text = "Import an image once and reuse it in your widgets. Missing references appear first.", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(top); content.Children.Add(message); content.Children.Add(list); content.Children.Add(details);
        var dialog = new ContentDialog
        {
            Title = "Image assets", Content = content, XamlRoot = XamlRoot,
            PrimaryButtonText = targetId is null ? "Add image meter" : "Use image",
            SecondaryButtonText = "Import image…", CloseButtonText = "Close",
            IsPrimaryButtonEnabled = false, DefaultButton = ContentDialogButton.Close
        };
        bool open = true, busy = false, truncated = false;
        string? importedReference = null;
        void SelectionChanged()
        {
            var selected = list.SelectedItem as AssetBrowserRow;
            dialog.IsPrimaryButtonEnabled = !busy && selected?.Available == true;
            details.Text = selected is null ? "Select an image to preview and use it." : selected.Path;
        }
        void Filter()
        {
            var selectedPath = (list.SelectedItem as AssetBrowserRow)?.Path;
            rows.Clear();
            foreach (var row in all.Where(r => r.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase))) rows.Add(row);
            list.SelectedItem = rows.FirstOrDefault(r => r.Path == selectedPath);
            message.Text = all.Count == 0 ? "No images yet. Import an image to get started." :
                $"{rows.Count} image(s)" + (truncated ? " · Showing a limited inventory; current widget references are included first." : "");
            if (all.Count > 0 && rows.Count == 0) message.Text = "No matching images. Try a different search.";
            SelectionChanged();
        }
        async Task Reload()
        {
            var references = ViewModel.Meters.Where(m => !string.IsNullOrWhiteSpace(m.ImagePath)).Select(m => m.ImagePath);
            if (importedReference is not null) references = references.Prepend(importedReference);
            var inventory = await _assets.ListAsync(references, cancellation.Token);
            if (!open) return;
            truncated = inventory.Truncated;
            all = inventory.Items.Select(asset => new AssetBrowserRow(asset, SelectionChanged)).ToList();
            Filter();
        }
        void Busy(bool value)
        {
            busy = value; refresh.IsEnabled = !value; dialog.IsSecondaryButtonEnabled = !value; SelectionChanged();
        }
        async Task RefreshSafely()
        {
            if (busy) return;
            Busy(true);
            try { await Reload(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (IsAssetError(ex)) { message.Text = AssetError(ex); }
            finally { Busy(false); }
        }
        search.TextChanged += (_, _) => Filter();
        list.SelectionChanged += (_, _) => SelectionChanged();
        refresh.Click += async (_, _) => await RefreshSafely();
        dialog.Opened += async (_, _) => await RefreshSafely();
        dialog.Closed += (_, _) => { open = false; cancellation.Cancel(); };
        dialog.SecondaryButtonClick += async (_, e) =>
        {
            e.Cancel = true;
            if (busy) return;
            var deferral = e.GetDeferral(); Busy(true);
            try
            {
                var importedPath = await ImportImageAsync(cancellation.Token);
                if (importedPath is null || !open) return;
                importedReference = importedPath;
                await Reload();
                search.Text = "";
                list.SelectedItem = rows.FirstOrDefault(r => r.Path == importedPath);
                message.Text = "Imported into your asset library. Select Use image or Add image meter to apply it.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (IsAssetError(ex)) { message.Text = AssetError(ex); }
            finally { Busy(false); deferral.Complete(); }
        };
        dialog.PrimaryButtonClick += async (_, e) =>
        {
            var deferral = e.GetDeferral(); Busy(true);
            try
            {
                if (list.SelectedItem is not AssetBrowserRow selected) { e.Cancel = true; return; }
                var current = WidgetAssetLibrary.Inspect(selected.Path);
                if (!current.Available) throw new InvalidDataException(current.Status);
                await ValidateImageAsync(await StorageFile.GetFileFromPathAsync(selected.Path));
                e.Cancel = !ViewModel.ApplyImageAsset(selected.Path, targetId);
                if (e.Cancel) message.Text = ViewModel.StatusMessage;
            }
            catch (Exception ex) when (IsAssetError(ex)) { e.Cancel = true; message.Text = AssetError(ex); }
            finally { Busy(false); deferral.Complete(); }
        };
        try { await dialog.ShowAsync(); }
        catch (Exception ex) when (IsAssetError(ex)) { ViewModel.StatusMessage = AssetError(ex); }
        finally { open = false; cancellation.Cancel(); _assetPickerOpen = false; }
    }
}

public sealed class AssetBrowserRow : INotifyPropertyChanged
{
    private readonly Action _changed;
    private BitmapImage? _thumbnail;
    public AssetBrowserRow(WidgetAsset asset, Action changed)
    {
        Path = asset.Path; Name = asset.Name; Status = asset.Status; Available = asset.Available; _changed = changed;
    }
    public string Path { get; }
    public string Name { get; }
    public string Status { get; private set; }
    public bool Available { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ImageSource? Thumbnail
    {
        get
        {
            if (!Available) return null;
            if (_thumbnail is not null) return _thumbnail;
            try
            {
                _thumbnail = new BitmapImage { DecodePixelWidth = 192, DecodePixelHeight = 128 };
                _thumbnail.ImageFailed += (_, _) => Failed();
                _thumbnail.UriSource = new Uri(Path);
                return _thumbnail;
            }
            catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException) { Failed(); return null; }
        }
    }
    private void Failed()
    {
        Available = false; Status = "Preview unavailable — refresh or import a replacement";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        _changed();
    }
}
