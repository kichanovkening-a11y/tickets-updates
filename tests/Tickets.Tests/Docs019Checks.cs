using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

static class Docs019Checks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static void Register(Action<string,Action<Store>> test)
    {
        test("universal package uses recipient requisites and separate manual contract identity",s=>{
            const string recipient="Универсальный 019";
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,LegalName:"ООО Тестовый Покупатель",Inn:"7700019019",Address:"Тестовый адрес",Signatory:"Петров П.П.",Position:"Директор",Authority:"Устава"));
            var invoice=Issue(s,recipient);s.SetContractNumber(invoice.EventId,invoice.Id,"18-07/26");s.SetContractDate(invoice.EventId,invoice.Id,new DateTime(2026,7,20));
            foreach(var kind in new[]{"contract","act","ttn","bill","specification","invoice"})
            {
                var info=s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!;Require(info.SourceDocument is not null,"missing universal "+kind);
                Require(!info.Body.Contains("Микуленко")&&!info.Body.Contains("Сбербанк")&&!info.Body.Contains("доверенности №74"),"sample recipient leaked "+kind);
                Require(info.Body.Replace('\u00a0',' ').Contains("Петров П.П."),"representative missing "+kind);
                if(kind!="invoice")Require(info.Body.Contains("18-07/26")&&info.Body.Contains("20.07.2026"),"manual contract reference missing "+kind);
                Require(!System.Text.RegularExpressions.Regex.IsMatch(info.Body,@"\{\w+\}"),"unfilled token "+kind);
                var xml=InvoiceDocuments.IssueDocumentXml(info);AssertStructure(xml);
            }
            var other=Issue(s,recipient);s.SetContractRepresentative(invoice.EventId,invoice.Id,"Сидоров С.С.","Уполномоченный");
            foreach(var kind in new[]{"contract","act","ttn","bill","specification","invoice"})Require(s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!.Body.Replace('\u00a0',' ').Contains("Сидоров С.С.")||kind=="bill","representative not synchronized "+kind);
            Require(s.Invoice(other.EventId,other.Id).RecipientDetails!.Signatory=="Петров П.П.","representative corrupted another invoice");
            Require(s.GetOrganizationDetails(recipient,false).Signatory=="Петров П.П.","representative corrupted directory");
        });
        test("imported buyer representative updates only that package, and manual identity survives merge",s=>{
            const string recipient="Импорт договора 019";s.SaveOrganizationDetails(recipient,false,new(Name:recipient,LegalName:"ООО Покупатель 019",Inn:"7700019019",Signatory:"Петров П.П."));
            var first=Issue(s,recipient);var second=Issue(s,recipient);
            s.SetContractNumber(first.EventId,first.Id,"РУЧНОЙ-019");s.SetContractDate(first.EventId,first.Id,new DateTime(2026,7,20));
            var raw=s.CustomIssueTemplate(first.EventId,first.Id,"contract")!;
            using var output=new MemoryStream();output.Write(raw);output.Position=0;
            using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true)){
                var entry=zip.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
                var paragraph=xml.Descendants(W+"p").First(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)).Contains("{buyerRepresentative}"));
                var nodes=paragraph.Descendants(W+"t").ToArray();nodes[0].Value="ООО Отправитель, именуемое «Исполнитель», в лице Директора Иванова И.И., действующего на основании Устава, и ООО Покупатель 019, именуемое «Покупатель», в лице Директора Сидорова С.С., действующего на основании Устава, заключили договор.";foreach(var node in nodes.Skip(1))node.Value="";
                entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
            }
            s.SaveCustomIssueForm(first.EventId,first.Id,"contract",output.ToArray());
            Require(s.ContractRepresentative(first.EventId,first.Id).Signatory=="Сидорова С.С.","import selected sender or missed buyer");
            Require(s.Invoice(second.EventId,second.Id).RecipientDetails!.Signatory=="Петров П.П.","import corrupted unrelated issue");
            var merged=s.MergeInvoices(first.EventId,[first.Id,second.Id]);Require(s.ContractNumber(first.EventId,merged)=="РУЧНОЙ-019","merge lost manual contract identity");
            Require(s.ContractDate(first.EventId,merged)==new DateTime(2026,7,20),"merge lost contract date");
        });
        test("editing standard act and TTN ordinary text preserves compact fields and aligned signatures",s=>{
            const string recipient="Правка макета 019";s.SaveOrganizationDetails(recipient,false,new(Name:recipient,LegalName:"ООО Правка 019",Inn:"7700000019",Address:"Адрес для проверки",Bank:"Банк для проверки",Account:"4070000019",Signatory:"Петров П.П.",Position:"Директор"));
            var invoice=Issue(s,recipient);
            foreach(var kind in new[]{"act","ttn"})
            {
                var raw=s.CustomIssueTemplate(invoice.EventId,invoice.Id,kind)!;
                using var output=new MemoryStream();output.Write(raw);output.Position=0;
                using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true)){
                    var entry=zip.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
                    var ordinary=xml.Descendants(W+"t").First(t=>!string.IsNullOrWhiteSpace(t.Value)&&!t.Value.Contains('{'));ordinary.Value+=" · правка";
                    entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
                }
                s.SaveCustomIssueForm(invoice.EventId,invoice.Id,kind,output.ToArray());
                var info=s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!;Require(info.Body.Contains("правка"),"ordinary edit disappeared");
                var result=InvoiceDocuments.IssueDocumentXml(info);AssertStructure(result);
                Require(result.Descendants(W+"tr").Any(row=>row.Elements(W+"tc").Count()==2&&row.Elements(W+"tc").All(cell=>System.Text.RegularExpressions.Regex.IsMatch(string.Concat(cell.Descendants(W+"t").Select(t=>t.Value)).Trim(),@"^_{8,}$"))),"editing restored unaligned signatures "+kind);
                if(kind=="ttn"){
                    var details=result.Descendants(W+"p").First(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)).Contains("7700000019"));
                    Require(!details.Descendants(W+"br").Any()&&string.Concat(details.Descendants(W+"t").Select(t=>t.Value)).Contains("4070000019"),"editing expanded or lost inline requisites");
                    Require(info.Body.Contains(DocumentTotals.Total(invoice.Tickets.Count,invoice.Total)),"TTN canonical total missing");
                }
            }
        });
        test("bill borders follow bank and services blocks, specification appendix is one right aligned column",s=>{
            s.SaveOrganizationDetails("Макет 019",false,new(Name:"Макет 019",Inn:"7700019019"));var invoice=Issue(s,"Макет 019");
            var bill=InvoiceDocuments.IssueDocumentXml(s.GetIssueDocument(invoice.EventId,invoice.Id,"bill"));
            var bodyRow=bill.Descendants(W+"tr").First(r=>string.Concat(r.Descendants(W+"t").Select(t=>t.Value)).Contains("Заказчик:"));
            Require(bodyRow.Descendants(W+"tcBorders").Elements().All(e=>(string?)e.Attribute(W+"val")=="nil"),"bill body still gridded");
            var bankRow=bill.Descendants(W+"tr").First(r=>string.Concat(r.Descendants(W+"t").Select(t=>t.Value)).Contains("БИК"));
            Require(bankRow.Descendants(W+"tcBorders").Elements().Any(e=>(string?)e.Attribute(W+"val")=="single"),"bank borders missing");
            var spec=InvoiceDocuments.IssueDocumentXml(s.CustomIssueDocument(invoice.EventId,invoice.Id,"specification")!);
            Require(s.CustomIssueDocument(invoice.EventId,invoice.Id,"specification")!.Body.Contains(DocumentTotals.Total(invoice.Tickets.Count,invoice.Total)),"specification total differs from package standard");
            foreach(var kind in new[]{"contract","act","specification","ttn","invoice"})
            {
                var generated=InvoiceDocuments.IssueDocumentXml(s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!);
                Require(generated.Descendants(W+"tr").Any(row=>row.Elements(W+"tc").Count()==2&&row.Elements(W+"tc").All(cell=>System.Text.RegularExpressions.Regex.IsMatch(string.Concat(cell.Descendants(W+"t").Select(t=>t.Value)).Trim(),@"^_{8,}$"))),"signature baselines do not share one row: "+kind);
            }
            var ttn=InvoiceDocuments.IssueDocumentXml(s.CustomIssueDocument(invoice.EventId,invoice.Id,"ttn")!);
            Require(ttn.Descendants(W+"p").Any(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)).StartsWith("Мероприятие:")),"TTN lost event context when compacting item descriptions");

            foreach(var p in spec.Descendants(W+"p").Where(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)).StartsWith("Приложение № 1")))Require((string?)p.Element(W+"pPr")?.Element(W+"jc")?.Attribute(W+"val")=="right","appendix not right aligned");
        });
    }
    private static void AssertStructure(XDocument xml)
    {
        foreach(var cell in xml.Descendants(W+"tc"))Require(cell.Elements().Last().Name==W+"p","cell missing final paragraph");
        foreach(var props in xml.Descendants(W+"tcPr"))
        {
            var names=props.Elements().Select(e=>e.Name.LocalName).ToList();
            foreach(var before in new[]{"gridSpan","vMerge","tcBorders"})if(names.Contains(before)&&names.Contains("tcMar"))Require(names.IndexOf(before)<names.IndexOf("tcMar"),"OOXML tcPr order invalid: "+before);
        }
        foreach(var table in xml.Descendants(W+"tbl"))Require(!table.Elements(W+"tblBorders").Any(),"borders outside table properties");
    }
    private static InvoiceInfo Issue(Store s,string name){var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(e.Id).First(x=>x.Available);var id=s.Issue(e.Id,[seat.Id],name,Guid.NewGuid().ToString("N")[..8],1);return s.Invoice(e.Id,id);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
