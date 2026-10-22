using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

internal static class Docs012Checks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static void Register(Action<string,Action<Store>> test)
    {
        test("source contract keeps clauses and excludes individual places",s=>{
            var (evt,id)=Issue(s);var info=s.GetIssueDocument(evt,id,IssueDocumentKinds.Contract);var xml=InvoiceDocuments.IssueDocumentXml(info);var text=string.Concat(xml.Descendants(W+"t").Select(x=>x.Value));
            Require(text.Contains("6.2. Настоящий договор составлен в 2-х экземплярах")&&text.Contains("4.2. В случае не возможности разрешения споров"),"universal source clauses missing");
            Require(!text.Contains("места ")&&!info.Template.Contains("{items}"),"contract exposes seat list");
            Require(text.Contains("Документные доп. сведения: Особое значение"),"populated custom requisite missing");
            Require(!text.Contains("[не заполнено]")&&!text.Contains("не указано")&&!text.Contains("ОКПО:"),"empty requisites rendered");
            var path=s.SaveIssueDocumentWord(evt,id,IssueDocumentKinds.Contract);using var zip=ZipFile.OpenRead(path);
            var footers=zip.Entries.Where(x=>x.FullName.StartsWith("word/footer",StringComparison.Ordinal)&&x.FullName.EndsWith(".xml",StringComparison.Ordinal)).ToArray();
            Require(footers.Any(x=>{using var stream=x.Open();var footer=XDocument.Load(stream);return (footer.Descendants(W+"fldSimple").Any(f=>((string?)f.Attribute(W+"instr"))?.Contains("PAGE")==true)||footer.Descendants(W+"instrText").Any(f=>f.Value.Contains("PAGE")))&&footer.Descendants(W+"jc").Any(j=>(string?)j.Attribute(W+"val")=="left");}),"left page number missing");
        });
        test("invoice and issuance specification share grouped places without requisites or state",s=>{
            var (evt,id)=Issue(s);var invoice=s.Invoice(evt,id);var dir=System.IO.Path.GetDirectoryName(s.Path)!;
            var path=System.IO.Path.Combine(dir,"grouped-spec.docx");InvoiceDocuments.SaveIssueSpecificationWord(invoice,path);
            var xml=Read(path);var text=string.Concat(xml.Descendants(W+"t").Select(t=>t.Value));
            Require(text.Contains("СПЕЦИФИКАЦИЯ № "+id)&&text.Contains(invoice.Title),"issuance specification identity missing");
            Require(!text.Contains("ИНН:")&&!text.Contains("Состояние"),"invoice-only exclusions missing");
            Require(InvoiceDocuments.Headers.Length==6&&InvoiceDocuments.Rows(invoice).All(x=>x.Length==6),"invoice status column remains");
            foreach(var row in InvoiceDocuments.Rows(invoice))Require(xml.Descendants(W+"tr").Any(tr=>tr.Elements(W+"tc").Select(c=>string.Concat(c.Descendants(W+"t").Select(t=>t.Value))).SequenceEqual(row)),"grouped places differ in specification");
        });
        test("legacy Word exports are archived before source form regeneration",s=>{
            var (evt,id)=Issue(s);var path=s.SaveIssueDocumentWord(evt,id,IssueDocumentKinds.Contract);
            using(var zip=ZipFile.Open(path,ZipArchiveMode.Update))zip.GetEntry("tickets/generated.xml")!.Delete();
            var old=File.ReadAllBytes(path);s.SaveIssueDocumentWord(evt,id,IssueDocumentKinds.Contract);
            var backups=Directory.GetFiles(System.IO.Path.GetDirectoryName(path)!,System.IO.Path.GetFileNameWithoutExtension(path)+".до-0.12-*.docx");
            Require(backups.Any(file=>old.SequenceEqual(File.ReadAllBytes(file))),"legacy file was not preserved before migration");
            using var current=ZipFile.OpenRead(path);Require(current.GetEntry("tickets/generated.xml") is not null,"regenerated source form missing manifest");
        });
        test("external Word edits survive automatic regeneration",s=>{
            var (evt,id)=Issue(s);var path=s.SaveIssueDocumentWord(evt,id,IssueDocumentKinds.Act);var marker="Сохранённая внешняя правка";
            using(var zip=ZipFile.Open(path,ZipArchiveMode.Update))
            {
                XDocument xml;using(var stream=zip.GetEntry("word/document.xml")!.Open())xml=XDocument.Load(stream);
                xml.Descendants(W+"t").First().Value=marker;zip.GetEntry("word/document.xml")!.Delete();using var output=zip.CreateEntry("word/document.xml").Open();xml.Save(output);
            }
            var prior=File.ReadAllBytes(path);var rejected=false;try{s.SaveIssueDocumentWord(evt,id,IssueDocumentKinds.Act);}catch(TicketException){rejected=true;}
            Require(rejected&&prior.SequenceEqual(File.ReadAllBytes(path)),"external edits were overwritten");
        });
        test("source forms contain no unresolved placeholders or sample customer",s=>{
            var (evt,id)=Issue(s);
            foreach(var doc in s.PrepareIssueDocuments(evt,id))
            {
                var xml=InvoiceDocuments.IssueDocumentXml(doc);var text=string.Concat(xml.Descendants(W+"t").Select(t=>t.Value));
                Require(!System.Text.RegularExpressions.Regex.IsMatch(text,@"\{\w+\}"),"source placeholder unresolved: "+doc.Kind);
                foreach(var sample in new[]{"Павлюков","7704026689","Баринов Сергей","Следственного комитета РФ"})Require(!text.Contains(sample),"sample customer leaked: "+sample);
                Require(text.Contains("Документы получателя"),"scoped recipient absent");
            }
        });
    }
    private static (long,long) Issue(Store s)
    {
        var evt=s.Events().First(e=>s.Seats(e.Id).Count(x=>x.Available)>=2);var seats=s.Seats(evt.Id).Where(x=>x.Available).Take(2).Select(x=>x.Id).ToArray();
        var sender="Документы отправителя";s.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"1234567890",Signatory:"Отправитель И.И.",Position:"Директор",ExtraFields:[new("Документные доп. сведения","Особое значение")]));
        s.SaveDocumentSettings(sender,s.DocumentsDirectory);var id=s.Issue(evt.Id,seats,"Документы получателя","DOC012",1);return(evt.Id,id);
    }
    private static XDocument Read(string path){using var zip=ZipFile.OpenRead(path);using var stream=zip.GetEntry("word/document.xml")!.Open();return XDocument.Load(stream);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
