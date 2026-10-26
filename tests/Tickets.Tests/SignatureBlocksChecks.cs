using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

internal static class SignatureBlocksChecks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static void Register(Action<string,Action<Store>> test)
    {
        test("024 all signature blocks separate title and unbroken surname initials",s=>{
            var evt=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));
            var id=s.Issue(evt.Id,[s.Seats(evt.Id).First(x=>x.Available).Id],"Подписи 024","SIG024",1);
            var invoice=s.Invoice(evt.Id,id) with{SenderDetails=new(Signatory:"Баринов Алексей Сергеевич",Position:"Предприниматель"),RecipientDetails=new(Signatory:"Уманец Олег Валерьевич",Position:"Председатель комитета первичной организации Профсоюза Центрального аппарата ПАО Сбербанк")};
            var directory=Path.Combine(Path.GetDirectoryName(s.Path)!,"signatures");Directory.CreateDirectory(directory);
            foreach(var kind in new[]{"invoice","specification","contract","act","bill","ttn"})
            {
                var starter=Store.DefaultRecipientForm(kind)!.Content;
                var filled=CustomDocumentForms.Fill(starter,invoice,kind,DateTime.Today);
                var xml=Xml(filled);Check(xml,kind);
                // A body wording edit still follows the signature layout rule.
                using var edited=new MemoryStream();edited.Write(starter);edited.Position=0;
                using(var zip=new ZipArchive(edited,ZipArchiveMode.Update,true))
                {
                    var entry=zip.GetEntry("word/document.xml")!;XDocument doc;using(var stream=entry.Open())doc=XDocument.Load(stream);
                    doc.Descendants(W+"t").First(t=>!t.Value.Contains('{')&&t.Value.Length>5).Value+=" Правка024";
                    entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();doc.Save(target);
                }
                Check(Xml(CustomDocumentForms.Fill(edited.ToArray(),invoice,kind,DateTime.Today)),"edited "+kind);
                File.WriteAllBytes(Path.Combine(directory,kind+".docx"),filled);
            }
            var invoiceFile=Path.Combine(directory,"direct-invoice.docx");InvoiceDocuments.SaveWord(invoice,invoiceFile);Check(Xml(File.ReadAllBytes(invoiceFile)),"direct invoice");
            foreach(var kind in IssueDocumentKinds.All)
            {
                var source=new IssueDocumentInfo(kind,invoice,BusinessDocumentTemplates.Default(kind),"");
                Check(InvoiceDocuments.IssueDocumentXml(source),"source "+kind);
                var labels=BusinessDocumentTemplates.PartyLabels(kind);
                var body=BusinessDocumentTemplates.Requisites(invoice.SenderDetails!)+"\n"+BusinessDocumentTemplates.Requisites(invoice.RecipientDetails!)+"\nУсловия авторского бланка.\n"+
                    labels.Seller+": "+invoice.SenderDetails!.Position+" ____________ / "+invoice.SenderDetails.Signatory+" /\n"+
                    labels.Buyer+": "+invoice.RecipientDetails!.Position+" ____________ / "+invoice.RecipientDetails.Signatory+" /";
                var fallback=source with{Template="Авторский бланк",Body=body};
                var fallbackXml=InvoiceDocuments.IssueDocumentXml(fallback);Check(fallbackXml,"fallback "+kind);
                if(!fallbackXml.Descendants(W+"t").Any(t=>t.Value=="Условия авторского бланка."))throw new Exception("fallback custom body lost "+kind);
            }
            if(SignatureBlocks.Name("Уманец О. В.")!="Уманец\u00a0О.В.")throw new Exception("initials normalization");
        });
    }
    private static XDocument Xml(byte[] bytes){using var zip=new ZipArchive(new MemoryStream(bytes));using var stream=zip.GetEntry("word/document.xml")!.Open();return XDocument.Load(stream);}
    private static void Check(XDocument xml,string kind)
    {
        var table=xml.Root!.Element(W+"body")!.Elements(W+"tbl").Last();
        var line=table.Elements(W+"tr").First(r=>r.Elements(W+"tc").All(c=>string.IsNullOrWhiteSpace(Text(c))||Text(c).StartsWith("________"))&&r.Descendants(W+"t").Any(t=>t.Value.StartsWith("________")));
        if(line.Elements(W+"tc").Count()!=2)throw new Exception("two baseline columns "+kind);
        var gap=line.PreviousNode as XElement;if(gap is null||gap.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value)))throw new Exception("blank below identities "+kind);
        var names=(XElement)gap.PreviousNode!;
        foreach(var cell in names.Elements(W+"tc"))
        {
            if(string.IsNullOrWhiteSpace(Text(cell)))continue;
            if(kind.Contains("bill")&&Text(cell).Contains("Бухгалтер:"))
            {if(Text(cell).Contains("Баринов"))throw new Exception("director copied into accountant");continue;}
            var paragraphs=cell.Elements(W+"p").ToArray();
            if(paragraphs.Length<2)throw new Exception("position and name must be separate paragraphs "+kind);
            var expectedAlignment=cell==names.Elements(W+"tc").First()?"left":"right";
            if(paragraphs.Any(p=>(string?)p.Element(W+"pPr")?.Element(W+"jc")?.Attribute(W+"val")!=expectedAlignment))throw new Exception("signature outer alignment "+kind);
            if((string?)cell.Element(W+"tcPr")?.Element(W+"vAlign")?.Attribute(W+"val")=="bottom")throw new Exception("signature titles must start together "+kind);
            var name=Text(paragraphs[1]);
            if(name is not ("Баринов\u00a0А.С." or "Уманец\u00a0О.В."))throw new Exception("surname initials own line "+kind+": "+name);
            if(Text(paragraphs[0]).Contains("Уманец")||Text(paragraphs[0]).Contains("Баринов"))throw new Exception("name in position "+kind);
            if(paragraphs[1].Descendants(W+"br").Any()||name.Contains(' '))throw new Exception("breakable signatory "+kind);
        }
    }
    private static string Text(XElement element)=>string.Concat(element.Descendants(W+"t").Select(t=>t.Value));
}
