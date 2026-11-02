using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Tickets.App;
using Tickets.Core;

internal static class Excel047UiChecks
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var buyer=new OrganizationDetails(Name:"Покупатель",LegalName:"МГО профсоюза работников Госучреждений, РОО МГО ПРГУ РФ (122)",Ogrn:"1027739838081",Inn:"7704026689",Kpp:"770401001",Address:"119034, г. Москва, Гагаринский переулок, дом 5 стр 1",Account:"40703810800000000031",Bank:"АО Россельхозбанк г. Москва",CorrespondentAccount:"30101810200000000111",Bik:"044525111",Signatory:"Павлюков А. А.",Position:"Председатель");
        var seller=new OrganizationDetails(Name:"Отправитель",LegalName:"ООО Альянс ТА",Inn:"7729768795",Kpp:"770901001",Address:"109548, г. Москва, ул. Шоссейная, д. 20, кв. 75",Signatory:"Заборский Л. Н.",Position:"Ген.директор");
        var existing=Process.GetProcessesByName("EXCEL").Select(p=>p.Id).ToHashSet();
        string? lastPath=null;InvoiceInfo? lastInvoice=null;
        foreach(var count in new[]{1,7,12})
        {
            var tickets=Enumerable.Range(1,count).Select(i=>new TicketInfo(i,i,"Партер","1",i.ToString(),"Покупатель","TEST",i,i*1000,"2026-11-02T10:20:00+00:00",null)).ToArray();
            var invoice=new InvoiceInfo(47,1,"Диско Хит 90-х","Государственный Кремлёвский Дворец","12.03.2027 19:00","Покупатель","2026-11-02T10:20:00+00:00",tickets){RecipientDetails=buyer,SenderDetails=seller,ContractNumber="0011-03/26"};
            var request=ExcelTtnData.CreateRequest(new("ttn",invoice,"",""));
            var clock=Stopwatch.StartNew();var path=WordDocumentPages.RenderAsync(request).GetAwaiter().GetResult();
            lastPath=path;lastInvoice=invoice;
            Console.WriteLine($"TIMING047 Excel {count} prices={clock.ElapsedMilliseconds}ms");
            using var zip=ZipFile.OpenRead(path);
            var pages=zip.Entries.Where(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)).Select(e=>{using var s=e.Open();return XDocument.Load(s);}).ToArray();
            Console.WriteLine("TTN047 pages="+pages.Length+" path="+path);
            Check(pages.Length==(count<=7?2:4),"Excel page count for "+count);
            Check(pages.All(p=>(double)p.Root!.Attribute("Width")!>(double)p.Root!.Attribute("Height")!),"all Excel pages landscape");
            var text=string.Concat(pages.SelectMany(p=>p.Descendants().Attributes("UnicodeString")).Select(a=>a.Value)).Replace(" ","");
            foreach(var required in new[]{buyer.Inn,buyer.Account,buyer.CorrespondentAccount,buyer.Bik})Check(text.Contains(required),"complete requisites rendered: "+required);
            Check(text.Contains("ТОВАРНАЯНАКЛАДНАЯ")&&text.Contains("Грузполучилгрузополучатель"),"front header and back signature blocks present");
            File.Copy(Path.Combine(Path.GetDirectoryName(path)!,"document.xlsx"),Path.Combine(output,"ttn-"+count+".xlsx"),true);
            File.Copy(path,Path.Combine(output,"ttn-"+count+".xps"),true);
            clock.Restart();Check(WordDocumentPages.RenderAsync(request).GetAwaiter().GetResult()==path,"same TTN uses cached pages");
            Console.WriteLine($"TIMING047 Excel repeat={clock.ElapsedMilliseconds}ms");
        }
        var root=Path.GetDirectoryName(Path.GetDirectoryName(lastPath!))!;
        var record=File.ReadAllLines(Path.Combine(root,"owned-excel.txt"));
        using(var owned=Process.GetProcessById(int.Parse(record[0])))
        {
            Check(!existing.Contains(owned.Id)&&owned.ProcessName.Equals("EXCEL",StringComparison.OrdinalIgnoreCase)&&owned.StartTime.ToUniversalTime().Ticks==long.Parse(record[1]),"crash simulation targets verified owned Excel only");
            owned.Kill();Check(owned.WaitForExit(5000),"owned Excel stopped for recovery test");
        }
        var recovery=ExcelTtnData.CreateRequest(new("ttn",lastInvoice! with {Title="После перезапуска Excel"},"",""));
        try{WordDocumentPages.RenderAsync(recovery).GetAwaiter().GetResult();}catch(TicketException){}
        var restored=WordDocumentPages.RenderAsync(recovery).GetAwaiter().GetResult();
        Check(File.Exists(restored),"Excel retry recovers after owned process exits");
        foreach(var id in existing){using var user=Process.GetProcessById(id);Check(!user.HasExited,"existing user Excel survives rendering and recovery");}
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
