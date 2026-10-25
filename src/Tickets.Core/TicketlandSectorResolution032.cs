using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
namespace Tickets.Core;

public sealed partial class Store
{
    private sealed record TlSectorResolution032(string? Sector,IReadOnlyList<string> Aliases,string Reason);
    // All decisions here use names alone. Callers may inspect rows/seats only afterwards.
    private static TlSectorResolution032 ResolveSector032(string source,TlShow029 show)
    {
        var names=show.SectorNames.Distinct(StringComparer.Ordinal).ToArray();
        var identity=SectorIdentity031(source);
        var exact=names.Where(n=>SectorIdentity031(n)==identity).ToArray();
        TlSectorResolution032 Resolved(string[] matches)=>matches.Length==1?new(matches[0],[],""):new(null,[],$"Сектор «{source}»: название неоднозначно; подходят {string.Join(", ",matches.Select(x=>$"«{x}»"))}. Сначала требуется однозначное название сектора.");
        if(exact.Length>0)return Resolved(exact);
        var words=identity.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        static bool Type(string x)=>x is "ПАРТЕР" or "АМФИТЕАТР" or "БЕЛЬЭТАЖ" or "БАЛКОН" or "ЛОЖА" or "VIP";
        static bool Side(string x)=>x is "ЛЕВЫЙ" or "ПРАВЫЙ" or "ЦЕНТР";
        var family=words.Where(Type).ToArray();var sides=words.Where(Side).ToArray();var numbers=words.Where(w=>w.All(char.IsDigit)).ToArray();
        // A combined family is an actual named local sector, never a pool of split sectors.
        if(family.Length==1&&family[0] is "ПАРТЕР" or "АМФИТЕАТР" or "БЕЛЬЭТАЖ"&&words.All(w=>Type(w)||Side(w)))
        {
            var combined=names.Where(n=>SectorIdentity031(n)==family[0]).ToArray();
            if(combined.Length>0)return Resolved(combined);
        }
        bool SameExplicit(string name)
        {
            var local=SectorIdentity031(name).Split(' ',StringSplitOptions.RemoveEmptyEntries);
            return CompatibleSector031(source,name)&&family.All(local.Contains)&&sides.All(local.Contains)&&numbers.All(local.Contains);
        }
        // Side + number can abbreviate a known hall sector only when the hall names
        // provide exactly one interpretation, independent of the sold position.
        var meaningful=family.Length>0||sides.Length>0&&numbers.Length>0&&words.All(w=>Side(w)||w.All(char.IsDigit));
        var possible=meaningful?names.Where(SameExplicit).ToArray():[];
        if(possible.Length>1)return Resolved(possible);
        if(family.Length==0&&sides.Length>0&&numbers.Length>0&&words.All(w=>Side(w)||w.All(char.IsDigit))&&possible.Length==1)return Resolved(possible);
        var reason=$"Сектор «{source}»: соответствующее название отсутствует в местном зале.";
        return new(null,possible,reason);
    }
    private static TlSeat029? RetainedSeat032(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,TlShow029 show,TlSectorResolution032 resolution)
    {
        if(resolution.Aliases.Count>1||resolution.Reason.Contains("неоднознач",StringComparison.OrdinalIgnoreCase))return null;
        long seatId;string original;
        using(var q=Command(c,"SELECT p.seat_id,p.sector_identity FROM ticketland_match_proof_032 p JOIN ticketland_sale_028 old ON old.barcode=p.barcode WHERE p.barcode=$b AND p.event_id=$e","$b",sale.Barcode,"$e",show.Id))
        {
            q.Transaction=tx;using var r=q.ExecuteReader();if(!r.Read()||r.GetString(1).Length==0)return null;
            seatId=r.GetInt64(0);original=r.GetString(1);
        }
        var seat=show.Seats.SingleOrDefault(s=>s.Id==seatId);
        var known=PriorSectorIdentity032(c,tx,sale,show)??original;
        return seat is not null&&(resolution.Sector is null||resolution.Sector==seat.Sector)&&CompatibleSector031(sale.Sector,seat.Sector)&&CompatibleSector031(original,seat.Sector)&&CompatibleSector031(known,seat.Sector)?seat:null;
    }

    private static string ShowIdentity032(TlShow029 show)=>$"{show.TitleKey}|{show.At:O}|{show.VenueKey}";
    private static string MergeSectorIdentity032(string? known,string current)=>string.Join(" ",((known??"")+" "+current).Split(' ',StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal));
    private static string? PriorSectorIdentity032(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,TlShow029 show)
    {
        string? known;
        using(var q=Command(c,"SELECT sector_identity FROM ticketland_sector_identity_032 WHERE event_id=$e AND show_identity=$show AND source_sector=$source","$e",show.Id,"$show",ShowIdentity032(show),"$source",SectorIdentity031(sale.Sector)))
        {q.Transaction=tx;known=q.ExecuteScalar() as string;}
        using(var q=Command(c,"SELECT sector_identity FROM ticketland_match_proof_032 WHERE barcode=$b AND event_id=$e","$b",sale.Barcode,"$e",show.Id))
        {q.Transaction=tx;if(q.ExecuteScalar() is string proof)known=MergeSectorIdentity032(known,proof);}
        return known;
    }
    private static void RememberSector032(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,TlShow029 show,TlSeat029 seat)
    {
        // Preserve semantic name knowledge through returns and weaker local captions.
        // This stores no row/seat geometry and cannot resolve an ambiguous hall name.
        var identity=MergeSectorIdentity032(PriorSectorIdentity032(c,tx,sale,show),SectorIdentity031(seat.Sector));
        using(var q=Command(c,"INSERT OR REPLACE INTO ticketland_sector_identity_032(event_id,show_identity,source_sector,sector_identity) VALUES($e,$show,$source,$identity)","$e",show.Id,"$show",ShowIdentity032(show),"$source",SectorIdentity031(sale.Sector),"$identity",identity)){q.Transaction=tx;q.ExecuteNonQuery();}
        using(var q=Command(c,"INSERT OR REPLACE INTO ticketland_match_proof_032(barcode,event_id,seat_id,sector_identity) VALUES($b,$e,$s,$identity)","$b",sale.Barcode,"$e",show.Id,"$s",seat.Id,"$identity",identity)){q.Transaction=tx;q.ExecuteNonQuery();}
    }
    private static string? ApprovedSector032(string source,TlShow029 show,string? approved)
    {
        if(approved is null||!CompatibleSector031(source,approved))return null;
        var sectors=show.SectorNames.Distinct(StringComparer.Ordinal).Where(s=>s==approved).ToArray();
        return sectors.Length==1?sectors[0]:null;
    }
    private static (bool Rejected,string? Sector) SectorDecision032(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,TlShow029 show)
    {
        var decision=Decision030(c,tx,SectorKey030(sale,show));
        if(decision.Rejected||decision.Sector is not null)return decision;
        // 0.31 stored the old boundary-prefix title key. Keep accepted name knowledge
        // and refusals when the title normalization changes, with all current guards.
        var source=Text029(sale.Sector);
        if(!show.LegacySectorKeys.TryGetValue(source,out var legacyKey))
        {
            var legacyTitle=Regex.Replace(Text029(show.Title),@"^(?:(?:ПРЕМЬЕРА(?: СПЕКТАКЛЯ)?|СПЕКТАКЛЬ|КОНЦЕРТ|МЮЗИКЛ|БАЛЕТ|ОПЕРА|ТЕАТРАЛЬНОЕ ПРЕДСТАВЛЕНИЕ)\s+)+","");
            legacyKey=show.LegacySectorKeys[source]=Hash029($"{show.Id}|{legacyTitle}|{show.At:O}|{show.VenueKey}|{source}|"+SeatFingerprint032(show));
        }
        return Decision030(c,tx,legacyKey);
    }
    private static string SourceSectorIdentity032(SqliteConnection c,SqliteTransaction tx,TicketlandSale028 sale,TlShow029 show)
    {
        var identity=SectorIdentity031(sale.Sector);
        static bool Type(string x)=>x is "ПАРТЕР" or "АМФИТЕАТР" or "БЕЛЬЭТАЖ" or "БАЛКОН" or "ЛОЖА" or "VIP";
        var words=identity.Split(' ');
        if(!words.Any(Type))
        {
            var context=PriorSectorIdentity032(c,tx,sale,show)??(ResolveSector032(sale.Sector,show).Sector is string sector?SectorIdentity031(sector):"");
            return MergeSectorIdentity032(identity,string.Join(" ",context.Split(' ').Where(Type)));
        }
        return identity;
    }
    private static string MissingPosition032(TlShow029 show,string sector,TicketlandSale028 sale)
    {
        var row=show.Seats.Where(s=>s.Sector==sector&&CatalogText.Key(s.Row)==CatalogText.Key(sale.Row)).ToArray();
        return row.Length==0?$"Сектор «{sale.Sector}» → «{sector}»: отсутствует ряд {sale.Row} (место {sale.Seat}). Изменение номера ряда запрещено.":$"Сектор «{sale.Sector}» → «{sector}»: отсутствует или неоднозначно место {sale.Seat} в ряду {sale.Row}. Изменение номера места запрещено.";
    }
    public TicketlandSyncSummary028? ReapplyCachedTicketland032()=>TicketlandLastSync028.HasValue?ApplyTicketlandReport028(CachedTicketlandReport028(),true):null;
}
