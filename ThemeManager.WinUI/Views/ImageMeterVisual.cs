using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Views;

/// <summary>The editor and desktop host use the same image sizing and missing-file fallback.</summary>
internal static class ImageMeterVisual
{
    public static FrameworkElement Create(MeterDefinition meter)
    {
        var frame = new Grid { Width = meter.Width, Height = meter.Height };
        var placeholder = new TextBlock
        {
            Text = "Choose an image",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
        };
        frame.Children.Add(placeholder);
        if (string.IsNullOrWhiteSpace(meter.ImagePath)) return frame;
        placeholder.Text = "Image unavailable";
        if (!System.IO.Path.IsPathFullyQualified(meter.ImagePath)
            || !SkinPackageService.IsSupportedImage(meter.ImagePath)
            || !System.IO.File.Exists(meter.ImagePath)) return frame;
        try
        {
            var image = new Image { Width = meter.Width, Height = meter.Height, Stretch = Stretch.Uniform };
            image.ImageOpened += (_, _) => placeholder.Visibility = Visibility.Collapsed;
            image.ImageFailed += (_, _) => placeholder.Visibility = Visibility.Visible;
            var bitmap = new BitmapImage
            {
                DecodePixelWidth = (int)Math.Clamp(Math.Ceiling(meter.Width * 2), 1, 4096),
            };
            bitmap.UriSource = new Uri(meter.ImagePath);
            image.Source = bitmap;
            frame.Children.Add(image);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // A missing/undecodable image should not take down its widget or the editor.
        }
        return frame;
    }
}
