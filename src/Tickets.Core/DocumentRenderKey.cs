using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Tickets.Core;

// Hash package contents, not ZIP timestamps, entry ordering or compression.
public static class DocumentRenderKey
{
    public static string Create(byte[] document)
    {
        using var zip=new ZipArchive(new MemoryStream(document),ZipArchiveMode.Read);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var entry in zip.Entries.OrderBy(e=>e.FullName,StringComparer.Ordinal))
        {
            var name=Encoding.UTF8.GetBytes(entry.FullName);
            hash.AppendData(BitConverter.GetBytes(name.Length));hash.AppendData(name);
            hash.AppendData(BitConverter.GetBytes(entry.Length));
            using var input=entry.Open();var buffer=new byte[81920];int read;
            while((read=input.Read(buffer))>0)hash.AppendData(buffer,0,read);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
