using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace ThemeManager.WinUI.Views
{
    public sealed partial class WidgetEditorWindow : Window
    {
        private UIElement? _selectedElement;
        private UIElement? _draggingElement;
        private Point _dragStartPoint;
        private Point _elementStartPoint;

        public WidgetEditorWindow()
        {
            this.InitializeComponent();
        }

        private void OnAddTextMeterClicked(object sender, RoutedEventArgs e)
        {
            var textBlock = new TextBlock 
            { 
                Text = "12:00 PM", 
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 24,
                IsTextSelectionEnabled = false
            };
            AddElementToCanvas(textBlock, 120, 120, 100, 40);
        }

        private void OnAddBarMeterClicked(object sender, RoutedEventArgs e)
        {
            var rect = new Rectangle 
            { 
                Fill = new SolidColorBrush(Colors.DodgerBlue),
                RadiusX = 4, RadiusY = 4
            };
            AddElementToCanvas(rect, 120, 170, 200, 10);
        }

        private void OnAddGraphMeterClicked(object sender, RoutedEventArgs e)
        {
            var rect = new Rectangle 
            { 
                Fill = new SolidColorBrush(Colors.LimeGreen),
                Opacity = 0.5
            };
            AddElementToCanvas(rect, 120, 200, 200, 60);
        }

        private void AddElementToCanvas(UIElement element, double x, double y, double width, double height)
        {
            var wrapper = new Border 
            { 
                Child = element, 
                Width = width, 
                Height = height,
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255))
            };

            Canvas.SetLeft(wrapper, x);
            Canvas.SetTop(wrapper, y);
            
            WidgetCanvas.Children.Add(wrapper);
            SelectElement(wrapper);
        }

        private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(WidgetCanvas);
            
            // Find if we clicked on an element
            UIElement? hitElement = null;
            for (int i = WidgetCanvas.Children.Count - 1; i >= 0; i--)
            {
                var child = WidgetCanvas.Children[i];
                if (child == WidgetBounds) continue;

                var x = Canvas.GetLeft(child);
                var y = Canvas.GetTop(child);
                var width = (double)child.GetValue(FrameworkElement.WidthProperty);
                var height = (double)child.GetValue(FrameworkElement.HeightProperty);

                if (pt.Position.X >= x && pt.Position.X <= x + width &&
                    pt.Position.Y >= y && pt.Position.Y <= y + height)
                {
                    hitElement = child;
                    break;
                }
            }

            if (hitElement != null)
            {
                _draggingElement = hitElement;
                _dragStartPoint = pt.Position;
                _elementStartPoint = new Point(Canvas.GetLeft(hitElement), Canvas.GetTop(hitElement));
                hitElement.CapturePointer(e.Pointer);
                SelectElement(hitElement);
            }
            else
            {
                SelectElement(null);
            }
        }

        private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_draggingElement != null)
            {
                var pt = e.GetCurrentPoint(WidgetCanvas);
                double dx = pt.Position.X - _dragStartPoint.X;
                double dy = pt.Position.Y - _dragStartPoint.Y;

                double newX = _elementStartPoint.X + dx;
                double newY = _elementStartPoint.Y + dy;

                Canvas.SetLeft(_draggingElement, newX);
                Canvas.SetTop(_draggingElement, newY);

                if (_draggingElement == _selectedElement)
                {
                    PosXSlider.Value = newX;
                    PosYSlider.Value = newY;
                }
            }
        }

        private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_draggingElement != null)
            {
                _draggingElement.ReleasePointerCapture(e.Pointer);
                _draggingElement = null;
            }
        }

        private void SelectElement(UIElement? element)
        {
            if (_selectedElement is Border oldBorder)
            {
                oldBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }

            _selectedElement = element;

            if (_selectedElement is Border newBorder)
            {
                newBorder.BorderBrush = new SolidColorBrush(Colors.Orange);
                
                NoSelectionText.Visibility = Visibility.Collapsed;
                PropertiesPanel.Visibility = Visibility.Visible;

                PosXSlider.Value = Canvas.GetLeft(newBorder);
                PosYSlider.Value = Canvas.GetTop(newBorder);
                WidthSlider.Value = newBorder.Width;
                HeightSlider.Value = newBorder.Height;
            }
            else
            {
                NoSelectionText.Visibility = Visibility.Visible;
                PropertiesPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnPositionChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_selectedElement != null && _draggingElement == null) // avoid feedback loop
            {
                if (sender as Slider == PosXSlider) Canvas.SetLeft(_selectedElement, e.NewValue);
                if (sender as Slider == PosYSlider) Canvas.SetTop(_selectedElement, e.NewValue);
            }
        }

        private void OnSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_selectedElement is FrameworkElement fw)
            {
                if (sender as Slider == WidthSlider) fw.Width = e.NewValue;
                if (sender as Slider == HeightSlider) fw.Height = e.NewValue;
            }
        }

        private void OnDataBindingChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the preview based on binding
        }

        private void OnRemoveMeterClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedElement != null)
            {
                WidgetCanvas.Children.Remove(_selectedElement);
                SelectElement(null);
            }
        }
    }
}
