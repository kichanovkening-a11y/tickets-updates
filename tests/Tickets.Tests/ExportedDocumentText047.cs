using System.IO.Compression;
using System.Xml.Linq;

internal static class ExportedDocumentText047
{
    internal static string Read(string path)
    {
        using var archive=ZipFile.OpenRead(path);
        var entries=archive.GetEntry("word/document.xml") is {} word?new[]{word}:archive.Entries.Where(e=>e.FullName.StartsWith("xl/worksheets/sheet")&&e.FullName.EndsWith(".xml")).ToArray();
        if(entries.Length==0)throw new Exception("Export has no document pages: "+path);
        return string.Concat(entries.Select(entry=>{using var stream=entry.Open();return string.Concat(XDocument.Load(stream).Descendants().Where(e=>e.Name.LocalName=="t").Select(e=>e.Value));}));
    }
}
