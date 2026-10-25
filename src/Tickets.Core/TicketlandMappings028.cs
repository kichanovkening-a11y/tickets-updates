using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record TicketlandShow028(string Title,DateTime StartsAt,int Tickets,long? EventId);

public sealed partial class Store
{
    private static DateTime TicketlandAt028(DateTime at)=>DateTime.SpecifyKind(at,DateTimeKind.Unspecified);

    private static void EnsureTicketlandMappings028(SqliteConnection c)
    {
        using var q=Command(c,"""
            CREATE TABLE IF NOT EXISTS ticketland_show_map_028(
                title TEXT NOT NULL,starts_at TEXT NOT NULL,
                event_id INTEGER NOT NULL REFERENCES event(id) ON DELETE CASCADE,
                PRIMARY KEY(title,starts_at));
            CREATE TABLE IF NOT EXISTS ticketland_sector_map_028(
                title TEXT NOT NULL,starts_at TEXT NOT NULL,source_sector TEXT NOT NULL,
                event_id INTEGER NOT NULL REFERENCES event(id) ON DELETE CASCADE,local_sector TEXT NOT NULL,
                PRIMARY KEY(title,starts_at,source_sector,event_id));
            """);q.ExecuteNonQuery();
    }

    private static Dictionary<(string Title,DateTime At),long> TicketlandBindings028(SqliteConnection c,SqliteTransaction? tx=null)
    {
        using var q=Command(c,"SELECT title,starts_at,event_id FROM ticketland_show_map_028");q.Transaction=tx;
        using var r=q.ExecuteReader();var result=new Dictionary<(string Title,DateTime At),long>();
        while(r.Read())result[(r.GetString(0),TicketlandAt028(DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture)))]=r.GetInt64(2);
        return result;
    }

    public IReadOnlyList<TicketlandShow028> TicketlandShows028()
    {
        return TicketlandSourceTotals030().Select(x=>new TicketlandShow028(x.Title,x.StartsAt,x.TicketCount,x.EventId)).ToArray();
    }

    public void BindTicketlandShow028(string title,DateTime startsAt,long eventId)
    {
        using var c=Open();using var tx=c.BeginTransaction();var shows=Local029(c,tx);
        var sales=ReadTicketlandSales029(c,tx).Where(s=>CatalogText.Key(s.Title)==CatalogText.Key(title)&&s.StartsAt==TicketlandAt028(startsAt)).ToArray();
        if(sales.Length==0||sales.Any(s=>Exact030(s,shows) is var exact&&(exact.Length!=1||exact[0].Id!=eventId)))
            throw new TicketException("Показ должен однозначно совпадать по названию, дате, времени и площадке. Связывать разные показы запрещено.");
        // Kept for compatibility: exact shows require no manual show binding.
    }

    public IReadOnlyList<string> TicketlandSectors028(string title,DateTime at)
    {
        if(string.IsNullOrWhiteSpace(title))return Array.Empty<string>();
        using var c=Open();using var q=Command(c,"SELECT DISTINCT sector FROM ticketland_sale_028 WHERE title=$t AND starts_at=$at ORDER BY sector","$t",CatalogText.Key(title),"$at",TicketlandAt028(at).ToString("O",CultureInfo.InvariantCulture));
        using var r=q.ExecuteReader();var result=new List<string>();while(r.Read())result.Add(r.GetString(0));return result;
    }

    public void BindTicketlandSector028(string title,DateTime at,string sourceSector,long eventId,string localSector)
    {
        var reviews=TicketlandSectorReviews030().Where(g=>g.EventId==eventId&&g.StartsAt==TicketlandAt028(at)&&Text029(g.SourceSector)==Text029(sourceSector)&&Title029(g.Title)==Title029(title)).ToArray();
        if(reviews.Length!=1)throw new TicketException("Выберите сектор единственного точного показа Ticketland.");
        AcceptTicketlandSector030(reviews[0].Key,localSector);
    }

    private static Dictionary<(string Title,DateTime At,string Sector,long Event),string> TicketlandSectorBindings028(SqliteConnection c,SqliteTransaction tx)
    {
        using var q=Command(c,"SELECT title,starts_at,source_sector,event_id,local_sector FROM ticketland_sector_map_028");q.Transaction=tx;
        using var r=q.ExecuteReader();var result=new Dictionary<(string Title,DateTime At,string Sector,long Event),string>();
        while(r.Read())result[(r.GetString(0),TicketlandAt028(DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture)),r.GetString(2),r.GetInt64(3))]=r.GetString(4);return result;
    }

    public TicketlandReport028 CachedTicketlandReport028()
    {
        using var c=Open();using var tx=c.BeginTransaction();DateTimeOffset fetched=default;
        using(var q=Command(c,"SELECT fetched_at FROM ticketland_sync_028 WHERE id=1"))
        {q.Transaction=tx;if(q.ExecuteScalar() is string text)fetched=DateTimeOffset.Parse(text,CultureInfo.InvariantCulture);}
        var sales=ReadTicketlandSales029(c,tx);
        tx.Commit();return new(sales,fetched);
    }
    private static List<TicketlandSale028> ReadTicketlandSales029(SqliteConnection c,SqliteTransaction tx)
    {
        var sales=new List<TicketlandSale028>();
        using(var q=Command(c,"SELECT title,starts_at,sector,row_label,seat_label,barcode,nominal,source_venue,source_channel FROM ticketland_sale_028 ORDER BY barcode"))
        {
            q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())sales.Add(new(r.GetString(0),TicketlandAt028(DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),decimal.Parse(r.GetString(6),CultureInfo.InvariantCulture)){Venue=r.GetString(7),SourceChannel=r.GetString(8)});
        }
        return sales;
    }

}
