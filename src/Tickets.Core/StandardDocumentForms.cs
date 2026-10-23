using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace Tickets.Core;

internal static class StandardDocumentForms
{
    public static byte[]? Resolve(string kind,byte[]? custom)
    {
        if(custom is not null)
        {
            // Only replace byte-identical built-in starters. A user's edited upload wins.
            using var original=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.SberForms.{kind}.docx");
            using var ms=new MemoryStream();original?.CopyTo(ms);
            using var previous=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.UniversalForms.{kind}-v021.docx");
            using var old=new MemoryStream();previous?.CopyTo(old);
            if(!custom.AsSpan().SequenceEqual(ms.ToArray())&&!custom.AsSpan().SequenceEqual(old.ToArray()))return custom;
        }
        using var source=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.UniversalForms.{kind}.docx");
        if(source is null)return custom;
        using var output=new MemoryStream();source.CopyTo(output);return output.ToArray();
    }
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static byte[] Read(ZipArchiveEntry entry){using var stream=entry.Open();using var output=new MemoryStream();stream.CopyTo(output);return output.ToArray();}
    private static XDocument Comparable(XDocument original,bool textShape)
    {
        var xml=new XDocument(original);WordDocumentStructure.Normalize(xml);
        foreach(var attribute in xml.Descendants().Attributes().Where(a=>a.IsNamespaceDeclaration||a.Name==XNamespace.Xml+"space").ToArray())attribute.Remove();
        if(textShape)foreach(var text in xml.Descendants(W+"t"))text.Value="";
        return xml;
    }
    // Ordinary text edits retain standard layout behavior. Any changed package
    // part, protected field, drawing, cell, border or other formatting excludes it.
    public static bool IsStandard(byte[] bytes,string kind)
    {
        var standard=Resolve(kind,null);if(standard is null)return false;
        if(bytes.AsSpan().SequenceEqual(standard))return true;
        using var input=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);using var reference=new ZipArchive(new MemoryStream(standard),ZipArchiveMode.Read);
        if(!input.Entries.Select(e=>e.FullName).Order().SequenceEqual(reference.Entries.Select(e=>e.FullName).Order()))return false;
        foreach(var part in reference.Entries)
        {
            var current=Read(input.GetEntry(part.FullName)!);var expected=Read(part);
            if(current.AsSpan().SequenceEqual(expected))continue;
            if(part.FullName!="word/document.xml")return false;
            using var a=new MemoryStream(current);using var b=new MemoryStream(expected);var actual=XDocument.Load(a);var canonical=XDocument.Load(b);
            string Fields(XDocument doc)=>string.Join("|",Regex.Matches(string.Join("\n",doc.Descendants(W+"p").Select(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)))),@"\{\{?\w+\}\}?").Select(m=>m.Value).Order(StringComparer.Ordinal));
            if(Fields(actual)!=Fields(canonical)||!XNode.DeepEquals(Comparable(actual,true),Comparable(canonical,true)))return false;
        }
        return true;
    }
    public static bool HasOriginalBillSignatures(XElement table)
    {
        using var zip=new ZipArchive(new MemoryStream(Resolve("bill",null)!),ZipArchiveMode.Read);using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
        var expected=xml.Root!.Element(W+"body")!.Elements(W+"tbl").Last();
        return XNode.DeepEquals(Comparable(new XDocument(new XElement(table)),false),Comparable(new XDocument(new XElement(expected)),false));
    }
    public static byte[]? EditorTemplate(string kind)
    {
        using var source=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.BusinessForms.{kind}.docx");
        if(source is null)return null;using var output=new MemoryStream();source.CopyTo(output);return output.ToArray();
    }
}
