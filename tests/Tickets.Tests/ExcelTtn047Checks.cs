using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Tickets.Core;

internal static class ExcelTtn047Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("047 native TTN preserves saved cell text and exports Excel",s=>{
            var invoice=new InvoiceInfo(47,1,"Тест","Зал","12.12.2026","Получатель","2026-11-02T10:20:00+00:00",Array.Empty<TicketInfo>());
            var source=ApprovedTtn.Fill(Store.DefaultRecipientForm("ttn")!.Content,invoice,DateTime.Today);
            using(var output=new MemoryStream())
            {
                output.Write(source);output.Position=0;
                using(var archive=new ZipArchive(output,ZipArchiveMode.Update,true))
                {
                    var entry=archive.GetEntry("word/document.xml")!;XDocument xml;using(var input=entry.Open())xml=XDocument.Load(input);
                    XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    var cell=xml.Descendants(w+"bookmarkStart").Single(x=>(string?)x.Attribute(w+"name")=="TtnCell_I16").Ancestors(w+"tc").First();
                    var texts=cell.Descendants(w+"t").ToArray();texts[0].Value="Плательщик, сохранённый вручную";foreach(var text in texts.Skip(1))text.Value="";
                    entry.Delete();using var target=archive.CreateEntry("word/document.xml").Open();xml.Save(target);
                }
                source=output.ToArray();
            }
            var info=new IssueDocumentInfo("ttn",invoice,"",""){SourceDocument=source};
            using(var archive=new ZipArchive(new MemoryStream(ExcelTtnData.CreateRequest(info))))
            using(var input=archive.GetEntry(ExcelTtnData.PayloadPath)!.Open())
            using(var json=JsonDocument.Parse(input))
                Check(json.RootElement.GetProperty("Pages")[0].GetProperty("Cells").GetProperty("I16").GetString()=="Плательщик, сохранённый вручную","saved payer text discarded");
            var path=Path.Combine(Path.GetDirectoryName(s.Path)!,"ttn.xlsx");InvoiceDocuments.SaveIssueDocumentWord(info,path);
            using var workbook=ZipFile.OpenRead(path);Check(workbook.GetEntry("xl/workbook.xml") is not null,"Excel export contains Word package");
            using var workbookXml=workbook.GetEntry("xl/workbook.xml")!.Open();var definition=XDocument.Load(workbookXml);
            XNamespace xl="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var areas=definition.Descendants(xl+"definedName").Where(x=>(string?)x.Attribute("name")=="_xlnm.Print_Area").ToArray();
            Check(areas.Length==2&&areas[0].Value.EndsWith("$A$1:$CQ$34")&&areas[1].Value.EndsWith("$A$1:$CQ$37"),"Excel print areas do not match original pages");
        });
        foreach(var count in new[]{1,7,12})
        test($"047 native Excel TTN preserves {count} price lines and full parties",_=>{
            var tickets=Enumerable.Range(1,count).Select(i=>new TicketInfo(i,i,"Партер","1",i.ToString(),"Получатель","TEST",i,i*100,"2026-11-02T10:20:00+00:00",null)).ToArray();
            var recipient=new OrganizationDetails(Name:"Получатель",LegalName:"Полное наименование получателя без сокращений",Address:"Полный адрес получателя",Bank:"Полное наименование банка",Account:"40703810800000000031",CorrespondentAccount:"30101810200000000111",Bik:"044525111",Position:"Председатель комитета первичной организации",Signatory:"Павлюков Александр Александрович");
            var invoice=new InvoiceInfo(47,1,"Длинное название мероприятия","Площадка","12.12.2026 19:00","Получатель","2026-11-02T10:20:00+00:00",tickets){RecipientDetails=recipient,ContractNumber="Д-047",ContractDate=new DateTime(2026,10,25),SenderDetails=new OrganizationDetails(Name:"Отправитель",LegalName:"Полный отправитель",Signatory:"Иванов Иван Иванович",Position:"Генеральный директор")};
            var bytes=ExcelTtnData.CreateRequest(new("ttn",invoice,"",""));
            Check(ExcelTtnData.IsRequest(bytes),"request marker missing");
            using var zip=new ZipArchive(new MemoryStream(bytes));using var stream=zip.GetEntry(ExcelTtnData.PayloadPath)!.Open();using var json=JsonDocument.Parse(stream);
            Check(json.RootElement.GetProperty("Version").GetInt32()==1,"wrong version");
            var pages=json.RootElement.GetProperty("Pages").EnumerateArray().ToArray();
            Check(pages.Length==(count<=7?2:4),"continuation page count wrong");
            var front=pages[0].GetProperty("Cells");var last=pages[^1].GetProperty("Cells");
            Check(front.GetProperty("I16").GetString()==front.GetProperty("L12").GetString(),"payer differs from recipient");
            foreach(var text in new[]{recipient.LegalName,recipient.Address,recipient.Bank,recipient.Account,recipient.CorrespondentAccount,recipient.Bik})Check(front.GetProperty("I16").GetString()!.Contains(text),"recipient detail clipped: "+text);
            Check(front.GetProperty("CF19").GetString()=="25.10.2026"&&front.GetProperty("BI26").GetString()=="02.11.2026","dates wrong");
            Check(last.GetProperty("BN67").GetString()==recipient.Position&&last.GetProperty("CF67").GetString()==recipient.Signatory,"signature abbreviated");
            Check(last.GetProperty("BB45").GetInt32()==count&&last.GetProperty("BQ45").GetDecimal()==invoice.Total,"grand totals wrong");
            var seen=new List<int>();decimal sum=0;
            for(var p=0;p<pages.Length;p++)
            {
                Check(pages[p].GetProperty("First").GetBoolean()==(p==0)&&pages[p].GetProperty("Last").GetBoolean()==(p==pages.Length-1),"page flags wrong");
                var cells=pages[p].GetProperty("Cells");
                foreach(var row in p==0?new[]{31,32,33}:new[]{40,41,42,43})
                {
                    var number=cells.GetProperty("A"+row);
                    if(number.ValueKind==JsonValueKind.String&&number.GetString()=="")continue;
                    seen.Add(number.GetInt32());
                    Check(cells.GetProperty("BB"+row).GetInt32()==1,"quantity type/value wrong");
                    var amount=cells.GetProperty("BQ"+row).GetDecimal();sum+=amount;
                    Check(amount==cells.GetProperty("BH"+row).GetDecimal()&&amount==cells.GetProperty("CI"+row).GetDecimal(),"line amount wrong");
                }
                if(p>0&&p<pages.Length-1)Check(cells.GetProperty("BB45").GetString()==""&&cells.GetProperty("CF67").GetString()=="","intermediate page contains final totals/signature");
            }
            Check(seen.SequenceEqual(Enumerable.Range(1,count))&&sum==invoice.Total,"items lost or duplicated");
            Check(last.GetProperty("AG65").GetString()==""&&last.GetProperty("Y47").GetString()=="","sample accountant/attachment retained");
            Check(!ExcelTtnData.IsRequest(new byte[]{1,2,3}),"invalid archive recognized");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
