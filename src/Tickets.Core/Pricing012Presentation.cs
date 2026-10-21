namespace Tickets.Core;

public sealed record PriceSummary(long Amount,int Count,decimal Total,string Color,int Row,int Column);

/// <summary>Pure presentation rules shared by the pricing editor and its checks.</summary>
public static class Pricing012Presentation
{
    public static IReadOnlyList<PriceSummary> Summaries(IEnumerable<PriceValue> prices)
        =>prices.GroupBy(p=>p.Amount).OrderByDescending(g=>g.Key)
            .Select((g,index)=>new PriceSummary(g.Key,g.Count(),g.Sum(p=>(decimal)p.Amount),g.First().Color,index%3,index/3)).ToArray();

    public static bool SamePrices(IEnumerable<PriceValue> original,IEnumerable<long> originalWithdrawn,
        IEnumerable<PriceValue> current,IEnumerable<long> currentWithdrawn)
        =>original.Select(p=>(p.SeatId,p.Amount)).OrderBy(p=>p.SeatId)
            .SequenceEqual(current.Select(p=>(p.SeatId,p.Amount)).OrderBy(p=>p.SeatId))
            &&originalWithdrawn.ToHashSet().SetEquals(currentWithdrawn);

    public static string Foreground(string color)
    {
        static double Linear(byte value){var n=value/255d;return n<=0.04045?n/12.92:Math.Pow((n+0.055)/1.055,2.4);}
        var r=Convert.ToByte(color.Substring(1,2),16);var g=Convert.ToByte(color.Substring(3,2),16);var b=Convert.ToByte(color.Substring(5,2),16);
        var luminance=0.2126*Linear(r)+0.7152*Linear(g)+0.0722*Linear(b);
        return (luminance+0.05)/0.05>=1.05/(luminance+0.05)?"#000000":"#FFFFFF";
    }
}
