using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Tickets.Core;

public static class InvoiceDocuments
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly CultureInfo Ru=CultureInfo.GetCultureInfo("ru-RU");
    public static string Money(decimal amount)=>amount.ToString("N0",Ru)+" ₽";
    public static string[] Headers=>["Билет","Сектор","Ряд","Место","Цена, ₽","Состояние"];
    public static string[] Row(TicketInfo ticket)=>[ticket.Code,ticket.Sector,ticket.Row,ticket.Seat,ticket.Amount.ToString("N0",Ru),ticket.Status];
    public static void SaveWord(InvoiceInfo invoice,string destination)
    {
        var body=new XElement(W+"body",
            Paragraph($"НАКЛАДНАЯ № {invoice.Id}",true,32),
            Paragraph("на передачу билетов",false,24),Paragraph("Дата выдачи: "+invoice.Date),
            Paragraph("Мероприятие: "+invoice.Title,true),Paragraph("Дата и время: "+invoice.EventDate),
            Paragraph("Площадка: "+invoice.Venue),Paragraph("Получатель: "+invoice.Recipient),Paragraph(""));
        var widths=new[]{2100,2050,650,1050,1400,1850};
        var table=new XElement(W+"tbl",new XElement(W+"tblPr",
            new XElement(W+"tblW",new XAttribute(W+"w",9100),new XAttribute(W+"type","dxa")),
            new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),
            new XElement(W+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","B8C1CC"))))),
            new XElement(W+"tblGrid",widths.Select(width=>new XElement(W+"gridCol",new XAttribute(W+"w",width)))));
        XElement TableRow(string[] values,bool header)=>new(W+"tr",new XElement(W+"trPr",new XElement(W+"cantSplit"),header?new XElement(W+"tblHeader"):null),
            values.Select((text,i)=>new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",widths[i]),new XAttribute(W+"type","dxa")),
                header?new XElement(W+"shd",new XAttribute(W+"fill","E8EDF3")):null),Paragraph(text,header,20))));
        table.Add(TableRow(Headers,true));foreach(var ticket in invoice.Tickets)table.Add(TableRow(Row(ticket),false));body.Add(table);
        body.Add(Paragraph($"Всего билетов: {invoice.Tickets.Count}. Сумма выдачи: {Money(invoice.Total)}",true));
        if(invoice.ActiveCount!=invoice.Tickets.Count)
            body.Add(Paragraph($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount}. Осталось на руках: {invoice.ActiveCount}, на сумму {Money(invoice.ActiveTotal)}."));
        body.Add(Paragraph(""),Paragraph("Передал: ____________________    Получил: ____________________"));
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
            new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text)));
    private static void Add(ZipArchive archive,string path,XDocument xml){using var stream=archive.CreateEntry(path).Open();xml.Save(stream);}
}
