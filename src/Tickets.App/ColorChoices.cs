using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Tickets.App;

internal static class ColorChoices
{
    internal static DataTemplate Template()
    {
        var panel=new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var swatch=new FrameworkElementFactory(typeof(Border));
        swatch.SetValue(FrameworkElement.WidthProperty,68d);
        swatch.SetValue(FrameworkElement.HeightProperty,25d);
        swatch.SetValue(FrameworkElement.MarginProperty,new Thickness(0,2,12,2));
        swatch.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));
        swatch.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        swatch.SetValue(Border.BorderBrushProperty,Brushes.Black);
        swatch.SetBinding(Border.BackgroundProperty,new Binding());panel.AppendChild(swatch);
        var label=new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty,new Binding());
        label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);panel.AppendChild(label);
        return new DataTemplate{VisualTree=panel};
    }
}
