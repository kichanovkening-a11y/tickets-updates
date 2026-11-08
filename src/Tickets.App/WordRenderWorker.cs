using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Tickets.Core;

namespace Tickets.App;

internal static class WordRenderWorker
{
    internal static int Run(string directory)
    {
        WordAutomationSession? session=null;
        try{return Render(directory,directory,ref session);}
        finally{session?.Dispose();ExcelTtnRenderer.DisposeSession();}
    }
    internal static int RunPersistent(string owningRoot,int parentId,long parentStarted)
    {
        Directory.CreateDirectory(owningRoot);
        using var finished=new CancellationTokenSource();
        // The watcher runs independently of STA COM calls, including blocked Office dialogs.
        var watcher=new Thread(()=>
        {
            try
            {
                using var parent=Process.GetProcessById(parentId);
                if(parent.StartTime.ToUniversalTime().Ticks!=parentStarted)throw new InvalidOperationException();
                while(!finished.IsCancellationRequested)
                {
                    if(parent.WaitForExit(250))break;
                }
                if(finished.IsCancellationRequested)return;
            }
            catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            var ownershipDeadline=DateTime.UtcNow.AddSeconds(60);
            while(!finished.IsCancellationRequested&&!File.Exists(Path.Combine(owningRoot,"owned-word.txt"))&&!File.Exists(Path.Combine(owningRoot,"owned-excel.txt"))&&DateTime.UtcNow<ownershipDeadline)
                Thread.Sleep(100);
            if(finished.IsCancellationRequested)return;
            StopOwnedWord(owningRoot);ExcelTtnRenderer.StopOwnedExcel(owningRoot);
            Environment.Exit(1);
        }){IsBackground=true};watcher.Start();
        WordAutomationSession? session=null;
        try
        {
            string? request;
            while((request=Console.ReadLine()) is not null)
            {
                var job=System.Text.Json.JsonSerializer.Deserialize<string>(request)!;
                var result=Render(job,owningRoot,ref session);
                Console.WriteLine(result.ToString(CultureInfo.InvariantCulture));Console.Out.Flush();
            }
            return 0;
        }
        finally{session?.Dispose();ExcelTtnRenderer.DisposeSession();finished.Cancel();watcher.Join(1000);}
    }
    private static int Render(string directory,string owningRoot,ref WordAutomationSession? session)
    {
        object? document=null;
        try
        {
            using(var package=ZipFile.OpenRead(Path.Combine(directory,"document.docx")))
                if(package.GetEntry("customXml/tickets-ttn047.json") is not null)return ExcelTtnRenderer.Render(directory,owningRoot);
            session??=WordAutomationSession.Start(owningRoot);
            document=session.OpenDocument(Path.Combine(directory,"document.docx"));
            ((dynamic)document).Repaginate();CheckContractTail(document);CheckTtnFields(document,directory);
            ((dynamic)document).ExportAsFixedFormat(OutputFileName:Path.Combine(directory,"pages.xps"),ExportFormat:18,OpenAfterExport:false,OptimizeFor:0,Range:0,Item:0,IncludeDocProps:false,KeepIRM:true,CreateBookmarks:0,DocStructureTags:true,BitmapMissingFonts:true);
            return 0;
        }
        catch(Exception ex)
        {
            try{File.WriteAllText(Path.Combine(directory,"error.txt"),"Не удалось подготовить документ в Word. "+ex.Message);}catch(IOException){}catch(UnauthorizedAccessException){}
            if(ex is COMException){session?.Dispose();session=null;}
            return 1;
        }
        finally
        {
            try{if(document is not null)((dynamic)document).Close(SaveChanges:0);}catch(COMException){}
            Release(document);
        }
    }
    private static void CheckTtnFields(object document,string directory)
    {
        using var zip=ZipFile.OpenRead(Path.Combine(directory,"document.docx"));
        var metadata=ApprovedTtn.ReadLayout(zip);if(metadata is null)return;
        var fields=metadata.Root!.Elements().ToDictionary(e=>(string)e.Attribute("name")!,e=>e,StringComparer.Ordinal);
        object? bookmarks=null;
        try
        {
            bookmarks=((dynamic)document).Bookmarks;
            for(var i=1;i<=(int)((dynamic)bookmarks).Count;i++)
            {
                object? bookmark=null,range=null;
                try
                {
                    bookmark=((dynamic)bookmarks).Item(i);var name=(string)((dynamic)bookmark).Name;
                    var sourceName=System.Text.RegularExpressions.Regex.Replace(name,@"_p\d+$","");
                    if(!fields.TryGetValue(sourceName,out var field))continue;
                    range=((dynamic)bookmark).Range;
                    var lines=(int)((dynamic)range).ComputeStatistics(1);if(lines==0)continue;
                    var size=(float)((dynamic)range).Font.Size;
                    if(size<=0||size>1000)size=(float)(double)field.Attribute("fontSize")!;
                    var height=(double)field.Attribute("height")!;
                    if(lines*size*1.15>height+1.5)
                        throw new InvalidOperationException("Текст не помещается в поле ТТН ("+sourceName[8..]+"). Сократите текст этого поля. Утверждённая форма и размер шрифта сохранены.");
                }
                finally{Release(range);Release(bookmark);}
            }
        }
        finally{Release(bookmarks);}
    }
    private static void CheckContractTail(object document)
    {
        object? bookmarks=null;
        try
        {
            bookmarks=((dynamic)document).Bookmarks;
            if(!((dynamic)bookmarks).Exists(ContractPagination.ClauseBookmark)||!((dynamic)bookmarks).Exists(ContractPagination.SignatureBookmark))return;
            int Page(string name,bool atEnd)
            {
                object? bookmark=null,range=null;
                try
                {
                    bookmark=((dynamic)bookmarks).Item(name);range=((dynamic)bookmark).Range;((dynamic)range).Collapse(atEnd?0:1);
                    return (int)((dynamic)range).Information[3]; // wdActiveEndPageNumber, physical page number
                }
                finally{Release(range);Release(bookmark);}
            }
            if(Page(ContractPagination.ClauseBookmark,false)!=Page(ContractPagination.SignatureBookmark,true))
                throw new InvalidOperationException("Последний пункт вместе с реквизитами и подписями не помещается на один лист. Сократите этот блок или измените поля документа. Размер шрифта сохранён.");
        }
        finally{Release(bookmarks);}
    }
    internal static void StopOwnedWord(string directory)
    {
        try
        {
            var record=File.ReadAllLines(Path.Combine(directory,"owned-word.txt"));
            if(record.Length!=2||!int.TryParse(record[0],out var pid)||!long.TryParse(record[1],out var ticks))return;
            using var process=Process.GetProcessById(pid);
            // A recycled PID or a Word instance that existed before the job
            // must never be terminated by the renderer's timeout/exit cleanup.
            if(process.ProcessName.Equals("WINWORD",StringComparison.OrdinalIgnoreCase)&&process.StartTime.ToUniversalTime().Ticks==ticks&&!process.HasExited)process.Kill();
        }
        catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
    }
    private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
}

// Identify our own Word process with an empty hidden document before opening
// user content. Import/password dialogs in Open can then be cleaned up safely.
internal sealed class WordAutomationSession:IDisposable
{
    private object? application,documents;
    private bool owned;
    private WordAutomationSession(){}
    internal static WordAutomationSession Start(string directory)
    {
        var session=new WordAutomationSession();object? blank=null,window=null;
        try
        {
            var existing=Process.GetProcessesByName("WINWORD").Select(p=>p.Id).ToHashSet();
            var type=Type.GetTypeFromProgID("Word.Application")??throw new InvalidOperationException("Microsoft Word не установлен.");
            session.application=Activator.CreateInstance(type)??throw new InvalidOperationException("Не удалось открыть Word.");
            dynamic word=session.application;word.Visible=false;word.DisplayAlerts=0;word.AutomationSecurity=3;
            session.documents=word.Documents;blank=((dynamic)session.documents).Add(Visible:false);
            window=((dynamic)blank).ActiveWindow;
            GetWindowThreadProcessId(FindWordWindow(window!),out var pid);
            if(pid==0||existing.Contains((int)pid))throw new InvalidOperationException("Не удалось создать отдельный процесс Word для просмотра.");
            session.owned=true;
            using var process=Process.GetProcessById((int)pid);
            File.WriteAllText(Path.Combine(directory,"owned-word.txt"),pid+"\n"+process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
            return session;
        }
        catch{session.Dispose();throw;}
        finally
        {
            try{if(blank is not null)((dynamic)blank).Close(SaveChanges:0);}catch(COMException){}
            Release(window);Release(blank);
        }
    }
    private static IntPtr FindWordWindow(object window)
    {
        try { return new IntPtr((int)((dynamic)window).Hwnd); }
        catch (Exception ex) when (ex is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ||
            ex is COMException { HResult: unchecked((int)0x80020003) or unchecked((int)0x80020006) })
        {
            // Word 2010 may omit Hwnd. Identify only our temporary document window.
            string marker = "Tickets-" + Guid.NewGuid().ToString("N");
            ((dynamic)window).Caption = marker;
            IntPtr handle = IntPtr.Zero;
            while ((handle = FindWindowEx(IntPtr.Zero, handle, "OpusApp", null)) != IntPtr.Zero)
            {
                var caption = new System.Text.StringBuilder(512);
                GetWindowText(handle, caption, caption.Capacity);
                if (caption.ToString().Contains(marker, StringComparison.Ordinal)) return handle;
            }
            return IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string windowClass, string? caption);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int capacity);
    internal object OpenDocument(string path)=>((dynamic)documents!).Open(FileName:path,ConfirmConversions:false,ReadOnly:true,AddToRecentFiles:false,Visible:false,NoEncodingDialog:true);
    public void Dispose()
    {
        try{if(owned&&application is not null)((dynamic)application).Quit(SaveChanges:0);}catch(COMException){}
        Release(documents);Release(application);documents=null;application=null;owned=false;
    }
    private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
}
