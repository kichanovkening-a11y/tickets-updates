using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Tickets.Core;

public sealed record TransferHall033(string Name,string Address,List<DraftSeat> Seats);
public sealed record TransferPrice033(int SeatIndex,long Amount,string Color);
public sealed record TransferResult033(string Kind,string Id,string Name);
public sealed record TransferChoice033(string Kind,string Id,string Name)
{ public override string ToString()=>Name; }
public sealed class TransferPackage033
{
    public string Format {get;set;}="Tickets.Exchange";
    public int Version {get;set;}=1;
    public string Kind {get;set;}="";
    public string Name {get;set;}="";
    public TransferHall033? Hall {get;set;}
    public List<TransferPrice033>? Prices {get;set;}
    public List<int>? Blocked {get;set;}
    public TicketBlank? Blank {get;set;}
    public byte[]? Scan {get;set;}
    public string ScanExtension {get;set;}="";
    public string TypeName=>Kind switch{"hall"=>"Зал","performance"=>"Спектакль","pricing"=>"Расценовка","blank"=>"Бланк",_=>"Неизвестный тип"};
    public static TransferPackage033 Read(byte[] bytes)
    {
        if(bytes.Length is 0 or >32*1024*1024)throw new TicketException("Размер файла импорта должен быть до 32 МБ.");
        TransferPackage033 p;
        try
        {
            using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=32});var root=doc.RootElement;
            static void Fields(JsonElement element,params string[] keys)
            {
                if(element.ValueKind!=JsonValueKind.Object||keys.Any(k=>!element.TryGetProperty(k,out _)))throw new JsonException();
            }
            Fields(root,"Format","Version","Kind","Name");
            var kind=root.GetProperty("Kind").GetString();
            if(kind is "hall" or "pricing")
            {
                Fields(root,"Hall");var hall=root.GetProperty("Hall");Fields(hall,"Name","Address","Seats");
                var seats=hall.GetProperty("Seats");if(seats.ValueKind!=JsonValueKind.Array)throw new JsonException();
                foreach(var seat in seats.EnumerateArray())
                {
                    Fields(seat,"Sector","Row","Number","X","Y","Hidden","RowAisleAnchors","SectorCaption");
                    if(seat.GetProperty("RowAisleAnchors") is var anchors&&anchors.ValueKind!=JsonValueKind.Null)
                    {if(anchors.ValueKind!=JsonValueKind.Array)throw new JsonException();foreach(var anchor in anchors.EnumerateArray())Fields(anchor,"X","Y","Centre");}
                    if(seat.GetProperty("SectorCaption") is var caption&&caption.ValueKind!=JsonValueKind.Null)Fields(caption,"Text","OffsetX","OffsetY","Angle");
                }
            }
            if(kind=="pricing")
            {Fields(root,"Prices","Blocked");if(root.GetProperty("Prices").ValueKind!=JsonValueKind.Array)throw new JsonException();foreach(var price in root.GetProperty("Prices").EnumerateArray())Fields(price,"SeatIndex","Amount","Color");}
            if(kind=="blank"){Fields(root,"Blank");Fields(root.GetProperty("Blank"),"Id","Name","PageWidth","PageHeight","ScanPath","Positions","Fields","FourUpA4","PrintBackground","Legacy");}
            p=JsonSerializer.Deserialize<TransferPackage033>(bytes,new JsonSerializerOptions{MaxDepth=32})??throw new JsonException();
        }
        catch(Exception e) when(e is JsonException or InvalidOperationException){throw new TicketException("Файл повреждён или не является файлом обмена «Билетов».");}
        if(p.Format!="Tickets.Exchange")throw new TicketException("Это не файл обмена «Билетов».");
        if(p.Version!=1)throw new TicketException("Версия файла обмена не поддерживается. Обновите программу.");
        if(p.Kind is not ("hall" or "performance" or "pricing" or "blank"))throw new TicketException("Неизвестный тип данных в файле.");
        Name033(p.Name,p.Kind=="blank"?100:200);
        if(p.Kind is "hall" or "pricing")ValidateHall(p.Hall);
        else if(p.Hall is not null)throw new TicketException("Тип файла не соответствует его содержимому.");
        if(p.Kind=="pricing")
        {
            if(p.Prices is null||p.Blocked is null)throw new TicketException("В файле нет расценовки.");
            var seats=p.Hall!.Seats;var assigned=new HashSet<int>();
            foreach(var x in p.Prices){if(x is null||x.SeatIndex<0||x.SeatIndex>=seats.Count||seats[x.SeatIndex].Hidden||x.Amount<0||!Regex.IsMatch(x.Color??"","^#[0-9A-Fa-f]{6}$")||!assigned.Add(x.SeatIndex))throw new TicketException("Некорректные цены или места расценовки.");}
            foreach(var i in p.Blocked)if(i<0||i>=seats.Count||seats[i].Hidden||!assigned.Add(i))throw new TicketException("Некорректные заблокированные места расценовки.");
            if(p.Prices.GroupBy(x=>x.Amount).Any(g=>g.Select(x=>x.Color.ToUpperInvariant()).Distinct().Count()!=1))throw new TicketException("Для одной цены должны быть одинаковые цвета.");
        }
        if(p.Kind=="blank")
        {
            if(p.Blank is null)throw new TicketException("В файле нет бланка.");
            try{p.Blank.Validate();}catch(NullReferenceException){throw new TicketException("Повреждены поля бланка.");}
            if(p.Scan is {Length:>0})
            {
                var png=p.Scan.Length>=8&&p.Scan.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
                var jpg=p.Scan.Length>=3&&p.Scan[0]==255&&p.Scan[1]==216&&p.Scan[2]==255;
                if(p.Scan.Length>20*1024*1024||!(png&&p.ScanExtension==".png"||jpg&&p.ScanExtension is ".jpg" or ".jpeg"))throw new TicketException("Некорректный скан бланка.");
            }
            else if(!string.IsNullOrEmpty(p.Blank.ScanPath))throw new TicketException("В файле отсутствует скан бланка.");
        }
        else if(p.Blank is not null||p.Scan is not null)throw new TicketException("Тип файла не соответствует его содержимому.");
        return p;
    }
    internal static string Name033(string? value,int max=200)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>max||value.Any(char.IsControl))throw new TicketException("Некорректное название в файле импорта.");return value.Trim();
    }
    private static void ValidateHall(TransferHall033? hall)
    {
        if(hall is null||hall.Seats is null||hall.Seats.Count is <1 or >50000)throw new TicketException("В файле нет корректной схемы зала.");
        Name033(hall.Name);if(hall.Address is null||hall.Address.Length>2000)throw new TicketException("Некорректный адрес зала.");
        var keys=new HashSet<(string,string,string)>();
        foreach(var s in hall.Seats)
        {
            if(s is null)throw new TicketException("Повреждено место в схеме зала.");
            s.Sector=Name033(s.Sector);s.Row=Name033(s.Row,32);s.Number=Name033(s.Number,32);
            if(!keys.Add((s.Sector.ToUpperInvariant(),s.Row.ToUpperInvariant(),s.Number.ToUpperInvariant())))throw new TicketException($"Повторяется место: {s.Sector}, ряд {s.Row}, место {s.Number}.");
            static bool Coord(double n)=>double.IsFinite(n)&&Math.Abs(n)<=1000000;
            if(!Coord(s.X)||!Coord(s.Y)||s.RowAisleAnchors is {Count:>1000}||s.RowAisleAnchors?.Any(a=>a is null||!Coord(a.X)||!Coord(a.Y))==true)throw new TicketException("Некорректные координаты схемы зала.");
        }
        Store.ValidateTransferCaptions033(hall.Seats);
    }
}
public sealed partial class Store
{
    internal static void ValidateTransferCaptions033(IReadOnlyCollection<DraftSeat> seats)=>ValidateSectorCaptions029(seats);
    public IReadOnlyList<TransferChoice033> TransferChoices033(string kind)=>kind switch
    {
        "hall"=>Venues().Select(v=>new TransferChoice033(kind,v.Id.ToString(),v.Name)).ToArray(),
        "performance"=>Performances().Select(p=>new TransferChoice033(kind,p.Id.ToString(),p.Title)).ToArray(),
        "pricing"=>Venues().SelectMany(v=>Plans(v.Id).Select(p=>new TransferChoice033(kind,p.Id.ToString(),p.Name+" · "+v.Name))).ToArray(),
        "blank"=>TicketBlanks().Select(b=>new TransferChoice033(kind,b.Id,b.Name)).ToArray(),_=>[]
    };
    public byte[] ExportTransfer033(string kind,string id)
    {
        var p=new TransferPackage033{Kind=kind};
        TransferHall033 Hall(long venueId){var v=Venues().SingleOrDefault(x=>x.Id==venueId)??throw new TicketException("Зал не найден.");return new(v.Name,v.Address,HallDraft(v.Id));}
        if(kind=="hall"){p.Hall=Hall(ParseId(id));p.Name=p.Hall.Name;}
        else if(kind=="performance")p.Name=Performances().SingleOrDefault(x=>x.Id==ParseId(id))?.Title??throw new TicketException("Спектакль не найден.");
        else if(kind=="pricing")
        {
            var plan=Venues().SelectMany(v=>Plans(v.Id)).SingleOrDefault(x=>x.Id==ParseId(id))??throw new TicketException("Расценовка не найдена.");
            p.Name=plan.Name;p.Hall=Hall(plan.VenueId);var draft=p.Hall.Seats;
            var indexes=draft.Select((s,i)=>(Key:(s.Sector,s.Row,s.Number),Index:i)).ToDictionary(x=>x.Key,x=>x.Index);
            var seats=VenueSeats(plan.VenueId).ToDictionary(s=>s.Id,s=>indexes[(s.Sector,s.Row,s.Number)]);
            p.Prices=PlanPrices(plan.Id).Select(x=>new TransferPrice033(seats[x.SeatId],x.Amount,x.Color)).ToList();
            using var c=Open();using var q=Command(c,"SELECT seat_id FROM plan_seat_block WHERE plan_id=$p","$p",plan.Id);using var r=q.ExecuteReader();p.Blocked=[];while(r.Read())if(seats.TryGetValue(r.GetInt64(0),out var i))p.Blocked.Add(i);
        }
        else if(kind=="blank")
        {
            p.Blank=TicketBlanks().SingleOrDefault(b=>b.Id==id)??throw new TicketException("Бланк не найден.");p.Name=p.Blank.Name;
            if(!string.IsNullOrEmpty(p.Blank.ScanPath)){p.Scan=File.ReadAllBytes(TicketBlankScanPath(p.Blank.ScanPath));p.ScanExtension=System.IO.Path.GetExtension(p.Blank.ScanPath).ToLowerInvariant();}
        }
        else throw new TicketException("Неизвестный тип экспорта.");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(p);TransferPackage033.Read(bytes);return bytes;
    }
    private static long ParseId(string value)=>long.TryParse(value,out var id)?id:throw new TicketException("Выберите данные для экспорта.");
    public bool TransferNameExists033(string kind,string name)=>TransferChoices033(kind).Any(x=>CatalogText.Key(kind=="pricing"?x.Name.Split(" · ")[0]:x.Name)==CatalogText.Key(name));
    public TransferResult033 ImportTransfer033(byte[] bytes,string? newName=null)
    {
        var p=TransferPackage033.Read(bytes);var name=TransferPackage033.Name033(newName??p.Name,p.Kind=="blank"?100:200);
        if(TransferNameExists033(p.Kind,name))throw new TicketException("Такое название уже есть. Укажите другое название для копии.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);string id;string? scanCreated=null;
        try
        {
            if(p.Kind is "hall" or "pricing")
            {
                var hall=p.Hall!;var hallName=p.Kind=="hall"?name:hall.Name+" — "+name;
                if(hallName.Length>200)hallName=hallName[..200];
                if(p.Kind=="pricing")
                {
                    var baseName=hallName;var suffix=2;while(Venues().Any(v=>CatalogText.Key(v.Name)==CatalogText.Key(hallName)))hallName=baseName[..Math.Min(baseName.Length,185)]+" ("+suffix+++ ")";
                }
                var venueId=InsertHall033(c,tx,hallName,hall.Address,hall.Seats);id=venueId.ToString(CultureInfo.InvariantCulture);
                if(p.Kind=="pricing")
                {
                    using var add=Command(c,"INSERT INTO pricing_plan(venue_id,name,name_key) VALUES($v,$n,$k) RETURNING id","$v",venueId,"$n",name,"$k",CatalogText.Key(name));add.Transaction=tx;var planId=Convert.ToInt64(add.ExecuteScalar());id=planId.ToString(CultureInfo.InvariantCulture);
                    var map=new Dictionary<(string,string,string),long>();using(var q=Command(c,"SELECT z.name,s.row_label,s.number,s.id FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v","$v",venueId)){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())map[(r.GetString(0),r.GetString(1),r.GetString(2))]=r.GetInt64(3);}
                    long Seat(int i){var s=hall.Seats[i];return map[(s.Sector,s.Row,s.Number)];}
                    foreach(var price in p.Prices!){var color=ExistingPriceColor(c,tx,price.Amount);
                        if(color is null){using var used=Command(c,"SELECT count(*) FROM price_color WHERE color=$c","$c",price.Color);used.Transaction=tx;var occupied=Convert.ToInt32(used.ExecuteScalar())>0;color=EnsurePriceColor(c,tx,price.Amount,occupied?null:price.Color);}using var q=Command(c,"INSERT INTO plan_price VALUES($p,$s,$a,$c)","$p",planId,"$s",Seat(price.SeatIndex),"$a",price.Amount,"$c",color);q.Transaction=tx;q.ExecuteNonQuery();}
                    foreach(var i in p.Blocked!){using var q=Command(c,"INSERT INTO plan_seat_block VALUES($p,$s)","$p",planId,"$s",Seat(i));q.Transaction=tx;q.ExecuteNonQuery();}
                }
            }
            else if(p.Kind=="performance")
            {
                using var q=Command(c,"INSERT INTO performance(title,name_key) VALUES($n,$k) RETURNING id","$n",name,"$k",name.Normalize().ToUpperInvariant());q.Transaction=tx;id=Convert.ToInt64(q.ExecuteScalar()).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var blank=p.Blank!;blank.Id=Guid.NewGuid().ToString("N");blank.Name=name;blank.IsEditableCopy=true;blank.ScanPath="";
                if(p.Scan is {Length:>0}){blank.ScanPath="PrintBlanks/"+Guid.NewGuid().ToString("N")+p.ScanExtension;scanCreated=TicketBlankScanPath(blank.ScanPath);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(scanCreated)!);File.WriteAllBytes(scanCreated,p.Scan);}
                blank.Validate();using var q=Command(c,"INSERT INTO ticket_blank(id,name,definition) VALUES($id,$n,$j)","$id",blank.Id,"$n",name,"$j",JsonSerializer.Serialize(blank));q.Transaction=tx;q.ExecuteNonQuery();id=blank.Id;
            }
            tx.Commit();return new(p.Kind,id,name);
        }
        catch{if(scanCreated is not null&&File.Exists(scanCreated))File.Delete(scanCreated);throw;}
    }
}
