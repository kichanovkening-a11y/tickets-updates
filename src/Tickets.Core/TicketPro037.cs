using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record TicketProPlace037(string Id,string Sector,string Row,string Seat,string Status,string Fund,decimal Price);
public sealed record TicketProSnapshot037(string Title,DateTime StartsAt,string SourceUrl,int ExpectedCount,IReadOnlyList<TicketProPlace037> Places,DateTimeOffset FetchedAt);
public sealed record TicketProBinding037(long EventId,string SourceUrl,DateTimeOffset FetchedAt,int Count);

public static class TicketPro037
{
    public static string State(string status)=>status switch
    {
        "Продано" or "Продано агентами"=>"sold",
        "Забронировано" or "Не использовать"=>"occupied",
        "В продаже"=>"available",
        _=>throw new TicketException("Ticket PRO: неизвестный статус места. Предыдущие данные сохранены.")
    };
    public static bool IsSchemeUrl(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.Host=="pro.ticketland.ru"&&u.IsDefaultPort&&u.UserInfo.Length==0&&u.Query.Length==0&&u.Fragment.Length==0&&Regex.IsMatch(u.AbsolutePath,@"^/management/show/[1-9]\d*/ticket-control/[1-9]\d*$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    public static TicketProSnapshot037 Parse(string json,DateTimeOffset fetchedAt)
    {
        try
        {
            using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
            string Field(JsonElement e,string name)=>e.GetProperty(name).GetString()?.Trim()??"";
            var url=Field(root,"url");var title=Field(root,"title");var date=Field(root,"date");
            if(!IsSchemeUrl(url)||!DateTime.TryParseExact(date,["dd.MM.yyyy HH:mm","dd.MM.yyyy HH:mm:ss"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var at))throw Invalid();
            var places=root.GetProperty("places").EnumerateArray().Select(e=>new TicketProPlace037(Field(e,"id"),Field(e,"sector"),Field(e,"row"),Field(e,"seat"),Field(e,"status"),Field(e,"fund"),decimal.Parse(Field(e,"price"),CultureInfo.InvariantCulture))).ToArray();
            var snapshot=new TicketProSnapshot037(title,at,url,root.GetProperty("expectedCount").GetInt32(),places,fetchedAt);Validate(snapshot);return snapshot;
        }
        catch(TicketException){throw;}
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){throw Invalid();}
    }
    internal static void Validate(TicketProSnapshot037 s)
    {
        if(s is null||s.Places is null||s.FetchedAt==default||s.StartsAt==default||!IsSchemeUrl(s.SourceUrl)||string.IsNullOrWhiteSpace(s.Title)||s.Title.Length>1024||s.ExpectedCount<1||s.ExpectedCount>100000||s.ExpectedCount!=s.Places.Count)throw Invalid();
        var ids=new HashSet<string>(StringComparer.Ordinal);var positions=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in s.Places)
        {
            if(p is null||!Regex.IsMatch(p.Id??"",@"^p?[1-9]\d*$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1))||!ids.Add(p.Id!)||p.Price<0||new[]{p.Sector,p.Row,p.Seat,p.Fund}.Any(v=>string.IsNullOrWhiteSpace(v)||v.Length>1024||v.Any(char.IsControl))||!positions.Add(string.Join("\u001f",p.Sector.ToUpperInvariant(),p.Row.ToUpperInvariant(),p.Seat.ToUpperInvariant())))throw Invalid();
            State(p.Status);
        }
    }
    private static TicketException Invalid()=>new("Ticket PRO: схема неполная или не распознана. Предыдущие данные сохранены.");
}

public sealed partial class Store
{
    private void EnsureTicketPro037()
    {
        using var c=Open();using var q=Command(c,"""
            CREATE TABLE IF NOT EXISTS ticketpro_show_037(event_id INTEGER PRIMARY KEY REFERENCES event(id) ON DELETE CASCADE,source_url TEXT NOT NULL UNIQUE,fetched_at TEXT NOT NULL,place_count INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS ticketpro_place_037(event_id INTEGER NOT NULL REFERENCES event(id) ON DELETE CASCADE,seat_id INTEGER NOT NULL REFERENCES seat(id),place_id TEXT NOT NULL,state TEXT NOT NULL,status TEXT NOT NULL,fund TEXT NOT NULL,price TEXT NOT NULL,PRIMARY KEY(event_id,seat_id),UNIQUE(event_id,place_id));
            CREATE TABLE IF NOT EXISTS ticketpro_release_037(event_id INTEGER NOT NULL REFERENCES event(id) ON DELETE CASCADE,seat_id INTEGER NOT NULL REFERENCES seat(id),PRIMARY KEY(event_id,seat_id));
            CREATE VIEW IF NOT EXISTS ticketland_effective_sold_037 AS
            SELECT tl.* FROM ticketland_sale_028 tl WHERE NOT EXISTS(SELECT 1 FROM ticketpro_place_037 p WHERE p.event_id=tl.event_id AND p.seat_id=tl.seat_id)
            UNION ALL
            SELECT 'PRO:'||p.event_id||':'||p.place_id,e.title,e.starts_at,z.name,s.row_label,s.number,p.price,b.fetched_at,p.event_id,p.seat_id,v.name,'Ticket PRO'
            FROM ticketpro_place_037 p JOIN ticketpro_show_037 b ON b.event_id=p.event_id JOIN event e ON e.id=p.event_id JOIN seat s ON s.id=p.seat_id JOIN venue v ON v.id=e.venue_id JOIN sector z ON z.id=s.sector_id AND z.venue_id=s.venue_id WHERE p.state='sold';
            CREATE VIEW IF NOT EXISTS ticketland_unavailable_037 AS
            SELECT event_id,seat_id FROM ticketland_effective_sold_037
            UNION
            SELECT p.event_id,p.seat_id FROM ticketpro_place_037 p WHERE p.state='occupied' AND NOT EXISTS(SELECT 1 FROM ticketpro_release_037 r WHERE r.event_id=p.event_id AND r.seat_id=p.seat_id);
            """);q.ExecuteNonQuery();
    }
    public IReadOnlyList<TicketProBinding037> TicketProBindings037()
    {
        using var c=Open();using var q=Command(c,"SELECT event_id,source_url,fetched_at,place_count FROM ticketpro_show_037 ORDER BY event_id");using var r=q.ExecuteReader();var result=new List<TicketProBinding037>();while(r.Read())result.Add(new(r.GetInt64(0),r.GetString(1),DateTimeOffset.Parse(r.GetString(2),CultureInfo.InvariantCulture),r.GetInt32(3)));return result;
    }
    public void ApplyTicketPro037(long eventId,TicketProSnapshot037 snapshot)
    {
        TicketPro037.Validate(snapshot);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);RequireActiveEvent(c,tx,eventId);
        var local=Local029(c,tx);var show=local.SingleOrDefault(e=>e.Id==eventId)??throw new TicketException("Показ не найден.");
        // Explicit connection permits a local caption edit, but never another date/time or hall seat.
        if(show.At!=snapshot.StartsAt)throw new TicketException("Ticket PRO: дата сеанса не совпадает с выбранным показом.");
        var bound=false;
        using(var q=Command(c,"SELECT source_url,fetched_at FROM ticketpro_show_037 WHERE event_id=$e","$e",eventId))
        {
            q.Transaction=tx;using var r=q.ExecuteReader();bound=r.Read();if(bound&&(r.GetString(0)!=snapshot.SourceUrl||snapshot.FetchedAt<DateTimeOffset.Parse(r.GetString(1),CultureInfo.InvariantCulture)))throw new TicketException("Ticket PRO: другой сеанс или устаревшая схема. Предыдущие данные сохранены.");
        }
        if(!bound&&Title029(show.Title)!=Title029(snapshot.Title))throw new TicketException("Ticket PRO: название сеанса не совпадает с выбранным показом.");
        var source=snapshot.Places.Select(p=>new TicketlandSale028(show.Title,snapshot.StartsAt,p.Sector,p.Row,p.Seat,"PRO:"+p.Id,p.Price){Venue=show.Venue}).ToArray();
        var matches=Matches029(c,tx,source,[show],TicketlandBindings028(c,tx),TicketlandSectorBindings028(c,tx));
        var prepared=new List<(long Seat,TicketProPlace037 Place)>();var unique=new HashSet<long>();
        foreach(var pair in snapshot.Places.Zip(source))
        {
            var match=matches[pair.Second.Barcode];
            if(match.Event is null&&match.Seat is null)continue; // Source-only seats cannot alter any local seat.
            if(match.Event!=eventId||match.Seat is not long seat||!unique.Add(seat))throw new TicketException("Ticket PRO: не удалось однозначно сопоставить все места. Предыдущие данные сохранены.");
            prepared.Add((seat,pair.First));
        }
        if(unique.Count!=show.Seats.Count)throw new TicketException("Ticket PRO: число мест схемы не совпадает с залом программы. Предыдущие данные сохранены.");
        using(var q=Command(c,"DELETE FROM ticketpro_place_037 WHERE event_id=$e","$e",eventId)){q.Transaction=tx;q.ExecuteNonQuery();}
        foreach(var (seat,p) in prepared)
        {
            var state=TicketPro037.State(p.Status);
            using(var q=Command(c,"INSERT INTO ticketpro_place_037(event_id,seat_id,place_id,state,status,fund,price) VALUES($e,$s,$id,$state,$status,$fund,$price)","$e",eventId,"$s",seat,"$id",p.Id,"$state",state,"$status",p.Status,"$fund",p.Fund,"$price",p.Price.ToString(CultureInfo.InvariantCulture))){q.Transaction=tx;q.ExecuteNonQuery();}
            if(state!="occupied"){using var clear=Command(c,"DELETE FROM ticketpro_release_037 WHERE event_id=$e AND seat_id=$s","$e",eventId,"$s",seat);clear.Transaction=tx;clear.ExecuteNonQuery();}
        }
        using(var q=Command(c,"INSERT INTO ticketpro_show_037(event_id,source_url,fetched_at,place_count) VALUES($e,$url,$at,$count) ON CONFLICT(event_id) DO UPDATE SET source_url=excluded.source_url,fetched_at=excluded.fetched_at,place_count=excluded.place_count","$e",eventId,"$url",snapshot.SourceUrl,"$at",snapshot.FetchedAt.ToString("O",CultureInfo.InvariantCulture),"$count",snapshot.Places.Count)){q.Transaction=tx;q.ExecuteNonQuery();}
        tx.Commit();
    }
    public void ReleaseTicketPro037(long eventId,IReadOnlyCollection<long> ids)
    {
        if(ids.Count==0||ids.Distinct().Count()!=ids.Count)throw new TicketException("Выберите места без повторов.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);RequireActiveEvent(c,tx,eventId);
        foreach(var seat in ids)
        {
            using var q=Command(c,"SELECT count(*) FROM ticketpro_place_037 p WHERE p.event_id=$e AND p.seat_id=$s AND p.state='occupied' AND NOT EXISTS(SELECT 1 FROM ticket t WHERE t.event_id=$e AND t.seat_id=$s AND t.returned_at IS NULL)","$e",eventId,"$s",seat);q.Transaction=tx;
            if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new TicketException("Освободить можно только занятые непроданные и невыданные места. Изменения отменены целиком.");
            using var save=Command(c,"INSERT OR IGNORE INTO ticketpro_release_037(event_id,seat_id) VALUES($e,$s)","$e",eventId,"$s",seat);save.Transaction=tx;save.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
