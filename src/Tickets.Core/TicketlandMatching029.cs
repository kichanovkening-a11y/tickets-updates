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
    private static string Title029(string text)=>Regex.Replace(Text029(text),@"^(?:СПЕКТАКЛЬ|КОНЦЕРТ|МЮЗИКЛ|БАЛЕТ|ОПЕРА|ТЕАТРАЛЬНОЕ ПРЕДСТАВЛЕНИЕ)\s+","");
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
    private static List<TlShow029> Local029(SqliteConnection c,SqliteTransaction tx)
    {
        var result=new List<TlShow029>();
        var hiddenPerformances=new HashSet<string>(StringComparer.Ordinal);
        using(var q=Command(c,"SELECT entity_key FROM catalog_hidden WHERE kind='performance'"))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())hiddenPerformances.Add(r.GetString(0));}
        using(var q=Command(c,"SELECT e.id,e.venue_id,e.title,v.name,e.starts_at FROM event e JOIN venue v ON v.id=e.venue_id WHERE NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='venue' AND h.entity_key=CAST(e.venue_id AS TEXT))"))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())if(!hiddenPerformances.Contains(CatalogText.Key(r.GetString(2)))&&DateTime.TryParse(r.GetString(4),CultureInfo.InvariantCulture,DateTimeStyles.None,out var at))result.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),TicketlandAt028(at),[]));}
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
    private static TlMatch029 Match029(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,List<TlShow029> shows,Dictionary<(string Title,DateTime At),long> bindings,Dictionary<(string Title,DateTime At,string Sector,long Event),string> sectorBindings,string collision="")
    {
        var hasBinding=bindings.TryGetValue((sale.Title,sale.StartsAt),out var bound);
        var exact=shows.Where(s=>s.At==sale.StartsAt&&(hasBinding?s.Id==bound:Title029(s.Title)==Title029(sale.Title))&&(string.IsNullOrWhiteSpace(sale.Venue)||Venue029(s.Venue)==Venue029(sale.Venue))).ToArray();
        var general=string.IsNullOrWhiteSpace(sale.Row)||string.IsNullOrWhiteSpace(sale.Seat)||sale.Row=="--"||sale.Seat=="--";
        TlSeat029[] Seats(TlShow029 show,bool strict)
        {
            if(general)return [];
            var sector=hasBinding&&sectorBindings.TryGetValue((sale.Title,sale.StartsAt,sale.Sector,show.Id),out var mapped)?mapped:sale.Sector;
            var seats=show.Seats.Where(s=>CatalogText.Key(s.Row)==sale.Row&&CatalogText.Key(s.Number)==sale.Seat).ToArray();
            if(!strict)return seats.Length>0?seats:show.Seats.Where(s=>Sector029(s.Sector)==Sector029(sector)&&(CatalogText.Key(s.Row)==sale.Row||CatalogText.Key(s.Number)==sale.Seat)).ToArray();
            var literal=seats.Where(s=>CatalogText.Key(s.Sector)==sector).ToArray();
            return literal.Length>0?literal:seats.Where(s=>Sector029(s.Sector)==Sector029(sector)).ToArray();
        }
        if(exact.Length==1&&!general&&collision.Length==0)
        {var seats=Seats(exact[0],true);if(seats.Length==1)return new(exact[0].Id,seats[0].Id,null);}
        var plausible=shows.Where(s=>exact.Contains(s)||s.At==sale.StartsAt||Title029(s.Title)==Title029(sale.Title)||(!string.IsNullOrWhiteSpace(sale.Venue)&&Venue029(s.Venue)==Venue029(sale.Venue))).OrderBy(s=>s.Id).ToArray();
        var candidates=new List<TicketlandCandidate029>();
        foreach(var show in plausible)
        {
            var differences=new List<string>();
            if(collision.Length>0)differences.Add("разные исходные места претендуют на одно местное место");
            if(Title029(show.Title)!=Title029(sale.Title))differences.Add($"название: «{sale.Title}» → «{show.Title}»");
            if(show.At.Date!=sale.StartsAt.Date)differences.Add($"дата: {sale.StartsAt:dd.MM.yyyy} → {show.At:dd.MM.yyyy}");
            if(show.At.TimeOfDay!=sale.StartsAt.TimeOfDay)differences.Add($"время: {sale.StartsAt:HH:mm:ss} → {show.At:HH:mm:ss}");
            if(!string.IsNullOrWhiteSpace(sale.Venue)&&Venue029(show.Venue)!=Venue029(sale.Venue))differences.Add($"площадка: «{sale.Venue}» → «{show.Venue}»");
            var seats=Seats(show,false);
            if(general)differences.Add("ряд или место отсутствуют; конкретное место не определено");
            else if(seats.Length==0)differences.Add($"ряд {sale.Row}, место {sale.Seat}: отсутствуют в зале");
            if(exact.Length>1)differences.Add("показ неоднозначен");
            if(seats.Length==0)candidates.Add(new(show.Id,null,$"{show.Title}, {show.At:dd.MM.yyyy HH:mm}, {show.Venue}",string.Join("; ",differences)));
            foreach(var seat in seats)
            {
                var d=new List<string>(differences);
                if(CatalogText.Key(seat.Row)!=sale.Row)d.Add($"ряд: «{sale.Row}» → «{seat.Row}»");
                if(CatalogText.Key(seat.Number)!=sale.Seat)d.Add($"место: «{sale.Seat}» → «{seat.Number}»");
                if(Sector029(seat.Sector)!=Sector029(sale.Sector))d.Add($"сектор: «{sale.Sector}» → «{seat.Sector}»");
                if(seats.Length>1)d.Add("ряд и место встречаются в нескольких секторах");
                candidates.Add(new(show.Id,seat.Id,$"{show.Title}, {show.At:dd.MM.yyyy HH:mm}, {show.Venue}; {seat.Sector}, ряд {seat.Row}, место {seat.Number}",string.Join("; ",d)));
            }
        }
        var local=string.Join("\n",plausible.Select(s=>$"{s.Id}|{s.Title}|{s.Venue}|{s.At:O}|"+string.Join("/",s.Seats.OrderBy(x=>x.Id).Select(x=>$"{x.Id}:{x.Sector}:{x.Row}:{x.Number}"))));
        var key=Hash029($"{sale.Barcode}|{sale.Title}|{sale.StartsAt:O}|{sale.Venue}|{sale.Sector}|{sale.Row}|{sale.Seat}|{sale.Nominal.ToString(CultureInfo.InvariantCulture)}|{local}|{collision}");
        using(var q=Command(c,"SELECT rejected,event_id,seat_id FROM ticketland_decision_029 WHERE issue_key=$k","$k",key))
        {
            q.Transaction=tx;using var r=q.ExecuteReader();if(r.Read())
            {
                if(r.GetInt64(0)!=0)return new(null,null,null);
                var evt=r.GetInt64(1);long? seat=r.IsDBNull(2)?null:r.GetInt64(2);
                if(candidates.Any(x=>x.EventId==evt&&x.SeatId==seat))return new(evt,seat,null);
            }
        }
        var reason=collision.Length>0?"Разные исходные места нельзя автоматически объединить в одно местное место.":general?"Не указаны ряд или место.":exact.Length>1?"Несколько показов соответствуют названию, дате, времени и площадке.":exact.Length==1?"Сектор, ряд и место не определяют одно местное место.":"Нет единственного показа с совпадающими названием, датой, временем и площадкой.";
        return new(null,null,new(key,$"{sale.Title}, {sale.StartsAt:dd.MM.yyyy HH:mm:ss}; {sale.Venue}; {sale.Sector}, ряд {sale.Row}, место {sale.Seat}; {sale.Barcode}{(string.IsNullOrWhiteSpace(sale.SourceChannel)?"":" · "+sale.SourceChannel)}",reason,candidates));
    }
    private static Dictionary<string,TlMatch029> Matches029(SqliteConnection c,SqliteTransaction tx,IReadOnlyCollection<TicketlandSale028> sales,List<TlShow029> shows,Dictionary<(string Title,DateTime At),long> bindings,Dictionary<(string Title,DateTime At,string Sector,long Event),string> sectors)
    {
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
        return Matches029(c,tx,report.Sales.ToArray(),shows,bindings,sectors).Values.Select(s=>s.Issue).Where(x=>x is not null).Cast<TicketlandIssue029>().ToArray();
    }
    public void AcceptTicketlandIssue029(string key,long eventId,long? seatId)
    {
        var issue=TicketlandIssues029().SingleOrDefault(x=>x.Key==key);
        if(issue is null||!issue.Candidates.Any(x=>x.EventId==eventId&&x.SeatId==seatId))throw new TicketException("Предложение изменилось. Обновите список расхождений Ticketland.");
        using var c=Open();using var q=Command(c,"INSERT OR REPLACE INTO ticketland_decision_029(issue_key,rejected,event_id,seat_id) VALUES($k,0,$e,$s)","$k",key,"$e",eventId,"$s",(object?)seatId??DBNull.Value);q.ExecuteNonQuery();
    }
    public void RejectTicketlandIssue029(string key)
    {
        if(!TicketlandIssues029().Any(x=>x.Key==key))throw new TicketException("Предложение изменилось. Обновите список расхождений Ticketland.");
        using var c=Open();using var q=Command(c,"INSERT OR REPLACE INTO ticketland_decision_029(issue_key,rejected,event_id,seat_id) VALUES($k,1,NULL,NULL)","$k",key);q.ExecuteNonQuery();
    }
}
