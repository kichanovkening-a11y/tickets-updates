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
            // A current name-first resolution establishes provenance for legitimate later
            // local caption edits. Pre-0.32 marker IDs alone are never proof of correctness.
            if(resolved.Event.HasValue&&resolved.Seat.HasValue)
            {
                var show=local029.Single(x=>x.Id==resolved.Event.Value);
                RememberSector032(c,tx,sale,show,show.Seats.Single(x=>x.Id==resolved.Seat.Value));
            }
            // Keep original proof while this barcode remains in the source snapshot.
            // It prevents repeated cache reapplication from choosing a collision winner
            // or erasing an explicit original sector family after a failed resolution.
            prepared.Add((sale,eventId,seatId));
        }
        if(!replace)
        foreach(var cached in matchingSales029.Values.Where(x=>!sales.ContainsKey(x.Barcode)&&matches029[x.Barcode].Issue?.Reason.Contains("Разные исходные",StringComparison.Ordinal)==true))
        {
            using var clear=Command(c,"UPDATE ticketland_sale_028 SET event_id=NULL,seat_id=NULL WHERE barcode=$b","$b",cached.Barcode);clear.Transaction=tx;clear.ExecuteNonQuery();
        }
        if(replace)
        {
            using var clear=Command(c,"DELETE FROM ticketland_sale_028");clear.Transaction=tx;clear.ExecuteNonQuery();
            var retained=sales.Keys.ToHashSet(StringComparer.Ordinal);var removed=new List<string>();
            using(var q=Command(c,"SELECT barcode FROM ticketland_match_proof_032")){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())if(!retained.Contains(r.GetString(0)))removed.Add(r.GetString(0));}
            foreach(var barcode in removed){using var q=Command(c,"DELETE FROM ticketland_match_proof_032 WHERE barcode=$b","$b",barcode);q.Transaction=tx;q.ExecuteNonQuery();}
        }
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
