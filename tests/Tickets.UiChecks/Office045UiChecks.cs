using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Xps.Packaging;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tickets.App;
using Tickets.Core;

internal static class Office045UiChecks
{
    internal static void Run()
    {
        if(Type.GetTypeFromProgID("Word.Application") is null)throw new Exception("Native Office verification requires installed Word");
        BeforeOpenCleanup();
        TemplateSavedByWord();
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.WordDocumentPages",true)!;
        var render=type.GetMethod("RenderAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
        var before=Process.GetProcessesByName("WINWORD").Select(p=>p.Id).ToHashSet();
        var bytes=Fixture();var untouched=bytes.ToArray();
        Task<string> Start()=>(Task<string>)render.Invoke(null,[bytes])!;
        var first=Start();var second=Start();Wait(first);Wait(second);
        Check(bytes.AsSpan().SequenceEqual(untouched),"Word rendering leaves the filled source bytes intact");
        Check(first.Result==second.Result,"simultaneous preview and print requests reuse the same fixed pages");
        using(var zip=ZipFile.OpenRead(first.Result))
        {
            var content=zip.Entries.Where(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.FullName).Select(e=>{using var input=e.Open();return string.Concat(XDocument.Load(input).Descendants().Attributes("UnicodeString").Select(a=>a.Value));}).ToArray();
            Check(content.Length==2&&content[0].Contains("First native page")&&content[1].Contains("Second native page"),"the fixed pages retain the source text on the correct pages");
        }
        using(var xps=new XpsDocument(first.Result,FileAccess.Read))
        {
            var pages=xps.GetFixedDocumentSequence().DocumentPaginator;pages.ComputePageCount();
            Check(pages.PageCount==2,"Word preserves both explicitly separated source pages");
            for(var i=0;i<2;i++)
            {
                var page=pages.GetPage(i);
                Check(Math.Abs(page.Size.Width-1122.53)<2&&Math.Abs(page.Size.Height-793.73)<2,"Word preserves A4 landscape dimensions on page "+(i+1));
            }
        }
        Check(!Process.GetProcessesByName("WINWORD").Any(p=>!before.Contains(p.Id)),"the renderer closes only its own invisible Word instance");
        using(var firstView=new WordPageLease(first.Result))
        using(var secondView=new WordPageLease(first.Result))
        {
            firstView.Dispose();
            Check(secondView.Pages.DocumentPaginator.GetPage(0).Size.Width>1100,"closing another view preserves the remaining view's page and font resources");
        }
        var preview=new DocumentPagePreview();preview.SetDocument(bytes,_=>new FlowDocument());
        var host=new Window{Content=preview,Width=900,Height=650,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};host.Show();
        try
        {
            var deadline=DateTime.UtcNow.AddSeconds(75);
            while(!Logical(preview).OfType<DocumentPageView>().Any()&&DateTime.UtcNow<deadline){Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(20);}
            var displayed=Logical(preview).OfType<DocumentPageView>().ToArray();
            Check(displayed.Length==2,"the document tab displays both native Word pages");
            var size=displayed[0].DocumentPaginator.GetPage(0).Size;host.Width=500;host.UpdateLayout();
            Check(displayed[0].DocumentPaginator.GetPage(0).Size==size,"narrowing the document window does not change Word pagination");
        }
        finally{host.Close();}
        foreach(var count in new[]{47,48,98})
        {
            var invoice=new InvoiceInfo(1,1,"Проверка договора","Зал","01.11.2026","Проверка организации","2026-11-01T12:00:00Z",[]);
            var contract=CustomDocumentForms.Fill(ContractFixture(count),invoice,"contract",new DateTime(2026,11,1));
            var rendered=WordDocumentPages.RenderAsync(contract);Wait(rendered);
            using var zip=ZipFile.OpenRead(rendered.Result);
            var content=zip.Entries.Where(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.FullName).Select(e=>{using var input=e.Open();return string.Concat(XDocument.Load(input).Descendants().Attributes("UnicodeString").Select(a=>a.Value));}).ToArray();
            Check(content.Length>=2&&content[^1].Contains("Last substantive clause")&&content[^1].Contains("Recipient signature"),"a multipage contract keeps the last clause with its signatures for "+count+" preceding lines");
        }
        {
            var invoice=new InvoiceInfo(1,1,"Договор с приложением","Зал","01.11.2026","Получатель","2026-11-01T12:00:00Z",[]);
            var contract=CustomDocumentForms.Fill(ContractFixture(48,true),invoice,"contract",DateTime.Today);
            var rendered=WordDocumentPages.RenderAsync(contract);Wait(rendered);using var zip=ZipFile.OpenRead(rendered.Result);
            var content=zip.Entries.Where(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.FullName).Select(e=>{using var input=e.Open();return string.Concat(XDocument.Load(input).Descendants().Attributes("UnicodeString").Select(a=>a.Value));}).ToArray();
            Check(content.Length>=3&&content[^1].Contains("Appendix contents")&&content[^2].Contains("Last substantive clause")&&content[^2].Contains("Recipient signature"),"a contract appendix stays on its own page after the last clause and signatures");
        }
        foreach(var count in new[]{1,3,7,12})
        {
            var tickets=Enumerable.Range(1,count).Select(i=>new TicketInfo(i,i,"Партер","1",i.ToString(),"Получатель 045","TEST",i,1000*i,"2026-11-01T12:00:00Z",null)).ToArray();
            var invoice=new InvoiceInfo(65,1,"Любовь и голуби","Театр Русская песня","03.11.2026 19:00:00","Получатель 045","2026-11-01T12:00:00Z",tickets){SenderDetails=new OrganizationDetails(Name:"Отправитель 045",Inn:"7700000045",Signatory:"Иванов И. И."),RecipientDetails=new OrganizationDetails(Name:"Получатель 045",Inn:"7700000046",Signatory:"Петров П. П.")};
            var filled=CustomDocumentForms.Fill(Store.DefaultRecipientForm("ttn")!.Content,invoice,"ttn",new DateTime(2026,11,1));
            var path=Path.Combine(Path.GetTempPath(),"Tickets039-diagnostic","ttn045","filled-"+count+".docx");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,filled);
            var rendered=WordDocumentPages.RenderAsync(filled);Wait(rendered);using var zip=ZipFile.OpenRead(rendered.Result);
            var pages=zip.Entries.Where(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.FullName).ToArray();
            Check(pages.Length==2+Math.Max(0,(count-7+3)/4),"approved TTN keeps its printed sides and adds continuation pages for "+count+" prices");
            foreach(var page in pages){using var input=page.Open();var xml=XDocument.Load(input);Check(double.Parse(xml.Root!.Attribute("Width")!.Value,System.Globalization.CultureInfo.InvariantCulture)>1100,"each TTN side stays landscape");}
        }
        var tooLong=new InvoiceInfo(66,1,"Проверка","Зал","01.11.2026","Проверка","2026-11-01T12:00:00Z",[]){RecipientDetails=new OrganizationDetails(Name:string.Join(" ",Enumerable.Repeat("Длинные реквизиты",100)))};
        var oversized=CustomDocumentForms.Fill(Store.DefaultRecipientForm("ttn")!.Content,tooLong,"ttn",DateTime.Today);
        var refused=false;
        try{Wait(WordDocumentPages.RenderAsync(oversized));}catch(TicketException ex){refused=ex.Message.Contains("поле",StringComparison.OrdinalIgnoreCase)&&ex.Message.Contains("ТТН");}
        Check(refused,"approved TTN refuses to silently clip text outside fixed source cells");
    }
    private static void TemplateSavedByWord()
    {
        var directory=Path.Combine(Path.GetTempPath(),"Tickets-word-template-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        object? document=null;
        try
        {
            var source=Path.Combine(directory,"template.docx");var target=Path.Combine(directory,"saved.docx");File.WriteAllBytes(source,Store.DefaultRecipientForm("ttn")!.Content);
            using(var session=WordAutomationSession.Start(directory))
            {
                try{document=session.OpenDocument(source);((dynamic)document).SaveAs2(FileName:target,FileFormat:16);}
                finally{if(document is not null){((dynamic)document).Close(SaveChanges:0);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(document);document=null;}}
            }
            var saved=File.ReadAllBytes(target);Check(ApprovedTtn.IsApproved(saved),"Word-saved TTN keeps its approved identity after custom XML parts are renamed");
            var invoice=new InvoiceInfo(1,1,"ТТН после Word","Зал","01.11.2026","Получатель","2026-11-01T12:00:00Z",[]);
            var filled=CustomDocumentForms.Fill(saved,invoice,"ttn",DateTime.Today);
            Check(!CustomDocumentForms.Text(filled).Contains('{'),"automatic TTN fields still fill after Word saves the template");
            var rendered=WordDocumentPages.RenderAsync(filled);Wait(rendered);
            using var zip=ZipFile.OpenRead(rendered.Result);Check(zip.Entries.Count(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase))==2,"Word-saved TTN still renders its two approved sides");
        }
        finally{Directory.Delete(directory,true);}
    }
    private static void BeforeOpenCleanup()
    {
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.WordAutomationSession");
        Check(type is not null,"Word establishes an owned session before opening supplied files");
        var start=type!.GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Static)!;
        var directory=Path.Combine(Path.GetTempPath(),"Tickets-office-ownership-"+Guid.NewGuid().ToString("N"));
        var unrelated=Path.Combine(directory,"unrelated");var rendering=Path.Combine(directory,"rendering");Directory.CreateDirectory(unrelated);Directory.CreateDirectory(rendering);
        IDisposable? other=null,worker=null;
        try
        {
            other=(IDisposable)start.Invoke(null,[unrelated])!;
            worker=(IDisposable)start.Invoke(null,[rendering])!;
            var otherPid=int.Parse(File.ReadAllLines(Path.Combine(unrelated,"owned-word.txt"))[0]);
            var workerPid=int.Parse(File.ReadAllLines(Path.Combine(rendering,"owned-word.txt"))[0]);
            Check(otherPid!=workerPid,"the render job owns a different Word process from an existing session");
            WordRenderWorker.StopOwnedWord(rendering);
            using var live=Process.GetProcessById(otherPid);
            var until=DateTime.UtcNow.AddSeconds(5);
            while(Process.GetProcessesByName("WINWORD").Any(p=>p.Id==workerPid)&&DateTime.UtcNow<until)Thread.Sleep(20);
            Check(!Process.GetProcessesByName("WINWORD").Any(p=>p.Id==workerPid)&&!live.HasExited,"timeout cleanup before Documents.Open closes only the recorded render instance");
        }
        finally{worker?.Dispose();other?.Dispose();Directory.Delete(directory,true);}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var value in Logical(child))yield return value;}
    private static void Wait(Task task)
    {
        var deadline=DateTime.UtcNow.AddSeconds(75);
        while(!task.IsCompleted&&DateTime.UtcNow<deadline){Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(20);}
        if(!task.IsCompleted)throw new Exception("Office render did not finish");task.GetAwaiter().GetResult();
    }
    private static byte[] Fixture()
    {
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            void Part(string name,string text){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(text);}
            Part("[Content_Types].xml","<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/><Default Extension='xml' ContentType='application/xml'/><Override PartName='/word/document.xml' ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/></Types>");
            Part("_rels/.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='word/document.xml'/></Relationships>");
            Part("word/document.xml","<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:rPr><w:rFonts w:ascii='Arial'/><w:sz w:val='24'/></w:rPr><w:t>First native page</w:t><w:br w:type='page'/></w:r></w:p><w:p><w:r><w:t>Second native page</w:t></w:r></w:p><w:sectPr><w:pgSz w:w='16838' w:h='11906' w:orient='landscape'/><w:pgMar w:top='510' w:right='567' w:bottom='397' w:left='907' w:header='283' w:footer='283' w:gutter='0'/></w:sectPr></w:body></w:document>");
        }
        return output.ToArray();
    }
    private static byte[] ContractFixture(int preceding,bool appendix=false)
    {
        using var output=new MemoryStream();output.Write(Fixture());output.Position=0;
        const string w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        string P(string text)=>"<w:p><w:pPr><w:spacing w:before='0' w:after='0' w:line='280' w:lineRule='exact'/></w:pPr><w:r><w:rPr><w:rFonts w:ascii='Arial'/><w:sz w:val='24'/></w:rPr><w:t>"+text+"</w:t></w:r></w:p>";
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            zip.GetEntry("word/document.xml")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write("<w:document xmlns:w='"+w+"'><w:body>"+string.Concat(Enumerable.Range(0,preceding).Select(i=>P("Contract provision "+i)))+P("Last substantive clause")+P("7. Реквизиты сторон")+"<w:tbl><w:tblPr><w:tblW w:w='9026' w:type='dxa'/></w:tblPr><w:tblGrid><w:gridCol w:w='4513'/><w:gridCol w:w='4513'/></w:tblGrid><w:tr><w:trPr><w:cantSplit/><w:trHeight w:val='1600' w:hRule='exact'/></w:trPr><w:tc><w:tcPr><w:tcW w:w='4513' w:type='dxa'/></w:tcPr>"+P("Sender signature")+"</w:tc><w:tc><w:tcPr><w:tcW w:w='4513' w:type='dxa'/></w:tcPr>"+P("Recipient signature")+"</w:tc></w:tr></w:tbl>"+(appendix?"<w:p><w:r><w:br w:type='page'/></w:r></w:p>"+P("Приложение № 1")+P("Appendix contents"):"")+"<w:sectPr><w:pgSz w:w='11906' w:h='16838'/><w:pgMar w:top='1440' w:right='1440' w:bottom='1440' w:left='1440' w:header='0' w:footer='0' w:gutter='0'/></w:sectPr></w:body></w:document>");
        }
        return output.ToArray();
    }
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
}
