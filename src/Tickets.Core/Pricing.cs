using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace Tickets.Core;

public sealed record PriceColor(long Amount,string Color);
public sealed record EventPriceState(long SeatId,long? Amount,string? Color);
public sealed record PriceChange(long EventId,IReadOnlyList<EventPriceState> Before,IReadOnlyList<EventPriceState> After,
    long Amount,string? PreviousGlobalColor,string AppliedGlobalColor);

public static class PricePalette
{
    public static IReadOnlyList<string> Colors {get;}=Array.AsReadOnly(new[]{
        "#FF5252","#FFD600","#00E676","#2979FF","#E040FB","#FF9100","#00E5FF","#F50057",
        "#76FF03","#651FFF","#1DE9B6","#FF6E40","#D500F9","#C6FF00","#448AFF","#FFAB40",
        "#000000","#808080","#FF1744","#00B0FF","#FFEA00","#00C853","#AA00FF","#FF4081",
        "#304FFE","#FF3D00","#64DD17","#00BFA5","#F9A825","#6200EA","#0091EA","#C51162"});
    public static string ForAmount(long amount)=>Colors[(int)((ulong)amount%(ulong)Colors.Count)];
    public static string NextAvailable(IEnumerable<string> used)=>Unused(new HashSet<string>(used,StringComparer.OrdinalIgnoreCase));
    internal static string Unused(ISet<string> used)
    {
        foreach(var color in Colors)if(!used.Contains(color))return color;
        // A golden-angle walk keeps consecutive extra price levels far apart in hue.
        for(var i=0;i<65536;i++)
        {
            var hue=(i*137.50776405003785)%360/60;
            var saturation=0.68+0.30*((i/360)%8)/7;
            var chroma=255*saturation;var x=chroma*(1-Math.Abs(hue%2-1));var m=255-chroma;
            var (r,g,b)=(int)hue switch
            {
                0=>(chroma,x,0d),1=>(x,chroma,0d),2=>(0d,chroma,x),
                3=>(0d,x,chroma),4=>(x,0d,chroma),_=>(chroma,0d,x)
            };
            var color=$"#{(int)Math.Round(r+m):X2}{(int)Math.Round(g+m):X2}{(int)Math.Round(b+m):X2}";
            if(!used.Contains(color))return color;
        }
        throw new TicketException("Не осталось свободных цветов. Выберите другой цвет.");
    }
}

public sealed partial class Store
{
    private void EnsurePricing()
    {
        using var c=Open();using var version=Command(c,"PRAGMA user_version");
        var needsMigration=Convert.ToInt32(version.ExecuteScalar())<6;
        var copy=Path+".before-v6.db";
        if(needsMigration&&!File.Exists(copy))
        {using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy,Pooling=false}.ToString());dest.Open();c.BackupDatabase(dest);}
        using var tx=c.BeginTransaction(deferred:false);
        using(var create=Command(c,"CREATE TABLE IF NOT EXISTS price_color(amount INTEGER PRIMARY KEY CHECK(amount>=0),color TEXT NOT NULL COLLATE NOCASE UNIQUE)"))
        {create.Transaction=tx;create.ExecuteNonQuery();}
        var amounts=new List<long>();
        // Imports or older builds can add price levels after v6. Repair absent global mappings
        // without changing established colors, monetary amounts, or ticket states.
        using(var query=Command(c,"SELECT amount FROM event_price UNION SELECT amount FROM plan_price UNION SELECT amount FROM ticket EXCEPT SELECT amount FROM price_color ORDER BY amount"))
        {query.Transaction=tx;using var read=query.ExecuteReader();while(read.Read())amounts.Add(read.GetInt64(0));}
        foreach(var amount in amounts)EnsurePriceColor(c,tx,amount,null);
        RepairConfusablePriceColors(c,tx);
        if(needsMigration){using var bump=Command(c,"PRAGMA user_version=6");bump.Transaction=tx;bump.ExecuteNonQuery();}
        tx.Commit();
    }
    private static void RepairConfusablePriceColors(SqliteConnection c,SqliteTransaction tx)
    {
        var lower=ExistingPriceColor(c,tx,1500);var higher=ExistingPriceColor(c,tx,5000);
        if(lower is null||higher is null)return;
        static (int R,int G,int B) Rgb(string color)=>(Convert.ToInt32(color.Substring(1,2),16),Convert.ToInt32(color.Substring(3,2),16),Convert.ToInt32(color.Substring(5,2),16));
        static int Distance(string first,string second)
        {var a=Rgb(first);var b=Rgb(second);return (a.R-b.R)*(a.R-b.R)+(a.G-b.G)*(a.G-b.G)+(a.B-b.B)*(a.B-b.B);}
        if(Distance(lower,higher)>=10000)return;
        var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var query=Command(c,"SELECT color FROM price_color WHERE amount!=5000"))
        {query.Transaction=tx;using var read=query.ExecuteReader();while(read.Read())used.Add(read.GetString(0));}
        // Only repair this reported ambiguous pair. Preserve all other user color choices.
        var replacement=PricePalette.Colors.Where(color=>!used.Contains(color))
            .Where(color=>{var rgb=Rgb(color);return Math.Max(rgb.R,Math.Max(rgb.G,rgb.B))-Math.Min(rgb.R,Math.Min(rgb.G,rgb.B))>=120;})
            .OrderByDescending(color=>Distance(lower,color)).FirstOrDefault();
        if(replacement is null)
        {
            // Palette exhaustion must still permit a clearly different, persistent RGB value.
            replacement=Enumerable.Range(0,24).Select(i=>PricePalette.NextAvailable(used))
                .Select(color=>{used.Add(color);return color;}).OrderByDescending(color=>Distance(lower,color)).First();
        }
        EnsurePriceColor(c,tx,5000,replacement);
    }
    private static string NormalizePriceColor(string color)
    {
        color=color.Trim().ToUpperInvariant();
        if(!Regex.IsMatch(color,"^#[0-9A-F]{6}$"))throw new TicketException("Выберите цвет в формате #RRGGBB.");
        return color;
    }
    private static string? ExistingPriceColor(SqliteConnection c,SqliteTransaction tx,long amount)
    {
        using var query=Command(c,"SELECT color FROM price_color WHERE amount=$a","$a",amount);query.Transaction=tx;
        return query.ExecuteScalar() as string;
    }
    private static string EnsurePriceColor(SqliteConnection c,SqliteTransaction tx,long amount,string? requested)
    {
        var existing=ExistingPriceColor(c,tx,amount);
        if(requested is null&&existing is not null)return existing;
        var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var query=Command(c,"SELECT color FROM price_color WHERE amount!=$a","$a",amount))
        {query.Transaction=tx;using var read=query.ExecuteReader();while(read.Read())used.Add(read.GetString(0));}
        var color=requested is null?PricePalette.Unused(used):NormalizePriceColor(requested);
        if(used.Contains(color))throw new TicketException("Этот цвет уже используется для другой цены. Выберите другой цвет.");
        using var save=Command(c,"INSERT INTO price_color(amount,color) VALUES($a,$c) ON CONFLICT(amount) DO UPDATE SET color=excluded.color","$a",amount,"$c",color);
        save.Transaction=tx;save.ExecuteNonQuery();return color;
    }
    public IReadOnlyList<PriceColor> PriceColors()
    {
        using var c=Open();using var query=Command(c,"SELECT amount,color FROM price_color ORDER BY amount");using var read=query.ExecuteReader();var list=new List<PriceColor>();
        while(read.Read())list.Add(new(read.GetInt64(0),read.GetString(1)));return list;
    }
    public void SetPriceColor(long amount,string color)
    {
        if(amount<0)throw new TicketException("Цена не может быть отрицательной.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);EnsurePriceColor(c,tx,amount,color);tx.Commit();
    }
    private static void CheckPriceSeats(SqliteConnection c,SqliteTransaction? tx,long eventId,IReadOnlyCollection<long> seatIds,bool requireFree)
    {
        if(seatIds.Count==0||seatIds.Distinct().Count()!=seatIds.Count)throw new TicketException("Выберите места без повторов.");
        foreach(var id in seatIds)
        {
            using var query=Command(c,"""
                SELECT count(*) FROM event e JOIN seat s ON s.venue_id=e.venue_id WHERE e.id=$e AND s.id=$s
                AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))
                AND ($free=0 OR NOT EXISTS(SELECT 1 FROM ticket t WHERE t.event_id=e.id AND t.seat_id=s.id AND t.returned_at IS NULL))
                ""","$e",eventId,"$s",id,"$free",requireFree?1:0);query.Transaction=tx;
            if(Convert.ToInt64(query.ExecuteScalar())!=1)throw new TicketException("Одно из мест выдано или не относится к показу. Изменения отменены целиком.");
        }
    }
    private static IReadOnlyList<EventPriceState> ReadEventPrices(SqliteConnection c,SqliteTransaction? tx,long eventId,IReadOnlyCollection<long> seatIds)
    {
        var list=new List<EventPriceState>();
        foreach(var id in seatIds)
        {
            using var query=Command(c,"SELECT amount,color FROM event_price WHERE event_id=$e AND seat_id=$s","$e",eventId,"$s",id);query.Transaction=tx;using var read=query.ExecuteReader();
            list.Add(read.Read()?new(id,read.GetInt64(0),read.GetString(1)):new(id,null,null));
        }
        return list.AsReadOnly();
    }
    public IReadOnlyList<EventPriceState> EventPrices(long eventId,IReadOnlyCollection<long> seatIds)
    {
        using var c=Open();using var tx=c.BeginTransaction();CheckPriceSeats(c,tx,eventId,seatIds,false);var result=ReadEventPrices(c,tx,eventId,seatIds);tx.Commit();return result;
    }
    public PriceChange SetEventPrices(long eventId,IReadOnlyCollection<long> seatIds,long amount,string? color=null)
    {
        if(amount<0)throw new TicketException("Цена не может быть отрицательной.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);CheckPriceSeats(c,tx,eventId,seatIds,true);
        var before=ReadEventPrices(c,tx,eventId,seatIds);var previousColor=ExistingPriceColor(c,tx,amount);
        var appliedColor=EnsurePriceColor(c,tx,amount,color);
        foreach(var id in seatIds)
        {
            using var save=Command(c,"INSERT INTO event_price(event_id,seat_id,amount,color) VALUES($e,$s,$a,$c) ON CONFLICT(event_id,seat_id) DO UPDATE SET amount=excluded.amount,color=excluded.color",
                "$e",eventId,"$s",id,"$a",amount,"$c",appliedColor);save.Transaction=tx;save.ExecuteNonQuery();
        }
        var after=ReadEventPrices(c,tx,eventId,seatIds);tx.Commit();return new(eventId,before,after,amount,previousColor,appliedColor);
    }
    public void UndoPriceChange(PriceChange change)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var ids=change.After.Select(p=>p.SeatId).ToArray();CheckPriceSeats(c,tx,change.EventId,ids,true);
        if(!ReadEventPrices(c,tx,change.EventId,ids).SequenceEqual(change.After)||
            ExistingPriceColor(c,tx,change.Amount)!=change.AppliedGlobalColor||
            !change.Before.Select(p=>p.SeatId).SequenceEqual(ids))
            throw new TicketException("Цены уже изменились. Отмена недоступна; обновите схему.");
        // A newly introduced global mapping may now be in use elsewhere. Preserve it in that case.
        if(change.PreviousGlobalColor is not null&&change.PreviousGlobalColor!=change.AppliedGlobalColor)
            EnsurePriceColor(c,tx,change.Amount,change.PreviousGlobalColor);
        foreach(var state in change.Before)
        {
            using var restore=state.Amount is null?
                Command(c,"DELETE FROM event_price WHERE event_id=$e AND seat_id=$s","$e",change.EventId,"$s",state.SeatId):
                Command(c,"UPDATE event_price SET amount=$a,color=$c WHERE event_id=$e AND seat_id=$s","$e",change.EventId,"$s",state.SeatId,"$a",state.Amount.Value,"$c",state.Color!);
            restore.Transaction=tx;restore.ExecuteNonQuery();
        }
        if(change.PreviousGlobalColor is null)
        {
            using var remove=Command(c,"DELETE FROM price_color WHERE amount=$a AND NOT EXISTS(SELECT 1 FROM event_price WHERE amount=$a) AND NOT EXISTS(SELECT 1 FROM plan_price WHERE amount=$a) AND NOT EXISTS(SELECT 1 FROM ticket WHERE amount=$a)","$a",change.Amount);
            remove.Transaction=tx;remove.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
