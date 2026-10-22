using System.Xml.Linq;
using Tickets.Core;

static class Docs013Checks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static void Register(Action<string,Action<Store>> test)
    {
        test("contract 2.2 has three day invoice payment deadline",s=>{
            var doc=Document(s,IssueDocumentKinds.Contract);
            var text=string.Concat(InvoiceDocuments.IssueDocumentXml(doc).Descendants(W+"t").Select(t=>t.Value));
            Require(text.Contains("не позднее 3 дней с момента выставления счёта"),"contract payment deadline remains blank");
        });
        test("v013 automatic contract upgrades while custom payment text stays intact",s=>{
            var old=IssueDocumentKinds.DefaultTemplate(IssueDocumentKinds.Contract).Replace("не позднее 3 дней с момента выставления счёта","не позднее 3 дней с момента получения счёта");
            s.SaveDocumentTemplate(IssueDocumentKinds.Contract,old);
            var doc=Document(new Store(s.Path),IssueDocumentKinds.Contract);
            Require(doc.Template.Contains("не позднее 3 дней с момента выставления счёта"),"old automatic contract did not upgrade");
            var edited=old.Replace("не позднее 3 дней с момента получения счёта","не позднее 10 дней по соглашению сторон");
            s.SaveIssueDocument(doc.Invoice.EventId,doc.Invoice.Id,doc.Kind,edited);
            Require(new Store(s.Path).GetIssueDocument(doc.Invoice.EventId,doc.Invoice.Id,doc.Kind).Template==edited,"custom contract overwritten");
        });
        test("manual document export folder is persisted globally without changing automatic exports",s=>{
            var folder=Path.Combine(Path.GetDirectoryName(s.Path)!,"manual");Directory.CreateDirectory(folder);
            var automatic=s.DocumentsDirectory;
            s.RememberDocumentSaveDirectory(folder);
            var reopened=new Store(s.Path);
            Require(reopened.DocumentSaveDirectory==folder,"manual save folder was not persisted");
            Require(reopened.DocumentsDirectory==automatic,"manual destination changed automatic export folder");
        });
        test("refresh current issue leaves both historical parties intact",s=>{
            var first=Document(s,IssueDocumentKinds.Bill).Invoice;
            s.SaveOrganizationDetails(first.Sender,true,new(Name:first.Sender,Inn:"014OLDSELLER"));
            s.SaveOrganizationDetails(first.Recipient,false,new(Name:first.Recipient,Inn:"014OLDBUYER"));
            s.RefreshInvoiceOrganizations(first.EventId,first.Id);
            var second=Document(s,IssueDocumentKinds.Bill).Invoice;
            s.SaveOrganizationDetails(first.Sender,true,new(Name:first.Sender,Inn:"014NEWSELLER"));
            s.SaveOrganizationDetails(first.Recipient,false,new(Name:first.Recipient,Inn:"014NEWBUYER"));
            s.RefreshInvoiceOrganizations(second.EventId,second.Id);
            var history=new Store(s.Path).Invoice(first.EventId,first.Id);
            Require(history.SenderDetails?.Inn=="014OLDSELLER"&&history.RecipientDetails?.Inn=="014OLDBUYER","refresh changed another invoice");
        });
        test("act heading uses event calendar date without time",s=>{
            var doc=Document(s,IssueDocumentKinds.Act);
            var expected=DateTime.Parse(doc.Invoice.EventDate,System.Globalization.CultureInfo.GetCultureInfo("ru-RU")).ToString("dd.MM.yyyy");
            var heading=InvoiceDocuments.IssueDocumentXml(doc).Descendants(W+"tr").Select(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value))).First(t=>t.StartsWith("г. Москва"));
            Require(heading=="г. Москва"+expected,"act heading uses issue date or includes time: "+heading);
            Require(doc.Body.Contains("г. Москва\n"+expected),"act text preview date differs");
        });
        test("explicit requisites refresh updates both parties and generated supporting documents",s=>{
            var doc=Document(s,IssueDocumentKinds.Bill);var invoice=doc.Invoice;
            s.SaveOrganizationDetails(invoice.Sender,true,new(Name:invoice.Sender,Inn:"0131111111",Signatory:"ПодписантОтправителя014"));
            s.SaveOrganizationDetails(invoice.Recipient,false,new(Name:invoice.Recipient,Inn:"0132222222",Signatory:"ПодписантПолучателя014"));
            s.RefreshInvoiceOrganizations(invoice.EventId,invoice.Id);var paths=s.SaveAllDocuments(invoice.EventId,invoice.Id);
            Require(paths.Count==6,"not all six documents regenerated");
            foreach(var path in paths)
            {
                using var archive=System.IO.Compression.ZipFile.OpenRead(path);using var xml=archive.GetEntry("word/document.xml")!.Open();
                var content=string.Concat(XDocument.Load(xml).Descendants(W+"t").Select(t=>t.Value));
                Require(content.Contains("ПодписантОтправителя014")&&content.Contains("ПодписантПолучателя014"),"stale signatures in "+Path.GetFileName(path));
            }
            var refreshed=s.Invoice(invoice.EventId,invoice.Id);
            Require(refreshed.SenderDetails?.Inn=="0131111111"&&refreshed.RecipientDetails?.Inn=="0132222222","party snapshots did not refresh");
            using var zip=System.IO.Compression.ZipFile.OpenRead(s.IssueDocumentPath(invoice.Id,IssueDocumentKinds.Bill));
            using var stream=zip.GetEntry("word/document.xml")!.Open();var text=string.Concat(XDocument.Load(stream).Descendants(W+"t").Select(t=>t.Value));
            Require(text.Contains("0131111111")&&text.Contains("0132222222"),"saved supporting document still has old requisites");
        });
    }
    private static IssueDocumentInfo Document(Store s,string kind)
    {
        var evt=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));
        var seat=s.Seats(evt.Id).First(x=>x.Available).Id;
        var id=s.Issue(evt.Id,new[]{seat},"Документы 013",Guid.NewGuid().ToString("N")[..8],1);
        return s.GetIssueDocument(evt.Id,id,kind);
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
