using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Tickets.App;

internal static class TicketPro039CatalogChecks
{
    internal static void Run()
    {
        var window=new Window{Width=600,Height=400,Left=-10000,ShowInTaskbar=false};
        var web=new WebView2{CreationProperties=new CoreWebView2CreationProperties{UserDataFolder=Path.Combine(Path.GetTempPath(),"Tickets-catalog039-"+Guid.NewGuid().ToString("N"))}};window.Content=web;window.Show();
        var task=Application.Current.Dispatcher.InvokeAsync(CheckAsync).Task.Unwrap();
        var frame=new DispatcherFrame();
        _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));
        Dispatcher.PushFrame(frame);
        try{task.GetAwaiter().GetResult();}finally{web.Dispose();window.Close();}
        async Task CheckAsync()
        {
            await web.EnsureCoreWebView2Async();
            var environment=web.CoreWebView2.Environment;
            web.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.Document);
            web.CoreWebView2.WebResourceRequested+=(_,e)=>{
                const string html="<main><table><tbody><tr><td>Отель Филадельфия<button>Редактировать</button></td><td><a href='/management/show/21540221'>Схема</a></td></tr></tbody></table><div class='q-table__bottom'></div></main>";
                e.Response=environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)),200,"OK","Content-Type: text/html; charset=utf-8");
            };
            var script=(string)typeof(TicketProSession037).GetField("CatalogScript038",BindingFlags.Static|BindingFlags.NonPublic)!.GetRawConstantValue()!;
            foreach(var path in new[]{"/management/show","/management/show/active","/management/show/active/"})
            {
                var done=new TaskCompletionSource<bool>();
                void Completed(object? _,CoreWebView2NavigationCompletedEventArgs e)=>done.TrySetResult(e.IsSuccess);
                web.NavigationCompleted+=Completed;
                web.CoreWebView2.Navigate("https://pro.ticketland.ru"+path);
                try{if(!await done.Task.WaitAsync(TimeSpan.FromSeconds(15)))throw new Exception("Fixture navigation failed");}
                finally{web.NavigationCompleted-=Completed;}
                using var json=JsonDocument.Parse(JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync(script))!);
                var root=json.RootElement;
                if(!root.GetProperty("ready").GetBoolean())throw new Exception("Catalog not accepted at "+path);
                var row=root.GetProperty("items")[0];
                if(row.GetProperty("title").GetString()!="Отель Филадельфия"||row.GetProperty("url").GetString()!="https://pro.ticketland.ru/management/show/21540221")throw new Exception("Catalog row corrupted");
                Console.WriteLine("PASS catalog ready and row extracted at "+path);
            }
            var cache=typeof(TicketProSession037).Assembly.GetType("Tickets.App.TicketProSessionCache039");
            if(cache is null)throw new Exception("Signed-in session is not preserved across browser restarts");
            var cachePath=Path.Combine(environment.UserDataFolder,"session039.bin");
            var manager=web.CoreWebView2.CookieManager;
            var cookie=manager.CreateCookie("session_test039","synthetic-session-value-039","pro.ticketland.ru","/management");
            cookie.IsHttpOnly=true;cookie.IsSecure=true;cookie.SameSite=CoreWebView2CookieSameSiteKind.Strict;
            manager.AddOrUpdateCookie(cookie);
            await (Task)cache.GetMethod("SaveAsync")!.Invoke(null,[manager,cachePath])!;
            if(!File.Exists(cachePath)||Encoding.UTF8.GetString(File.ReadAllBytes(cachePath)).Contains("synthetic-session-value-039"))throw new Exception("Session must be encrypted for this Windows account");
            manager.DeleteCookie(cookie);
            await (Task)cache.GetMethod("RestoreAsync")!.Invoke(null,[manager,cachePath])!;
            var restored=(await manager.GetCookiesAsync("https://pro.ticketland.ru/management/show")).Single(x=>x.Name=="session_test039");
            if(restored.Value!=cookie.Value||!restored.IsSession||!restored.IsHttpOnly||!restored.IsSecure||restored.SameSite!=cookie.SameSite)throw new Exception("Session cookie properties changed");
            Console.WriteLine("PASS encrypted session roundtrip retains scoped cookie properties");
            manager.DeleteCookie(restored);
            File.WriteAllText(cachePath,"damaged");
            await (Task)cache.GetMethod("RestoreAsync")!.Invoke(null,[manager,cachePath])!;
            if((await manager.GetCookiesAsync("https://pro.ticketland.ru/management/show")).Any(x=>x.Name=="session_test039"))throw new Exception("Damaged session restored");
            Console.WriteLine("PASS damaged saved session does not prevent a new sign-in");
            await TicketPro039RestartChecks.RunAsync(window);
        }
    }
}
