using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace Tickets.Core;

internal static class DocumentFormCorrections
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    // These are the literal venue fragments in the previously distributed starter.
    // Repair fields in persisted copies too, including copies with ordinary text edits.
    internal static void UpgradeVenueFields(XDocument xml,string kind)
    {
        foreach(var paragraph in xml.Descendants(W+"p"))
        {
            var nodes=paragraph.Descendants(W+"t").ToArray();var text=string.Concat(nodes.Select(t=>t.Value));
            var pattern=@"(?:ГБУК\s+г\.\s*Москвы\s+)?«Московский театр Эстрады»(?:\s*\((?:город|г\.)\s*Москва,\s*Берсеневская набережная,\s*дом\s*20/2\))?";
            foreach(var match in Regex.Matches(text,pattern).Cast<Match>().Reverse())ReplaceRange(nodes,match.Index,match.Length,"{venue}");
        }
    }
    private static void ReplaceRange(XElement[] nodes,int start,int length,string value)
    {
        var offset=0;var end=start+length;
        foreach(var node in nodes)
        {
            var count=node.Value.Length;var next=offset+count;
            if(next>start&&offset<end){var from=Math.Max(0,start-offset);var to=Math.Min(count,end-offset);node.Value=node.Value[..from]+(offset<=start?value:"")+node.Value[to..];node.SetAttributeValue(XNamespace.Xml+"space","preserve");}
            offset=next;
        }
    }
    // Called only for byte-identical built-in forms; uploaded layouts retain all cells and drawing content.
    internal static void SpaceSignatures(XDocument xml)
    {
        var body=xml.Root?.Element(W+"body");if(body is null)return;
        var table=body.Elements(W+"tbl").LastOrDefault();if(table is null)return;
        var text=string.Concat(table.Descendants(W+"t").Select(t=>t.Value));
        if(text.Contains("Руководитель")&&text.Contains("Бухгалтер")&&table.Elements(W+"tr").FirstOrDefault()?.Elements(W+"tc").Count()==3)
        {
            var replacement=new XElement(W+"tbl",new XElement(W+"tblPr",new XElement(W+"tblW",new XAttribute(W+"w",9600),new XAttribute(W+"type","dxa")),new XElement(W+"tblLayout",new XAttribute(W+"type","fixed"))),new XElement(W+"tblGrid",new XElement(W+"gridCol",new XAttribute(W+"w",4800)),new XElement(W+"gridCol",new XAttribute(W+"w",4800))));
            foreach(var values in new[]{new[]{"Руководитель: {senderSignatory}","Бухгалтер: {senderSignatory}"},new[]{"________________________","________________________"}})
                replacement.Add(new XElement(W+"tr",values.Select(v=>new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",4800),new XAttribute(W+"type","dxa"))),InvoiceDocuments.Paragraph(v,false,20)))));
            table.ReplaceWith(replacement);table=replacement;
        }
        var rows=table.Elements(W+"tr").ToArray();
        var line=rows.FirstOrDefault(r=>r.Elements(W+"tc").All(c=>string.Concat(c.Descendants(W+"t").Select(t=>t.Value)).Trim().StartsWith("___",StringComparison.Ordinal)));
        if(line is null)return;
        bool Empty(XElement row)=>row.Name==W+"tr"&&!row.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value));
        var names=line.PreviousNode as XElement;
        while(names is not null&&Empty(names)){var prior=names.PreviousNode as XElement;names.Remove();names=prior;}
        if(names?.Name!=W+"tr")return;
        var gap=names.PreviousNode as XElement;
        while(gap is not null&&Empty(gap)){var prior=gap.PreviousNode as XElement;gap.Remove();gap=prior;}
        XElement Blank()=>new(W+"tr",new XElement(W+"trPr",new XElement(W+"cantSplit")),names.Elements(W+"tc").Select(c=>new XElement(W+"tc",c.Element(W+"tcPr") is {} pr?new XElement(pr):null,InvoiceDocuments.Paragraph("",false,20))));
        if(names==table.Elements(W+"tr").First())
        {
            var previous=table.PreviousNode as XElement;
            while(previous?.Name==W+"p"&&!previous.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value))){var prior=previous.PreviousNode as XElement;previous.Remove();previous=prior;}
            table.AddBeforeSelf(InvoiceDocuments.Paragraph(""),InvoiceDocuments.Paragraph(""));
        }
        else names.AddBeforeSelf(Blank(),Blank());
        line.AddBeforeSelf(Blank());
        foreach(var row in new[]{names,line})foreach(var cell in row.Elements(W+"tc"))
        {var pr=cell.Element(W+"tcPr");if(pr is null){pr=new XElement(W+"tcPr");cell.AddFirst(pr);}pr.Elements(W+"vAlign").Remove();pr.Add(new XElement(W+"vAlign",new XAttribute(W+"val","bottom")));}
    }
    internal static void RestoreInvoiceBorders(XDocument xml)
    {
        foreach(var table in xml.Descendants(W+"tbl"))
        {
            var header=table.Elements(W+"tr").FirstOrDefault();
            if(header is null||!header.Descendants(W+"t").Any(t=>t.Value.Equals("Места",StringComparison.OrdinalIgnoreCase)||t.Value.Equals("место",StringComparison.OrdinalIgnoreCase)))continue;
            foreach(var cell in table.Elements(W+"tr").Elements(W+"tc"))
            {
                var props=cell.Element(W+"tcPr");if(props is null){props=new XElement(W+"tcPr");cell.AddFirst(props);}
                props.Element(W+"tcBorders")?.Remove();
                props.Add(new XElement(W+"tcBorders",new[]{"top","left","bottom","right"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","000000")))));
            }
        }
    }
}
