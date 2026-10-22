using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

static class Docs022Checks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static byte[] Resource(string group,string kind){using var stream=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.{group}.{kind}.docx")!;using var output=new MemoryStream();stream.CopyTo(output);return output.ToArray();}
    private static byte[] Edit(byte[] bytes,Action<XDocument> edit)
    {
        using var output=new MemoryStream();output.Write(bytes);output.Position=0;
        using(var package=new ZipArchive(output,ZipArchiveMode.Update,true)){var entry=package.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);edit(xml);entry.Delete();using var target=package.CreateEntry("word/document.xml").Open();xml.Save(target);}return output.ToArray();
    }
    private static XDocument Xml(byte[] bytes){using var package=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);using var stream=package.GetEntry("word/document.xml")!.Open();return XDocument.Load(stream);}
    private static InvoiceInfo Issue(Store s,int index)
    {
        var venue=s.CreateHall("Площадка022-"+index,"Адрес022-"+index,[new(){Number="1"},new(){Number="2",X=20},new(){Row="2",Number="1",Y=20}]);
        var seats=s.VenueSeats(venue);var plan=s.SavePlan(venue,"Цены",seats.Select((x,i)=>new PriceValue(x.Id,1000+i*500,s.PriceColors().First(c=>c.Amount==1000+i*500).Color)).ToArray());
        var evt=s.CreateEvent(s.SavePerformance("Спектакль022-"+index),venue,plan,new DateTime(2027,1,index+1,18,30,0));
        var buyer="Покупатель022-"+index;var sender="Исполнитель022-"+index;
        s.SaveOrganizationDetails(buyer,false,new(Name:buyer,LegalName:"ООО Полное наименование "+index,Inn:"770002200"+index,Position:"Директор",Signatory:"Петров Петр Петрович "+index,Authority:"Устава"));
        s.SaveOrganizationDetails(sender,true,new(Name:sender,LegalName:sender,Inn:"780002200"+index,Position:"Генеральный директор",Signatory:"Иванов Иван Иванович "+index,Authority:"Доверенности022"));
        s.SaveDocumentSettings(sender,s.DocumentsDirectory);
        var issue=s.Issue(evt,seats.Select(x=>x.Id).ToArray(),buyer,"Д022"+index,1);s.SetContractNumber(evt,issue,"К022/"+index);s.SetContractDate(evt,issue,new DateTime(2026,10,22));return s.Invoice(evt,issue);
    }
    public static void Register(Action<string,Action<Store>> test)
    {
        test("022 all forms fill actual venue address for different events and saved older starters",s=>{
            foreach(var n in new[]{1,2}){
                var invoice=Issue(s,n);
                foreach(var kind in new[]{"invoice","contract","act","bill","ttn","specification"}){
                    foreach(var saved in new[]{false,true}){
                        if(saved){var old=Resource(kind=="contract"?"SberForms":"UniversalForms",kind);old=Edit(old,xml=>xml.Descendants(W+"t").First(t=>!string.IsNullOrWhiteSpace(t.Value)&&!t.Value.Contains('{')).Value+=" правка022");s.SaveCustomIssueForm(invoice.EventId,invoice.Id,kind,old);}
                        var info=s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!;
                        Require(info.Body.Contains(invoice.Venue)&&info.Body.Contains("Адрес022-"+n),"venue/address missing "+kind);
                        Require(!info.Body.Contains("Московский театр Эстрады")&&!info.Body.Contains("Берсеневская"),"old venue leaked "+kind);
                        if(saved)Require(info.Body.Contains("правка022"),"ordinary edit lost "+kind);
                    }
                }
            }
        });
        test("022 bills have exactly one total service row across price groups and recipient layouts",s=>{
            var invoice=Issue(s,3);
            foreach(var template in new[]{Resource("UniversalForms","bill"),Resource("BusinessForms","bill"),Resource("UniversalForms","invoice")}){
                var result=CustomDocumentForms.Fill(template,invoice,"bill",invoice.ContractDate!.Value);var xml=Xml(result);
                var itemRows=xml.Descendants(W+"tr").Where(row=>!string.Concat(row.Descendants(W+"t").Select(t=>t.Value)).StartsWith("Всего к оплате")&&row.Elements(W+"tc").Any(c=>string.Concat(c.Descendants(W+"t").Select(t=>t.Value))==invoice.Tickets.Count.ToString())&&row.Elements(W+"tc").Any(c=>string.Concat(c.Descendants(W+"t").Select(t=>t.Value))==DocumentTotals.Amount(invoice.Total))).ToArray();
                Require(itemRows.Length==1,"bill split or totals missing: "+itemRows.Length+" / "+string.Join(" // ",itemRows.Select(r=>string.Concat(r.Descendants(W+"t").Select(t=>t.Value)))));Require(!CustomDocumentForms.Text(result).Contains("{item"),"unfilled item");
            }
            Require(BusinessDocumentTemplates.Items(invoice,"bill").Split('\n').Length==2,"plain bill split by price/place");
            var plainTemplate=Edit(Resource("UniversalForms","invoice"),xml=>xml.Root!.Element(W+"body")!.ReplaceNodes(new XElement(W+"p",new XElement(W+"r",new XElement(W+"t","{itemDescription} / {itemCount} / {itemTotal}")))));
            var plain=CustomDocumentForms.Text(CustomDocumentForms.Fill(plainTemplate,invoice,"bill",invoice.ContractDate!.Value));
            Require(plain.Split("Театральные билеты").Length==2,"standalone service description duplicated by price");
        });
        test("022 invoice keeps grouped places and full row borders including places column",s=>{
            var invoice=Issue(s,4);foreach(var kind in new[]{"invoice","specification"}){var info=s.CustomIssueDocument(invoice.EventId,invoice.Id,kind)!;var xml=Xml(info.SourceDocument!);
            var table=xml.Descendants(W+"tbl").First(t=>t.Descendants(W+"t").Any(x=>x.Value=="Места"||x.Value=="место"));
            Require(table.Elements(W+"tr").Count()==(kind=="invoice"?1:2)+InvoiceDocuments.Lines(invoice).Count,"grouped seats changed");
            foreach(var cell in table.Elements(W+"tr").Elements(W+"tc"))foreach(var edge in new[]{"top","left","bottom","right"})Require((string?)cell.Element(W+"tcPr")?.Element(W+"tcBorders")?.Element(W+edge)?.Attribute(W+"val")=="single","missing row/cell border: "+edge);}
        });
        test("022 service act has approved clauses parties dates appendix and aligned signatures",s=>{
            var invoice=Issue(s,5);var info=s.CustomIssueDocument(invoice.EventId,invoice.Id,"act")!;
            foreach(var text in new[]{"Приложение №2","СДАЧИ-ПРИЕМКИ УСЛУГ","1. В соответствии","2. Вышеуказанные услуги","3. Недостатки оказанных услуг не выявлены.","4. Сумма","5. Результаты услуг соответствуют","К022/5","22.10.2026","06.01.2027","ООО Полное наименование 5","Петров Петр Петрович 5","Иванов Иван Иванович 5","Устава","Доверенности022",RussianWords.Money(invoice.Total),"Адрес022-5"})Require(info.Body.Contains(text),"act missing "+text);
            var xml=Xml(info.SourceDocument!);var appendix=xml.Descendants(W+"p").First(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value)).Contains("Приложение №2"));Require((string?)appendix.Element(W+"pPr")?.Element(W+"jc")?.Attribute(W+"val")=="right","appendix not top right");
            Require(xml.Descendants(W+"tr").Any(r=>r.Elements(W+"tc").Count()==2&&r.Elements(W+"tc").All(c=>string.Concat(c.Descendants(W+"t").Select(t=>t.Value)).StartsWith("________"))),"signature baselines misaligned");
            s.SaveCustomIssueForm(invoice.EventId,invoice.Id,"act",Resource("UniversalForms","act-v021"));Require(s.CustomIssueDocument(invoice.EventId,invoice.Id,"act")!.Body.Contains("5. Результаты услуг"),"persisted standard act did not migrate");
        });
        test("022 Excel TTN grid compaction preserves occupied cell edges and all text",s=>{
            var before=Xml(Resource("UniversalForms","ttn"));var repaired=CustomDocumentForms.Map(Resource("UniversalForms","ttn"),"never-occurs-022","event");var after=Xml(repaired);
            AssertCompact(before,after);
            var noProperties=Edit(Resource("UniversalForms","invoice"),xml=>xml.Descendants(W+"tbl").First().ReplaceWith(new XElement(W+"tbl",new XElement(W+"tblPr"),new XElement(W+"tblGrid",Enumerable.Range(0,95).Select(_=>new XElement(W+"gridCol",new XAttribute(W+"w",100)))),new XElement(W+"tr",Enumerable.Range(0,95).Select(_=>new XElement(W+"tc",new XElement(W+"p")))))));
            var blank=Xml(CustomDocumentForms.Map(noProperties,"never-occurs-022","event"));
            Require(blank.Descendants(W+"tblGrid").All(g=>g.Elements(W+"gridCol").Count()<=63),"blank cells without properties failed to compact");
            var fixture=Environment.GetEnvironmentVariable("TICKETS_TTN_022_FIXTURE");if(!string.IsNullOrWhiteSpace(fixture)){var input=File.ReadAllBytes(fixture);var output=CustomDocumentForms.Map(input,"never-occurs-022","event");AssertCompact(Xml(input),Xml(output));File.WriteAllBytes(fixture+"-repaired.docx",output);}
        });
    }
    private static void AssertCompact(XDocument before,XDocument after)
    {
        var textBefore=before.Descendants(W+"t").Where(t=>!string.IsNullOrWhiteSpace(t.Value)).Select(t=>t.Value).ToArray();var textAfter=after.Descendants(W+"t").Where(t=>!string.IsNullOrWhiteSpace(t.Value)).Select(t=>t.Value).ToArray();Require(textBefore.SequenceEqual(textAfter),"TTN visible text changed");
        Require(after.Descendants(W+"tblGrid").All(g=>g.Elements(W+"gridCol").Count()<=63),"Word grid exceeds 63");
        static IEnumerable<string> Edges(XDocument xml){foreach(var table in xml.Descendants(W+"tbl")){var widths=table.Element(W+"tblGrid")!.Elements(W+"gridCol").Select(c=>(int)c.Attribute(W+"w")!).ToArray();foreach(var row in table.Elements(W+"tr")){var pos=(int?)row.Element(W+"trPr")?.Element(W+"gridBefore")?.Attribute(W+"val")??0;foreach(var c in row.Elements(W+"tc")){var span=(int?)c.Element(W+"tcPr")?.Element(W+"gridSpan")?.Attribute(W+"val")??1;if(c.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value)))yield return widths.Take(pos).Sum()+":"+widths.Take(pos+span).Sum()+":"+string.Concat(c.Descendants(W+"t").Select(t=>t.Value));pos+=span;}}}}
        Require(Edges(before).SequenceEqual(Edges(after)),"TTN occupied cell widths/positions changed");
    }
}
