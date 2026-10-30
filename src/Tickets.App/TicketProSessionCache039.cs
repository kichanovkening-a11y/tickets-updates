using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace Tickets.App;

// Session cookies stay on this PC, encrypted for the current Windows account.
internal static class TicketProSessionCache039
{
    private static bool AllowedDomain(string domain)=>domain.TrimStart('.') is "pro.ticketland.ru" or "ticketland.ru";
    private sealed record Cookie(string Name,string Value,string Domain,string Path,bool Secure,bool HttpOnly,CoreWebView2CookieSameSiteKind SameSite);
    public static async Task SaveAsync(CoreWebView2CookieManager manager,string path)
    {
        var cookies=(await manager.GetCookiesAsync("")).Where(c=>c.IsSession&&AllowedDomain(c.Domain))
            .Select(c=>new Cookie(c.Name,c.Value,c.Domain,c.Path,c.IsSecure,c.IsHttpOnly,c.SameSite)).ToArray();
        var bytes=JsonSerializer.SerializeToUtf8Bytes(cookies);
        try
        {
            var encrypted=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path+".tmp",encrypted);File.Move(path+".tmp",path,true);
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public static async Task RestoreAsync(CoreWebView2CookieManager manager,string path)
    {
        if(!File.Exists(path))return;
        byte[]? bytes=null;
        try
        {
            if(new FileInfo(path).Length>1024*1024)return;
            bytes=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
            var cookies=JsonSerializer.Deserialize<Cookie[]>(bytes)??[];
            var existing=await manager.GetCookiesAsync("");
            foreach(var saved in cookies)
            {
                if(!AllowedDomain(saved.Domain)||!saved.Path.StartsWith('/')||!Enum.IsDefined(saved.SameSite))continue;
                if(existing.Any(c=>c.Name==saved.Name&&c.Domain==saved.Domain&&c.Path==saved.Path))continue;
                var cookie=manager.CreateCookie(saved.Name,saved.Value,saved.Domain,saved.Path);
                cookie.IsSecure=saved.Secure;cookie.IsHttpOnly=saved.HttpOnly;cookie.SameSite=saved.SameSite;
                manager.AddOrUpdateCookie(cookie);
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException){/* A new sign-in remains available. */}
        finally{if(bytes is not null)CryptographicOperations.ZeroMemory(bytes);}
    }
}
