using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Tickets.App;

internal static class TicketPro039RestartChecks
{
    internal static async Task RunAsync(Window owner)
    {
        var id=Guid.NewGuid().ToString();
        var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Tickets","TicketPRO",id);
        try
        {
            await Cycle(true);
            await Cycle(false);
            Console.WriteLine("PASS application restores sign-in before first request after native browser restart");
        }
        finally{try{Directory.Delete(profile,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        async Task Cycle(bool first)
        {
            var session=new TicketProSession037(owner,null!,id,()=>Task.CompletedTask,()=>{});
            var web=(WebView2)typeof(TicketProSession037).GetField("view",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
            var requested=new TaskCompletionSource<bool>();
            var exited=new TaskCompletionSource<bool>();
            web.CoreWebView2InitializationCompleted+=(_,e)=>{
                if(!e.IsSuccess){requested.TrySetException(e.InitializationException);return;}
                var core=web.CoreWebView2;
                core.Environment.BrowserProcessExited+=(_,_)=>exited.TrySetResult(true);
                if(first)
                {
                    var cookie=core.CookieManager.CreateCookie("session_restart039","synthetic_restart039","pro.ticketland.ru","/management");
                    cookie.IsSecure=true;cookie.IsHttpOnly=true;core.CookieManager.AddOrUpdateCookie(cookie);
                }
                core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.Document);
                core.WebResourceRequested+=(_,request)=>{
                    var signedIn=request.Request.Headers.Contains("Cookie")&&request.Request.Headers.GetHeader("Cookie").Contains("session_restart039=synthetic_restart039");
                    var html=signedIn?"<a href='/management/statistics'>Account</a><main>Authenticated</main>":"<main>Login required</main>";
                    request.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)),200,"OK","Content-Type: text/html; charset=utf-8");
                    requested.TrySetResult(signedIn);
                };
            };
            try
            {
                await session.PrepareAsync(true,CancellationToken.None);
                if(!await requested.Task.WaitAsync(TimeSpan.FromSeconds(15)))throw new Exception("First request lost session cookie after restart");
                if(first)
                {
                    await session.OnSignInObserved038(true);
                    web.CoreWebView2.CookieManager.DeleteAllCookies();
                    if((await web.CoreWebView2.CookieManager.GetCookiesAsync("")).Count>0)throw new Exception("Cookie reset failed");
                }
            }
            finally{session.DisposeSession();await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));}
        }
    }
}
