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
        WordAutomationSession? session=null;object? document=null;
        try
        {
            session=WordAutomationSession.Start(directory);
            document=session.OpenDocument(Path.Combine(directory,"document.docx"));
            ((dynamic)document).Repaginate();CheckContractTail(document);CheckTtnFields(document,directory);
            ((dynamic)document).ExportAsFixedFormat(OutputFileName:Path.Combine(directory,"pages.xps"),ExportFormat:18,OpenAfterExport:false,OptimizeFor:0,Range:0,Item:0,IncludeDocProps:false,KeepIRM:true,CreateBookmarks:0,DocStructureTags:true,BitmapMissingFonts:true);
            return 0;
        }
        catch(Exception ex)
        {
            try{File.WriteAllText(Path.Combine(directory,"error.txt"),"Не удалось подготовить документ в Word. "+ex.Message);}catch(IOException){}catch(UnauthorizedAccessException){}
            return 1;
        }
        finally
        {
            try{if(document is not null)((dynamic)document).Close(SaveChanges:0);}catch(COMException){}
            Release(document);session?.Dispose();
        }
    }
    private static void CheckTtnFields(object document,string directory)
    {
        using var zip=ZipFile.OpenRead(Path.Combine(directory,"document.docx"));
        var entry=zip.GetEntry("customXml/tickets-ttn-045.xml");if(entry is null)return;
        using var input=entry.Open();var metadata=XDocument.Load(input);
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
            window=((dynamic)blank).ActiveWindow;GetWindowThreadProcessId(new IntPtr((int)((dynamic)window).Hwnd),out var pid);
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
    internal object OpenDocument(string path)=>((dynamic)documents!).Open(FileName:path,ConfirmConversions:false,ReadOnly:true,AddToRecentFiles:false,Visible:false,NoEncodingDialog:true);
    public void Dispose()
    {
        try{if(owned&&application is not null)((dynamic)application).Quit(SaveChanges:0);}catch(COMException){}
        Release(documents);Release(application);documents=null;application=null;owned=false;
    }
    private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
}
