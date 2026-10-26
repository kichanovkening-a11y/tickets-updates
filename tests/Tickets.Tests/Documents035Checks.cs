using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;
internal static class Documents035Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("033 035 legal document name updates old packages without changing recipient alias",s=>{
            var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);
            s.SaveOrganizationDetails("Сбер035",false,new(Inn:"1111111111"));var id=s.Issue(e.Id,[seat.Id],"Сбер035","N035",1);
            s.SaveOrganizationDetails("Сбер035",false,new(Inn:"2222222222",LegalName:"ПАО Сбербанк035"));
            var invoice=s.Invoice(e.Id,id);Check(invoice.Recipient=="Сбер035","alias renamed");Check(invoice.RecipientDetails!.Inn=="1111111111","historical requisites overwritten");
            Check(invoice.RecipientDetails.LegalName=="ПАО Сбербанк035","legal name missing in existing issue");
            var path=Path.Combine(Path.GetDirectoryName(s.Path)!,"direct035.docx");InvoiceDocuments.SaveWord(invoice,path);
            Check(Text(File.ReadAllBytes(path)).Contains("Получатель: ПАО Сбербанк035"),"direct invoice uses alias instead of legal name");
            foreach(var kind in IssueDocumentKinds.All){var info=s.GetIssueDocument(e.Id,id,kind);Check(!info.Body.Contains("Сбер035"),"alias leaked into "+kind);}
        });
        run("033 035 bill accountant never receives director identity",s=>{
            var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);
            var id=s.Issue(e.Id,[seat.Id],"035 buyer","B035",1);var invoice=s.Invoice(e.Id,id) with{SenderDetails=new(Name:"ООО Отправитель035",Signatory:"Директор Д.Д.",Position:"Генеральный директор")};
            var template=s.CustomIssueTemplate(e.Id,id,"bill")!;
            var filled=CustomDocumentForms.Fill(template,invoice,"bill",DateTime.Today);var text=Text(filled);
            Check(!text.Contains("Бухгалтер: Генеральный директор")&&!text.Contains("Бухгалтер:Генеральный директор"),"director duplicated as accountant");
            Check(text.Split("Директор").Length-1==1,"director name must appear only in its own signature");
        });
        run("033 035 contract export uses compatible physical margins and borders",s=>{
            var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);var id=s.Issue(e.Id,[seat.Id],"035 contract buyer","C035",1);
            var template=s.CustomIssueTemplate(e.Id,id,"contract")!;var bytes=CustomDocumentForms.Fill(template,s.Invoice(e.Id,id),"contract",DateTime.Today);
            using var zip=new ZipArchive(new MemoryStream(bytes));XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
            Check(!xml.Descendants().Any(x=>x.Name==w+"start"||x.Name==w+"end"),"logical borders remain in Word contract");
            Check(!xml.Descendants(w+"ind").Attributes().Any(x=>x.Name==w+"start"||x.Name==w+"end"),"logical indentation remains");
        });
    }
    static string Text(byte[] b){using var z=new ZipArchive(new MemoryStream(b));using var r=z.GetEntry("word/document.xml")!.Open();return string.Join("\n",XDocument.Load(r).Descendants(XName.Get("t","http://schemas.openxmlformats.org/wordprocessingml/2006/main")).Select(x=>x.Value));}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
