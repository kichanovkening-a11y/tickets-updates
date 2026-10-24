using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Tickets.Core;

public sealed record VenueInfo(long Id,string Name,string Address);
public sealed record PerformanceInfo(long Id,string Title);
public sealed record PlanInfo(long Id,long VenueId,string Name);
public sealed record PriceValue(long SeatId,long Amount,string Color);
public sealed class DraftSeat
{
    public string Sector {get;set;}="Партер";
    public string Row {get;set;}="1";
    public string Number {get;set;}="1";
    public double X {get;set;}
    public double Y {get;set;}
    public IReadOnlyList<HallEditing025.RowAnchor>? RowAisleAnchors {get;set;}
    public bool Hidden {get;set;}
    public SectorCaption029? SectorCaption {get;set;}
}
public sealed partial class Store
{
    private void EnsureCatalog()
    {
        using var c=Open(); using var version=Command(c,"PRAGMA user_version");
        var v=Convert.ToInt32(version.ExecuteScalar());
        if(v>8) throw new TicketException("База создана более новой программой. Обновите программу.");
        if(v>=2) return;
        var copy=Path+".before-v2.db";
        if(!System.IO.File.Exists(copy))
        {
            using var dest=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=copy,Pooling=false}.ToString());
            dest.Open(); c.BackupDatabase(dest);
        }
        using var tx=c.BeginTransaction(deferred:false);
        using var cmd=Command(c,"""
            CREATE TABLE IF NOT EXISTS performance(id INTEGER PRIMARY KEY, title TEXT NOT NULL, name_key TEXT NOT NULL UNIQUE);
            CREATE TABLE IF NOT EXISTS pricing_plan(id INTEGER PRIMARY KEY, venue_id INTEGER NOT NULL REFERENCES venue(id),
              name TEXT NOT NULL, name_key TEXT NOT NULL, UNIQUE(venue_id,name_key));
            CREATE TABLE IF NOT EXISTS plan_price(plan_id INTEGER NOT NULL REFERENCES pricing_plan(id),
              seat_id INTEGER NOT NULL REFERENCES seat(id), amount INTEGER NOT NULL CHECK(amount>=0),color TEXT NOT NULL,
              PRIMARY KEY(plan_id,seat_id));
            PRAGMA user_version=2;
            """); cmd.Transaction=tx; cmd.ExecuteNonQuery();
        using var titles=Command(c,"SELECT DISTINCT title FROM event"); titles.Transaction=tx;
        var names=new List<string>(); using(var r=titles.ExecuteReader()) while(r.Read()) names.Add(r.GetString(0));
        foreach(var name in names)
        {
            using var add=Command(c,"INSERT OR IGNORE INTO performance(title,name_key) VALUES($n,$k)","$n",name,"$k",name.Trim().Normalize().ToUpperInvariant());
            add.Transaction=tx; add.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private static string Required(string value,string field,int max=200)
    {
        value=value.Trim();
        if(value.Length==0 || value.Length>max || value.Any(char.IsControl)) throw new TicketException($"Укажите поле «{field}» (до {max} символов).");
        return value;
    }
    public List<VenueInfo> Venues()
    {
        using var c=Open(); using var cmd=Command(c,"SELECT id,name,address FROM venue WHERE NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='venue' AND h.entity_key=CAST(venue.id AS TEXT)) ORDER BY name");
        using var r=cmd.ExecuteReader(); var list=new List<VenueInfo>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2))); return list;
    }
    public List<PerformanceInfo> Performances()
    {
        using var c=Open(); using var cmd=Command(c,"SELECT id,title FROM performance ORDER BY title");
        using var r=cmd.ExecuteReader(); var list=new List<PerformanceInfo>();
        var hidden=HiddenCatalogKeys("performance");
        while(r.Read()) if(!hidden.Contains(CatalogText.Key(r.GetString(1)))) list.Add(new(r.GetInt64(0),r.GetString(1))); return list;
    }
    public long SavePerformance(string title)
    {
        title=Required(title,"Спектакль"); var key=title.Normalize().ToUpperInvariant(); using var c=Open();
        using var tx=c.BeginTransaction(deferred:false);
        using var restore=Command(c,"DELETE FROM catalog_hidden WHERE kind='performance' AND entity_key=$k","$k",CatalogText.Key(title)); restore.Transaction=tx; restore.ExecuteNonQuery();
        using var cmd=Command(c,"INSERT OR IGNORE INTO performance(title,name_key) VALUES($n,$k)","$n",title,"$k",key); cmd.Transaction=tx; cmd.ExecuteNonQuery();
        using var find=Command(c,"SELECT id FROM performance WHERE name_key=$k","$k",key); find.Transaction=tx; var id=Convert.ToInt64(find.ExecuteScalar()); tx.Commit(); return id;
    }
    public List<DraftSeat> HallDraft(long venueId)
    {
        using var c=Open();using var cmd=Command(c,"SELECT z.name,s.row_label,s.number,s.x,s.y,h.seat_id IS NOT NULL,(SELECT anchors FROM hall_row_anchors a WHERE a.seat_id=s.id),(SELECT caption FROM hall_sector_caption_029 cap WHERE cap.venue_id=z.venue_id AND cap.sector_id=z.id) FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id LEFT JOIN hall_hidden_seat h ON h.seat_id=s.id WHERE s.venue_id=$v AND NOT EXISTS(SELECT 1 FROM catalog_hidden x WHERE x.kind='sector' AND x.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT)) ORDER BY z.y,z.x,s.id","$v",venueId);
        using var r=cmd.ExecuteReader();var result=new List<DraftSeat>();
        while(r.Read())result.Add(new(){Sector=r.GetString(0),Row=r.GetString(1),Number=r.GetString(2),X=r.GetDouble(3),Y=r.GetDouble(4),Hidden=r.GetBoolean(5),RowAisleAnchors=ReadRowAnchors027(r,6),SectorCaption=ReadSectorCaption029(r,7)});return result;
    }
    public long CreateHall(string name,string address,IReadOnlyCollection<DraftSeat> seats)
    {
        name=Required(name,"Название зала");
        if(seats.Count is 0 or >50000) throw new TicketException("В зале должно быть от 1 до 50 000 мест.");
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var s in seats)
        {
            s.Sector=Required(s.Sector,"Сектор"); s.Row=Required(s.Row,"Ряд",32); s.Number=Required(s.Number,"Место",32);
            if(!double.IsFinite(s.X)||!double.IsFinite(s.Y)||Math.Abs(s.X)>1000000||Math.Abs(s.Y)>1000000)
                throw new TicketException("Недопустимые координаты места.");
            if(!keys.Add(s.Sector+"\u001f"+s.Row+"\u001f"+s.Number)) throw new TicketException($"Повторяется место: {s.Sector}, ряд {s.Row}, место {s.Number}.");
        }
        ValidateSectorCaptions029(seats);
        HallAnchors027.Capture(seats.ToArray());
        using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        using var add=Command(c,"INSERT INTO venue(name,address) VALUES($n,$a) RETURNING id","$n",name,"$a",address.Trim()); add.Transaction=tx;
        var id=Convert.ToInt64(add.ExecuteScalar()); long zid=0;
        foreach(var sector in seats.GroupBy(s=>s.Sector,StringComparer.OrdinalIgnoreCase))
        {
            zid++; using var zone=Command(c,"INSERT INTO sector VALUES($v,$z,$n,$x,$y)","$v",id,"$z",zid,"$n",sector.Key,"$x",sector.Min(s=>s.X),"$y",sector.Min(s=>s.Y)-26);
            zone.Transaction=tx; zone.ExecuteNonQuery();
            SaveSectorCaption029(c,tx,id,zid,sector.First().SectorCaption);
            foreach(var s in sector)
            {
                using var seat=Command(c,"INSERT INTO seat(venue_id,sector_id,row_label,number,x,y) VALUES($v,$z,$r,$n,$x,$y) RETURNING id",
                    "$v",id,"$z",zid,"$r",s.Row,"$n",s.Number,"$x",s.X,"$y",s.Y); seat.Transaction=tx;var seatId=Convert.ToInt64(seat.ExecuteScalar());
                if(s.RowAisleAnchors is not null){using var anchors=Command(c,"INSERT INTO hall_row_anchors(seat_id,anchors) VALUES($s,$a)","$s",seatId,"$a",System.Text.Json.JsonSerializer.Serialize(s.RowAisleAnchors));anchors.Transaction=tx;anchors.ExecuteNonQuery();}
                if(s.Hidden){using var hidden=Command(c,"INSERT INTO hall_hidden_seat(seat_id) VALUES($s)","$s",seatId);hidden.Transaction=tx;hidden.ExecuteNonQuery();}
            }
        }
        tx.Commit(); return id;
    }
    public List<PlanInfo> Plans(long venueId)
    {
        EnsureEventPlans(venueId);
        using var c=Open(); using var cmd=Command(c,"SELECT id,venue_id,name FROM pricing_plan WHERE venue_id=$v AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE (h.kind='plan' AND h.entity_key=CAST(pricing_plan.id AS TEXT)) OR (h.kind='venue' AND h.entity_key=CAST(pricing_plan.venue_id AS TEXT))) ORDER BY name","$v",venueId);
        using var r=cmd.ExecuteReader(); var list=new List<PlanInfo>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2))); return list;
    }
    public List<PriceValue> PlanPrices(long planId)
    {
        using var c=Open(); using var cmd=Command(c,"SELECT p.seat_id,p.amount,coalesce(pc.color,p.color) FROM plan_price p JOIN seat s ON s.id=p.seat_id LEFT JOIN price_color pc ON pc.amount=p.amount WHERE p.plan_id=$p AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat hs WHERE hs.seat_id=s.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))","$p",planId);
        using var r=cmd.ExecuteReader(); var list=new List<PriceValue>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2))); return list;
    }
    public List<SeatInfo> VenueSeats(long venueId)
    {
        using var c=Open(); using var cmd=Command(c,"SELECT s.id,z.name,s.row_label,s.number,s.x,s.y,(SELECT anchors FROM hall_row_anchors a WHERE a.seat_id=s.id) FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat hs WHERE hs.seat_id=s.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT)) ORDER BY z.y,z.x,s.y,s.x","$v",venueId);
        using var r=cmd.ExecuteReader(); var list=new List<SeatInfo>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetDouble(4),r.GetDouble(5),null,"#CBD5E1",null,"","",null){RowAisleAnchors=ReadRowAnchors027(r,6)}); return list;
    }
    public long SavePlan(long venueId,string name,IReadOnlyCollection<PriceValue> prices,IReadOnlyCollection<long>? withdrawnSeatIds=null)
    {
        name=Required(name,"Название расценовки");
        var ids=VenueSeats(venueId).Select(s=>s.Id).ToHashSet();
        if((prices.Count==0 && withdrawnSeatIds is null) || prices.Select(x=>x.SeatId).Distinct().Count()!=prices.Count || prices.Any(p=>!ids.Contains(p.SeatId)||p.Amount<0||!Regex.IsMatch(p.Color,"^#[0-9a-fA-F]{6}$")))
            throw new TicketException("Назначьте корректные цены местам выбранного зала. Цена не может быть отрицательной.");
        var withdrawn=withdrawnSeatIds?.ToHashSet()??[];
        if(withdrawnSeatIds is not null)
        {
            if(withdrawn.Count!=withdrawnSeatIds.Count||withdrawn.Any(id=>!ids.Contains(id))||prices.Any(p=>withdrawn.Contains(p.SeatId)))
                throw new TicketException("Проверьте места, заблокированные.");
            var assigned=prices.Select(p=>p.SeatId).ToHashSet();
            var missing=VenueSeats(venueId).Where(s=>!assigned.Contains(s.Id)&&!withdrawn.Contains(s.Id)).ToArray();
            if(missing.Length>0)throw new TicketException("Назначьте цену или заблокируйте следующие места:\n"+string.Join("\n",missing.Select(s=>s.Label)));
        }
        if(prices.GroupBy(p=>p.Amount).Any(g=>g.Select(p=>NormalizePriceColor(p.Color)).Distinct().Count()!=1))
            throw new TicketException("Для одной цены выберите один общий цвет.");
        using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        try
        {
            RequireActiveVenue(c,tx,venueId);
            using(var existing=Command(c,"SELECT name FROM pricing_plan WHERE venue_id=$v","$v",venueId))
            {
                existing.Transaction=tx;using var read=existing.ExecuteReader();
                while(read.Read())if(CatalogText.Key(read.GetString(0))==CatalogText.Key(name))
                    throw new TicketException("Расценовка с таким названием уже есть. Сохраните под новым названием.");
            }
            using var add=Command(c,"INSERT INTO pricing_plan(venue_id,name,name_key) VALUES($v,$n,$k) RETURNING id","$v",venueId,"$n",name,"$k",CatalogText.Key(name)); add.Transaction=tx;
            var id=Convert.ToInt64(add.ExecuteScalar());
            foreach(var p in prices)
            {
                var color=EnsurePriceColor(c,tx,p.Amount,p.Color);
                using var cmd=Command(c,"INSERT INTO plan_price VALUES($p,$s,$a,$c)","$p",id,"$s",p.SeatId,"$a",p.Amount,"$c",color); cmd.Transaction=tx; cmd.ExecuteNonQuery();
            }
            foreach(var seatId in withdrawn)
            {
                using var block=Command(c,"INSERT INTO plan_seat_block(plan_id,seat_id) VALUES($p,$s)","$p",id,"$s",seatId);
                block.Transaction=tx;block.ExecuteNonQuery();
            }
            tx.Commit(); return id;
        }
        catch(SqliteException ex) when(ex.SqliteErrorCode==19) { throw new TicketException("Расценовка с таким названием уже есть. Сохраните под новым названием."); }
    }
    public long CreateEvent(long performanceId,long venueId,long planId,DateTime date)
        =>CreateEvents(performanceId,venueId,planId,new[]{date})[0];
    public List<long> CreateEvents(long performanceId,long venueId,long planId,IReadOnlyList<DateTime> dates)
    {
        if(dates.Count is <1 or >500)throw new TicketException("Добавьте от 1 до 500 дат показа.");
        var normalized=dates.Select(d=>new DateTime(d.Year,d.Month,d.Day,d.Hour,d.Minute,d.Second)).ToArray();
        if(normalized.Distinct().Count()!=normalized.Length)throw new TicketException("В списке повторяется дата и время показа.");
        using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        using var title=Command(c,"SELECT title FROM performance WHERE id=$p","$p",performanceId); title.Transaction=tx;
        var name=title.ExecuteScalar() as string ?? throw new TicketException("Выберите спектакль.");
        RequireActiveVenue(c,tx,venueId);
        if(IsCatalogHidden(c,tx,"performance",CatalogText.Key(name))) throw new TicketException("Спектакль удалён из справочника.");
        if(IsCatalogHidden(c,tx,"plan",planId.ToString(CultureInfo.InvariantCulture))) throw new TicketException("Расценовка удалена из справочника.");
        using var check=Command(c,"SELECT count(*) FROM pricing_plan WHERE id=$p AND venue_id=$v","$p",planId,"$v",venueId); check.Transaction=tx;
        if(Convert.ToInt32(check.ExecuteScalar())!=1) throw new TicketException("Выберите расценовку этого зала.");
        var ids=new List<long>();
        foreach(var date in normalized.OrderBy(d=>d))
        {
        using var add=Command(c,"INSERT INTO event(venue_id,title,starts_at,legacy_date) VALUES($v,$n,$d,$l) RETURNING id",
            "$v",venueId,"$n",name,"$d",date.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture),"$l",date.ToString("dd.MM.yyyy HH:mm:ss",CultureInfo.InvariantCulture));
        add.Transaction=tx;long id;
        try{id=Convert.ToInt64(add.ExecuteScalar());}
        catch(SqliteException ex) when(ex.SqliteErrorCode==19&&ex.Message.Contains("event_slot_conflict"))
        {throw new TicketException("На этой сцене уже есть показ в выбранную дату и время.");}
        using var prices=Command(c,"INSERT INTO event_price SELECT $e,seat_id,amount,color FROM plan_price WHERE plan_id=$p","$e",id,"$p",planId);
        prices.Transaction=tx; prices.ExecuteNonQuery();
        using var blocks=Command(c,"INSERT INTO event_seat_block(event_id,seat_id,changed_at) SELECT $e,seat_id,$t FROM plan_seat_block WHERE plan_id=$p","$e",id,"$p",planId,"$t",DateTimeOffset.Now.ToString("O"));
        blocks.Transaction=tx;blocks.ExecuteNonQuery();ids.Add(id);
        }
        tx.Commit();return ids;
    }
}
