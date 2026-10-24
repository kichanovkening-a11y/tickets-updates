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
        var local=Events();using var c=Open();var bindings=TicketlandBindings028(c);
        using var q=Command(c,"SELECT title,starts_at,count(*) FROM ticketland_sale_028 GROUP BY title,starts_at ORDER BY starts_at,title");
        using var r=q.ExecuteReader();var result=new List<TicketlandShow028>();
        while(r.Read())
        {
            var title=r.GetString(0);var at=TicketlandAt028(DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture));
            var candidates=local.Where(e=>TicketlandAt028(DateTime.Parse(e.SortDate,CultureInfo.InvariantCulture))==at).ToArray();
            long? eventId=null;
            if(bindings.TryGetValue((title,at),out var bound)){if(candidates.Any(e=>e.Id==bound))eventId=bound;}
            else{var exact=candidates.Where(e=>CatalogText.Key(e.Title)==title).ToArray();if(exact.Length==1)eventId=exact[0].Id;}
            result.Add(new(title,at,r.GetInt32(2),eventId));
        }
        return result;
    }

    public void BindTicketlandShow028(string title,DateTime startsAt,long eventId)
    {
        if(string.IsNullOrWhiteSpace(title)||startsAt==default)throw new TicketException("Выберите показ Ticketland.");
        title=CatalogText.Key(title);startsAt=TicketlandAt028(startsAt);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);RequireActiveEvent(c,tx,eventId);
        using(var q=Command(c,"SELECT starts_at FROM event WHERE id=$e","$e",eventId))
        {
            q.Transaction=tx;var value=q.ExecuteScalar() as string;
            if(value is null||!DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)||TicketlandAt028(at)!=startsAt)
                throw new TicketException("Связать можно только показы с одинаковыми датой и временем.");
        }
        var serialized=startsAt.ToString("O",CultureInfo.InvariantCulture);
        using(var q=Command(c,"SELECT count(*) FROM ticketland_sale_028 WHERE title=$t AND starts_at=$at","$t",title,"$at",serialized))
        {q.Transaction=tx;if(Convert.ToInt64(q.ExecuteScalar())==0)throw new TicketException("Показ отсутствует в последнем сохранённом отчёте Ticketland.");}
        using(var q=Command(c,"INSERT INTO ticketland_show_map_028(title,starts_at,event_id) VALUES($t,$at,$e) ON CONFLICT(title,starts_at) DO UPDATE SET event_id=excluded.event_id","$t",title,"$at",serialized,"$e",eventId))
        {q.Transaction=tx;q.ExecuteNonQuery();}
        tx.Commit();
    }

    public IReadOnlyList<string> TicketlandSectors028(string title,DateTime at)
    {
        if(string.IsNullOrWhiteSpace(title))return Array.Empty<string>();
        using var c=Open();using var q=Command(c,"SELECT DISTINCT sector FROM ticketland_sale_028 WHERE title=$t AND starts_at=$at ORDER BY sector","$t",CatalogText.Key(title),"$at",TicketlandAt028(at).ToString("O",CultureInfo.InvariantCulture));
        using var r=q.ExecuteReader();var result=new List<string>();while(r.Read())result.Add(r.GetString(0));return result;
    }

    public void BindTicketlandSector028(string title,DateTime at,string sourceSector,long eventId,string localSector)
    {
        if(string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(sourceSector)||string.IsNullOrWhiteSpace(localSector)||at==default)throw new TicketException("Выберите исходный и местный секторы.");
        title=CatalogText.Key(title);sourceSector=CatalogText.Key(sourceSector);localSector=CatalogText.Key(localSector);at=TicketlandAt028(at);
        var serialized=at.ToString("O",CultureInfo.InvariantCulture);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);RequireActiveEvent(c,tx,eventId);
        if(!TicketlandBindings028(c,tx).TryGetValue((title,at),out var bound)||bound!=eventId)throw new TicketException("Сначала свяжите этот показ Ticketland с выбранным местным показом.");
        using(var q=Command(c,"SELECT starts_at FROM event WHERE id=$e","$e",eventId))
        {q.Transaction=tx;if(q.ExecuteScalar() is not string value||TicketlandAt028(DateTime.Parse(value,CultureInfo.InvariantCulture))!=at)throw new TicketException("Дата и время связанного показа изменились.");}
        using(var q=Command(c,"SELECT count(*) FROM ticketland_sale_028 WHERE title=$t AND starts_at=$at AND sector=$z","$t",title,"$at",serialized,"$z",sourceSector))
        {q.Transaction=tx;if(Convert.ToInt64(q.ExecuteScalar())==0)throw new TicketException("Исходный сектор отсутствует в отчёте Ticketland.");}
        var found=0;
        using(var q=Command(c,"""
            SELECT z.name FROM sector z JOIN event e ON e.venue_id=z.venue_id WHERE e.id=$e
            AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(z.venue_id AS TEXT)||':'||CAST(z.id AS TEXT))
            ""","$e",eventId))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())if(CatalogText.Key(r.GetString(0))==localSector)found++;}
        if(found!=1)throw new TicketException("Выберите существующий сектор зала этого показа.");
        using(var q=Command(c,"""
            INSERT INTO ticketland_sector_map_028(title,starts_at,source_sector,event_id,local_sector) VALUES($t,$at,$z,$e,$local)
            ON CONFLICT(title,starts_at,source_sector,event_id) DO UPDATE SET local_sector=excluded.local_sector
            ""","$t",title,"$at",serialized,"$z",sourceSector,"$e",eventId,"$local",localSector))
        {q.Transaction=tx;q.ExecuteNonQuery();}tx.Commit();
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
        var sales=new List<TicketlandSale028>();
        using(var q=Command(c,"SELECT title,starts_at,sector,row_label,seat_label,barcode,nominal FROM ticketland_sale_028 ORDER BY barcode"))
        {
            q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())sales.Add(new(r.GetString(0),TicketlandAt028(DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),decimal.Parse(r.GetString(6),CultureInfo.InvariantCulture)));
        }
        tx.Commit();return new(sales,fetched);
    }
}
