using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Tickets.App;

internal static class Office047UiChecks
{
    internal static void Run()
    {
        if(!WordDocumentPages.Available)throw new Exception("Native Office verification requires installed Word");
        var existing=Process.GetProcessesByName("WINWORD").Select(p=>p.Id).ToHashSet();
        var fixture=(byte[])typeof(Office045UiChecks).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        var timer=Stopwatch.StartNew();var first=WordDocumentPages.RenderAsync(fixture).GetAwaiter().GetResult();var cold=timer.ElapsedMilliseconds;
        var root=Path.GetDirectoryName(Path.GetDirectoryName(first))!;
        var owner=File.ReadAllText(Path.Combine(root,"owned-word.txt"));
        var pid=int.Parse(owner.Split('\n')[0]);Check(!existing.Contains(pid),"renderer owns a separate hidden Word process");
        using var ownWord=Process.GetProcessById(pid);
        var secondBytes=Rewrite(fixture,true);timer.Restart();
        var second=WordDocumentPages.RenderAsync(secondBytes).GetAwaiter().GetResult();var warm=timer.ElapsedMilliseconds;
        Check(second!=first,"different document receives distinct fixed pages");
        Check(File.ReadAllText(Path.Combine(root,"owned-word.txt"))==owner&&!ownWord.HasExited,"new document reuses the same Word session");
        using(var zip=ZipFile.OpenRead(second))
        {
            var text=string.Concat(zip.Entries.Where(e=>e.FullName.EndsWith(".fpage")).Select(e=>{using var input=e.Open();return string.Concat(XDocument.Load(input).Descendants().Attributes("UnicodeString").Select(a=>a.Value));}));
            Check(text.Contains("Changed native page"),"reused Word session renders new content");
        }
        timer.Restart();var repeatTask=WordDocumentPages.RenderAsync(Rewrite(fixture,false));
        Check(repeatTask.IsCompletedSuccessfully&&repeatTask.Result==first,"repeat document with changed ZIP timestamps uses completed cached pages");
        var repeat=timer.ElapsedMilliseconds;
        Console.WriteLine($"TIMING047 cold={cold}ms new-with-reused-Word={warm}ms repeat={repeat}ms");
        typeof(WordDocumentPages).GetMethod("StopWorker",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
        Check(ownWord.WaitForExit(5000),"owned Office exits with the rendering worker");
        foreach(var existingId in existing)
        {
            try{using var process=Process.GetProcessById(existingId);Check(!process.HasExited,"existing user's Word process survives renderer cleanup");}
            catch(ArgumentException){throw new Exception("Existing user's Word process disappeared");}
        }
    }
    private static byte[] Rewrite(byte[] bytes,bool changeText)
    {
        using var input=new ZipArchive(new MemoryStream(bytes));using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
            foreach(var entry in input.Entries.Reverse())
            {
                var copy=zip.CreateEntry(entry.FullName,CompressionLevel.NoCompression);copy.LastWriteTime=new DateTimeOffset(2020,1,1,0,0,0,TimeSpan.Zero);
                using var source=entry.Open();using var target=copy.Open();
                if(changeText&&entry.FullName=="word/document.xml")
                {using var reader=new StreamReader(source);using var writer=new StreamWriter(target);writer.Write(reader.ReadToEnd().Replace("First native page","Changed native page"));}
                else source.CopyTo(target);
            }
        return output.ToArray();
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
