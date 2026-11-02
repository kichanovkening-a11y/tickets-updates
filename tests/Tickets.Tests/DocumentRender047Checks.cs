using System.IO.Compression;
using Tickets.Core;

internal static class DocumentRender047Checks
{
    internal static void Run(Action<string,Action<Store>> test)
    {
        test("047 render cache ignores ZIP metadata but preserves content and layout changes",_=>
        {
            byte[] Package(int year,string text,string layout,bool reverse)
            {
                using var output=new MemoryStream();
                using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
                    foreach(var part in (reverse?new[]{("word/styles.xml",layout),("word/document.xml",text)}:new[]{("word/document.xml",text),("word/styles.xml",layout)}))
                    {
                        var entry=zip.CreateEntry(part.Item1,year==2020?CompressionLevel.NoCompression:CompressionLevel.Optimal);entry.LastWriteTime=new DateTimeOffset(year,1,1,0,0,0,TimeSpan.Zero);
                        using var writer=new StreamWriter(entry.Open());writer.Write(part.Item2);
                    }
                return output.ToArray();
            }
            var key=DocumentRenderKey.Create(Package(2020,"text","layout",false));
            if(key!=DocumentRenderKey.Create(Package(2026,"text","layout",true)))throw new Exception("ZIP timestamps, compression and entry order invalidate pages");
            if(key==DocumentRenderKey.Create(Package(2026,"changed","layout",false)))throw new Exception("Text change reused stale pages");
            if(key==DocumentRenderKey.Create(Package(2026,"text","changed",false)))throw new Exception("Layout change reused stale pages");
        });
    }
}
