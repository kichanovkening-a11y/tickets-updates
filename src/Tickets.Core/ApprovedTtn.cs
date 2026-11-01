using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Tickets.Core;

// The approved form is a fixed two-sided grid. Fill fields only; never rebuild
// its cells, remove empty rows, or normalize the source's font sizes.
public static class ApprovedTtn
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly CultureInfo Ru=CultureInfo.GetCultureInfo("ru-RU");
    public static bool IsApproved(byte[] content)
    {
        using var zip=new ZipArchive(new MemoryStream(content),ZipArchiveMode.Read);
        return ReadLayout(zip) is not null;
    }
    public static XDocument? ReadLayout(ZipArchive zip)
    {
        // Word preserves custom XML data but renames its package part to
        // itemN.xml when saving. Identify the schema, never the physical name.
        foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("customXml/",StringComparison.Ordinal)&&e.FullName.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)))
        {
            using var input=entry.Open();var xml=XDocument.Load(input);
            if(xml.Root?.Name==XName.Get("layout","urn:tickets:approved-ttn")&&(string?)xml.Root.Attribute("version")=="045")return xml;
        }
        return null;
    }
    private sealed record Line(InvoiceInfo Show,int Count,long Price)
    {internal decimal Total=>(decimal)Count*Price;}
    public static byte[] Fill(byte[] content,InvoiceInfo invoice,DateTime contractDate)
    {
        var shows=invoice.PackageShows.Count>0?invoice.PackageShows:new[]{invoice};
        var lines=shows.SelectMany(show=>show.Tickets.GroupBy(t=>t.Amount).OrderByDescending(g=>g.Key).Select(g=>new Line(show,g.Count(),g.Key))).ToArray();
        var common=SourceBusinessForms.Values(invoice,"ttn");
        var sender=SignatureBlocks.Sender(invoice);var recipient=SignatureBlocks.Recipient(invoice);
        common["senderPosition"]=Position(sender.Position);common["senderSignatory"]=sender.Name;
        common["recipientPosition"]=Position(recipient.Position);common["recipientSignatory"]=recipient.Name;
        // The supplied form's sender lines contain the legal name, INN/KPP and
        // address. Do not dump unrelated registration/bank fields into that row.
        var details=DocumentParties.Sender(invoice);
        var senderParts=new[]{invoice.DocumentSender,string.IsNullOrWhiteSpace(details.Inn)?"":"ИНН/КПП "+string.Join("/",new[]{details.Inn,details.Kpp}.Where(s=>!string.IsNullOrWhiteSpace(s))),details.Address};
        common["senderDetailsInline"]=string.Join(", ",senderParts.Where(s=>!string.IsNullOrWhiteSpace(s)));
        var date=DateTimeOffset.Parse(invoice.IssuedAt);
        common["date"]=date.ToString("dd.MM.yyyy",Ru);common["contractDate"]=contractDate.ToString("dd.MM.yyyy",Ru);
        common["lineCountWords"]=RussianWords.Number(lines.Length);
        common["ttnAttachmentPages"]="";
        common["ttnDay"]=date.Day.ToString(Ru);common["ttnMonth"]=Ru.DateTimeFormat.MonthGenitiveNames[date.Month-1];common["ttnYear"]=date.Year.ToString(Ru);
        static string Description(InvoiceInfo show)=>$"Театральные билеты на мероприятие «{show.Title}». {show.VenueWithAddress}. {show.EventDate}.";
        common["ttnDescription"]=string.Join("\n",lines.Take(3).Select(l=>l.Show).Distinct().Select(Description));
        using var output=new MemoryStream();output.Write(content);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            var entry=zip.GetEntry("word/document.xml")!;XDocument xml;using(var input=entry.Open())xml=XDocument.Load(input);
            // Imported/legacy finished documents have no editable automatic
            // fields. Keep their existing pages instead of appending them again.
            if(!xml.Descendants(W+"p").Any(p=>System.Text.RegularExpressions.Regex.IsMatch(string.Concat(p.Descendants(W+"t").Select(t=>t.Value)),@"\{\{?ttn\w+\}\}?")))return content;
            var body=xml.Root!.Element(W+"body")!;var tables=body.Elements(W+"tbl").ToArray();
            if(tables.Length!=2)throw new TicketException("В утверждённой форме ТТН должны быть лицевая и оборотная стороны.");
            var backTemplate=new XElement(tables[1]);var separator=body.Elements(W+"p").Single(p=>p.Element(W+"pPr")?.Element(W+"sectPr") is not null);
            Dictionary<string,string> Values(int start,int take,int slotStart,bool final)
            {
                var values=new Dictionary<string,string>(common,StringComparer.OrdinalIgnoreCase);
                for(var n=1;n<=7;n++)foreach(var suffix in new[]{"Number","Unit","Count","Price","Total","Description"})values["ttnItem"+suffix+n]="";
                var selected=lines.Skip(start).Take(take).ToArray();
                for(var i=0;i<selected.Length;i++)
                {
                    var line=selected[i];var slot=slotStart+i;
                    values["ttnItemNumber"+slot]=(start+i+1).ToString(Ru);values["ttnItemUnit"+slot]="шт";
                    values["ttnItemCount"+slot]=line.Count.ToString(Ru);values["ttnItemPrice"+slot]=line.Price.ToString("F2",Ru);values["ttnItemTotal"+slot]=line.Total.ToString("F2",Ru);
                    values["ttnItemDescription"+slot]=lines.Take(start+i).Any(prior=>prior.Show==line.Show)?"То же":Description(line.Show);
                }
                foreach(var name in new[]{"Front","Back"}){values["ttn"+name+"Count"]=selected.Sum(l=>l.Count).ToString(Ru);values["ttn"+name+"Total"]=selected.Sum(l=>l.Total).ToString("F2",Ru);}
                values["ttnAllCount"]=final?invoice.Tickets.Count.ToString(Ru):"";values["ttnAllTotal"]=final?invoice.Total.ToString("F2",Ru):"";
                if(!final)foreach(var key in new[]{"lineCountWords","countWords","amountWords","senderPosition","senderSignatory","recipientPosition","recipientSignatory","ttnDay","ttnMonth","ttnYear"})values[key]="";
                return values;
            }
            void FillTable(XElement table,Dictionary<string,string> values){foreach(var paragraph in table.Descendants(W+"p"))CustomDocumentForms.Replace(paragraph,values);}
            FillTable(tables[0],Values(0,3,1,false));
            FillTable(tables[1],Values(3,4,4,lines.Length<=7));
            for(var start=7;start<lines.Length;start+=4)
            {
                var next=new XElement(backTemplate);
                var page=3+(start-7)/4;
                foreach(var mark in next.Descendants().Where(e=>e.Name==W+"bookmarkStart"||e.Name==W+"bookmarkEnd"))
                {
                    mark.SetAttributeValue(W+"id",(int)mark.Attribute(W+"id")!+page*1000);
                    if(mark.Attribute(W+"name") is {} name)name.Value+="_p"+page;
                }
                FillTable(next,Values(start,4,4,start+4>=lines.Length));
                body.Element(W+"sectPr")!.AddBeforeSelf(new XElement(separator),next);
            }
            foreach(var text in xml.Descendants(W+"t").Where(t=>t.Value.Contains('\n')).ToArray())
                text.ReplaceWith(text.Value.Replace("\r\n","\n").Split('\n').SelectMany((line,i)=>i==0?new XElement[]{new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}:new XElement[]{new(W+"br"),new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}));
            WordDocumentStructure.Normalize(xml);WordDocumentStructure.Validate(xml);
            entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
        }
        return output.ToArray();
    }
    private static string Position(string value)
    {
        if(value.Equals("Генеральный директор",StringComparison.OrdinalIgnoreCase))return "Ген.директор";
        // The organization is named in the recipient field. Its long legal
        // description is retained in contracts and the recipient directory.
        if(value.StartsWith("Председатель комитета первичной организации",StringComparison.OrdinalIgnoreCase))return "Председатель";
        return value;
    }
}
