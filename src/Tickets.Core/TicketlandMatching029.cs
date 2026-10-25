using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
namespace Tickets.Core;

public sealed record TicketlandCandidate029(long EventId,long? SeatId,string Caption,string Differences);
public sealed record TicketlandIssue029(string Key,string Source,string Reason,IReadOnlyList<TicketlandCandidate029> Candidates);

public sealed partial class Store
{
    private sealed record TlSeat029(long Id,string Sector,string Row,string Number);
    private sealed record TlShow029(long Id,long VenueId,string Title,string Venue,DateTime At,List<TlSeat029> Seats);
    private sealed record TlMatch029(long? Event,long? Seat,TicketlandIssue029? Issue);
    private static string Text029(string text)=>Regex.Replace((text??"").Normalize().ToUpperInvariant().Replace('Ё','Е'),@"[^\p{L}\p{Nd}]+"," ").Trim();
    private static string Title029(string text)=>Regex.Replace(Text029(text),@"^(?:(?:ПРЕМЬЕРА(?: СПЕКТАКЛЯ)?|СПЕКТАКЛЬ|КОНЦЕРТ|МЮЗИКЛ|БАЛЕТ|ОПЕРА|ТЕАТРАЛЬНОЕ ПРЕДСТАВЛЕНИЕ)\s+)+","");
    // These pairs are observed Ticketland HallBuildingName values and local catalogue names.
    // Keep stage and address distinctions outside this explicit list intact.
    private static string Venue029(string text)=>Text029(text) switch
    {
        "ТЕАТР ЭСТРАДЫ" or "МОСКОВСКИЙ ГОСУДАРСТВЕННЫЙ ТЕАТР ЭСТРАДЫ"=>"ТЕАТР ЭСТРАДЫ",
        "ТЕАТР РУССКАЯ ПЕСНЯ" or "ТЕАТР РУССКАЯ ПЕСНЯ ОЛИМПИЙСКИЙ ПР Д 14"=>"ТЕАТР РУССКАЯ ПЕСНЯ ОСНОВНАЯ СЦЕНА",
        "ЦЕНТРАЛЬНЫЙ ДОМ КУЛЬТУРЫ ЖЕЛЕЗНОДОРОЖНИКОВ" or "ТЕАТРАЛЬНО КОНЦЕРТНЫЙ ЗАЛ ЦДКЖ"=>"ЦДКЖ",
        var key=>key
    };
    private static string Sector029(string text)
    {
        var key=Text029(text);
        var family=Regex.Match(key,@"^(ПАРТЕР|АМФИТЕАТР|БЕЛЬ ?ЭТАЖ)(?: (?:ЛЕВЫЙ|ЛЕВАЯ|ЛЕВ|ПРАВЫЙ|ПРАВАЯ|ПРАВ|ЦЕНТР|ЦЕНТРАЛЬНЫЙ|ЦЕНТРАЛЬНАЯ|СЕРЕДИНА|СРЕДНИЙ|СРЕДНЯЯ)(?: СТОРОНА| ЧАСТЬ)?)?$");
        if(family.Success)return family.Groups[1].Value.Replace(" ","");
        if(key.Contains("БАЛКОН"))
        {
            var number=Regex.Match(key,@"\d+").Value;
            var side=key.Contains("ЛЕВ")?"ЛЕВЫЙ":key.Contains("ПРАВ")?"ПРАВЫЙ":key.Contains("ЦЕНТР")?"ЦЕНТР":"";
            // Keep unknown qualifiers rather than equating unrelated balcony sections.
            var rest=Regex.Replace(key,@"БАЛКОН|\d+|ЛЕВЫЙ|ЛЕВАЯ|ПРАВЫЙ|ПРАВАЯ|ЦЕНТРАЛЬНЫЙ|ЦЕНТР|СТОРОНА|\bЙ\b","").Trim();
            if(rest.Length==0)return "БАЛКОН "+number+" "+side;
        }
        return key;
    }
    private static List<TlShow029> Local029(SqliteConnection c,SqliteTransaction tx,bool loadSeats=true)
    {
        var result=new List<TlShow029>();
        var hiddenPerformances=new HashSet<string>(StringComparer.Ordinal);
        using(var q=Command(c,"SELECT entity_key FROM catalog_hidden WHERE kind='performance'"))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())hiddenPerformances.Add(r.GetString(0));}
        using(var q=Command(c,"SELECT e.id,e.venue_id,e.title,v.name,e.starts_at FROM event e JOIN venue v ON v.id=e.venue_id WHERE NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='venue' AND h.entity_key=CAST(e.venue_id AS TEXT))"))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())if(!hiddenPerformances.Contains(CatalogText.Key(r.GetString(2)))&&DateTime.TryParse(r.GetString(4),CultureInfo.InvariantCulture,DateTimeStyles.None,out var at))result.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),TicketlandAt028(at),[]));}
        if(!loadSeats)return result;
        var byVenue=new Dictionary<long,List<TlSeat029>>();
        foreach(var show in result)
        {
            if(!byVenue.TryGetValue(show.VenueId,out var seats))
            {
                seats=[];byVenue[show.VenueId]=seats;
                using var q=Command(c,"SELECT s.id,z.name,s.row_label,s.number FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat h WHERE h.seat_id=s.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))","$v",show.VenueId);
                q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())seats.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3)));
            }
            show.Seats.AddRange(seats);
        }
        return result;
    }
    private static string Hash029(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static TlShow029[] Exact030(TicketlandSale028 sale,List<TlShow029> shows)=>string.IsNullOrWhiteSpace(sale.Venue)?[]:shows.Where(s=>s.At==sale.StartsAt&&Title029(s.Title)==Title029(sale.Title)&&Venue029(s.Venue)==Venue029(sale.Venue)).ToArray();
    private static bool General030(TicketlandSale028 sale)=>string.IsNullOrWhiteSpace(sale.Row)||string.IsNullOrWhiteSpace(sale.Seat)||sale.Row=="--"||sale.Seat=="--";
    private static string SectorKey030(TicketlandSale028 sale,TlShow029 show)=>Hash029($"{show.Id}|{Title029(show.Title)}|{show.At:O}|{Venue029(show.Venue)}|{Text029(sale.Sector)}|"+string.Join("/",show.Seats.OrderBy(s=>s.Id).Select(s=>$"{s.Id}:{s.Sector}:{s.Row}:{s.Number}")));
    private static void EnsureDecisions030(SqliteConnection c,SqliteTransaction tx)
    {
        using var q=Command(c,"CREATE TABLE IF NOT EXISTS ticketland_sector_decision_030(issue_key TEXT PRIMARY KEY,rejected INTEGER NOT NULL,local_sector TEXT)");q.Transaction=tx;q.ExecuteNonQuery();
    }
    private static (bool Rejected,string? Sector) Decision030(SqliteConnection c,SqliteTransaction tx,string key)
    {
        using var q=Command(c,"SELECT rejected,local_sector FROM ticketland_sector_decision_030 WHERE issue_key=$k","$k",key);q.Transaction=tx;using var r=q.ExecuteReader();return r.Read()?(r.GetInt64(0)!=0,r.IsDBNull(1)?null:r.GetString(1)):(false,null);
    }
    private static TlMatch029 Match029(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,List<TlShow029> shows,Dictionary<(string Title,DateTime At),long> bindings,Dictionary<(string Title,DateTime At,string Sector,long Event),string> sectorBindings,string collision="")
    {
        // Legacy show/seat approvals cannot override the four source show identifiers.
        var exact=Exact030(sale,shows);
        if(exact.Length!=1||General030(sale))return new(null,null,null);
        var show=exact[0];var key=SectorKey030(sale,show);var decision=Decision030(c,tx,key);
        if(decision.Rejected)return new(null,null,null);
        var sector=decision.Sector??sale.Sector;
        var positions=show.Seats.Where(s=>CatalogText.Key(s.Row)==CatalogText.Key(sale.Row)&&CatalogText.Key(s.Number)==CatalogText.Key(sale.Seat)).ToArray();
        var seats=positions.Where(s=>Text029(s.Sector)==Text029(sector)).ToArray();
        if(seats.Length==0&&decision.Sector is null)seats=positions.Where(s=>Sector029(s.Sector)==Sector029(sector)).ToArray();
        if(seats.Length==1&&collision.Length==0)return new(show.Id,seats[0].Id,null);
        var reason=collision.Length>0?"Разные исходные места нельзя объединить в одно местное место.":positions.Length==0?"Исходные ряд и место отсутствуют в местном зале. Изменение номера ряда или места запрещено.":"Требуется подтверждение названия сектора в этом показе.";
        // Compatibility view exposes only exact row/seat identities, never renumbering guesses.
        var candidates=collision.Length>0?new List<TicketlandCandidate029>():positions.Select(s=>new TicketlandCandidate029(show.Id,s.Id,$"{show.Title}, {show.At:dd.MM.yyyy HH:mm}, {show.Venue}; {s.Sector}, ряд {s.Row}, место {s.Number}","сектор: «"+sale.Sector+"» → «"+s.Sector+"»")).ToList();
        return new(null,null,new(key,$"{show.Title}, {show.At:dd.MM.yyyy HH:mm}; {show.Venue}; {sale.Sector}",reason,candidates));
    }
    private static Dictionary<string,TlMatch029> Matches029(SqliteConnection c,SqliteTransaction tx,IReadOnlyCollection<TicketlandSale028> sales,List<TlShow029> shows,Dictionary<(string Title,DateTime At),long> bindings,Dictionary<(string Title,DateTime At,string Sector,long Event),string> sectors)
    {
        EnsureDecisions030(c,tx);
        var result=sales.ToDictionary(s=>s.Barcode,s=>Match029(c,tx,s,shows,bindings,sectors));
        foreach(var group in sales.Where(s=>result[s.Barcode].Event.HasValue&&result[s.Barcode].Seat.HasValue).GroupBy(s=>(result[s.Barcode].Event,result[s.Barcode].Seat)))
        {
            var identities=group.Select(s=>$"{s.Sector}|{s.Row}|{s.Seat}").Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(identities.Length<2)continue;
            var collision=string.Join("/",identities);
            foreach(var sale in group)result[sale.Barcode]=Match029(c,tx,sale,shows,bindings,sectors,collision);
        }
        return result;
    }
    public IReadOnlyList<TicketlandIssue029> TicketlandIssues029()
    {
        var report=CachedTicketlandReport028();using var c=Open();using var tx=c.BeginTransaction();var shows=Local029(c,tx);var bindings=TicketlandBindings028(c,tx);var sectors=TicketlandSectorBindings028(c,tx);
        return Matches029(c,tx,report.Sales.ToArray(),shows,bindings,sectors).Values.Select(s=>s.Issue).Where(x=>x is not null).Cast<TicketlandIssue029>().DistinctBy(x=>x.Key).ToArray();
    }
    public void AcceptTicketlandIssue029(string key,long eventId,long? seatId)
    {
        var group=TicketlandSectorReviews030().SingleOrDefault(x=>x.Key==key&&x.EventId==eventId);
        if(group is null||!seatId.HasValue)throw new TicketException("Подтверждать можно только название сектора точного показа.");
        using var c=Open();using var q=Command(c,"SELECT z.name FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.id=$id","$id",seatId.Value);
        if(q.ExecuteScalar() is not string sector)throw new TicketException("Место отсутствует в зале.");
        AcceptTicketlandSector030(key,sector);
    }
    public void RejectTicketlandIssue029(string key)=>RejectTicketlandSector030(key);
}
