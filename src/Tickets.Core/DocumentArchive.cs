using System.Globalization;

namespace Tickets.Core;

// A view of document dates. It never moves, rewrites or deletes stored documents.
public sealed class DocumentArchiveStatus
{
    private readonly Dictionary<long,EventInfo> events;
    private readonly HashSet<long> active;
    private readonly DateTime now;
    internal DocumentArchiveStatus(IEnumerable<EventInfo> events,IEnumerable<long> active,DateTime now)
    {this.events=events.ToDictionary(e=>e.Id);this.active=active.ToHashSet();this.now=now;}
    public bool Contains(long eventId,string snapshotDate)
    {
        if(events.TryGetValue(eventId,out var showing))
            return !active.Contains(eventId)||TryDate(showing.SortDate,out var date)&&date<now;
        // Saved packages retain their own dates even if an event was explicitly deleted.
        return TryDate(snapshotDate,out var savedDate)&&savedDate<now;
    }
    public bool Contains(RecipientPackageInfo package)
    {
        var shows=package.Invoice.PackageShows;
        return shows.Count>0?shows.All(show=>Contains(show.EventId,show.EventDate)):
            Contains(package.Invoice.EventId,package.Invoice.EventDate);
    }
    private static bool TryDate(string text,out DateTime value)=>
        DateTime.TryParse(text,CultureInfo.GetCultureInfo("ru-RU"),DateTimeStyles.None,out value)||
        DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out value);
}

public sealed partial class Store
{
    public DocumentArchiveStatus DocumentArchive(DateTime? now=null)=>new(Events(true),Events().Select(e=>e.Id),now??DateTime.Now);
}
