using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

// Shared static samples for authoring surfaces; never creates live measures or web views.
internal static class MeterPreviewVisual
{
    internal static FrameworkElement Build(MeterEditorItem meter)
    {
        if (meter.Kind == MeterKind.Image) return ImageMeterVisual.Create(meter.Definition);
        if (meter.Kind == MeterKind.String)
        {
            return new TextBlock
            {
                Width = meter.Width,
                Height = meter.Height,
                FontSize = meter.FontSize,
                FontWeight = meter.Bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                TextAlignment = meter.CenterText ? TextAlignment.Center : TextAlignment.Left,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"],
                Text = meter.PreviewText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
        }

        if (meter.Kind == MeterKind.Icon)
        {
            var iconAccent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
            var chip = new Border
            {
                Width = meter.Width,
                Height = meter.Height,
                CornerRadius = new CornerRadius(Math.Min(meter.Width, meter.Height) * 0.25),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, iconAccent.Color.R, iconAccent.Color.G, iconAccent.Color.B)),
                Child = new FontIcon
                {
                    // Match the live renderer's Windows 11 symbol font so current Fluent
                    // glyphs don't fall back to a tiny outlined missing-character box.
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    Width = meter.Width,
                    Height = meter.Height,
                    FontSize = Math.Max(8, Math.Min(meter.Width, meter.Height) * 0.55),
                    Glyph = string.IsNullOrWhiteSpace(meter.IconGlyph) ? "\uE946" : meter.IconGlyph,
                    Foreground = iconAccent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            return chip;
        }

        if (meter.Kind == MeterKind.WebEmbed)
        {
            // No live WebView2 in authoring previews. An outlined
            // placeholder box reads as "something lives here" without pretending to render the
            // actual page.
            var placeholder = new Border
            {
                Width = meter.Width,
                Height = meter.Height,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = "\uE774  Web embed",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons, Segoe UI"),
                    FontSize = 13,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = meter.Width - 12,
                },
            };
            return placeholder;
        }

        if (meter.Kind == MeterKind.Ring)
        {
            // Same geometry as SkinHostWindow.BuildRingVisual, just computed once from
            // meter.PreviewFraction rather than redrawn from a ticking measure — this preview
            // has no live data to tick from, same reasoning as Bar/Graph's static fill below.
            var ringAccent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
            var trackBrush = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"];

            double thickness = Math.Max(3, Math.Min(meter.Width, meter.Height) * 0.12);
            double cx = meter.Width / 2, cy = meter.Height / 2;
            double radius = Math.Min(meter.Width, meter.Height) / 2 - thickness / 2;

            var ringTrack = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = trackBrush,
                StrokeThickness = thickness,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var arc = new Microsoft.UI.Xaml.Shapes.Path
            {
                Width = meter.Width,
                Height = meter.Height,
                Stroke = ringAccent,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            if (meter.PreviewFraction > 0.001)
            {
                double sweepDeg = Math.Min(meter.PreviewFraction, 0.999) * 360.0;

                Windows.Foundation.Point PointOnCircle(double angleDeg)
                {
                    double rad = (angleDeg - 90) * Math.PI / 180.0;
                    return new Windows.Foundation.Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
                }

                var figure = new PathFigure { StartPoint = PointOnCircle(0), IsClosed = false };
                figure.Segments.Add(new ArcSegment
                {
                    Point = PointOnCircle(sweepDeg),
                    Size = new Windows.Foundation.Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = sweepDeg > 180.0,
                });
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                arc.Data = geometry;
            }

            var ringGrid = new Grid { Width = meter.Width, Height = meter.Height };
            ringGrid.Children.Add(ringTrack);
            ringGrid.Children.Add(arc);
            return ringGrid;
        }

        if (meter.Kind == MeterKind.Graph)
        {
            // A fixed illustrative history, scaled by the sample value. No timer or data collection.
            var accent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
            var graph = new Canvas
            {
                Width = meter.Width, Height = meter.Height,
                Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
                Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, meter.Width, meter.Height) }
            };
            double inset = Math.Min(2, Math.Min(meter.Width, meter.Height) / 4);
            double[] samples = [0.55, 0.72, 0.61, 0.95, 1.18, 0.83, 0.66, 0.92, 1.42, 1.15, 0.88, 1.08, 0.76, 0.94, 1.2, 1.0];
            var line = new Polyline { Stroke = accent, StrokeThickness = Math.Min(2, inset * 2), StrokeLineJoin = PenLineJoin.Round };
            var area = new Polygon
            {
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0x30, accent.Color.R, accent.Color.G, accent.Color.B))
            };
            area.Points.Add(new Windows.Foundation.Point(inset, meter.Height - inset));
            for (int i = 0; i < samples.Length; i++)
            {
                var point = new Windows.Foundation.Point(
                    inset + (meter.Width - 2 * inset) * i / (samples.Length - 1),
                    meter.Height - inset - (meter.Height - 2 * inset) * Math.Clamp(samples[i] * meter.PreviewFraction, 0, 1));
                line.Points.Add(point);
                area.Points.Add(point);
            }
            area.Points.Add(new Windows.Foundation.Point(meter.Width - inset, meter.Height - inset));
            graph.Children.Add(area);
            graph.Children.Add(line);
            return graph;
        }

        // Static bar fill uses the host's theme gradient with representative sample values.
        var track = new Border
        {
            Width = meter.Width,
            Height = meter.Height,
            CornerRadius = new CornerRadius(meter.Kind == MeterKind.Bar ? meter.Height / 2 : 4),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };
        var fillAccent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var fillStrong = (SolidColorBrush)Application.Current.Resources["StrongAccentBrush"];
        var fillMiddle = Windows.UI.Color.FromArgb(
            0xF0,
            (byte)Math.Round(fillAccent.Color.R + (fillStrong.Color.R - fillAccent.Color.R) * 0.38),
            (byte)Math.Round(fillAccent.Color.G + (fillStrong.Color.G - fillAccent.Color.G) * 0.38),
            (byte)Math.Round(fillAccent.Color.B + (fillStrong.Color.B - fillAccent.Color.B) * 0.38));
        var fill = new Border
        {
            Width = meter.Width * meter.PreviewFraction,
            Height = meter.Height,
            CornerRadius = new CornerRadius(meter.Kind == MeterKind.Bar ? meter.Height / 2 : 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xEA, fillAccent.Color.R, fillAccent.Color.G, fillAccent.Color.B), Offset = 0.0 },
                    new GradientStop { Color = fillMiddle, Offset = 0.52 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xDA, fillStrong.Color.R, fillStrong.Color.G, fillStrong.Color.B), Offset = 1.0 },
                },
            },
        };
        var grid = new Grid { Width = meter.Width, Height = meter.Height };
        grid.Children.Add(track);
        grid.Children.Add(fill);
        return grid;
    }
}
