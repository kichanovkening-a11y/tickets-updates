using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

static class RecipientForms018Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("recipient templates survive alias rename and fill each invoice without changing legal name",s=>{
            s.SaveRecipient("Сбер тест 018");
            s.SaveRecipientForm("Сбер тест 018","contract",Doc("Юридическое имя банка {count} {amount} {eventDate} {contractDate}"),"contract.docx");
            s.RenameRecipient("Сбер тест 018","Банк новое имя 018");
            Require(s.RecipientForm("Банк новое имя 018","contract") is not null,"renaming lost template");
            var invoice=Issue(s,"Банк новое имя 018");s.SetContractDate(invoice.EventId,invoice.Id,new DateTime(2025,1,7));
            var info=s.GetIssueDocument(invoice.EventId,invoice.Id,"contract");
            Require(info.SourceDocument is not null,"custom DOCX not routed");
            Require(info.Body.Contains("Юридическое имя банка 1")&&info.Body.Contains("07.01.2025")&&!info.Body.Contains("Банк новое имя"),"legal name or dynamic fields changed incorrectly");
            s.SaveRecipientForm("Банк новое имя 018","contract",Doc("Новый шаблон {count}"),"replacement.docx");
            Require(s.GetIssueDocument(invoice.EventId,invoice.Id,"contract").Body.Contains("Юридическое имя"),"new template overwrote existing invoice");
            s.SaveIssueDocument(invoice.EventId,invoice.Id,"bill","Ручной текст счета {amount}");
            s.SaveRecipientForm("Банк новое имя 018","bill",Doc("Новый счет {amount}"),"newbill.docx");
            Require(s.GetIssueDocument(invoice.EventId,invoice.Id,"bill").SourceDocument is null&&s.GetIssueDocument(invoice.EventId,invoice.Id,"bill").Body.Contains("Ручной текст"),"late uploaded form overwrote saved document");
        });
        test("custom act always uses event date and specification groups places by row",s=>{
            const string recipient="Документы 018";s.SaveRecipientForm(recipient,"act",Doc("АКТ {date} {eventCalendarDate} {contractDate}"),"act.docx");
            var invoice=Issue(s,recipient);s.SetContractDate(invoice.EventId,invoice.Id,new DateTime(2025,1,7));
            var body=s.GetIssueDocument(invoice.EventId,invoice.Id,"act").Body;
            var parts=body.Split(' ');Require(parts[1]==parts[2]&&parts[3]=="07.01.2025","act date differs from event or contract reference changed");
        });
        test("recipient without requisites can save invoice and is not offered other documents",s=>{
            var invoice=Issue(s,"Без реквизитов 018");
            Require(!s.CanUseBusinessDocuments(invoice.EventId,invoice.Id),"empty recipient details enabled business forms");
            var paths=s.SaveAllDocuments(invoice.EventId,invoice.Id);Require(paths.Count==1&&File.Exists(paths[0]),"invoice-only save failed");
            s.SaveOrganizationDetails(invoice.Recipient,false,new(Name:invoice.Recipient,Inn:"7700000018"));s.RefreshInvoiceRecipient(invoice.EventId,invoice.Id);
            Require(s.CanUseBusinessDocuments(invoice.EventId,invoice.Id),"filled details did not enable forms");
        });
        test("Sber forms split into separate documents and retain templates across merge and extensions",s=>{
            var first=Issue(s,"Сбер");var second=Issue(s,"Сбер");
            s.SetContractDate(first.EventId,first.Id,new DateTime(2026,7,20));
            foreach(var kind in new[]{"contract","act","specification","bill"})
            {
                var doc=s.CustomIssueDocument(first.EventId,first.Id,kind)!;
                Require(doc.SourceDocument is not null&&!doc.Body.Contains("{count}")&&!doc.Body.Contains("{itemSeat}"),"unfilled Sber form "+kind);
                if(kind=="contract")Require(doc.Body.Contains("Порядок")&&!doc.Body.Contains("Приложение №2"),"contract includes act");
            }
            var merged=s.MergeInvoices(first.EventId,[first.Id,second.Id]);
            Require(s.ContractDate(first.EventId,merged)==new DateTime(2026,7,20),"merge lost contract date");
            Require(s.CustomIssueDocument(first.EventId,merged,"contract")!.Body.Contains(merged.ToString()),"merged custom contract number stale");
            var count=s.CustomIssueDocument(first.EventId,merged,"specification")!.Invoice.Tickets.Count;
            Require(count==2,"merged spec missing source tickets");
            var recipientHistory=s.RecipientHistory("Сбер").Where(h=>h.Issue.EventId==first.EventId).ToArray();
            Require(recipientHistory.Length==1&&recipientHistory[0].Issue.Id==merged&&recipientHistory[0].Issue.Count==2,"recipient history shows empty merged invoice or duplicate sources");
            var paths=s.SaveAllDocuments(first.EventId,merged);Require(paths.Count==6,"Sber documents not automatically saved");
            var before=Directory.GetFiles(System.IO.Path.GetDirectoryName(paths[1])!).Length;s.SaveAllDocuments(first.EventId,merged);
            Require(Directory.GetFiles(System.IO.Path.GetDirectoryName(paths[1])!).Length==before,"unchanged custom export created backups");
        });
        test("invalid template import leaves previous template intact",s=>{
            s.SaveRecipientForm("Импорт 018","bill",Doc("Счет {amount}"),"bill.docx");
            try{s.SaveRecipientForm("Импорт 018","bill",[1,2,3],"bad.docx");throw new Exception("invalid accepted");}catch(TicketException){}
            Require(s.RecipientForm("Импорт 018","bill")!.FileName=="bill.docx","bad import replaced good form");
        });
    }
    private static byte[] Doc(string text){using var ms=new MemoryStream();using(var z=new ZipArchive(ms,ZipArchiveMode.Create,true)){using var w=new StreamWriter(z.CreateEntry("word/document.xml").Open());w.Write(new XDocument(new XElement(XName.Get("document","http://schemas.openxmlformats.org/wordprocessingml/2006/main"),new XElement(XName.Get("body","http://schemas.openxmlformats.org/wordprocessingml/2006/main"),new XElement(XName.Get("p","http://schemas.openxmlformats.org/wordprocessingml/2006/main"),new XElement(XName.Get("r","http://schemas.openxmlformats.org/wordprocessingml/2006/main"),new XElement(XName.Get("t","http://schemas.openxmlformats.org/wordprocessingml/2006/main"),text)))))).ToString());}return ms.ToArray();}
    private static InvoiceInfo Issue(Store s,string name){var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);var id=s.Issue(e.Id,[seat.Id],name,Guid.NewGuid().ToString("N")[..8],1);return s.Invoice(e.Id,id);}
    private static void Require(bool v,string m){if(!v)throw new Exception(m);}
}
