using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using Tickets.Core;

namespace Tickets.App;

internal static class PriceLabel
{
    internal static CompactText043 Create(decimal price, long count, decimal total)
    {
        var label = new CompactText043
        {
            Text = $"{DocumentTotals.Amount(price)} × {DocumentTotals.Amount(count)} = {DocumentTotals.Amount(total)}",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Left,
            FontSize = 13.44,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            SnapsToDevicePixels = true
        };
        label.SetBinding(CompactText043.ForegroundProperty,new Binding("Background"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Border),1),Converter=new PriceContrastConverter()});
        TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(label, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(label, ClearTypeHint.Enabled);
        return label;
    }
}

internal sealed class PriceContrastConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {
        if(value is not SolidColorBrush b)return Brushes.Black;
        static double Linear(byte v){var s=v/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
        var l=.2126*Linear(b.Color.R)+.7152*Linear(b.Color.G)+.0722*Linear(b.Color.B);
        return l<.179?Brushes.White:Brushes.Black;
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
