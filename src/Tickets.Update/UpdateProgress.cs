namespace Tickets.Update;

/// <summary>A measured phase; absent totals intentionally display an animated indicator.</summary>
public sealed record UpdateProgress(string Stage,long Completed=0,long? Total=null,string Detail="")
{
    public double? Percent=>Total is >0?Math.Clamp(100.0*Completed/Total.Value,0,100):null;
}