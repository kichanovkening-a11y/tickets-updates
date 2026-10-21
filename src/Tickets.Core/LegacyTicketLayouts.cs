using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tickets.Core;

public sealed record LegacyTicketElement
{
    public string Name { get; set; }="";
    public string Binding { get; set; }="";
    public string Text { get; set; }="";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string FontFamily { get; set; }="Times New Roman";
    public double FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikeout { get; set; }
    public int Rotation { get; set; }
    public string Alignment { get; set; }="LeftAlign";
    public bool Suppressed { get; set; }
    public int Argb { get; set; }=unchecked((int)0xff000000);
}
public sealed record LegacyTicketBand
{
    public string Name { get; set; }="";
    public string Kind { get; set; }="Detail";
    public double Height { get; set; }
    public bool Suppressed { get; set; }
    public bool NewPageBefore { get; set; }
    public bool NewPageAfter { get; set; }
    public bool AtBottom { get; set; }
    public List<LegacyTicketElement> Elements { get; set; }=[];
}
public sealed record LegacyTicketLayout
{
    public string SourceFile { get; set; }="";
    public string SourceSha256 { get; set; }="";
    public double LeftMargin { get; set; }
    public double TopMargin { get; set; }
    public double RightMargin { get; set; }
    public double BottomMargin { get; set; }
    public string SourcePaperSize { get; set; }="";
    public string SourceOrientation { get; set; }="";
    public double SourceContentWidth { get; set; }
    public double SourceContentHeight { get; set; }
    public double ContentWidth { get; set; }
    public double ContentHeight { get; set; }
    public List<LegacyTicketBand> Bands { get; set; }=[];
    public void Validate()
    {
        static bool Finite(double x)=>double.IsFinite(x)&&x>=0&&x<=2000;
        if(string.IsNullOrEmpty(SourceFile)||SourceSha256.Length!=64||!Finite(ContentWidth)||!Finite(ContentHeight)||ContentWidth<1||ContentHeight<1||new[]{LeftMargin,TopMargin,RightMargin,BottomMargin}.Any(x=>!Finite(x)))throw new TicketException("Повреждён исходный макет печати.");
        if(Bands.Count==0||Bands.Any(b=>!Finite(b.Height)||b.Kind is not ("ReportHeader" or "PageHeader" or "Detail" or "ReportFooter" or "PageFooter")))throw new TicketException("Неподдерживаемая секция исходного макета.");
        if(!Bands.Where(b=>!b.Suppressed).SelectMany(b=>b.Elements).Any(e=>!e.Suppressed))throw new TicketException("Включите печать хотя бы одного поля билета.");
        foreach(var e in Bands.SelectMany(b=>b.Elements))
            if(new[]{e.X,e.Y,e.Width,e.Height}.Any(x=>!Finite(x))||e.Width<=0||e.Height<=0||!double.IsFinite(e.FontSize)||e.FontSize is <1 or >200||e.Rotation is not (0 or 90 or 180 or 270)||(!string.IsNullOrEmpty(e.Binding)&&!LegacyTicketTemplates.Bindings.Contains(e.Binding)))throw new TicketException("Повреждено поле исходного макета.");
    }
}
public sealed record LegacyTicketPlacement(LegacyTicketElement Element,double X,double Y,int TicketIndex,int PageNumber,LegacyTicketElement? SourceElement=null,double SourceScale=1,bool SourceQuarterTurn=false);
public sealed record LegacyTicketPage(IReadOnlyList<LegacyTicketPlacement> Elements);

public static class LegacyTicketTemplates
{
    public static readonly string[] Bindings=["theatre_name","theatre_adress","ticket_number","perf_name","perf_date","perf_small_date","ticket_zone","ticket_place","ticket_price","small_info1","small_info2","small_info3","ticket_series","ticket_serial","event_date","event_time","ticket_row","ticket_seat","ticket_amount","PrintDate","PageNumber"];
    public static double Millimetres(double twips)=>twips*25.4/1440.0;
    public static IReadOnlyList<TicketBlank> All()
    {
        var assembly=typeof(LegacyTicketTemplates).Assembly;
        return assembly.GetManifestResourceNames().Where(n=>n.Contains(".LegacyTickets.",StringComparison.Ordinal)&&n.EndsWith(".layout.json",StringComparison.Ordinal))
            .OrderBy(n=>n,StringComparer.Ordinal).Select(n=>{using var stream=assembly.GetManifestResourceStream(n)!;using var json=JsonDocument.Parse(stream);return Convert(json.RootElement);}).ToArray();
    }
    public static TicketBlank Convert(JsonElement root)
    {
        static double Number(JsonElement value,string key)=>value.GetProperty(key).GetDouble();
        static string Text(JsonElement value,string key,string fallback="")=>value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??fallback:fallback;
        static bool Flag(JsonElement value,string key)=>value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
        var print=root.GetProperty("printOptions");var margins=root.GetProperty("pageMargins");
        double Margin(string key)=>Millimetres(Number(margins,key));
        var layout=new LegacyTicketLayout{SourceFile=Text(root,"sourceFile"),SourceSha256=Text(root,"sourceSha256"),ContentWidth=Millimetres(Number(print,"PageContentWidth")),ContentHeight=Millimetres(Number(print,"PageContentHeight")),LeftMargin=Margin("leftMargin"),TopMargin=Margin("topMargin"),RightMargin=Margin("rightMargin"),BottomMargin=Margin("bottomMargin")};
        layout.SourcePaperSize=Text(print,"PaperSize");layout.SourceOrientation=Text(print,"PaperOrientation");
        layout.SourceContentWidth=layout.ContentWidth;layout.SourceContentHeight=layout.ContentHeight;
        // Disassociated old printer layouts retain their source dimensions in
        // metadata. Physical preview uses the explicitly saved standard paper.
        if(Flag(print,"DissociatePageSizeAndPrinterPaperSize")&&layout.SourcePaperSize is "PaperLetter" or "PaperA4")
        {
            var pw=layout.SourcePaperSize=="PaperLetter"?215.9:210;var ph=layout.SourcePaperSize=="PaperLetter"?279.4:297;
            if(layout.SourceOrientation=="Landscape")(pw,ph)=(ph,pw);
            layout.ContentWidth=pw-layout.LeftMargin-layout.RightMargin;layout.ContentHeight=ph-layout.TopMargin-layout.BottomMargin;
        }
        var rotations=root.GetProperty("rasObjects").EnumerateArray().ToDictionary(o=>Text(o.GetProperty("properties"),"Name"),o=>o.GetProperty("format"),StringComparer.Ordinal);
        foreach(var section in root.GetProperty("sections").EnumerateArray())
        {
            var properties=section.GetProperty("properties");var format=section.GetProperty("format");
            var band=new LegacyTicketBand{Name=Text(properties,"Name"),Kind=Text(properties,"Kind"),Height=Millimetres(Number(properties,"Height")),Suppressed=Flag(format,"EnableSuppress"),NewPageBefore=Flag(format,"EnableNewPageBefore"),NewPageAfter=Flag(format,"EnableNewPageAfter"),AtBottom=Flag(format,"EnablePrintAtBottomOfPage")};
            foreach(var obj in section.GetProperty("objects").EnumerateArray())
            {
                var p=obj.GetProperty("properties");var f=obj.GetProperty("font");var of=obj.GetProperty("format");var name=Text(p,"Name");
                if(Text(p,"Kind") is not ("FieldObject" or "TextObject"))throw new TicketException("Неподдерживаемый объект исходного макета: "+name);
                if(!rotations.TryGetValue(name,out var rf)||!rf.TryGetProperty("TextRotationAngle",out var angle))throw new TicketException("Не извлечён поворот исходного поля: "+name);
                var rotation=angle.ValueKind==JsonValueKind.String?angle.GetString()!:angle.ToString();
                var degrees=rotation switch {"0" or "crTextRotationAngle0"=>0,"1" or "90" or "crTextRotationAngle90"=>90,"2" or "270" or "crTextRotationAngle270"=>270,"180" or "crTextRotationAngle180"=>180,_=>throw new TicketException("Неизвестный поворот исходного поля: "+rotation)};
                var data=obj.GetProperty("dataSource");var binding=data.ValueKind==JsonValueKind.Object?Text(data,"Name"):"";
                var alignment=Text(of,"HorizontalAlignment");
                if(alignment=="DefaultAlign"&&data.ValueKind==JsonValueKind.Object&&Text(data,"ValueType") is "Int32uField" or "Int32sField" or "NumberField" or "CurrencyField")alignment="RightAlign";
                band.Elements.Add(new LegacyTicketElement{Name=name,Binding=binding,Text=Text(p,"Text"),X=Millimetres(Number(p,"Left")),Y=Millimetres(Number(p,"Top")),Width=Millimetres(Number(p,"Width")),Height=Millimetres(Number(p,"Height")),FontFamily=Text(f,"Name"),FontSize=Number(f,"SizeInPoints"),Bold=Flag(f,"Bold"),Italic=Flag(f,"Italic"),Underline=Flag(f,"Underline"),Strikeout=Flag(f,"Strikeout"),Rotation=degrees,Alignment=alignment,Suppressed=Flag(of,"EnableSuppress"),Argb=p.TryGetProperty("Color",out var c)?c.GetProperty("argb").GetInt32():unchecked((int)0xff000000)});
            }
            layout.Bands.Add(band);
        }
        var id=System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("original-ticket:"+layout.SourceSha256))).ToLowerInvariant()[..32];
        var blank=new TicketBlank{Id=id,Name=System.IO.Path.GetFileNameWithoutExtension(layout.SourceFile),PageWidth=layout.LeftMargin+layout.ContentWidth+layout.RightMargin,PageHeight=layout.TopMargin+layout.ContentHeight+layout.BottomMargin,Positions=[],Fields=[],Legacy=layout};blank.Validate();return blank;
    }
    // Matches recovered Tickets.PrintForm.PrintForm_Load's XML values, including
    // the three tear-off strings. Always use the original issuance's ticket data.
    public static string Value(string binding,InvoiceInfo source,TicketInfo ticket,string venueAddress,DateTime printDate,int pageNumber)
    {
        var ru=CultureInfo.GetCultureInfo("ru-RU");
        if(!DateTime.TryParse(source.EventDate,ru,DateTimeStyles.None,out var date)&&!DateTime.TryParse(source.EventDate,CultureInfo.InvariantCulture,DateTimeStyles.None,out date))throw new TicketException("Не удалось прочитать дату показа для исходного бланка.");
        var number=ticket.Series+" № "+ticket.Number.ToString("D6",CultureInfo.InvariantCulture);
        var price="Цена: "+ticket.Amount.ToString(CultureInfo.InvariantCulture)+"р.";
        return binding switch
        {
            "ticket_series"=>ticket.Series,"ticket_serial"=>ticket.Number.ToString("D6",CultureInfo.InvariantCulture),
            "event_date"=>date.ToString("dd.MM.yyyy",ru),"event_time"=>date.ToString("HH:mm",ru),
            "ticket_row"=>ticket.Row,"ticket_seat"=>ticket.Seat,"ticket_amount"=>ticket.Amount.ToString(CultureInfo.InvariantCulture),
            "theatre_name"=>source.Venue,"theatre_adress"=>venueAddress,"ticket_number"=>number,
            "perf_name"=>source.Title.Trim().ToUpper(ru),"perf_date"=>date.ToString("D",ru)+" "+date.Hour+" ч. "+date.ToString("mm",ru)+" мин.","perf_small_date"=>date.ToString("dd.MM.yyyy HH:mm",ru),
            "ticket_zone"=>ticket.Sector,"ticket_place"=>"Ряд: "+ticket.Row+"   Место: "+ticket.Seat,"ticket_price"=>price,
            "small_info1"=>number+" "+price+" "+date.ToString("dd.MM.yyyy HH:mm",ru),"small_info2"=>ticket.Sector+" Ряд: "+ticket.Row+" Место: "+ticket.Seat,"small_info3"=>source.Title.Trim(),
            "PrintDate"=>printDate.ToString("dd.MM.yyyy",ru),"PageNumber"=>pageNumber.ToString(ru),_=>throw new TicketException("Неизвестное поле исходного бланка: "+binding)
        };
    }
    public static IReadOnlyList<LegacyTicketPage> Paginate(LegacyTicketLayout layout,int ticketCount)
    {
        layout.Validate();if(ticketCount<=0)return [];
        var pages=new List<List<LegacyTicketPlacement>>();var elements=new List<LegacyTicketPlacement>();var y=0.0;
        var bands=layout.Bands.Where(b=>!b.Suppressed).ToArray();
        double Sum(string kind)=>bands.Where(b=>b.Kind==kind).Sum(b=>b.Height);
        var footerHeight=Sum("PageFooter");var limit=layout.ContentHeight-footerHeight;
        void Place(LegacyTicketBand b,int ticket,double top)
        {foreach(var e in b.Elements.Where(e=>!e.Suppressed))elements.Add(new(e,layout.LeftMargin+e.X,layout.TopMargin+top+e.Y,ticket,pages.Count+1));}
        void Start(bool first,int ticket)
        {
            elements=[];y=0;
            if(first)foreach(var b in bands.Where(b=>b.Kind=="ReportHeader")){Place(b,ticket,y);y+=b.Height;}
            foreach(var b in bands.Where(b=>b.Kind=="PageHeader")){Place(b,ticket,y);y+=b.Height;}
        }
        void Finish(int ticket)
        {var fy=layout.ContentHeight-footerHeight;foreach(var b in bands.Where(b=>b.Kind=="PageFooter")){Place(b,ticket,fy);fy+=b.Height;}pages.Add(elements);}
        Start(true,0);
        for(var ticket=0;ticket<ticketCount;ticket++)foreach(var b in bands.Where(b=>b.Kind=="Detail"))
        {
            if(b.NewPageBefore&&y>Sum("PageHeader")+0.00001){Finish(Math.Max(0,ticket-1));Start(false,ticket);}
            if(y+b.Height>limit+0.00001){Finish(Math.Max(0,ticket-1));Start(false,ticket);}
            if(y+b.Height>limit+0.00001)throw new TicketException("Секция исходного бланка не помещается на исходный лист.");
            Place(b,ticket,y);y+=b.Height;
            if(b.NewPageAfter&&ticket<ticketCount-1){Finish(ticket);Start(false,ticket+1);}
        }
        foreach(var b in bands.Where(b=>b.Kind=="ReportFooter"))
        {
            if(y+b.Height>limit+0.00001){Finish(ticketCount-1);Start(false,ticketCount-1);}
            if(y+b.Height>limit+0.00001)throw new TicketException("Подвал исходного бланка не помещается на исходный лист.");
            if(b.AtBottom)y=limit-b.Height;Place(b,ticketCount-1,y);y+=b.Height;
        }
        Finish(ticketCount-1);return pages.Select(p=>new LegacyTicketPage(p)).ToArray();
    }
}
