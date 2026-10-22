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
