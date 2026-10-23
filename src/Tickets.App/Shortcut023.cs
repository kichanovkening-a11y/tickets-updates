using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
namespace Tickets.App;

internal static class Shortcut023
{
    [DllImport("shell32.dll")]private static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    // Use the Unicode ShellLink API. WScript.Shell can convert a Cyrillic filename
    // through the system ANSI codepage on an English Windows installation.
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int count,IntPtr data,uint flags);
        void GetIDList(out IntPtr list);void SetIDList(IntPtr list);
        void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string value);
        void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string value);
        void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string value);
        void GetHotkey(out short value);void SetHotkey(short value);void GetShowCmd(out int value);void SetShowCmd(int value);
        void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int count,out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string value,int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string value,uint reserved);
        void Resolve(IntPtr window,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string value);
    }
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
        var type=Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!;
        var instance=Activator.CreateInstance(type)!;
        try
        {
            var link=(IShellLinkW)instance;var file=(IPersistFile)instance;
            if(File.Exists(path))
            {
                file.Load(path,0);var previous=new StringBuilder(32768);link.GetPath(previous,previous.Capacity,IntPtr.Zero,0);
                if(previous.Length>0&&!string.Equals(Path.GetFullPath(previous.ToString()),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))return;
            }
            link.SetPath(target);link.SetWorkingDirectory(directory);link.SetIconLocation(icon,0);link.SetDescription("Билеты");file.Save(path,true);
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }
        finally{if(Marshal.IsComObject(instance))Marshal.FinalReleaseComObject(instance);}
    }
}
