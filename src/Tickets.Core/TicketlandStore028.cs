using System.Globalization;
using System.Text.RegularExpressions;
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
        var columns=new HashSet<string>();using(var info=Command(c,"PRAGMA table_info(ticketland_sale_028)")){using var r=info.ExecuteReader();while(r.Read())columns.Add(r.GetString(1));}
        if(!columns.Contains("source_venue")){using var add=Command(c,"ALTER TABLE ticketland_sale_028 ADD COLUMN source_venue TEXT NOT NULL DEFAULT ''");add.ExecuteNonQuery();}
        if(!columns.Contains("source_channel")){using var add=Command(c,"ALTER TABLE ticketland_sale_028 ADD COLUMN source_channel TEXT NOT NULL DEFAULT ''");add.ExecuteNonQuery();}
        using var decisions=Command(c,"CREATE TABLE IF NOT EXISTS ticketland_decision_029(issue_key TEXT PRIMARY KEY,rejected INTEGER NOT NULL,event_id INTEGER,seat_id INTEGER)");decisions.ExecuteNonQuery();
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
            var sale=item with{Title=Field(item.Title,"название"),Sector=Field(item.Sector,"сектор"),Row=Field(item.Row,"ряд"),Seat=Field(item.Seat,"место"),Barcode=item.Barcode?.Trim()??"",StartsAt=DateTime.SpecifyKind(item.StartsAt,DateTimeKind.Unspecified),Venue=string.IsNullOrWhiteSpace(item.Venue)?"":Field(item.Venue,"площадка"),SourceChannel=item.SourceChannel?.Trim()??""};
            if(sale.SourceChannel.Length>1024||sale.SourceChannel.Any(ch=>char.IsControl(ch)&&!char.IsWhiteSpace(ch)))throw new TicketException("Некорректное поле Ticketland: источник.");
            if(sale.Barcode.Length is 0 or >512||sale.Barcode.Any(char.IsControl))throw new TicketException("Некорректный штрихкод Ticketland.");
            if(sales.TryGetValue(sale.Barcode,out var prior)&&prior with{SourceChannel=""}!=sale with{SourceChannel=""})throw new TicketException("В отчёте Ticketland один штрихкод относится к разным билетам.");
            sales[sale.Barcode]=sale;
        }
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var q=Command(c,"SELECT fetched_at FROM ticketland_sync_028 WHERE id=1"))
        {
            q.Transaction=tx;if(q.ExecuteScalar() is string prior&&report.FetchedAt<DateTimeOffset.Parse(prior,CultureInfo.InvariantCulture))throw new TicketException("Получен устаревший отчёт Ticketland. Последняя успешная загрузка сохранена.");
        }
        var local029=Local029(c,tx);
        var bindings=TicketlandBindings028(c,tx);
        var sectorBindings=TicketlandSectorBindings028(c,tx);
        var matchingSales029=new Dictionary<string,TicketlandSale028>(sales,StringComparer.Ordinal);
        if(!replace)foreach(var cached in ReadTicketlandSales029(c,tx))matchingSales029.TryAdd(cached.Barcode,cached);
        var matches029=Matches029(c,tx,matchingSales029.Values.ToArray(),local029,bindings,sectorBindings);
        var matched=0;var unmatched=0;var conflicts=0;var general=0;
        var prepared=new List<(TicketlandSale028 Sale,long? Event,long? Seat)>();
        foreach(var sale in sales.Values)
        {
            long? priorEvent=null;long? priorSeat=null;
            // Across fetches a barcode cannot silently move to another seat or amount.
            using(var q=Command(c,"SELECT title,starts_at,sector,row_label,seat_label,nominal,event_id,seat_id,source_venue FROM ticketland_sale_028 WHERE barcode=$b","$b",sale.Barcode))
            {
                q.Transaction=tx;using var r=q.ExecuteReader();if(r.Read())
                {
                    // Old HTML continuation pages could replace the showing heading with this
                    // exact report footer. A fully validated current XML snapshot may repair
                    // that known invalid heading once; seat labels and amount stay immutable.
                    static bool Footer(string title)=>Regex.IsMatch(title,@"^\d+СТР\. ИЗ \d+ ЛИЧНЫЙ КАБИНЕТ BACKSTAGE$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
                    var repairFooter=replace&&report.IsCurrentEventSnapshot&&Footer(r.GetString(0))&&!Footer(sale.Title);
                    if((!repairFooter&&(r.GetString(0)!=sale.Title||DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture)!=sale.StartsAt))||r.GetString(2)!=sale.Sector||r.GetString(3)!=sale.Row||r.GetString(4)!=sale.Seat||decimal.Parse(r.GetString(5),CultureInfo.InvariantCulture)!=sale.Nominal)throw new TicketException("Штрихкод Ticketland изменил место или стоимость. Последняя успешная загрузка сохранена.");
                    if(!repairFooter&&!r.IsDBNull(6)&&!r.IsDBNull(7))
                    {
                        var venueUnchanged=r.GetString(8)==sale.Venue;
                        // The old report did not carry venue provenance. An upgrade can keep a
                        // proven marker only when the newly supplied venue identifies its hall.
                        var knownVenueUpgrade=r.GetString(8).Length==0&&sale.Venue.Length>0&&local029.Any(show=>show.Id==r.GetInt64(6)&&Venue029(show.Venue)==Venue029(sale.Venue));
                        if(venueUnchanged||knownVenueUpgrade){priorEvent=r.GetInt64(6);priorSeat=r.GetInt64(7);}
                    }
                }
            }
            long? eventId=null;long? seatId=null;
            var resolved=matches029[sale.Barcode];
            eventId=resolved.Event;seatId=resolved.Seat;
            // A show-only approval never invents a numbered seat.
            if(!seatId.HasValue)eventId=null;
            if(sale.Row=="--"||sale.Seat=="--")general++;
            else if(eventId.HasValue&&seatId.HasValue)
            {
                matched++;
                using var occupied=Command(c,"SELECT count(*) FROM ticket WHERE event_id=$e AND seat_id=$s AND returned_at IS NULL","$e",eventId.Value,"$s",seatId.Value);occupied.Transaction=tx;
                if(Convert.ToInt64(occupied.ExecuteScalar())>0)conflicts++;
            }
            else unmatched++;
            // Preserve a proven seat through sector/row/seat label edits only while its
            // source still identifies this same unique exact show. Old cross-show manual
            // approvals and subsequently ambiguous show identities cannot bypass strict matching.
            if(eventId is null&&priorEvent.HasValue&&priorSeat.HasValue&&Exact030(sale,local029) is var priorExact030&&priorExact030.Length==1&&priorExact030[0].Id==priorEvent.Value)
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
                INSERT INTO ticketland_sale_028(barcode,title,starts_at,sector,row_label,seat_label,nominal,fetched_at,event_id,seat_id,source_venue,source_channel)
                VALUES($b,$t,$at,$z,$r,$s,$n,$f,$e,$id,$venue,$channel)
                ON CONFLICT(barcode) DO UPDATE SET fetched_at=excluded.fetched_at,source_venue=excluded.source_venue,source_channel=excluded.source_channel,
                event_id=excluded.event_id,seat_id=excluded.seat_id
                ""","$channel",sale.SourceChannel,"$venue",sale.Venue,"$b",sale.Barcode,"$t",sale.Title,"$at",sale.StartsAt.ToString("O",CultureInfo.InvariantCulture),"$z",sale.Sector,"$r",sale.Row,"$s",sale.Seat,"$n",sale.Nominal.ToString(CultureInfo.InvariantCulture),"$f",report.FetchedAt.ToString("O",CultureInfo.InvariantCulture),"$e",(object?)row.Event??DBNull.Value,"$id",(object?)row.Seat??DBNull.Value);
            q.Transaction=tx;q.ExecuteNonQuery();
        }
        using(var q=Command(c,"INSERT INTO ticketland_sync_028(id,fetched_at) VALUES(1,$at) ON CONFLICT(id) DO UPDATE SET fetched_at=excluded.fetched_at","$at",report.FetchedAt.ToString("O",CultureInfo.InvariantCulture))){q.Transaction=tx;q.ExecuteNonQuery();}
        tx.Commit();return new(matched,unmatched,conflicts,general);
    }
}
