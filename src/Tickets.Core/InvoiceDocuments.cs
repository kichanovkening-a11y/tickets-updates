using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Tickets.Core;

public sealed record InvoiceLine(string Sector,string Row,string Seats,int Count,decimal Price,decimal Total,string Status);

public static class InvoiceDocuments
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly CultureInfo Ru=CultureInfo.GetCultureInfo("ru-RU");
    public static string Money(decimal amount)=>amount.ToString("N0",Ru)+" ₽";
    public static string[] Headers=>["Сектор","Ряд","Места","Кол-во","Цена, ₽","Сумма, ₽","Состояние"];
    public static IReadOnlyList<InvoiceLine> Lines(InvoiceInfo invoice)=>invoice.Tickets
        .GroupBy(t=>new{t.Sector,t.Row,t.Amount,t.Status})
        .Select(group=>new InvoiceLine(group.Key.Sector,group.Key.Row,
            string.Join(", ",group.Select(t=>t.Seat).OrderBy(s=>s,NaturalComparer.Instance)),group.Count(),group.Key.Amount,
            group.Sum(t=>(decimal)t.Amount),group.Key.Status))
        .OrderBy(line=>line.Sector,NaturalComparer.Instance).ThenBy(line=>line.Row,NaturalComparer.Instance)
        .ThenBy(line=>line.Price).ThenBy(line=>line.Status,StringComparer.Ordinal).ToArray();
    public static string[] Row(InvoiceLine line)=>[line.Sector,line.Row,line.Seats,line.Count.ToString(Ru),line.Price.ToString("N0",Ru),line.Total.ToString("N0",Ru),line.Status];
    public static string[][] Rows(InvoiceInfo invoice)=>Lines(invoice).Select(Row).ToArray();
    public static void SaveWord(InvoiceInfo invoice,string destination)
    {
        var body=new XElement(W+"body",
            Paragraph($"НАКЛАДНАЯ № {invoice.Id}",true,32),
            Paragraph("на передачу билетов",false,24),Paragraph("Дата выдачи: "+invoice.Date),
            Paragraph("Мероприятие: "+invoice.Title,true),Paragraph("Дата и время: "+invoice.EventDate),
            Paragraph("Площадка: "+invoice.Venue),Paragraph("Отправитель: "+invoice.Sender),
            Paragraph("Получатель: "+invoice.Recipient),Paragraph(""));
        var widths=new[]{1300,800,1900,900,1100,1200,1400};
        var table=new XElement(W+"tbl",new XElement(W+"tblPr",
            new XElement(W+"tblW",new XAttribute(W+"w",widths.Sum()),new XAttribute(W+"type","dxa")),
            new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),
            new XElement(W+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","B8C1CC"))))),
            new XElement(W+"tblGrid",widths.Select(width=>new XElement(W+"gridCol",new XAttribute(W+"w",width)))));
        XElement TableRow(string[] values,bool header)=>new(W+"tr",new XElement(W+"trPr",header?new XElement(W+"tblHeader"):null),
            values.Select((text,i)=>new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",widths[i]),new XAttribute(W+"type","dxa")),
                new XElement(W+"vAlign",new XAttribute(W+"val","top")),header?new XElement(W+"shd",new XAttribute(W+"fill","E8EDF3")):null),Paragraph(text,header,20))));
        table.Add(TableRow(Headers,true));foreach(var row in Rows(invoice))table.Add(TableRow(row,false));body.Add(table);
        body.Add(Paragraph($"Всего билетов: {invoice.Tickets.Count} ({RussianWords.Quantity(invoice.Tickets.Count)}).",true),
            Paragraph($"Сумма выдачи: {Money(invoice.Total)} ({RussianWords.Money(invoice.Total)}).",true));
        if(invoice.ActiveCount!=invoice.Tickets.Count)
            body.Add(Paragraph($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount} ({RussianWords.Quantity(invoice.Tickets.Count-invoice.ActiveCount)})."),
                Paragraph($"Осталось на руках: {invoice.ActiveCount} ({RussianWords.Quantity(invoice.ActiveCount)}), на сумму {Money(invoice.ActiveTotal)} ({RussianWords.Money(invoice.ActiveTotal)})."));
        body.Add(Paragraph(""),Paragraph("Передал: ____________________    Получил: ____________________"));
        SaveDocument(body,destination);
    }
    public static void SaveSpecificationWord(SpecificationInfo specification,string destination)
    {
        var body=new XElement(W+"body",Paragraph("СПЕЦИФИКАЦИЯ № "+specification.Id,true,32),
            Paragraph(specification.Title,true,26),Paragraph("Отправитель: "+specification.Sender),
            Paragraph("Получатель: "+specification.Recipient),Paragraph("Дата изменения: "+specification.UpdatedAt),Paragraph(""));
        foreach(var line in specification.Body.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))body.Add(Paragraph(line));
        body.Add(Paragraph(""),Paragraph("Отправитель: ____________________    Получатель: ____________________"));
        SaveDocument(body,destination);
    }
    private static void SaveDocument(XElement body,string destination)
    {
        body.Add(new XElement(W+"sectPr",new XElement(W+"pgSz",new XAttribute(W+"w",11906),new XAttribute(W+"h",16838)),
            new XElement(W+"pgMar",new XAttribute(W+"top",1134),new XAttribute(W+"bottom",1134),new XAttribute(W+"left",1134),new XAttribute(W+"right",1134))));
        var document=new XDocument(new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"w",W),body));
        var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Create))
            {
                Add(archive,"[Content_Types].xml",new XDocument(new XElement(XName.Get("Types","http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XElement(XName.Get("Default","http://schemas.openxmlformats.org/package/2006/content-types"),new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(XName.Get("Default","http://schemas.openxmlformats.org/package/2006/content-types"),new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),
                    new XElement(XName.Get("Override","http://schemas.openxmlformats.org/package/2006/content-types"),new XAttribute("PartName","/word/document.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")))));
                Add(archive,"_rels/.rels",new XDocument(new XElement(XName.Get("Relationships","http://schemas.openxmlformats.org/package/2006/relationships"),
                    new XElement(XName.Get("Relationship","http://schemas.openxmlformats.org/package/2006/relationships"),new XAttribute("Id","rId1"),new XAttribute("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),new XAttribute("Target","word/document.xml")))));
                Add(archive,"word/document.xml",document);
            }
            File.Move(temporary,destination,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static XElement Paragraph(string text,bool bold=false,int size=22)=>new(W+"p",
        new XElement(W+"pPr",new XElement(W+"spacing",new XAttribute(W+"after",100))),
        new XElement(W+"r",new XElement(W+"rPr",new XElement(W+"rFonts",new XAttribute(W+"ascii","Arial"),new XAttribute(W+"hAnsi","Arial")),bold?new XElement(W+"b"):null,new XElement(W+"sz",new XAttribute(W+"val",size))),
            text.Replace("\r\n","\n").Replace('\r','\n').Split('\n').SelectMany((line,index)=>index==0
                ?new XElement[]{new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}
                :new XElement[]{new(W+"br"),new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)})));
    private static void Add(ZipArchive archive,string path,XDocument xml){using var stream=archive.CreateEntry(path).Open();xml.Save(stream);}
    private sealed class NaturalComparer:IComparer<string>
    {
        public static readonly NaturalComparer Instance=new();
        public int Compare(string? left,string? right)
        {
            if(ReferenceEquals(left,right))return 0;
            if(left is null)return -1;if(right is null)return 1;
            var a=0;var b=0;
            while(a<left.Length&&b<right.Length)
            {
                if(char.IsAsciiDigit(left[a])&&char.IsAsciiDigit(right[b]))
                {
                    var endA=a;while(endA<left.Length&&char.IsAsciiDigit(left[endA]))endA++;
                    var endB=b;while(endB<right.Length&&char.IsAsciiDigit(right[endB]))endB++;
                    var digitsA=left[a..endA].TrimStart('0');var digitsB=right[b..endB].TrimStart('0');
                    var comparison=digitsA.Length.CompareTo(digitsB.Length);
                    if(comparison==0)comparison=StringComparer.Ordinal.Compare(digitsA,digitsB);
                    if(comparison!=0)return comparison;
                    a=endA;b=endB;
                }
                else
                {
                    var comparison=left[a].CompareTo(right[b]);if(comparison!=0)return comparison;a++;b++;
                }
            }
            var lengthComparison=(left.Length-a).CompareTo(right.Length-b);
            return lengthComparison!=0?lengthComparison:StringComparer.Ordinal.Compare(left,right);
        }
    }
}
