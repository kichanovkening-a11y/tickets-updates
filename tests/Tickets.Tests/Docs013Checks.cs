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
            Require(text.Contains("не позднее 3 дней с момента получения счёта"),"contract payment deadline remains blank");
        });
        test("act heading uses event calendar date without time",s=>{
            var doc=Document(s,IssueDocumentKinds.Act);
            var expected=DateTime.Parse(doc.Invoice.EventDate,System.Globalization.CultureInfo.GetCultureInfo("ru-RU")).ToString("dd.MM.yyyy");
            var heading=InvoiceDocuments.IssueDocumentXml(doc).Descendants(W+"p").Select(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value))).First(t=>t.StartsWith("АКТ №"));
            Require(heading=="АКТ № "+doc.Invoice.Id+" от "+expected,"act heading uses issue date or includes time: "+heading);
            Require(doc.Body.Contains("АКТ № "+doc.Invoice.Id+" от "+expected),"act text preview date differs");
        });
        test("explicit requisites refresh updates both parties and generated supporting documents",s=>{
            var doc=Document(s,IssueDocumentKinds.Bill);var invoice=doc.Invoice;
            s.SaveOrganizationDetails(invoice.Sender,true,new(Name:invoice.Sender,Inn:"0131111111"));
            s.SaveOrganizationDetails(invoice.Recipient,false,new(Name:invoice.Recipient,Inn:"0132222222"));
            s.RefreshInvoiceOrganizations(invoice.EventId,invoice.Id);s.SaveAllDocuments(invoice.EventId,invoice.Id);
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
        var id=s.Issue(evt.Id,new[]{seat},"Документы 013","DOC013",1);
        return s.GetIssueDocument(evt.Id,id,kind);
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
