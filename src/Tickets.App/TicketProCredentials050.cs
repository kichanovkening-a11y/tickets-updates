using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tickets.App;

internal static class TicketProCredentials050
{
    internal static void Save(string path,TicketlandLogin028 login)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(login);
        try
        {
            var protectedBytes=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path+".tmp",protectedBytes);File.Move(path+".tmp",path,true);
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    internal static TicketlandLogin028? Read(string path)
    {
        byte[]? bytes=null;
        try
        {
            if(!File.Exists(path)||new FileInfo(path).Length>16384)return null;
            bytes=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
            var login=JsonSerializer.Deserialize<TicketlandLogin028>(bytes);
            return Valid(login)?login:null;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException){return null;}
        finally{if(bytes is not null)CryptographicOperations.ZeroMemory(bytes);}
    }
    internal static bool Valid(TicketlandLogin028? login)=>login is not null&&
        !string.IsNullOrWhiteSpace(login.Username)&&login.Username.Length<=513&&
        !string.IsNullOrEmpty(login.Password)&&login.Password.Length<=1280;
}
