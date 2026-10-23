using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace Tickets.App;

internal static class Shortcut023
{
    [DllImport("shell32.dll")]private static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    internal static void Ensure(string directory)
    {
        if(!OperatingSystem.IsWindows())return;
        var target=Path.Combine(directory,"Tickets.exe");if(!File.Exists(target))return;
        var icon=Path.Combine(directory,"Data","Tickets-red023.ico");Directory.CreateDirectory(Path.GetDirectoryName(icon)!);
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("Tickets.App.ShortcutRed.ico"))
        {
            if(source is null)throw new IOException("Красная иконка не найдена.");
            using var bytes=new MemoryStream();source.CopyTo(bytes);var current=bytes.ToArray();
            if(!File.Exists(icon)||!File.ReadAllBytes(icon).SequenceEqual(current))
            {File.WriteAllBytes(icon+".tmp",current);File.Move(icon+".tmp",icon,true);}
        }
        var path=Path.Combine(directory,"! Билеты.lnk");
        object? shell=null,shortcut=null;
        try
        {
            var type=Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("Ярлыки Windows недоступны.");
            shell=Activator.CreateInstance(type)!;
            shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path})!;
            var link=shortcut.GetType();
            var previous=link.InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null) as string;
            // Never repoint a user shortcut that launches a different installed copy.
            if(File.Exists(path)&&!string.IsNullOrWhiteSpace(previous)&&!string.Equals(Path.GetFullPath(previous),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))return;
            void Set(string name,object value)=>link.InvokeMember(name,BindingFlags.SetProperty,null,shortcut,new[]{value});
            Set("TargetPath",target);Set("WorkingDirectory",directory);Set("IconLocation",icon+",0");Set("Description","Билеты");
            link.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }
        finally
        {
            if(shortcut is not null&&Marshal.IsComObject(shortcut))Marshal.FinalReleaseComObject(shortcut);
            if(shell is not null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);
        }
    }
}
