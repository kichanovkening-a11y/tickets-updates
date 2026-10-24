using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;
internal static class Corrections027DocumentChecks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("027 future IP sender has no job titles while recipient and company keep theirs",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
            var issue=store.Issue(show.Id,[store.Seats(show.Id).First(s=>s.Available).Id],"Получатель027","DOC027",1);
            var invoice=store.Invoice(show.Id,issue) with {Sender="ИП Новиков Н.Н.",SenderDetails=new(Name:"ИП Новиков Н.Н.",Position:"Директор",Signatory:"Новиков Николай Николаевич"),RecipientDetails=new(Name:"Получатель027",Position:"Председатель",Signatory:"Петров Петр Петрович")};
            if(SignatureBlocks.Sender(invoice).Position!="")throw new Exception("IP sender title remains");
            if(SignatureBlocks.Sender(invoice with {Sender="ООО Новиков",SenderDetails=invoice.SenderDetails! with{Name="ООО Новиков"}}).Position!="Директор")throw new Exception("Company sender title lost");
            foreach(var kind in new[]{"invoice","specification","contract","act","bill","ttn"})
            {
                var xml=Xml(CustomDocumentForms.Fill(Store.DefaultRecipientForm(kind)!.Content,invoice,kind,DateTime.Today));
                var signatures=xml.Descendants(W+"p").Where(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)).Contains("Новиков\u00a0Н.Н."));
                if(!signatures.Any())throw new Exception("IP name missing: "+kind);
                var text=string.Concat(xml.Descendants(W+"t").Select(t=>t.Value));
                if(text.Contains("Руководитель")||text.Contains("Бухгалтер")||text.Contains("Директор"))throw new Exception("Sender job title remains: "+kind);
                if(kind!="bill"&&!text.Contains("Председатель"))throw new Exception("Recipient title lost: "+kind);
                foreach(var sourceKind in IssueDocumentKinds.All.Where(k=>k==kind)){
                    var source=InvoiceDocuments.IssueDocumentXml(new(sourceKind,invoice,BusinessDocumentTemplates.Default(sourceKind),""));
                    var sourceText=string.Concat(source.Descendants(W+"t").Select(t=>t.Value));
                    if(sourceText.Contains("Руководитель")||sourceText.Contains("Бухгалтер")||sourceText.Contains("бухгалтер")||sourceText.Contains("Директор"))throw new Exception("Source sender title remains: "+kind);
                }
            }
        });
    }
    private static XDocument Xml(byte[] data){using var zip=new ZipArchive(new MemoryStream(data));using var stream=zip.GetEntry("word/document.xml")!.Open();return XDocument.Load(stream);}
}
