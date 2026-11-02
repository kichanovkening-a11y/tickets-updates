using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Rectangle=System.Windows.Shapes.Rectangle;
using System.Windows.Xps.Packaging;
using Tickets.Core;

namespace Tickets.App;

// One immutable Word export is shared by preview and printing. Office runs in
// an isolated helper process, without opening a Store or starting the main UI.
internal static class WordDocumentPages
{
    private static readonly string directory=Path.Combine(Path.GetTempPath(),"Tickets-Document-pages-"+Guid.NewGuid().ToString("N"));
    private static readonly ConcurrentDictionary<string,Lazy<Task<string>>> cache=new();
    private static readonly object workerLock=new();
    private static Process? worker;
    private static readonly SemaphoreSlim gate=new(1);
    private static readonly CancellationTokenSource shutdown=new();
    static WordDocumentPages()
    {
        if(Application.Current is {} app)app.Exit+=(_,_)=>Stop();
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>Stop();
    }
    internal static bool Available=>Type.GetTypeFromProgID("Word.Application") is not null;
    internal static bool IsAvailable(byte[] document)
    {
        using var zip=new ZipArchive(new MemoryStream(document),ZipArchiveMode.Read);
        return zip.GetEntry("customXml/tickets-ttn047.json") is not null
            ?Type.GetTypeFromProgID("Excel.Application") is not null:Available;
    }
    internal static Task<string> RenderAsync(byte[] document)
    {
        var bytes=document.ToArray();
        if(!IsAvailable(bytes))return Task.FromException<string>(new TicketException("Для точного просмотра установите Microsoft Office."));
        var key=DocumentRenderKey.Create(bytes);
        return cache.GetOrAdd(key,_=>new Lazy<Task<string>>(()=>ExportAsync(key,bytes))).Value;
    }
    private static async Task<string> ExportAsync(string key,byte[] bytes)
    {
        await gate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        var job=Path.Combine(directory,key);
        try
        {
            Directory.CreateDirectory(job);await File.WriteAllBytesAsync(Path.Combine(job,"document.docx"),bytes,shutdown.Token).ConfigureAwait(false);
            var error=Path.Combine(job,"error.txt");if(File.Exists(error))File.Delete(error);
            var process=GetWorker();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
            string? response;
            try
            {
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(job).AsMemory(),timeout.Token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
                response=await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            }
            catch(OperationCanceledException){StopWorker();throw new TicketException("Office не завершил подготовку страниц за минуту. Закройте запросы Office и повторите просмотр.");}
            catch(IOException){StopWorker();throw new TicketException("Office прервал подготовку страниц. Повторите просмотр.");}
            var path=Path.Combine(job,"pages.xps");
            if(response!="0"||!File.Exists(path))
            {
                if(response is null)StopWorker();
                throw new TicketException(File.Exists(error)?await File.ReadAllTextAsync(error).ConfigureAwait(false):"Office не смог подготовить страницы документа.");
            }
            return path;
        }
        catch{cache.TryRemove(key,out _);throw;}
        finally{gate.Release();}
    }
    private static Process GetWorker()
    {
        lock(workerLock)
        {
            shutdown.Token.ThrowIfCancellationRequested();
            if(worker is not null&&!worker.HasExited)return worker;
            if(worker is not null)StopWorker();
            Directory.CreateDirectory(directory);
            var executable=Path.ChangeExtension(typeof(App).Assembly.Location,".exe");
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=directory,RedirectStandardInput=true,RedirectStandardOutput=true};
            using var parent=Process.GetCurrentProcess();
            start.ArgumentList.Add("--render-documents");start.ArgumentList.Add(directory);
            start.ArgumentList.Add(parent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(parent.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return worker=Process.Start(start)??throw new TicketException("Не удалось запустить подготовку страниц Office.");
        }
    }
    private static void StopWorker()
    {
        lock(workerLock)
        {
            // Kill only the helper and the PID/start-time verified owned Office instances.
            // Do not kill a process tree: COM servers need not be helper descendants.
            try{if(worker is not null&&!worker.HasExited)worker.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            WordRenderWorker.StopOwnedWord(directory);ExcelTtnRenderer.StopOwnedExcel(directory);
            worker?.Dispose();worker=null;
        }
    }
    private static void Stop()
    {
        shutdown.Cancel();StopWorker();
        try{Directory.Delete(directory,true);}catch(IOException){}catch(UnauthorizedAccessException){}
    }

}

// Every view owns its package URI so closing one document cannot invalidate
// another view's font resources. No reflow occurs when a window is resized.
internal sealed class WordPageLease:IDisposable
{
    private readonly string path;
    private readonly XpsDocument package;
    private readonly FixedDocumentSequence sequence;
    internal FixedDocument Pages{get;}
    internal WordPageLease(string source)
    {
        path=Path.Combine(Path.GetDirectoryName(source)!,"view-"+Guid.NewGuid().ToString("N")+".xps");File.Copy(source,path);
        try
        {
            package=new XpsDocument(path,FileAccess.Read);sequence=package.GetFixedDocumentSequence();
            var paginator=sequence.DocumentPaginator;paginator.ComputePageCount();
            Pages=new FixedDocument{Tag=this};
            for(var i=0;i<paginator.PageCount;i++)
            {
                var sourcePage=paginator.GetPage(i);
                var page=new FixedPage{Width=sourcePage.Size.Width,Height=sourcePage.Size.Height,Background=Brushes.White};
                page.Children.Add(new Rectangle{Width=page.Width,Height=page.Height,Fill=new VisualBrush(sourcePage.Visual){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top}});
                Pages.Pages.Add(new PageContent{Child=page});
            }
            if(Pages.Pages.Count==0)throw new TicketException("Word вернул документ без страниц.");
            Pages.DocumentPaginator.PageSize=new Size(Pages.Pages[0].Child.Width,Pages.Pages[0].Child.Height);
        }
        catch{package?.Close();try{File.Delete(path);}catch(IOException){}throw;}
    }
    public void Dispose(){package.Close();try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
