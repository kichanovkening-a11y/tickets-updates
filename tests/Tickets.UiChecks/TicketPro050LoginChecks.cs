using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Tickets.App;
using Tickets.Core;

internal static class TicketPro050LoginChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(string seed)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Tickets-login050-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        var db=Path.Combine(folder,"tickets.db");File.Copy(seed,db);var store=new Store(db);
        var owner=new Window{Width=600,Height=400,Left=-10000,ShowInTaskbar=false};owner.Show();
        var session=new TicketProSession037(owner,store,Guid.NewGuid().ToString(),()=>Task.CompletedTask,()=>{}){Left=-10000,ShowInTaskbar=false};
        // Tests use the production login code with intercepted pages, never the live service.
        var web=(WebView2)typeof(TicketProSession037).GetField("view",Private)!.GetValue(session)!;
        session.Show();
        var task=CheckAsync();var frame=new DispatcherFrame();
        _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));
        Dispatcher.PushFrame(frame);
        try{task.GetAwaiter().GetResult();}finally{session.DisposeSession();owner.Close();}
        async Task Call(string method,params object[] args)=>await (Task)typeof(TicketProSession037).GetMethod(method,Private)!.Invoke(session,args)!;
        async Task CheckAsync()
        {
            var profile=(string)typeof(TicketProSession037).GetField("profile",Private)!.GetValue(session)!;
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:profile);
            await web.EnsureCoreWebView2Async(environment);
            web.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            web.CoreWebView2.WebResourceRequested+=(_,e)=>{
                var login=new Uri(e.Request.Uri).AbsolutePath=="/login";
                var html=login?"""
                    <form><input type="text" autocomplete="username"><input type="password"><button type="submit">Войти</button></form>
                    <script>window.submissions=0;document.querySelector('form').addEventListener('submit',e=>{
                    e.preventDefault();window.submissions++;
                    if(document.querySelector('input[type=text]').value==='fixture050'&&document.querySelector('input[type=password]').value==='synthetic-secret050')location.href='/management/show';
                    });</script>
                    """:"<a href='/management/statistics'>Статистика</a><main>Fixture</main>";
                e.Response=environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)),200,"OK","Content-Type: text/html; charset=utf-8");
            };
            typeof(TicketProSession037).GetField("initialized",Private)!.SetValue(session,true);
            await Call("InitializeLogin050");
            async Task Navigate(string url)
            {
                var done=new TaskCompletionSource<bool>();
                void Completed(object? _,CoreWebView2NavigationCompletedEventArgs e)=>done.TrySetResult(e.IsSuccess);
                web.NavigationCompleted+=Completed;
                try{web.CoreWebView2.Navigate(url);Require(await done.Task.WaitAsync(TimeSpan.FromSeconds(15)),"fixture navigation");}
                finally{web.NavigationCompleted-=Completed;}
            }
            async Task WaitSignedIn()
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while(await web.ExecuteScriptAsync("!!document.querySelector('a[href=\"/management/statistics\"]')")!="true")await Task.Delay(50,timeout.Token);
            }
            var path=Path.Combine(profile,"credentials050.bin");
            var credentials=new TicketlandLogin028("fixture050","synthetic-secret050");
            await Navigate("https://pro.ticketland.ru/login");
            Require(await web.ExecuteScriptAsync(TicketProSession037.FillLogin050(credentials))=="true","login form submitted");
            await WaitSignedIn();await Call("RememberSignIn039Async");
            Require(TicketProCredentials050.Read(path)==credentials,"submitted credentials not saved after successful sign-in");
            Require(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(credentials.Password),"plaintext password persisted");
            session.Hide();
            await Navigate("https://pro.ticketland.ru/login");
            await Call("Reauthenticate050","https://pro.ticketland.ru/management/show/123",CancellationToken.None);
            Require(web.CoreWebView2.Source=="https://pro.ticketland.ru/management/show/123","reauth did not resume requested page");
            Require(!(bool)typeof(TicketProSession037).GetField("attemptedLogin050",Private)!.GetValue(session)!,"successful login did not reset retry guard");
            Console.WriteLine("PASS 050 protected credentials and hidden automatic session renewal");
            await Navigate("https://foreign.example/login");
            Require(await web.ExecuteScriptAsync(TicketProSession037.FillLogin050(credentials))=="false","credentials filled on foreign origin");
            foreach(var url in new[]{"http://pro.ticketland.ru/login","https://pro.ticketland.ru:8443/login","https://user@pro.ticketland.ru/login","https://foreign.example/login"})
                Require(!TicketProSession037.IsLoginPage050(url),"unsafe login origin accepted");
            TicketProCredentials050.Save(path,credentials with{Password="wrong"});
            await Navigate("https://pro.ticketland.ru/login");
            using(var cancel=new CancellationTokenSource(1500))
            {
                try{await Call("Reauthenticate050","https://pro.ticketland.ru/management/show",cancel.Token);throw new Exception("wrong password accepted");}
                catch(OperationCanceledException){}
            }
            try{await Call("Reauthenticate050","https://pro.ticketland.ru/management/show",CancellationToken.None);throw new Exception("retry guard missing");}
            catch(TicketException){}
            Require(await web.ExecuteScriptAsync("window.submissions")=="1","rejected login retried");
            Console.WriteLine("PASS 050 origin isolation, cancellation and rejected-password retry guard");
        }
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
