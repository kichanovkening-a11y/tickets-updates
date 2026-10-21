using System.Text.Json;

namespace Tickets.Core;

public sealed record TicketBlankField
{
    public string Key { get; set; } = "event";
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName=>Key switch {"event"=>"Мероприятие","venue"=>"Площадка","date"=>"Дата","sector"=>"Сектор","row"=>"Ряд","seat"=>"Место","price"=>"Цена","number"=>"Серия и номер",_=>Key};
    public bool Enabled { get; set; } = true;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 80;
    public double Height { get; set; } = 8;
    public double FontSize { get; set; } = 12;
}
public sealed record TicketBlankPosition
{
    public double X { get; set; } = 10;
    public double Y { get; set; } = 10;
    public double Width { get; set; } = 190;
    public double Height { get; set; } = 85;
}
public sealed record TicketBlank
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Новый бланк";
    public double PageWidth { get; set; } = 210;
    public double PageHeight { get; set; } = 297;
    public string ScanPath { get; set; } = "";
    public bool PrintBackground { get; set; }
    public List<TicketBlankPosition> Positions { get; set; } = [new()];
    public List<TicketBlankField> Fields { get; set; } = new[]{"event","venue","date","sector","row","seat","price","number"}
        .Select((key,index)=>new TicketBlankField{Key=key,X=5,Y=3+index*9}).ToList();
    public override string ToString()=>Name;
    public static readonly string[] FieldKeys=["event","venue","date","sector","row","seat","price","number"];
    public static readonly string[] FieldNames=["Мероприятие","Площадка","Дата","Сектор","Ряд","Место","Цена","Серия и номер"];
    public void Validate()
    {
        if(!Guid.TryParseExact(Id,"N",out _)||string.IsNullOrWhiteSpace(Name)||Name.Length>100)throw new TicketException("Укажите название бланка (до 100 символов).");
        static bool Range(double value,double min,double max)=>double.IsFinite(value)&&value>=min&&value<=max;
        if(!Range(PageWidth,20,1200)||!Range(PageHeight,20,1200))throw new TicketException("Размер листа должен быть от 20 до 1200 мм.");
        if(Positions is null||Positions.Count is <1 or >100)throw new TicketException("Добавьте от 1 до 100 позиций билетов на листе.");
        foreach(var p in Positions)
            if(!Range(p.X,0,PageWidth)||!Range(p.Y,0,PageHeight)||!Range(p.Width,5,PageWidth)||!Range(p.Height,5,PageHeight)||p.X+p.Width>PageWidth+0.001||p.Y+p.Height>PageHeight+0.001)
                throw new TicketException("Позиции билетов должны целиком помещаться на листе (размеры в мм).");
        for(var i=0;i<Positions.Count;i++)for(var j=i+1;j<Positions.Count;j++)
        {
            var a=Positions[i];var b=Positions[j];
            if(a.X<b.X+b.Width-0.001&&b.X<a.X+a.Width-0.001&&a.Y<b.Y+b.Height-0.001&&b.Y<a.Y+a.Height-0.001)
                throw new TicketException("Позиции билетов на листе не должны перекрываться.");
        }
        if(Fields is null||Fields.Count!=FieldKeys.Length||FieldKeys.Any(k=>Fields.Count(f=>f.Key==k)!=1))throw new TicketException("Бланк должен содержать все восемь полей билета без повторений.");
        if(!Fields.Any(f=>f.Enabled))throw new TicketException("Включите печать хотя бы одного поля билета.");
        foreach(var f in Fields)
            if(!Range(f.X,0,1200)||!Range(f.Y,0,1200)||!Range(f.Width,1,1200)||!Range(f.Height,1,1200)||!Range(f.FontSize,4,100)||(f.Enabled&&Positions.Any(p=>f.X+f.Width>p.Width+0.001||f.Y+f.Height>p.Height+0.001)))
                throw new TicketException("Поля должны помещаться в каждой позиции билета; шрифт — от 4 до 100 пунктов.");
        if(PrintBackground&&string.IsNullOrEmpty(ScanPath))throw new TicketException("Для печати фона сначала загрузите скан.");
    }
}

public sealed partial class Store
{
    private void EnsureTicketBlanks()
    {
        using var c=Open();using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS ticket_blank(id TEXT PRIMARY KEY,name TEXT NOT NULL,definition TEXT NOT NULL)");cmd.ExecuteNonQuery();
    }
    public List<TicketBlank> TicketBlanks()
    {
        EnsureTicketBlanks();using var c=Open();using var cmd=Command(c,"SELECT definition FROM ticket_blank ORDER BY name,id");using var r=cmd.ExecuteReader();var result=new List<TicketBlank>();
        while(r.Read())result.Add(JsonSerializer.Deserialize<TicketBlank>(r.GetString(0))??throw new TicketException("Не удалось прочитать бланк печати."));return result;
    }
    public void SaveTicketBlank(TicketBlank blank)
    {
        blank.Validate();if(blank.ScanPath.Length>0&&!File.Exists(TicketBlankScanPath(blank.ScanPath)))throw new TicketException("Файл скана бланка не найден. Загрузите его заново.");
        EnsureTicketBlanks();using var c=Open();using var cmd=Command(c,"INSERT INTO ticket_blank(id,name,definition) VALUES($id,$name,$json) ON CONFLICT(id) DO UPDATE SET name=excluded.name,definition=excluded.definition","$id",blank.Id,"$name",blank.Name.Trim(),"$json",JsonSerializer.Serialize(blank));cmd.ExecuteNonQuery();
    }
    public void DeleteTicketBlank(string id)
    {EnsureTicketBlanks();using var c=Open();using var cmd=Command(c,"DELETE FROM ticket_blank WHERE id=$id","$id",id);cmd.ExecuteNonQuery();}
    public string TicketBlankScanPath(string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||System.IO.Path.IsPathRooted(relative)||relative.Contains('\\')||!relative.StartsWith("PrintBlanks/",StringComparison.Ordinal)||relative.Split('/').Any(p=>p is "." or ".." or ""))throw new TicketException("Недопустимый путь скана бланка.");
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,relative.Replace('/',System.IO.Path.DirectorySeparatorChar));
    }
    public string ImportTicketBlankScan(string source)
    {
        var ext=System.IO.Path.GetExtension(source).ToLowerInvariant();if(ext is not ".png" and not ".jpg" and not ".jpeg")throw new TicketException("Выберите скан PNG или JPEG.");
        using(var stream=File.OpenRead(source))
        {
            var signature=new byte[8];var count=stream.Read(signature);
            var png=count==8&&signature.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});var jpeg=count>=3&&signature[0]==255&&signature[1]==216&&signature[2]==255;
            if(!(ext==".png"?png:jpeg))throw new TicketException("Файл не является изображением PNG или JPEG.");
            if(stream.Length>50*1024*1024)throw new TicketException("Размер скана не должен превышать 50 МБ.");
        }
        var relative="PrintBlanks/"+Guid.NewGuid().ToString("N")+ext;var destination=TicketBlankScanPath(relative);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);File.Copy(source,destination);return relative;
    }
}
