using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
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
    private static readonly ConcurrentDictionary<int,(Process Process,string Directory)> running=new();
    private static readonly SemaphoreSlim gate=new(1);
    private static readonly CancellationTokenSource shutdown=new();
    static WordDocumentPages()
    {
        if(Application.Current is {} app)app.Exit+=(_,_)=>Stop();
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>Stop();
    }
    internal static bool Available=>Type.GetTypeFromProgID("Word.Application") is not null;
    internal static Task<string> RenderAsync(byte[] document)
    {
        if(!Available)return Task.FromException<string>(new TicketException("Для точного просмотра установите Microsoft Word."));
        var bytes=document.ToArray();var key=Convert.ToHexString(SHA256.HashData(bytes));
        return cache.GetOrAdd(key,_=>new Lazy<Task<string>>(()=>ExportAsync(key,bytes))).Value;
    }
    private static async Task<string> ExportAsync(string key,byte[] bytes)
    {
        await gate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        var job=Path.Combine(directory,key);Process? process=null;
        try
        {
            Directory.CreateDirectory(job);await File.WriteAllBytesAsync(Path.Combine(job,"document.docx"),bytes,shutdown.Token).ConfigureAwait(false);
            var executable=Path.ChangeExtension(typeof(App).Assembly.Location,".exe");
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=job};
            start.ArgumentList.Add("--render-document");start.ArgumentList.Add(job);
            process=Process.Start(start)??throw new TicketException("Не удалось запустить подготовку страниц Word.");running[process.Id]=(process,job);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try{await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);}
            catch(OperationCanceledException){StopWorker(process,job);throw new TicketException("Word не завершил подготовку страниц за минуту. Закройте запросы Word и повторите просмотр.");}
            var path=Path.Combine(job,"pages.xps");
            if(process.ExitCode!=0||!File.Exists(path))
            {
                var error=Path.Combine(job,"error.txt");
                throw new TicketException(File.Exists(error)?await File.ReadAllTextAsync(error).ConfigureAwait(false):"Word не смог подготовить страницы документа.");
            }
            return path;
        }
        catch{cache.TryRemove(key,out _);throw;}
        finally{if(process is not null){running.TryRemove(process.Id,out _);process.Dispose();}gate.Release();}
    }
    private static void StopWorker(Process process,string job)
    {
        try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
        WordRenderWorker.StopOwnedWord(job);
    }
    private static void Stop()
    {
        shutdown.Cancel();foreach(var worker in running.Values)StopWorker(worker.Process,worker.Directory);
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
