using System.Windows;
using System.Windows.Media;
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
        TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(label, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(label, ClearTypeHint.Enabled);
        return label;
    }
}
