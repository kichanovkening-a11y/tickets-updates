using System.Xml.Linq;
namespace Tickets.Core;

internal static class WordTableGrid
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static int Number(XElement? value,string attribute,int fallback=0)=>(int?)value?.Attribute(W+attribute)??fallback;
    private static bool Visible(XElement? edge)=>edge is not null&&(string?)edge.Attribute(W+"val") is not (null or "nil" or "none");
    private static bool EmptySpacer(XElement cell)=>!cell.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value))
        &&cell.Element(W+"tcPr")?.Element(W+"vMerge") is null&&cell.Element(W+"tcPr")?.Element(W+"hMerge") is null
        &&cell.Elements().All(e=>e.Name==W+"tcPr"||e.Name==W+"p"&&e.Elements().All(p=>p.Name==W+"pPr"||p.Name==W+"r"&&p.Elements().All(r=>r.Name==W+"rPr"||r.Name==W+"t")));
    private static bool SameBlankFormatting(XElement left,XElement right)=>left.Elements(W+"p").Select(p=>p.ToString(SaveOptions.DisableFormatting)).SequenceEqual(right.Elements(W+"p").Select(p=>p.ToString(SaveOptions.DisableFormatting)));
    private static XElement Properties(XElement cell)
    {
        var result=new XElement(cell.Element(W+"tcPr")??new XElement(W+"tcPr"));
        result.Elements().Where(e=>e.Name==W+"tcW"||e.Name==W+"gridSpan").Remove();return result;
    }
    // Excel exports create one grid column for every unrelated line in the whole
    // sheet. Merge only equivalent blank, borderless spacers, then remove grid
    // divisions that no cell uses. All occupied cell edges keep exact twip offsets.
    internal static void Compact(XElement table)
    {
        var grid=table.Element(W+"tblGrid");if(grid is null||grid.Elements(W+"gridCol").Count()<=63)return;
        if(Visible(table.Element(W+"tblPr")?.Element(W+"tblBorders")?.Element(W+"insideV")))return;
        foreach(var row in table.Elements(W+"tr"))
        {
            XElement? previous=null;
            foreach(var cell in row.Elements(W+"tc").ToArray())
            {
                var borders=cell.Element(W+"tcPr")?.Element(W+"tcBorders");
                var previousBorders=previous?.Element(W+"tcPr")?.Element(W+"tcBorders");
                if(previous is not null&&EmptySpacer(previous)&&EmptySpacer(cell)&&!Visible(previousBorders?.Element(W+"right"))&&!Visible(previousBorders?.Element(W+"end"))&&!Visible(borders?.Element(W+"left"))&&!Visible(borders?.Element(W+"start"))&&XNode.DeepEquals(Properties(previous),Properties(cell))&&SameBlankFormatting(previous,cell))
                {
                    var props=previous.Element(W+"tcPr");if(props is null){props=new XElement(W+"tcPr");previous.AddFirst(props);}var span=Number(props.Element(W+"gridSpan"),"val",1)+Number(cell.Element(W+"tcPr")?.Element(W+"gridSpan"),"val",1);
                    props.Element(W+"gridSpan")?.Remove();props.Add(new XElement(W+"gridSpan",new XAttribute(W+"val",span)));
                    var width=props.Element(W+"tcW");if(width is not null)width.SetAttributeValue(W+"w",Number(width,"w")+Number(cell.Element(W+"tcPr")?.Element(W+"tcW"),"w"));
                    cell.Remove();
                }
                else previous=cell;
            }
        }
        var columns=grid.Elements(W+"gridCol").ToArray();var boundaries=new SortedSet<int>{0,columns.Length};
        foreach(var row in table.Elements(W+"tr"))
        {
            var position=Number(row.Element(W+"trPr")?.Element(W+"gridBefore"),"val");boundaries.Add(position);
            foreach(var cell in row.Elements(W+"tc")){position+=Number(cell.Element(W+"tcPr")?.Element(W+"gridSpan"),"val",1);boundaries.Add(position);}
        }
        var edges=boundaries.ToArray();if(edges[^1]>columns.Length)throw new TicketException("Некорректная сетка таблицы Word.");
        var indices=edges.Select((edge,index)=>(edge,index)).ToDictionary(p=>p.edge,p=>p.index);
        foreach(var row in table.Elements(W+"tr"))
        {
            var props=row.Element(W+"trPr");var position=Number(props?.Element(W+"gridBefore"),"val");
            props?.Element(W+"gridBefore")?.SetAttributeValue(W+"val",indices[position]);
            foreach(var cell in row.Elements(W+"tc"))
            {
                var cellProps=cell.Element(W+"tcPr");if(cellProps is null){cellProps=new XElement(W+"tcPr");cell.AddFirst(cellProps);}
                var end=position+Number(cellProps.Element(W+"gridSpan"),"val",1);var span=indices[end]-indices[position];
                cellProps.Element(W+"gridSpan")?.Remove();if(span>1)cellProps.Add(new XElement(W+"gridSpan",new XAttribute(W+"val",span)));position=end;
            }
            props?.Element(W+"gridAfter")?.SetAttributeValue(W+"val",edges.Length-1-indices[position]);
        }
        grid.ReplaceNodes(edges.Zip(edges.Skip(1),(start,end)=>new XElement(W+"gridCol",new XAttribute(W+"w",columns.Skip(start).Take(end-start).Sum(c=>Number(c,"w"))))));
    }
}
