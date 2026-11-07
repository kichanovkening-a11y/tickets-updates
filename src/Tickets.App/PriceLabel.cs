using Tickets.Core;

namespace Tickets.App;

internal static class PriceLabel
{
    internal static string Format(decimal price, long count, decimal total) =>
        $"{DocumentTotals.Amount(price)} × {DocumentTotals.Amount(count)} = {DocumentTotals.Amount(total)}";
}
