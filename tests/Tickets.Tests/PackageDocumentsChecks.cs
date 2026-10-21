using Tickets.Core;
using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Xml.Linq;

internal static class PackageDocumentsChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("document totals exact format and Russian agreement",s=>{
            Require(DocumentTotals.Summary(41,41000)=="Всего билетов: 41. Сумма выдачи: 41 000 ₽","summary format");
            Require(DocumentTotals.Total(41,41000)=="Итого: 41 билет (сорок один билет) на сумму 41.000 (сорок одна тысяча рублей ноль копеек)","total format");
            foreach(var value in new[]{(1L,"билет"),(2L,"билета"),(5L,"билетов"),(11L,"билетов"),(21L,"билет")})Require(DocumentTotals.TicketNoun(value.Item1)==value.Item2,"noun agreement");
            Require(DocumentTotals.Total(long.MaxValue,decimal.MaxValue).Contains("на сумму 79.228.162.514.264.337.593.543.950.335"),"large totals overflow");
            var tickets=Enumerable.Range(1,41).Select(i=>new TicketInfo(i,i,"Сектор","1",i.ToString(),"Получатель","TOTAL",i,1000,DateTimeOffset.Now.ToString("O"),null,1)).ToArray();
            var invoice=new InvoiceInfo(1,1,"Событие","Площадка","Дата","Получатель",DateTimeOffset.Now.ToString("O"),tickets);
            var path=s.Path+".totals.docx";InvoiceDocuments.SaveWord(invoice,path);
            using var archive=ZipFile.OpenRead(path);using var stream=archive.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var texts=xml.Descendants(w+"p").Select(p=>string.Concat(p.Descendants(w+"t").Select(t=>t.Value))).ToArray();
            Require(texts.Contains("Всего билетов: 41. Сумма выдачи: 41 000 ₽")&&texts.Contains("Итого: 41 билет (сорок один билет) на сумму 41.000 (сорок одна тысяча рублей ноль копеек)"),"Word exact 41 totals missing");
        });
        test("document templates snapshot current scoped recipient and sender",s=>{
            var (eventId,seats)=Choice(s);var issue=s.Issue(eventId,new[]{seats[0]},"PRIVATE & <one> {sender}","DOC",1);
            s.SaveDocumentTemplate(IssueDocumentKinds.Contract,"Первый {recipient} / {sender} / {count} / {amount}");
            var docs=s.PrepareIssueDocuments(eventId,issue);Require(docs.Count==4,"four documents not prepared");
            var snapshot=docs.Single(x=>x.Kind==IssueDocumentKinds.Contract).Template;
            s.SaveDocumentTemplate(IssueDocumentKinds.Contract,"Следующий {recipient}");
            Require(s.GetIssueDocument(eventId,issue,IssueDocumentKinds.Contract).Template==snapshot,"snapshot rewritten");
            s.UpdateInvoiceSender(eventId,issue,"Старый отправитель");s.UpdateInvoiceSender(eventId,issue,"Новый & <sender>");
            var current=s.GetIssueDocument(eventId,issue,IssueDocumentKinds.Contract);
            Require(current.Body.Contains("PRIVATE & <one> {sender}")&&current.Body.Contains("Новый & <sender>"),"current invoice values or single-pass rendering failed");
            var another=s.Issue(eventId,new[]{seats[1]},"SECOND PRIVATE","DOC",2);
            Require(s.GetIssueDocument(eventId,another,IssueDocumentKinds.Contract).Template=="Следующий {recipient}","global template not reused");
            s.SaveIssueDocument(eventId,issue,IssueDocumentKinds.Act,"Только эта выдача {recipient}");
            Require(s.GetIssueDocument(eventId,another,IssueDocumentKinds.Act).Template!=s.GetIssueDocument(eventId,issue,IssueDocumentKinds.Act).Template,"per-issue edit leaked");
            var otherEvent=s.Events().First(x=>x.Id!=eventId).Id;
            Reject(()=>s.GetIssueDocument(otherEvent,issue,IssueDocumentKinds.Contract));Reject(()=>s.SaveIssueDocument(otherEvent,issue,IssueDocumentKinds.Contract,"oops"));
            var reopened=new Store(s.Path);Require(reopened.Senders().Contains("Старый отправитель")&&reopened.Senders().Contains("Новый & <sender>"),"sender history lost");
            Require(reopened.GetIssueDocument(eventId,issue,IssueDocumentKinds.Contract).Template==snapshot,"snapshot not persisted");
            Require(File.Exists(s.Path+".before-v7.db"),"migration backup missing");
            using(var c=new SqliteConnection("Data Source="+s.Path)){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT count(*) FROM issue_document";Require(Convert.ToInt32(cmd.ExecuteScalar())==8,"preparation not idempotent");}
        });
        test("native Word totals bold and private XML escaped",s=>{
            var (eventId,seats)=Choice(s);var issue=s.Issue(eventId,new[]{seats[0]},"PRIVATE & <recipient>","WORD",1);var invoice=s.Invoice(eventId,issue);
            foreach(var path in new[]{s.SaveInvoiceDocument(eventId,issue),s.SaveIssueDocumentWord(eventId,issue,IssueDocumentKinds.Contract)})
            {
                using var zip=ZipFile.OpenRead(path);using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var paragraphs=xml.Descendants(w+"p").ToArray();string Text(XElement p)=>string.Concat(p.Descendants(w+"t").Select(x=>x.Value));
                Require(paragraphs.Any(p=>Text(p).Contains("PRIVATE & <recipient>")),"recipient escaping failed");
                if(path.Contains("Договор-",StringComparison.Ordinal)){Require(paragraphs.Any(p=>Text(p).Contains(DocumentTotals.Amount(invoice.Total))),"contract amount missing");continue;}
                var summary=paragraphs.Single(p=>Text(p)==DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total));Require(!summary.Descendants(w+"b").Any(),"summary must be normal");
                var total=paragraphs.Single(p=>Text(p)==DocumentTotals.Total(invoice.Tickets.Count,invoice.Total));Require(total.Descendants(w+"b").Any(),"total must be bold");
            }
        });
    }
    private static (long,long[]) Choice(Store s)
    {foreach(var e in s.Events()){var seats=s.Seats(e.Id).Where(x=>x.Available).Take(2).Select(x=>x.Id).ToArray();if(seats.Length==2)return(e.Id,seats);}throw new Exception("No event with free seats");}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
