using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Xml.Linq;

namespace Tickets.App;

internal sealed record DocxPageLayout(Size Paper,Thickness Margins)
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    internal bool Landscape=>Paper.Width>Paper.Height;
    internal static DocxPageLayout Read(byte[] bytes)
    {
        using var zip=new ZipArchive(new MemoryStream(bytes));using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
        var section=xml.Descendants(W+"sectPr").FirstOrDefault();var size=section?.Element(W+"pgSz");var margin=section?.Element(W+"pgMar");
        double Value(XElement? node,string name,double fallback)=>(double?)node?.Attribute(W+name)/15??fallback;
        return new(new Size(Value(size,"w",794),Value(size,"h",1123)),new Thickness(Value(margin,"left",32),Value(margin,"top",32),Value(margin,"right",32),Value(margin,"bottom",32)));
    }
    internal byte[] Apply(byte[] original,Size paper,Thickness margins)
    {
        // Unchanged controls must keep every section's own original layout,
        // including documents with mixed orientation or different first pages.
        if(Math.Abs(paper.Width-Paper.Width)<1&&Math.Abs(paper.Height-Paper.Height)<1&&new[]{margins.Left-Margins.Left,margins.Top-Margins.Top,margins.Right-Margins.Right,margins.Bottom-Margins.Bottom}.All(x=>Math.Abs(x)<.1))return original;
        using var output=new MemoryStream();output.Write(original);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            var entry=zip.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
            var sections=xml.Descendants(W+"sectPr").ToArray();
            if(sections.Length==0){var section=new XElement(W+"sectPr");xml.Root!.Element(W+"body")!.Add(section);sections=[section];}
            foreach(var section in sections)
            {
                var size=section.Element(W+"pgSz");if(size is null){size=new XElement(W+"pgSz");section.AddFirst(size);}
                size.SetAttributeValue(W+"w",Math.Round(paper.Width*15));size.SetAttributeValue(W+"h",Math.Round(paper.Height*15));size.SetAttributeValue(W+"orient",paper.Width>paper.Height?"landscape":"portrait");
                var margin=section.Element(W+"pgMar");if(margin is null){margin=new XElement(W+"pgMar");size.AddAfterSelf(margin);}
                margin.SetAttributeValue(W+"left",Math.Round(margins.Left*15));margin.SetAttributeValue(W+"top",Math.Round(margins.Top*15));margin.SetAttributeValue(W+"right",Math.Round(margins.Right*15));margin.SetAttributeValue(W+"bottom",Math.Round(margins.Bottom*15));
            }
            entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
        }
        return output.ToArray();
    }
}
