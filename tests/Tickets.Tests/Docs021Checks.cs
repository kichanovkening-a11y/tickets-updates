using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

static class Docs021Checks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static void Register(Action<string,Action<Store>> test)
    {
        test("021 all six filled and edited Word exports have continuous vertical merge grids",s=>{
            const string recipient="Документы 021";s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000021",Signatory:"Петров П.П."));
            var invoice=Issue(s,recipient);
            foreach(var kind in new[]{"invoice","contract","act","bill","ttn","specification"})
            foreach(var edited in new[]{false,true})
            {
                if(edited){var raw=s.CustomIssueTemplate(invoice.EventId,invoice.Id,kind)!;s.SaveCustomIssueForm(invoice.EventId,invoice.Id,kind,Edit(raw));}
                var info=s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!;
                var path=s.Path+"-"+kind+(edited?"-edited":"")+".docx";InvoiceDocuments.SaveIssueDocumentWord(info,path);
                using var package=ZipFile.OpenRead(path);Require(package.Entries.Select(e=>e.FullName).Distinct().Count()==package.Entries.Count,"duplicate package part");
                using var stream=package.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
                AssertMerges(xml,kind+(edited?" edited":" filled"));
                Require(info.Body.Contains(invoice.Venue),"event venue missing: "+kind);
                if(edited)Require(info.Body.Contains("правка021"),"ordinary text edit lost: "+kind);
            }
        });
        test("021 organization representative changes reach existing packages and survive restart",s=>{
            const string recipient="Представитель 021";s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"original-021",Signatory:"Петров П.П.",Position:"Директор"));
            var first=Issue(s,recipient);var second=Issue(s,recipient);
            s.SetContractNumber(first.EventId,first.Id,"АВТО-021");s.SetContractDate(first.EventId,first.Id,new DateTime(2026,10,22));
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"new-021",Signatory:"Сидоров С.С.",Position:"Управляющий"));
            var reopened=new Store(s.Path);
            foreach(var invoice in new[]{first,second}){
                var current=reopened.Invoice(invoice.EventId,invoice.Id);Require(current.RecipientDetails!.Signatory=="Сидоров С.С."&&current.RecipientDetails.Position=="Управляющий","stale issue representative");
                Require(current.RecipientDetails.Inn=="original-021","unrelated historical requisite changed");
                foreach(var kind in new[]{"invoice","contract","act","bill","ttn","specification"})Require(reopened.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!.Body.Replace('\u00a0',' ').Contains("Сидоров С.С."),"stale document representative: "+kind);
            }
            Require(reopened.ContractNumber(first.EventId,first.Id)=="АВТО-021"&&reopened.ContractDate(first.EventId,first.Id)==new DateTime(2026,10,22),"contract metadata lost");
        });
    }
    private static byte[] Edit(byte[] bytes)
    {
        using var output=new MemoryStream();output.Write(bytes);output.Position=0;
        using(var package=new ZipArchive(output,ZipArchiveMode.Update,true)){
            var entry=package.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
            xml.Descendants(W+"t").First(t=>!string.IsNullOrWhiteSpace(t.Value)&&!t.Value.Contains('{')).Value+=" правка021";
            entry.Delete();using var target=package.CreateEntry("word/document.xml").Open();xml.Save(target);
        }
        return output.ToArray();
    }
    private static void AssertMerges(XDocument xml,string context)
    {
        foreach(var table in xml.Descendants(W+"tbl")){
            var previous=new Dictionary<int,int>();var rowIndex=0;
            foreach(var row in table.Elements(W+"tr")){
                var current=new Dictionary<int,int>();var column=(int?)row.Element(W+"trPr")?.Element(W+"gridBefore")?.Attribute(W+"val")??0;
                foreach(var cell in row.Elements(W+"tc")){
                    var span=(int?)cell.Element(W+"tcPr")?.Element(W+"gridSpan")?.Attribute(W+"val")??1;
                    var merge=cell.Element(W+"tcPr")?.Element(W+"vMerge");
                    if(merge is not null){if((string?)merge.Attribute(W+"val")!="restart")Require(previous.TryGetValue(column,out var prior)&&prior==span,$"{context}: orphan vertical merge at row {rowIndex}, grid {column}, span {span}");current[column]=span;}
                    column+=span;
                }
                previous=current;rowIndex++;
            }
        }
    }
    private static InvoiceInfo Issue(Store s,string name){var e=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var id=s.Issue(e.Id,[s.Seats(e.Id).First(x=>x.Available).Id],name,Guid.NewGuid().ToString("N")[..8],1);return s.Invoice(e.Id,id);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
