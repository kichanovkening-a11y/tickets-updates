using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace Tickets.Core;

/// <summary>Cell values for the original Excel grid. Geometry belongs exclusively to the XLS.</summary>
public static class ExcelTtnData
{
    public const string PayloadPath="customXml/tickets-ttn047.json";
    private static readonly CultureInfo Ru=CultureInfo.GetCultureInfo("ru-RU");
    private sealed record Line(InvoiceInfo Show,int Count,long Price)
    {public decimal Total=>(decimal)Count*Price;}
    private sealed record Page(Dictionary<string,object> Cells,bool First,bool Last);
    private sealed record Request(int Version,IReadOnlyList<Page> Pages);

    public static bool CanRender(IssueDocumentInfo info)=>info.Kind==IssueDocumentKinds.Waybill&&(info.SourceDocument is null?(string.IsNullOrEmpty(info.Template)||info.Template==IssueDocumentKinds.DefaultTemplate(IssueDocumentKinds.Waybill)):ApprovedTtn.IsApproved(info.SourceDocument));

    public static bool IsRequest(byte[] bytes)
    {
        try
        {
            using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
            var entry=zip.GetEntry(PayloadPath);if(entry is null)return false;
            using var stream=entry.Open();using var json=JsonDocument.Parse(stream);
            return json.RootElement.ValueKind==JsonValueKind.Object&&json.RootElement.TryGetProperty("Version",out var version)&&version.ValueKind==JsonValueKind.Number&&version.TryGetInt32(out var number)&&number==1&&json.RootElement.TryGetProperty("Pages",out var pages)&&pages.ValueKind==JsonValueKind.Array&&pages.GetArrayLength()>=2;
        }
        catch(Exception ex) when(ex is InvalidDataException or JsonException or ArgumentException){return false;}
    }

    public static byte[] CreateRequest(IssueDocumentInfo info)
    {
        if(info.Kind!=IssueDocumentKinds.Waybill)throw new ArgumentException("Excel TTN requires a waybill.",nameof(info));
        var invoice=info.Invoice;
        var shows=invoice.PackageShows.Count>0?invoice.PackageShows:new[]{invoice};
        var lines=shows.SelectMany(show=>show.Tickets.GroupBy(t=>t.Amount).OrderByDescending(g=>g.Key).Select(g=>new Line(show,g.Count(),g.Key))).ToArray();
        var values=SourceBusinessForms.Values(invoice,IssueDocumentKinds.Waybill);
        var sender=DocumentParties.Sender(invoice);
        var recipient=invoice.RecipientDetails??new OrganizationDetails(Name:invoice.Recipient);
        var senderInline=string.Join(", ",new[]{invoice.DocumentSender,string.IsNullOrWhiteSpace(sender.Inn)?"":"ИНН/КПП "+string.Join("/",new[]{sender.Inn,sender.Kpp}.Where(x=>!string.IsNullOrWhiteSpace(x))),sender.Address}.Where(x=>!string.IsNullOrWhiteSpace(x)));
        var date=DateTimeOffset.Parse(invoice.IssuedAt);
        string Description(InvoiceInfo show)=>$"Театральные билеты на мероприятие «{show.Title}». {show.VenueWithAddress}. {show.EventDate}.";
        var pages=new List<Page>();
        void AddPage(int start,bool first,bool last)
        {
            var cells=new Dictionary<string,object>(StringComparer.Ordinal);
            var rows=first?new[]{31,32,33}:new[]{40,41,42,43};
            var selected=lines.Skip(start).Take(rows.Length).ToArray();
            for(var i=0;i<rows.Length;i++)
            {
                var row=rows[i];
                foreach(var col in new[]{"A","X","BB","BH","BQ","CI"})cells[col+row]="";
                if(!first)cells["D"+row]="";
                if(i>=selected.Length)continue;
                var line=selected[i];
                cells["A"+row]=start+i+1;cells["X"+row]="шт";cells["BB"+row]=line.Count;
                cells["BH"+row]=line.Price;cells["BQ"+row]=line.Total;cells["CI"+row]=line.Total;
                if(!first)cells["D"+row]=Description(line.Show);
            }
            var subtotal=first?34:44;
            cells["BB"+subtotal]=selected.Sum(l=>l.Count);cells["BQ"+subtotal]=selected.Sum(l=>l.Total);cells["CI"+subtotal]=selected.Sum(l=>l.Total);
            if(first)
            {
                cells["A7"]=senderInline;cells["I14"]=senderInline;
                cells["L12"]=values["recipientDetailsInline"];cells["I16"]=values["recipientDetailsInline"];
                cells["I18"]="Договор";cells["CF17"]=values["contractNumber"];cells["CF19"]=values["contractDate"];
                cells["AX26"]=values["issueId"];cells["BI26"]=date.ToString("dd.MM.yyyy",Ru);
                cells["D31"]=string.Join("\n",selected.Select(l=>l.Show).Distinct().Select(Description));
            }
            else
            {
                // Always overwrite every sample footer, even on continuation pages.
                foreach(var address in new[]{"BB45","BQ45","CI45","Y47","K48","K54","N59","L63","AG63","AG65","L67","AG67","BN67","CF67","N70","R70","AA70","BE70","BI70","BR70"})cells[address]="";
                if(last)
                {
                    cells["BB45"]=lines.Sum(l=>l.Count);cells["BQ45"]=lines.Sum(l=>l.Total);cells["CI45"]=lines.Sum(l=>l.Total);
                    cells["K48"]=RussianWords.Number(lines.Length);cells["K54"]=RussianWords.Number(lines.Sum(l=>l.Count));cells["N59"]=RussianWords.Money(lines.Sum(l=>l.Total));
                    cells["L63"]=sender.Position;cells["L67"]=sender.Position;cells["AG63"]=sender.Signatory;cells["AG67"]=sender.Signatory;
                    cells["BN67"]=recipient.Position;cells["CF67"]=string.IsNullOrWhiteSpace(recipient.Signatory)?invoice.DocumentRecipient:recipient.Signatory;
                    cells["N70"]=date.Day.ToString(Ru);cells["BE70"]=date.Day.ToString(Ru);
                    cells["R70"]=Ru.DateTimeFormat.MonthGenitiveNames[date.Month-1];cells["BI70"]=Ru.DateTimeFormat.MonthGenitiveNames[date.Month-1];
                    cells["AA70"]=date.Year.ToString(Ru);cells["BR70"]=date.Year.ToString(Ru);
                }
            }
            pages.Add(new(cells,first,last));
        }
        AddPage(0,true,false);
        for(var start=3;;start+=4)
        {
            var last=start+4>=lines.Length;AddPage(start,false,last);if(last)break;
        }
        PreserveSavedCells(info,pages);
        var source=info.SourceDocument??StandardDocumentForms.Resolve(IssueDocumentKinds.Waybill,null)??throw new TicketException("Не найден шаблон ТТН.");
        using var output=new MemoryStream();output.Write(source);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            zip.GetEntry(PayloadPath)?.Delete();using var stream=zip.CreateEntry(PayloadPath).Open();JsonSerializer.Serialize(stream,new Request(1,pages));
        }
        return output.ToArray();
    }
    private static void PreserveSavedCells(IssueDocumentInfo info,List<Page> pages)
    {
        if(info.SourceDocument is not {} saved)return;
        if(!ApprovedTtn.IsApproved(saved))
            throw new TicketException("Сохранённая ТТН не содержит разметку исходной формы. Её текст сохранён; автоматическая замена другой формой отменена.");
        var baseline=ApprovedTtn.Fill(StandardDocumentForms.Resolve("ttn",null)!,info.Invoice,info.Invoice.ContractDate??DateTimeOffset.Parse(info.Invoice.IssuedAt).Date);
        static Dictionary<string,string> Fields(byte[] bytes)
        {
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            using var zip=new ZipArchive(new MemoryStream(bytes));using var input=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(input);
            var result=new Dictionary<string,string>();
            var edges=new int[][]{new[]{0,1,3,8,11,19,23,28,29,33,38,43,48,49,53,59,60,63,66,68,71,74,75,76,79,82,83,86,95},new[]{0,3,6,10,11,12,13,15,17,19,20,21,23,24,25,26,27,28,29,31,32,33,35,38,39,43,44,45,47,48,49,53,55,56,58,59,60,65,66,68,69,72,73,75,76,77,78,79,80,82,83,86,88,89,92,95}};
            var tables=xml.Root!.Element(w+"body")!.Elements(w+"tbl").ToArray();
            for(var page=0;page<tables.Length;page++)
            {
                var rows=tables[page].Elements(w+"tr").ToArray();var map=edges[page==0?0:1];
                if(rows.Length!=(page==0?34:37))throw new TicketException("Изменена сетка сохранённой ТТН; автоматическая замена отменена.");
                for(var row=0;row<rows.Length;row++)
                {
                    var column=0;
                    foreach(var cell in rows[row].Elements(w+"tc"))
                    {
                        var properties=cell.Element(w+"tcPr");var span=(int?)properties?.Element(w+"gridSpan")?.Attribute(w+"val")??1;
                        if(column+span>=map.Length)throw new TicketException("Изменена сетка сохранённой ТТН.");
                        var merge=properties?.Element(w+"vMerge");
                        if(merge is null||(string?)merge.Attribute(w+"val")=="restart")
                        {
                            var n=map[column]+1;var address="";while(n>0){n--;address=(char)('A'+n%26)+address;n/=26;}
                            address+=(row+(page==0?1:35)).ToString(CultureInfo.InvariantCulture);
                            var key="TtnCell_"+address+(page>1?"_p"+(page+1):"");
                            result[key]=string.Join("\n",cell.Elements(w+"p").Select(p=>string.Concat(p.Descendants().Where(x=>x.Name==w+"t"||x.Name==w+"br").Select(x=>x.Name==w+"br"?"\n":x.Value))));
                        }
                        column+=span;
                    }
                }
            }
            return result;
        }
        var expected=Fields(baseline);
        foreach(var pair in Fields(saved))
        {
            if(expected.TryGetValue(pair.Key,out var previous)&&pair.Value==previous)continue;
            var parts=pair.Key[8..].Split("_p",StringSplitOptions.None);var address=parts[0];
            var row=int.Parse(new string(address.Where(char.IsDigit).ToArray()),CultureInfo.InvariantCulture);
            var page=parts.Length>1?int.Parse(parts[1],CultureInfo.InvariantCulture)-1:row<=34?0:1;
            if(page>=pages.Count)throw new TicketException("Сохранённая ТТН содержит дополнительные страницы; автоматическая замена отменена.");
            object value=pair.Value;
            if(pages[page].Cells.TryGetValue(address,out var original)&&original is not string&&decimal.TryParse(pair.Value,NumberStyles.Number,Ru,out var number))value=number;
            pages[page].Cells[address]=value;
        }
    }

}
