namespace Tickets.Core;

public sealed record TicketlandShowDifference031(string SourceTitle,DateTime StartsAt,string Venue,int TicketCount,bool TitleMatches,bool DateMatches,bool TimeMatches,bool VenueMatches);
public sealed record TicketlandShowDiagnostic031(long EventId,string Title,DateTime StartsAt,string Venue,int SourceTicketCount,int ExactTicketCount,IReadOnlyList<TicketlandShowDifference031> Differences);

public sealed partial class Store
{
    // Explains the selected showing using the cached, validated source snapshot.
    // Similarity here is diagnostic only and never authorizes a show/seat binding.
    public TicketlandShowDiagnostic031 TicketlandShowDiagnostics031(long eventId)
    {
        using var c=Open();using var tx=c.BeginTransaction();var shows=Local029(c,tx,loadSeats:false);
        var show=shows.SingleOrDefault(s=>s.Id==eventId)??throw new TicketException("Показ отсутствует в действующем каталоге.");
        var rows=ReadTicketlandSales029(c,tx).DistinctBy(s=>s.Barcode)
            .GroupBy(s=>(s.Title,s.StartsAt,s.Venue))
            .Select(g=>new TicketlandShowDifference031(g.Key.Title,g.Key.StartsAt,g.Key.Venue,g.Count(),Title029(g.Key.Title)==Title029(show.Title),g.Key.StartsAt.Date==show.At.Date,g.Key.StartsAt.TimeOfDay==show.At.TimeOfDay,!string.IsNullOrWhiteSpace(g.Key.Venue)&&Venue029(g.Key.Venue)==Venue029(show.Venue)))
            .Where(s=>s.TitleMatches||s.DateMatches&&s.TimeMatches&&s.VenueMatches)
            .OrderBy(s=>s.StartsAt).ThenBy(s=>s.SourceTitle).ThenBy(s=>s.Venue).ToArray();
        var exact=ReadTicketlandSales029(c,tx).DistinctBy(s=>s.Barcode).Count(s=>Exact030(s,shows) is var matches&&matches.Length==1&&matches[0].Id==eventId);
        return new(show.Id,show.Title,show.At,show.Venue,rows.Sum(s=>s.TicketCount),exact,rows);
    }
}
