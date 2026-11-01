using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tickets.Core;

// This changes pagination properties only, never contract text or typography.
public static class ContractPagination
{
    public const string ClauseBookmark="TicketsContractClause045",SignatureBookmark="TicketsContractSignature045";
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static string Text(XElement element)=>string.Concat(element.Descendants(W+"t").Select(t=>t.Value)).Trim();
    public static byte[] Prepare(byte[] content)
    {
        using var output=new MemoryStream();output.Write(content);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            var entry=zip.GetEntry("word/document.xml");if(entry is null)return content;
            XDocument xml;using(var input=entry.Open())xml=XDocument.Load(input);
            var before=new XDocument(xml);KeepTail(xml);WordDocumentStructure.Normalize(xml);
            if(XNode.DeepEquals(before,xml))return content;
            entry.Delete();using var stream=zip.CreateEntry("word/document.xml").Open();xml.Save(stream);
        }
        return output.ToArray();
    }
    internal static void KeepTail(XDocument xml)
    {
        var body=xml.Root?.Element(W+"body");if(body is null)return;
        var blocks=body.Elements().Where(e=>e.Name==W+"p"||e.Name==W+"tbl").ToArray();
        var appendix=Array.FindIndex(blocks,b=>Regex.IsMatch(Text(b),@"^(?:Приложение(?:\s|№|$)|Appendix(?:\s|$)|Annex(?:\s|$))",RegexOptions.IgnoreCase));
        if(appendix>=0)blocks=blocks[..appendix];
        var heading=Array.FindLastIndex(blocks,b=>b.Name==W+"p"&&Text(b).Length<140&&Regex.IsMatch(Text(b),@"^(?:\d+[.)]?\s*)?(?:адреса(?:\s+и\s+реквизиты)?|реквизиты|подписи)(?:\s|:|$)",RegexOptions.IgnoreCase));
        if(heading<0)heading=Array.FindLastIndex(blocks,b=>b.Name==W+"tbl"&&Text(b).Contains("{senderSignatory}")&&(Text(b).Contains("{recipientSignatory}")||Text(b).Contains("{receivedBy}")));
        if(heading<=0)return;
        var anchor=Array.FindLastIndex(blocks,heading-1,b=>b.Name==W+"p"&&!string.IsNullOrWhiteSpace(Text(b)));
        // A signature table is a complete block. Anything after it (including
        // an appendix's separate page-break paragraph) keeps its own layout.
        var signatureTable=Array.FindLastIndex(blocks,b=>b.Name==W+"tbl"&&
            (Regex.IsMatch(Text(b),@"senderSignatory|recipientSignatory|receivedBy|подпис|расшифров|signature|_{3,}",RegexOptions.IgnoreCase)));
        if(signatureTable<heading)signatureTable=-1;
        var end=signatureTable>=0?signatureTable:Array.FindLastIndex(blocks,b=>!string.IsNullOrWhiteSpace(Text(b)));
        if(anchor<0||end<heading)return;
        // Replace only our own bookmarks. Existing Word references stay intact.
        var old=xml.Descendants(W+"bookmarkStart").Where(b=>(string?)b.Attribute(W+"name") is ClauseBookmark or SignatureBookmark).ToArray();
        var ids=old.Select(b=>(string?)b.Attribute(W+"id")).ToHashSet();
        foreach(var node in old.Concat(xml.Descendants(W+"bookmarkEnd").Where(b=>ids.Contains((string?)b.Attribute(W+"id")))).ToArray())node.Remove();
        var tail=blocks.Skip(anchor).Take(end-anchor+1).ToArray();
        foreach(var block in tail)
        {
            var paragraphs=block.Name==W+"p"?new[]{block}:block.Descendants(W+"p").ToArray();
            foreach(var p in paragraphs)
            {
                var props=Props(p,"pPr");Set(props,"keepLines",true);Set(props,"keepNext",true);
                props.Elements(W+"pageBreakBefore").Remove();
                p.Descendants(W+"br").Where(b=>(string?)b.Attribute(W+"type")=="page").Remove();
            }
            foreach(var row in block.Descendants(W+"tr"))Set(Props(row,"trPr"),"cantSplit",true);
        }
        if(blocks[end].Name==W+"p")Set(Props(blocks[end],"pPr"),"keepNext",false);
        else
        {
            var lastRow=blocks[end].Elements(W+"tr").LastOrDefault();
            if(lastRow is not null)foreach(var cell in lastRow.Elements(W+"tc"))if(cell.Descendants(W+"p").LastOrDefault() is {} p)Set(Props(p,"pPr"),"keepNext",false);
        }
        var signature=blocks[end].Name==W+"p"?blocks[end]:blocks[end].Descendants(W+"p").LastOrDefault(p=>Text(p).Length>0);
        if(signature is null)return;
        var next=xml.Descendants(W+"bookmarkStart").Select(b=>(int?)b.Attribute(W+"id")??0).DefaultIfEmpty().Max()+1;
        Bookmark(blocks[anchor],ClauseBookmark,next);Bookmark(signature,SignatureBookmark,next+1);
    }
    private static XElement Props(XElement element,string name)
    {var props=element.Element(W+name);if(props is null){props=new XElement(W+name);element.AddFirst(props);}return props;}
    private static void Set(XElement props,string name,bool value)
    {props.Elements(W+name).Remove();props.Add(new XElement(W+name,new XAttribute(W+"val",value?1:0)));}
    private static void Bookmark(XElement paragraph,string name,int id)
    {
        var start=new XElement(W+"bookmarkStart",new XAttribute(W+"id",id),new XAttribute(W+"name",name));
        if(paragraph.Element(W+"pPr") is {} props)props.AddAfterSelf(start);else paragraph.AddFirst(start);
        paragraph.Add(new XElement(W+"bookmarkEnd",new XAttribute(W+"id",id)));
    }
}
