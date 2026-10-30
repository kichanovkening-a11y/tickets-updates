using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Tickets.App;
using Tickets.Core;

internal static class TicketPro040SchemeChecks
{
    internal static void Run()
    {
        var owner=new Window{Width=600,Height=400,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false};owner.Show();
        var task=Application.Current.Dispatcher.InvokeAsync(CheckAsync).Task.Unwrap();
        var frame=new DispatcherFrame();
        _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));
        Dispatcher.PushFrame(frame);
        try{task.GetAwaiter().GetResult();}finally{owner.Close();}
        async Task CheckAsync()
        {
            var session=new TicketProSession037(owner,null!,Guid.NewGuid().ToString(),()=>Task.CompletedTask,()=>{});
            var web=(WebView2)typeof(TicketProSession037).GetField("view",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
            web.CoreWebView2InitializationCompleted+=(_,e)=>{
                if(!e.IsSuccess)return;
                var core=web.CoreWebView2;
                core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
                core.WebResourceRequested+=(_,request)=>{
                    if(request.ResourceContext==CoreWebView2WebResourceContext.Document)
                    {
                        const string html="""
                            <main><h1>Fixture scheme — Управление билетами</h1><div>01.11.2026 Вс, 19:00</div><p id="result">Loading</p></main>
                            <script>
                            (async()=>{
                              try {
                                const response=await fetch('/api/basket/clear',{method:'POST'});
                                const data=await response.json();
                                if(!response.ok||data.status!==true||!Array.isArray(data.data))return;
                                document.querySelector('main').insertAdjacentHTML('beforeend',`<div>Оценено: 1 из 1</div><svg id="hall-scheme-svg"><rect id="p1" section="Партер" row="1" seat="1"/></svg>`);
                                setTimeout(()=>{const p=document.getElementById('p1');p.setAttribute('data-status-name','В продаже');p.setAttribute('data-fund-id','1');p.setAttribute('data-price','1000');},1200);
                              }catch(error){document.getElementById('result').textContent='Blocked';}
                            })();
                            </script>
                            """;
                        request.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)),200,"OK","Content-Type: text/html; charset=utf-8");
                    }
                    else request.Response=core.Environment.CreateWebResourceResponse(null,403,"Fixture never contacts the network","");
                };
            };
            try
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await session.PrepareAsync(true,timeout.Token);
                var raw=await session.ReadSchemeAsync("https://pro.ticketland.ru/management/show/1/ticket-control/2",timeout.Token);
                var snapshot=TicketPro037.Parse(raw,DateTimeOffset.UtcNow);
                if(snapshot.Places.Count!=1||snapshot.Places[0].Status!="В продаже")throw new Exception("Complete scheme was not read");
                Console.WriteLine("PASS read-only scheme initializes with local empty basket and no external writes");
                const string page="https://pro.ticketland.ru/management/show/1/ticket-control/2";
                foreach(var request in new[]{"https://pro.ticketland.ru/api/basket/add","https://pro.ticketland.ru/api/basket/clear?extra=1","https://pro.ticketland.ru/api/basket/clear/","http://pro.ticketland.ru/api/basket/clear","https://other.test/api/basket/clear","https://pro.ticketland.ru:444/api/basket/clear","https://user@pro.ticketland.ru/api/basket/clear"})
                    if(TicketProReadOnly040.ShouldEmulateEmptyBasket("POST",page,request))throw new Exception("Read-only adapter accepted an unrelated request");
                if(TicketProReadOnly040.ShouldEmulateEmptyBasket("DELETE",page,"https://pro.ticketland.ru/api/basket/clear")||TicketProReadOnly040.ShouldEmulateEmptyBasket("POST","https://pro.ticketland.ru/login","https://pro.ticketland.ru/api/basket/clear"))throw new Exception("Read-only adapter accepted an unrelated method or page");
                Console.WriteLine("PASS local adapter excludes other mutations origins ports and pages");
                var matches=typeof(TicketProSession037).GetMethod("MatchesRequestedDocument040",BindingFlags.Static|BindingFlags.NonPublic)??throw new Exception("Discovery accepts unrelated session-list documents");
                bool Matches(string requested,string actual)=>(bool)matches.Invoke(null,[requested,actual])!;
                const string root="https://pro.ticketland.ru/management/show";
                if(!Matches(root,root+"/active")||!Matches(root+"/1",root+"/1/ticket-control")||!Matches(root+"/1/ticket-control/2",root+"/1/ticket-control/2"))throw new Exception("Expected canonical document rejected");
                if(Matches(root+"/1",root+"/2/ticket-control")||Matches(root+"/1/ticket-control/2",root+"/1/ticket-control/3")||Matches(root,"https://other.test/management/show/active"))throw new Exception("Unrelated document accepted");
                Console.WriteLine("PASS navigation and table identity reject another showing or origin");
            }
            finally{session.DisposeSession();}
        }
    }
}
