using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

static class Docs020Checks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static void Register(Action<string,Action<Store>> test)
    {
        test("legacy authored business text opens in protected DOCX editor without replacing wording",s=>{
            const string recipient="Старый редактор 020";s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000020"));
            var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);var id=s.Issue(e.Id,[seat.Id],recipient,"СТАР020",1);
            foreach(var kind in IssueDocumentKinds.All){
                var wording="Мой собственный текст "+kind+" {recipient} / {amount} / {title} / {items}";
                s.SaveIssueDocument(e.Id,id,kind,wording);
                var text=CustomDocumentForms.Text(s.CustomIssueTemplate(e.Id,id,kind)!);
                if(!text.Contains(wording))throw new Exception("legacy editor discarded authored wording: "+kind);
                s.SaveCustomIssueForm(e.Id,id,kind,s.CustomIssueTemplate(e.Id,id,kind)!);
                var rendered=s.GetIssueDocument(e.Id,id,kind);
                if(!rendered.Body.Contains("Мой собственный текст "+kind)||!rendered.Body.Contains(recipient)||System.Text.RegularExpressions.Regex.IsMatch(rendered.Body,@"\{\w+\}"))throw new Exception("legacy editor lost text or automatic field: "+kind);
            }
        });
        test("all six exported documents keep Word cell margins in schema order",s=>{
            const string recipient="Совместимость Word 020";
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000020",Signatory:"Петров П.П."));
            var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);
            var id=s.Issue(e.Id,[seat.Id],recipient,"ДОК020",1);
            foreach(var kind in new[]{"invoice","contract","act","bill","ttn","specification"})
            {
                var info=s.CustomIssueDocument(e.Id,id,kind)!;
                AssertMarginOrder(InvoiceDocuments.IssueDocumentXml(info),kind);
                using(var memory=new MemoryStream(info.SourceDocument!))using(var package=new ZipArchive(memory,ZipArchiveMode.Read))
                    foreach(var entry in package.Entries.Where(e=>e.FullName.StartsWith("word/")&&e.FullName.EndsWith(".xml"))){using var part=entry.Open();var xml=XDocument.Load(part);AssertMarginOrder(xml,kind+"/"+entry.FullName);foreach(var alignment in xml.Descendants().Where(e=>e.Name==W+"lvlJc"||(e.Name==W+"jc"&&e.Parent?.Name==W+"tblPr")))if((string?)alignment.Attribute(W+"val") is "start" or "end")throw new Exception(kind+": invalid table/numbering alignment");}
                var raw=s.CustomIssueTemplate(e.Id,id,kind)!;
                // Existing saved / imported copies must be repaired on fill as well.
                using var output=new MemoryStream();output.Write(raw);output.Position=0;
                using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true)){
                    var entry=zip.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
                    var properties=xml.Descendants(W+"tcPr").First();properties.Element(W+"tcMar")?.Remove();
                    properties.Add(new XElement(W+"tcMar",new XElement(W+"bottom",new XAttribute(W+"w",0),new XAttribute(W+"type","dxa")),new XElement(W+"left",new XAttribute(W+"w",20),new XAttribute(W+"type","dxa"))));
                    entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
                }
                s.SaveCustomIssueForm(e.Id,id,kind,output.ToArray());
                AssertMarginOrder(InvoiceDocuments.IssueDocumentXml(s.CustomIssueDocument(e.Id,id,kind)!),"saved "+kind);
            }
        });
    }
    private static void AssertMarginOrder(XDocument xml,string kind)
    {
        var order="top left start bottom right end insideH insideV tl2br tr2bl".Split(' ');
        foreach(var margin in xml.Descendants().Where(e=>e.Name==W+"tcMar"||e.Name==W+"tblCellMar"||e.Name==W+"tcBorders"||e.Name==W+"tblBorders")){
            var previous=-1;foreach(var edge in margin.Elements()){var index=Array.IndexOf(order,edge.Name.LocalName);if(index<0)continue;if(index<previous)throw new Exception(kind+": invalid Word margin sequence "+string.Join(" ",margin.Elements().Select(e=>e.Name.LocalName)));previous=index;}
        }
    }
}
