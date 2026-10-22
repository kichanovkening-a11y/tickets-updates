using System.IO;
using System.Runtime.InteropServices;
using Tickets.Core;

namespace Tickets.App;

internal static class OfficeDocumentImport
{
    internal static byte[] Read(string path)
    {
        if(new FileInfo(path).Length>20*1024*1024)throw new TicketException("Загрузите документ размером до 20 МБ.");
        var extension=Path.GetExtension(path).ToLowerInvariant();
        if(extension==".docx")return CheckedBytes(path);
        if(extension is not (".doc" or ".xls" or ".xlsx"))throw new TicketException("Выберите DOCX, DOC, XLS или XLSX.");
        var directory=Path.Combine(Path.GetTempPath(),"Tickets-Office-import-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // Work only with a copy: Office conversion must never save over the source.
            var source=Path.Combine(directory,"source"+extension);File.Copy(path,source);
            var document=Path.Combine(directory,"converted.docx");
            if(extension is ".xls" or ".xlsx")source=ExcelHtml(source,directory);
            ConvertWord(source,document);
            return CheckedBytes(document);
        }
        catch(Exception ex) when(ex is COMException or InvalidOperationException or NotSupportedException)
        {throw new TicketException("Не удалось преобразовать старую форму. Для DOC требуется установленный Word, для XLS/XLSX — Word и Excel. Откройте исходный файл в Office, сохраните копию как DOCX и загрузите её. "+ex.Message);}
        finally{try{Directory.Delete(directory,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }

    internal static byte[] CheckedBytes(string path)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        if(file.Length>20*1024*1024)throw new TicketException("Загрузите документ размером до 20 МБ.");
        using var content=new MemoryStream();file.CopyTo(content);var bytes=content.ToArray();
        CustomDocumentForms.Validate(bytes);return bytes;
    }
    private static dynamic Create(string program)
    {
        var type=Type.GetTypeFromProgID(program)??throw new InvalidOperationException("Не установлен "+program+".");
        return Activator.CreateInstance(type)??throw new InvalidOperationException("Не удалось запустить "+program+".");
    }
    private static string ExcelHtml(string source,string directory)
    {
        object? application=null,books=null,book=null;
        try
        {
            application=Create("Excel.Application");dynamic excel=application;
            excel.Visible=false;excel.DisplayAlerts=false;excel.AutomationSecurity=3;excel.AskToUpdateLinks=false;
            books=excel.Workbooks;book=((dynamic)books).Open(Filename:source,UpdateLinks:0,ReadOnly:true,IgnoreReadOnlyRecommended:true,AddToMru:false);
            var html=Path.Combine(directory,"spreadsheet.htm");
            ((dynamic)book).SaveAs(Filename:html,FileFormat:44,AddToMru:false);
            return html;
        }
        finally
        {
            try{if(book is not null)((dynamic)book).Close(SaveChanges:false);}catch(COMException){}
            try{if(application is not null)((dynamic)application).Quit();}catch(COMException){}
            Release(book);Release(books);Release(application);
        }
    }
    private static void ConvertWord(string source,string target)
    {
        object? application=null,documents=null,document=null;
        try
        {
            application=Create("Word.Application");dynamic word=application;
            word.Visible=false;word.DisplayAlerts=0;word.AutomationSecurity=3;
            documents=word.Documents;
            document=((dynamic)documents).Open(FileName:source,ConfirmConversions:false,ReadOnly:true,AddToRecentFiles:false,Visible:false,NoEncodingDialog:true);
            ((dynamic)document).SaveAs2(FileName:target,FileFormat:16,AddToRecentFiles:false);
        }
        finally
        {
            try{if(document is not null)((dynamic)document).Close(SaveChanges:0);}catch(COMException){}
            try{if(application is not null)((dynamic)application).Quit(SaveChanges:0);}catch(COMException){}
            Release(document);Release(documents);Release(application);
        }
    }
    private static void Release(object? value)
    {if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
}
