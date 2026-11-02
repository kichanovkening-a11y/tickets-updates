using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Tickets.Core;

namespace Tickets.App;

// Runs only in the serial STA Office helper. The workbook owns the form geometry.
internal static class ExcelTtnRenderer
{
    private static object? application,workbooks;
    private static bool owned;
    internal static int Render(string directory,string owningRoot)
    {
        object? book=null,sheets=null;
        try
        {
            EnsureSession(owningRoot);
            var workbook=Path.Combine(directory,"document.xlsx");
            ExcelTtnWorkbook.SaveRequest(File.ReadAllBytes(Path.Combine(directory,"document.docx")),workbook);
            book=((dynamic)workbooks!).Open(Filename:workbook,UpdateLinks:0,ReadOnly:true,AddToMru:false);
            ((dynamic)book).ExportAsFixedFormat(Type:1,Filename:Path.Combine(directory,"pages.xps"),Quality:0,IncludeDocProperties:false,IgnorePrintAreas:false,OpenAfterPublish:false);
            return 0;
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(directory,"error.txt"),"Не удалось подготовить ТТН в Excel. "+ex.Message);
            return 1;
        }
        finally
        {
            try{if(book is not null)((dynamic)book).Close(SaveChanges:false);}catch(COMException){}
            Release(sheets);Release(book);
            if(File.Exists(Path.Combine(directory,"error.txt")))DisposeSession();
        }
    }
    private static void EnsureSession(string directory)
    {
        if(application is not null)return;
        var existing=Process.GetProcessesByName("EXCEL").Select(p=>p.Id).ToHashSet();
        try
        {
            application=Activator.CreateInstance(Type.GetTypeFromProgID("Excel.Application")??throw new InvalidOperationException("Microsoft Excel не установлен."));
            GetWindowThreadProcessId(new IntPtr((int)((dynamic)application!).Hwnd),out var pid);
            if(pid==0||existing.Contains((int)pid))throw new InvalidOperationException("Не удалось создать отдельный Excel для ТТН.");
            owned=true;
            using var process=Process.GetProcessById((int)pid);
            File.WriteAllText(Path.Combine(directory,"owned-excel.txt"),pid+"\n"+process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
            ((dynamic)application!).Visible=false;((dynamic)application).DisplayAlerts=false;((dynamic)application).AutomationSecurity=3;
            workbooks=((dynamic)application).Workbooks;
        }
        catch{DisposeSession();throw;}
    }
    internal static void DisposeSession()
    {
        try{if(owned&&application is not null)((dynamic)application).Quit();}catch(COMException){}
        Release(workbooks);Release(application);workbooks=null;application=null;owned=false;
    }
    internal static void StopOwnedExcel(string directory)
    {
        try
        {
            var record=File.ReadAllLines(Path.Combine(directory,"owned-excel.txt"));
            if(record.Length!=2||!int.TryParse(record[0],out var pid)||!long.TryParse(record[1],out var ticks))return;
            using var process=Process.GetProcessById(pid);
            if(process.ProcessName.Equals("EXCEL",StringComparison.OrdinalIgnoreCase)&&process.StartTime.ToUniversalTime().Ticks==ticks&&!process.HasExited)process.Kill();
        }
        catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
    }
    private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
}
