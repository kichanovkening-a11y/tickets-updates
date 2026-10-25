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
    private sealed record TlShow029(long Id,long VenueId,string Title,string Venue,DateTime At,List<TlSeat029> Seats)
    {
        public List<string> SectorNames {get;}=[];
        public string TitleKey {get;}=Title029(Title);
        public string VenueKey {get;}=Venue029(Venue);
        public string? SeatFingerprint {get;set;}
        public Dictionary<string,string> SectorKeys {get;}=new(StringComparer.Ordinal);
        public Dictionary<string,string> LegacySectorKeys {get;}=new(StringComparer.Ordinal);
    }
    private sealed record TlMatch029(long? Event,long? Seat,TicketlandIssue029? Issue,bool Retained=false);
    private static string Text029(string text)=>Regex.Replace((text??"").Normalize().ToUpperInvariant().Replace('Ё','Е'),@"[^\p{L}\p{Nd}]+"," ").Trim();
    private static string Title029(string text)
    {
        var title=Regex.Replace(Text029(text),@"^(?:(?:ПРЕМЬЕРА(?: СПЕКТАКЛЯ)?|СПЕКТАКЛЬ|КОНЦЕРТ|МЮЗИКЛ|БАЛЕТ|ОПЕРА|ТЕАТРАЛЬНОЕ ПРЕДСТАВЛЕНИЕ)\s+)+","");
        // An observed presentation descriptor, at a boundary only. Other words remain identity.
        title=Regex.Replace(title,@"^НОВОГОДНЯЯ ПРОГРАММА(?: |$)|(?:^| )НОВОГОДНЯЯ ПРОГРАММА$","").Trim();
        return string.Join(" ",title.Split(' ',StringSplitOptions.RemoveEmptyEntries).OrderBy(x=>x,StringComparer.Ordinal));
    }
    // These pairs are observed Ticketland HallBuildingName values and local catalogue names.
    // Keep stage and address distinctions outside this explicit list intact.
    private static string Venue029(string text)=>Text029(text) switch
    {
        "ТЕАТР ЭСТРАДЫ" or "МОСКОВСКИЙ ГОСУДАРСТВЕННЫЙ ТЕАТР ЭСТРАДЫ"=>"ТЕАТР ЭСТРАДЫ",
        "ТЕАТР РУССКАЯ ПЕСНЯ" or "ТЕАТР РУССКАЯ ПЕСНЯ ОЛИМПИЙСКИЙ ПР Д 14"=>"ТЕАТР РУССКАЯ ПЕСНЯ ОСНОВНАЯ СЦЕНА",
        "ЦЕНТРАЛЬНЫЙ ДОМ КУЛЬТУРЫ ЖЕЛЕЗНОДОРОЖНИКОВ" or "ТЕАТРАЛЬНО КОНЦЕРТНЫЙ ЗАЛ ЦДКЖ"=>"ЦДКЖ",
        var key=>key
    };
    // Only recognized grammatical forms are folded. Unknown words remain part of the
    // identity, so word-order normalization cannot erase a level, number or qualifier.
    private static string SectorIdentity031(string text)
    {
        var separated=Regex.Replace(Text029(text),@"\b(VIP|ВИП|БАЛКОН|ПАРТЕР|АМФИТЕАТР|БЕЛЬЭТАЖ|ЛОЖА)(\d+)\b","$1 $2");
        var key=Regex.Replace(separated,@"\bБЕЛЬ ЭТАЖ(?:А|Е)?\b","БЕЛЬЭТАЖ");
        key=Regex.Replace(key,@"\b(\d+) (?:Й|Я|ОЙ)\b","$1");
        var tokens=key.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(word=>word switch
        {
            "ЛОЖА" or "ЛОЖИ" or "ЛОЖУ" or "ЛОЖЕ"=>"ЛОЖА",
            "ПАРТЕР" or "ПАРТЕРА" or "ПАРТЕРЕ"=>"ПАРТЕР",
            "АМФИТЕАТР" or "АМФИТЕАТРА" or "АМФИТЕАТРЕ"=>"АМФИТЕАТР",
            "БЕЛЬЭТАЖ" or "БЕЛЬЭТАЖА" or "БЕЛЬЭТАЖЕ"=>"БЕЛЬЭТАЖ",
            "БАЛКОН" or "БАЛКОНА" or "БАЛКОНЕ"=>"БАЛКОН",
            "VIP" or "ВИП"=>"VIP",
            "ВТОРОЙ" or "ВТОРОГО" or "ВТОРАЯ"=>"2",
            "ПЕРВЫЙ" or "ПЕРВОГО" or "ПЕРВАЯ"=>"1",
            "СЛЕВА" or "ЛЕВ" or "ЛЕВЫЙ" or "ЛЕВАЯ" or "ЛЕВОЕ" or "ЛЕВОЙ" or "ЛЕВОГО" or "ЛЕВУЮ"=>"ЛЕВЫЙ",
            "СПРАВА" or "ПРАВ" or "ПРАВЫЙ" or "ПРАВАЯ" or "ПРАВОЕ" or "ПРАВОЙ" or "ПРАВОГО" or "ПРАВУЮ"=>"ПРАВЫЙ",
            "ЦЕНТР" or "ЦЕНТРАЛЬНЫЙ" or "ЦЕНТРАЛЬНАЯ" or "ЦЕНТРАЛЬНОЙ" or "СЕРЕДИНА" or "СРЕДНИЙ" or "СРЕДНЯЯ"=>"ЦЕНТР",
            _=>word
        }).ToList();
        if(tokens.Any(t=>t is "ЛЕВЫЙ" or "ПРАВЫЙ" or "ЦЕНТР"))tokens.RemoveAll(t=>t is "СТОРОНА" or "СТОРОНЫ" or "ЧАСТЬ" or "ЧАСТИ");
        // Short hall captions “Левая ложа” denote the same parterre box.
        // Keep бельэтаж and every other explicit level in the identity.
        if(tokens.Contains("ЛОЖА"))tokens.RemoveAll(t=>t=="ПАРТЕР");
        return string.Join(" ",tokens.OrderBy(t=>t,StringComparer.Ordinal));
    }
    private static string Sector029(string text)
    {
        var identity=SectorIdentity031(text);var tokens=identity.Split(' ');
        // These three sector families have combined sections. Keep the established fallback,
        // but never combine box/balcony identities or two explicitly opposite sides.
        var family=tokens.FirstOrDefault(t=>t is "ПАРТЕР" or "АМФИТЕАТР" or "БЕЛЬЭТАЖ");
        return family is not null&&tokens.All(t=>t==family||t is "ЛЕВЫЙ" or "ПРАВЫЙ" or "ЦЕНТР")?family:identity;
    }
    private static bool CompatibleSector031(string source,string local)
    {
        var a=SectorIdentity031(source).Split(' ');var b=SectorIdentity031(local).Split(' ');
        static string Family(string[] words)=>string.Join(" ",words.Where(t=>t is "ЛОЖА" or "ПАРТЕР" or "АМФИТЕАТР" or "БЕЛЬЭТАЖ" or "БАЛКОН" or "VIP"));
        var fa=Family(a);var fb=Family(b);
        if(fa.Length>0&&fb.Length>0&&fa!=fb)return false;
        static string Side(string[] words)=>string.Join(" ",words.Where(t=>t is "ЛЕВЫЙ" or "ПРАВЫЙ" or "ЦЕНТР"));
        var sa=Side(a);var sb=Side(b);
        if(sa.Length>0&&sb.Length>0&&sa!=sb)return false;
        static string Numbers(string[] words)=>string.Join(" ",words.Where(t=>t.All(char.IsDigit)));
        var na=Numbers(a);var nb=Numbers(b);
        return !(na.Length>0&&nb.Length>0&&na!=nb);
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
        var sectorNames=new Dictionary<long,List<string>>();
        foreach(var show in result)
        {
            if(!byVenue.TryGetValue(show.VenueId,out var seats))
            {
                seats=[];byVenue[show.VenueId]=seats;
                var names=new List<string>();sectorNames[show.VenueId]=names;
                using(var namesQuery=Command(c,"SELECT z.name FROM sector z WHERE z.venue_id=$v AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(z.venue_id AS TEXT)||':'||CAST(z.id AS TEXT))","$v",show.VenueId))
                {namesQuery.Transaction=tx;using var namesReader=namesQuery.ExecuteReader();while(namesReader.Read())names.Add(namesReader.GetString(0));}
                using var q=Command(c,"SELECT s.id,z.name,s.row_label,s.number FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat h WHERE h.seat_id=s.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))","$v",show.VenueId);
                q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())seats.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3)));
            }
            show.Seats.AddRange(seats);show.SectorNames.AddRange(sectorNames[show.VenueId]);
        }
        return result;
    }
    private static string Hash029(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static TlShow029[] Exact030(TicketlandSale028 sale,List<TlShow029> shows)
    {
        if(string.IsNullOrWhiteSpace(sale.Title)||sale.StartsAt==default||string.IsNullOrWhiteSpace(sale.Venue))return [];
        var title=Title029(sale.Title);var venue=Venue029(sale.Venue);
        return shows.Where(s=>s.TitleKey==title)
            .Where(s=>s.At.Date==sale.StartsAt.Date)
            .Where(s=>s.At.TimeOfDay==sale.StartsAt.TimeOfDay)
            .Where(s=>s.VenueKey==venue).ToArray();
    }
    private static bool General030(TicketlandSale028 sale)=>string.IsNullOrWhiteSpace(sale.Row)||string.IsNullOrWhiteSpace(sale.Seat)||sale.Row=="--"||sale.Seat=="--";
    // Every TlShow029 belongs to one fresh Local029 snapshot. Its hall fingerprint
    // can be reused across source barcodes without surviving a hall edit or restart.
    private static string SeatFingerprint032(TlShow029 show)=>show.SeatFingerprint??=string.Join("/",show.Seats.OrderBy(s=>s.Id).Select(s=>$"{s.Id}:{s.Sector}:{s.Row}:{s.Number}"));
    private static string SectorKey030(TicketlandSale028 sale,TlShow029 show)
    {
        var source=Text029(sale.Sector);if(show.SectorKeys.TryGetValue(source,out var key))return key;
        return show.SectorKeys[source]=Hash029($"{show.Id}|{show.TitleKey}|{show.At:O}|{show.VenueKey}|{source}|"+SeatFingerprint032(show));
    }
    private static void EnsureDecisions030(SqliteConnection c,SqliteTransaction tx)
    {
        using var q=Command(c,"CREATE TABLE IF NOT EXISTS ticketland_sector_decision_030(issue_key TEXT PRIMARY KEY,rejected INTEGER NOT NULL,local_sector TEXT); CREATE TABLE IF NOT EXISTS ticketland_match_proof_032(barcode TEXT PRIMARY KEY,event_id INTEGER NOT NULL REFERENCES event(id) ON DELETE CASCADE,seat_id INTEGER NOT NULL REFERENCES seat(id) ON DELETE CASCADE,sector_identity TEXT NOT NULL DEFAULT ''); CREATE TABLE IF NOT EXISTS ticketland_sector_identity_032(event_id INTEGER NOT NULL REFERENCES event(id) ON DELETE CASCADE,show_identity TEXT NOT NULL,source_sector TEXT NOT NULL,sector_identity TEXT NOT NULL,PRIMARY KEY(event_id,show_identity,source_sector))");q.Transaction=tx;q.ExecuteNonQuery();
        var columns=new HashSet<string>();using(var info=Command(c,"PRAGMA table_info(ticketland_match_proof_032)")){info.Transaction=tx;using var r=info.ExecuteReader();while(r.Read())columns.Add(r.GetString(1));}
        if(!columns.Contains("sector_identity")){using var add=Command(c,"ALTER TABLE ticketland_match_proof_032 ADD COLUMN sector_identity TEXT NOT NULL DEFAULT ''");add.Transaction=tx;add.ExecuteNonQuery();}
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
        var show=exact[0];var key=SectorKey030(sale,show);var decision=SectorDecision032(c,tx,sale,show);
        if(decision.Rejected)return new(null,null,null);
        var resolution=ResolveSector032(sale.Sector,show);
        // A saved explicit approval supplies name knowledge; current explicit family,
        // side and number guards still apply. Positions never decide the sector.
        var sector=ApprovedSector032(sale.Sector,show,decision.Sector)??resolution.Sector;
        // Contextual abbreviations retain their originally resolved family, side and
        // number even if a later hall caption would create another unique interpretation.
        if(sector is not null&&PriorSectorIdentity032(c,tx,sale,show) is string priorIdentity&&priorIdentity.Length>0&&!CompatibleSector031(priorIdentity,sector))
            return new(null,null,new(key,$"{show.Title}; {sale.Sector}",$"Сектор «{sale.Sector}» → «{sector}»: тип, сторона или номер не соответствует ранее установленному сектору «{priorIdentity}». Перенос места запрещен.",[]));
        var positions=sector is null?[]:show.Seats.Where(s=>s.Sector==sector&&CatalogText.Key(s.Row)==CatalogText.Key(sale.Row)&&CatalogText.Key(s.Number)==CatalogText.Key(sale.Seat)).ToArray();
        if(positions.Length==1&&collision.Length==0)return new(show.Id,positions[0].Id,null);
        var reason=collision.Length>0?$"Сектор «{sale.Sector}»: место занято другим исходным сектором. Разные исходные места нельзя объединить в одно местное место.":sector is null?resolution.Reason:MissingPosition032(show,sector,sale);
        var candidates=new List<TicketlandCandidate029>();
        if(collision.Length==0&&sector is null&&resolution.Aliases.Count==1)
        foreach(var candidate in show.Seats.Where(s=>s.Sector==resolution.Aliases[0]&&CatalogText.Key(s.Row)==CatalogText.Key(sale.Row)&&CatalogText.Key(s.Number)==CatalogText.Key(sale.Seat)))
            candidates.Add(new(show.Id,candidate.Id,$"{show.Title}, {show.At:dd.MM.yyyy HH:mm}, {show.Venue}; {candidate.Sector}, ряд {candidate.Row}, место {candidate.Number}","сектор: «"+sale.Sector+"» → «"+candidate.Sector+"»"));
        var issue=new TicketlandIssue029(key,$"{show.Title}, {show.At:dd.MM.yyyy HH:mm}; {show.Venue}; {sale.Sector}",reason,candidates);
        // Retained known markers participate in the same collision pass as fresh matches.
        if(collision.Length==0&&RetainedSeat032(c,tx,sale,show,resolution) is TlSeat029 retained)return new(show.Id,retained.Id,null,true);
        return new(null,null,issue);
    }

    private static Dictionary<string,TlMatch029> Matches029(SqliteConnection c,SqliteTransaction tx,IReadOnlyCollection<TicketlandSale028> sales,List<TlShow029> shows,Dictionary<(string Title,DateTime At),long> bindings,Dictionary<(string Title,DateTime At,string Sector,long Event),string> sectors)
    {
        EnsureDecisions030(c,tx);
        var result=sales.ToDictionary(s=>s.Barcode,s=>Match029(c,tx,s,shows,bindings,sectors));
        foreach(var group in sales.Where(s=>result[s.Barcode].Event.HasValue&&result[s.Barcode].Seat.HasValue).GroupBy(s=>(result[s.Barcode].Event,result[s.Barcode].Seat)))
        {
            var identities=group.Select(s=>$"{SourceSectorIdentity032(c,tx,s,shows.Single(x=>x.Id==result[s.Barcode].Event))}|{CatalogText.Key(s.Row)}|{CatalogText.Key(s.Seat)}").Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
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
