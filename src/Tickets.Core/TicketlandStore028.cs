using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record TicketlandSyncSummary028(int Matched,int Unmatched,int Conflicts,int GeneralAdmission);

public sealed partial class Store
{
    private void EnsureTicketland028()
    {
        using var c=Open();using var q=Command(c,"""
            CREATE TABLE IF NOT EXISTS ticketland_sale_028(
                barcode TEXT PRIMARY KEY,title TEXT NOT NULL,starts_at TEXT NOT NULL,
                sector TEXT NOT NULL,row_label TEXT NOT NULL,seat_label TEXT NOT NULL,nominal TEXT NOT NULL,
                fetched_at TEXT NOT NULL,event_id INTEGER REFERENCES event(id) ON DELETE CASCADE,
                seat_id INTEGER REFERENCES seat(id),CHECK((event_id IS NULL)=(seat_id IS NULL)));
            CREATE INDEX IF NOT EXISTS ticketland_sale_seat_028 ON ticketland_sale_028(event_id,seat_id);
            CREATE TABLE IF NOT EXISTS ticketland_sync_028(id INTEGER PRIMARY KEY CHECK(id=1),fetched_at TEXT NOT NULL);
            """);q.ExecuteNonQuery();EnsureTicketlandMappings028(c);
    }

    public DateTimeOffset? TicketlandLastSync028
    {
        get{using var c=Open();using var q=Command(c,"SELECT fetched_at FROM ticketland_sync_028 WHERE id=1");return q.ExecuteScalar() is string value?DateTimeOffset.Parse(value,CultureInfo.InvariantCulture):null;}
    }

    /// <summary>Apply only a fully downloaded and validated report. Replacement must be chosen
    /// explicitly for an authoritative complete snapshot; append imports never free missing sales.</summary>
    public TicketlandSyncSummary028 ApplyTicketlandReport028(TicketlandReport028 report,bool replace)
    {
        if(report is null||report.Sales is null||report.FetchedAt==default)throw new TicketException("Некорректный отчёт Ticketland.");
        var sales=new Dictionary<string,TicketlandSale028>(StringComparer.Ordinal);
        static string Field(string? value,string label)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Length>1024||value.Any(ch=>char.IsControl(ch)&&!char.IsWhiteSpace(ch)))throw new TicketException("Некорректное поле Ticketland: "+label+".");
            return CatalogText.Key(value);
        }
        foreach(var item in report.Sales)
        {
            if(item is null||item.StartsAt==default||item.Nominal<0)throw new TicketException("Некорректная продажа Ticketland.");
            var sale=item with{Title=Field(item.Title,"название"),Sector=Field(item.Sector,"сектор"),Row=Field(item.Row,"ряд"),Seat=Field(item.Seat,"место"),Barcode=item.Barcode?.Trim()??"",StartsAt=DateTime.SpecifyKind(item.StartsAt,DateTimeKind.Unspecified)};
            if(sale.Barcode.Length is 0 or >512||sale.Barcode.Any(char.IsControl))throw new TicketException("Некорректный штрихкод Ticketland.");
            if(sales.TryGetValue(sale.Barcode,out var prior)&&prior!=sale)throw new TicketException("В отчёте Ticketland один штрихкод относится к разным билетам.");
            sales[sale.Barcode]=sale;
        }
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var q=Command(c,"SELECT fetched_at FROM ticketland_sync_028 WHERE id=1"))
        {
            q.Transaction=tx;if(q.ExecuteScalar() is string prior&&report.FetchedAt<DateTimeOffset.Parse(prior,CultureInfo.InvariantCulture))throw new TicketException("Получен устаревший отчёт Ticketland. Последняя успешная загрузка сохранена.");
        }
        var shows=new Dictionary<(string Title,DateTime At),List<(long Event,long Venue)>>();
        using(var q=Command(c,"""
            SELECT e.id,e.venue_id,e.title,e.starts_at FROM event e
            WHERE NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='venue' AND h.entity_key=CAST(e.venue_id AS TEXT))
            """))
        {
            q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())
            {
                var title=CatalogText.Key(r.GetString(2));
                if(!DateTime.TryParse(r.GetString(3),CultureInfo.InvariantCulture,DateTimeStyles.None,out var at))continue;
                var key=(title,DateTime.SpecifyKind(at,DateTimeKind.Unspecified));if(!shows.TryGetValue(key,out var list))shows[key]=list=[];list.Add((r.GetInt64(0),r.GetInt64(1)));
            }
        }
        var bindings=TicketlandBindings028(c,tx);
        var sectorBindings=TicketlandSectorBindings028(c,tx);
        var matched=0;var unmatched=0;var conflicts=0;var general=0;
        var prepared=new List<(TicketlandSale028 Sale,long? Event,long? Seat)>();
        var venueSeats=new Dictionary<long,Dictionary<(string Sector,string Row,string Seat),List<long>>>();
        foreach(var sale in sales.Values)
        {
            long? priorEvent=null;long? priorSeat=null;
            // Across fetches a barcode cannot silently move to another seat or amount.
            using(var q=Command(c,"SELECT title,starts_at,sector,row_label,seat_label,nominal,event_id,seat_id FROM ticketland_sale_028 WHERE barcode=$b","$b",sale.Barcode))
            {
                q.Transaction=tx;using var r=q.ExecuteReader();if(r.Read())
                {
                    if(r.GetString(0)!=sale.Title||DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture)!=sale.StartsAt||r.GetString(2)!=sale.Sector||r.GetString(3)!=sale.Row||r.GetString(4)!=sale.Seat||decimal.Parse(r.GetString(5),CultureInfo.InvariantCulture)!=sale.Nominal)throw new TicketException("Штрихкод Ticketland изменил место или стоимость. Последняя успешная загрузка сохранена.");
                    if(!r.IsDBNull(6)&&!r.IsDBNull(7)){priorEvent=r.GetInt64(6);priorSeat=r.GetInt64(7);}
                }
            }
            long? eventId=null;long? seatId=null;
            var hasBinding=bindings.TryGetValue((sale.Title,sale.StartsAt),out var bound);
            var candidates=hasBinding
                ?shows.Where(pair=>pair.Key.At==sale.StartsAt).SelectMany(pair=>pair.Value).Where(show=>show.Event==bound).ToList()
                :shows.GetValueOrDefault((sale.Title,sale.StartsAt))??[];
            if(sale.Row=="--"||sale.Seat=="--")general++;
            else if(candidates.Count==1)
            {
                var show=candidates[0];
                if(!venueSeats.TryGetValue(show.Venue,out var map))
                {
                    venueSeats[show.Venue]=map=[];
                    using var q=Command(c,"""
                        SELECT s.id,z.name,s.row_label,s.number FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id
                        WHERE s.venue_id=$v AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat h WHERE h.seat_id=s.id)
                        AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))
                        ""","$v",show.Venue);
                    q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())
                    {
                        var key=(CatalogText.Key(r.GetString(1)),CatalogText.Key(r.GetString(2)),CatalogText.Key(r.GetString(3)));
                        if(!map.TryGetValue(key,out var ids))map[key]=ids=[];ids.Add(r.GetInt64(0));
                    }
                }
                var sector=hasBinding&&sectorBindings.TryGetValue((sale.Title,sale.StartsAt,sale.Sector,show.Event),out var mappedSector)?mappedSector:sale.Sector;
                var found=map.GetValueOrDefault((sector,sale.Row,sale.Seat))??[];
                if(found.Count==1)
                {
                    eventId=show.Event;seatId=found[0];matched++;
                    using var q=Command(c,"SELECT count(*) FROM ticket WHERE event_id=$e AND seat_id=$s AND returned_at IS NULL","$e",eventId.Value,"$s",seatId.Value);q.Transaction=tx;
                    if(Convert.ToInt64(q.ExecuteScalar())>0)conflicts++;
                }
                else unmatched++;
            }
            else unmatched++;
            // A still-present unchanged barcode remains sold even when local labels or the
            // catalogue changed. Keep its proven seat identity; report the unresolved current
            // match above rather than treating a label change as an external return.
            if(eventId is null&&priorEvent.HasValue&&priorSeat.HasValue)
            {
                using var valid=Command(c,"SELECT count(*) FROM event e JOIN seat s ON s.venue_id=e.venue_id WHERE e.id=$e AND s.id=$s","$e",priorEvent.Value,"$s",priorSeat.Value);valid.Transaction=tx;
                if(Convert.ToInt64(valid.ExecuteScalar())==1)
                {
                    eventId=priorEvent;seatId=priorSeat;
                    using var occupied=Command(c,"SELECT count(*) FROM ticket WHERE event_id=$e AND seat_id=$s AND returned_at IS NULL","$e",eventId.Value,"$s",seatId.Value);occupied.Transaction=tx;
                    if(Convert.ToInt64(occupied.ExecuteScalar())>0)conflicts++;
                }
            }
            prepared.Add((sale,eventId,seatId));
        }
        if(replace){using var clear=Command(c,"DELETE FROM ticketland_sale_028");clear.Transaction=tx;clear.ExecuteNonQuery();}
        foreach(var row in prepared)
        {
            var sale=row.Sale;
            using var q=Command(c,"""
                INSERT INTO ticketland_sale_028(barcode,title,starts_at,sector,row_label,seat_label,nominal,fetched_at,event_id,seat_id)
                VALUES($b,$t,$at,$z,$r,$s,$n,$f,$e,$id)
                ON CONFLICT(barcode) DO UPDATE SET fetched_at=excluded.fetched_at,
                event_id=CASE WHEN $replace=1 THEN excluded.event_id ELSE coalesce(ticketland_sale_028.event_id,excluded.event_id) END,
                seat_id=CASE WHEN $replace=1 THEN excluded.seat_id ELSE coalesce(ticketland_sale_028.seat_id,excluded.seat_id) END
                ""","$replace",replace||(bindings.TryGetValue((sale.Title,sale.StartsAt),out var boundEvent)&&row.Event==boundEvent)?1:0,"$b",sale.Barcode,"$t",sale.Title,"$at",sale.StartsAt.ToString("O",CultureInfo.InvariantCulture),"$z",sale.Sector,"$r",sale.Row,"$s",sale.Seat,"$n",sale.Nominal.ToString(CultureInfo.InvariantCulture),"$f",report.FetchedAt.ToString("O",CultureInfo.InvariantCulture),"$e",(object?)row.Event??DBNull.Value,"$id",(object?)row.Seat??DBNull.Value);
            q.Transaction=tx;q.ExecuteNonQuery();
        }
        using(var q=Command(c,"INSERT INTO ticketland_sync_028(id,fetched_at) VALUES(1,$at) ON CONFLICT(id) DO UPDATE SET fetched_at=excluded.fetched_at","$at",report.FetchedAt.ToString("O",CultureInfo.InvariantCulture))){q.Transaction=tx;q.ExecuteNonQuery();}
        tx.Commit();return new(matched,unmatched,conflicts,general);
    }
}
