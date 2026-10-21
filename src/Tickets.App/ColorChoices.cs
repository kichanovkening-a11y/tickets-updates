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
        swatch.SetValue(FrameworkElement.WidthProperty,26d);
        swatch.SetValue(FrameworkElement.HeightProperty,18d);
        swatch.SetValue(FrameworkElement.MarginProperty,new Thickness(0,2,4,2));
        swatch.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));
        swatch.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        swatch.SetValue(Border.BorderBrushProperty,Brushes.Black);
        swatch.SetBinding(Border.BackgroundProperty,new Binding());panel.AppendChild(swatch);
        return new DataTemplate{VisualTree=panel};
    }
}
