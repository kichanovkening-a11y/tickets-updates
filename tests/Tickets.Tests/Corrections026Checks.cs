using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;
internal static class Corrections026Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("026 IP bill retains only right sender signature",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
            var issue=store.Issue(show.Id,new[]{seat.Id},"Получатель026","T026",1);
            var invoice=store.Invoice(show.Id,issue) with {Sender="ИП Баринов А.С.",SenderDetails=new(Name:"ИП Баринов А.С.",Signatory:"Баринов А.С.")};
            using(var resource=typeof(Store).Assembly.GetManifestResourceStream("Tickets.Core.Resources.BusinessForms.bill.docx")!){using var form=new MemoryStream();resource.CopyTo(form);var filled=CustomDocumentForms.Text(CustomDocumentForms.Fill(form.ToArray(),invoice,"bill",DateTime.Today));if(!filled.Contains("Поставщик")||!filled.Contains("Покупатель")||!filled.Contains("Наименование")||!filled.Contains("Театральные билеты"))throw new Exception("IP correction erased non-signature contents of custom bill");}
            foreach(var sender in new[]{"ИП Баринов А.С.","ООО Альянс"}){
                using var source=typeof(Store).Assembly.GetManifestResourceStream("Tickets.Core.Resources.UniversalForms.bill.docx")!;using var input=new MemoryStream();source.CopyTo(input);
                var bytes=CustomDocumentForms.Fill(input.ToArray(),invoice with {Sender=sender,SenderDetails=new(Name:sender,Signatory:"Баринов А.С.")},"bill",DateTime.Today);
                using var zip=new ZipArchive(new MemoryStream(bytes));using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var table=xml.Root!.Element(w+"body")!.Elements(w+"tbl").Last();var names=table.Descendants(w+"t").Count(t=>t.Value.Replace('\u00a0',' ')=="Баринов А.С.");
                if(names!=1)throw new Exception("Wrong number of sender signatures: "+names);
                if(sender.StartsWith("ИП")&&table.Descendants(w+"tr").First().Elements(w+"tc").Take(1).Any(c=>c.Descendants(w+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value))))throw new Exception("Left signature remains");
            }
        });
    }
}
