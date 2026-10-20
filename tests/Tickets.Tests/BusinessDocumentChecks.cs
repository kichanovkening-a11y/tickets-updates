using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class BusinessDocumentChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("business forms use actual tickets and selected organization snapshots",s=>{
            var evt=s.Events().First(e=>s.Seats(e.Id).Count(x=>x.Available)>=2);
            var seats=s.Seats(evt.Id).Where(x=>x.Available).Take(2).ToArray();
            var sender="ИП Отправитель тест";var recipient="Получатель тест";
            s.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"123456789012",Account:"12345678901234567890",Signatory:"Иванов И.И.",Position:"Предприниматель"));
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"9876543210",Signatory:"Петров П.П.",Position:"Директор"));
            s.SaveDocumentSettings(sender,s.DocumentsDirectory);
            var id=s.Issue(evt.Id,seats.Select(x=>x.Id).ToArray(),recipient,"FORM",1);
            var invoice=s.Invoice(evt.Id,id);var docs=s.PrepareIssueDocuments(evt.Id,id);
            Require(docs.Select(x=>x.Template).Distinct().Count()==4,"forms must be different");
            foreach(var doc in docs)
            {
                Require(doc.Body.Contains(sender)&&doc.Body.Contains(recipient)&&doc.Body.Contains("12345678901234567890")&&doc.Body.Contains("9876543210"),"requisites missing");
                Require(!doc.Body.Contains("ПРОМСВЯЗЬБАНК")&&!doc.Body.Contains("7729768795"),"unrelated Alliance details leaked");
                Require(doc.Body.Contains(invoice.Title)&&doc.Body.Contains(invoice.EventDate)&&doc.Body.Contains(DocumentTotals.Total(2,invoice.Total)),"event or exact totals missing");
                Require(doc.Body.Contains("№ | Билеты и места |")&&!doc.Body.Contains("{senderDetails}"),"ticket table unresolved");
                var file=s.SaveIssueDocumentWord(evt.Id,id,doc.Kind);
                using var zip=ZipFile.OpenRead(file);using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                Require(xml.Descendants(w+"tbl").Any(),"native Word ticket table missing");
            }
            s.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"111111111111"));
            Require(new Store(s.Path).GetIssueDocument(evt.Id,id,IssueDocumentKinds.Bill).Body.Contains("123456789012"),"directory change rewrote issued requisites");
        });
        test("only legacy global defaults upgrade and historical templates remain",s=>{
            var evt=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(evt.Id).First(x=>x.Available);
            var old=BusinessDocumentTemplates.LegacyDefault(IssueDocumentKinds.Contract);
            s.SaveDocumentTemplate(IssueDocumentKinds.Contract,old);
            // Simulate an existing historical snapshot from the previous version.
            var id=s.Issue(evt.Id,new[]{seat.Id},"Test","OLD",1);
            using(var c=new SqliteConnection("Data Source="+s.Path))
            {c.Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT OR REPLACE INTO issue_document(event_id,issue_id,kind,template) VALUES($e,$i,'contract',$t)";cmd.Parameters.AddWithValue("$e",evt.Id);cmd.Parameters.AddWithValue("$i",id);cmd.Parameters.AddWithValue("$t",old);cmd.ExecuteNonQuery();}
            s.SaveDocumentTemplate(IssueDocumentKinds.Act,"Мой акт {recipient}");
            Require(s.DocumentTemplate(IssueDocumentKinds.Contract)==BusinessDocumentTemplates.Default(IssueDocumentKinds.Contract),"starter not upgraded");
            Require(s.DocumentTemplate(IssueDocumentKinds.Act)=="Мой акт {recipient}","custom template overwritten");
            Require(s.GetIssueDocument(evt.Id,id,IssueDocumentKinds.Contract).Template==old,"historical snapshot rewritten");
        });
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
