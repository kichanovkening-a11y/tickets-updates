using System.Globalization;
using Tickets.Core;

namespace Tickets.App;

public sealed record PriceSummaryRow044(string Text,bool IsFirst=false,bool IsTotal=false)
{
    internal static IReadOnlyList<PriceSummaryRow044> For(IReadOnlyCollection<SeatInfo> seats)
    {
        string Number(long value)=>value.ToString("N0",CultureInfo.GetCultureInfo("ru-RU"));
        var rows=seats.GroupBy(s=>s.Amount).OrderByDescending(g=>g.Key)
            .Select((g,index)=>new PriceSummaryRow044($"{(g.Key.HasValue?Number(g.Key.Value):"Без цены")}×{g.Count()} = {Number(g.Sum(s=>s.Amount??0))}",index==0)).ToList();
        rows.Add(new($"Всего: {seats.Count} · {Number(seats.Sum(s=>s.Amount??0))}",rows.Count==0,true));
        return rows;
    }
}
