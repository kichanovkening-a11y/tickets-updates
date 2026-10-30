using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Tickets.Core;

namespace Tickets.App;

internal sealed partial class TicketProSession037
{
    internal sealed record DiscoveredShow038(long EventId,string Url);
    internal sealed record Discovery038(IReadOnlyList<DiscoveredShow038> Shows,IReadOnlyList<string> Errors);
    private sealed record CatalogRow038(string Title,string Url);
    private sealed record SessionRow038(string Date,string Venue,string Url);
    private sealed record TablePage038<T>(bool Ready,string Key,bool Next,T[] Items,string Url);
    private static readonly JsonSerializerOptions Json038=new(){PropertyNameCaseInsensitive=true};
    private const string NextButton038="""
        [...document.querySelectorAll('main .q-table__bottom button')].find(b=>!b.disabled&&['chevron_right','keyboard_arrow_right','navigate_next'].includes(b.querySelector('i')?.textContent.trim()))
        """;
    private const string CatalogScript038="""
        (()=>{
          const table=document.querySelector('main table');
          const items=[...(table?.querySelectorAll('tbody tr')||[])].flatMap(r=>{
            const a=r.querySelector('a[href]');if(!a||!/^\/management\/show\/[1-9]\d*$/.test(a.getAttribute('href')))return [];
            const cell=r.cells[0]?.cloneNode(true);cell?.querySelectorAll('button').forEach(b=>b.remove());
            return [{title:(cell?.textContent||'').trim(),url:a.href}];
          });
          const next=[...document.querySelectorAll('main .q-table__bottom button')].some(b=>!b.disabled&&['chevron_right','keyboard_arrow_right','navigate_next'].includes(b.querySelector('i')?.textContent.trim()));
          return JSON.stringify({ready:/^\/management\/show(?:\/active)?\/?$/.test(location.pathname)&&!!table&&!!document.querySelector('main .q-table__bottom'),url:location.origin+location.pathname,key:items.map(x=>x.url).join('|'),next,items});
        })()
        """;
    private const string SessionsScript038="""
        (()=>{
          const table=[...document.querySelectorAll('main table')].find(t=>t.querySelector('thead')?.textContent.includes('Дата сеанса'));
          const items=[...(table?.querySelectorAll('tbody tr')||[])].flatMap(r=>{
            const cells=[...r.cells].map(c=>c.textContent.trim());
            if(cells.length<6||!/^\d+$/.test(cells[1]))return [];
            const date=cells[2].match(/\d{2}\.\d{2}\.\d{4}/)?.[0],time=cells[3].match(/^\d{2}:\d{2}(?::\d{2})?$/)?.[0];
            if(!date||!time)return [];
            return [{date:date+' '+time,venue:cells[4],url:location.origin+location.pathname.replace(/\/$/,'')+'/'+cells[1]}];
          });
          const next=[...document.querySelectorAll('main .q-table__bottom button')].some(b=>!b.disabled&&['chevron_right','keyboard_arrow_right','navigate_next'].includes(b.querySelector('i')?.textContent.trim()));
          return JSON.stringify({ready:/^\/management\/show\/[1-9]\d*\/ticket-control$/.test(location.pathname)&&!!table&&!!document.querySelector('main .q-table__bottom'),url:location.origin+location.pathname,key:items.map(x=>x.url).join('|'),next,items});
        })()
        """;
    internal static bool MatchesRequestedDocument040(string requested,string actual)
    {
        if(!Uri.TryCreate(requested,UriKind.Absolute,out var expected)||!Uri.TryCreate(actual,UriKind.Absolute,out var page)||
           expected.Scheme!="https"||expected.Host!="pro.ticketland.ru"||!expected.IsDefaultPort||expected.UserInfo.Length>0||
           page.Scheme!=expected.Scheme||page.Host!=expected.Host||page.Port!=expected.Port||page.UserInfo.Length>0)return false;
        var target=expected.AbsolutePath.TrimEnd('/');var current=page.AbsolutePath.TrimEnd('/');
        return current==target||target=="/management/show"&&current=="/management/show/active"||
            System.Text.RegularExpressions.Regex.IsMatch(target,@"^/management/show/[1-9]\d*$")&&current==target+"/ticket-control";
    }
    private async Task NavigateDocument038(string url,CancellationToken token)
    {
        var completed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong? id=null;
        void Starting(object? _,CoreWebView2NavigationStartingEventArgs e){if(e.Uri==url)id=e.NavigationId;}
        // A same-page startup navigation may have begun before this listener existed.
        void Done(object? _,CoreWebView2NavigationCompletedEventArgs e){if(id==e.NavigationId||id is null&&e.IsSuccess&&MatchesRequestedDocument040(url,view.CoreWebView2.Source))completed.TrySetResult(e.IsSuccess);}
        view.NavigationStarting+=Starting;view.NavigationCompleted+=Done;
        try
        {
            view.CoreWebView2.Navigate(url);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(45));
            if(!await completed.Task.WaitAsync(timeout.Token))throw new TicketException("Ticket PRO: страница не загрузилась. Последние данные сохранены.");
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested){throw new TicketException("Ticket PRO: время загрузки истекло. Последние данные сохранены.");}
        finally{view.NavigationStarting-=Starting;view.NavigationCompleted-=Done;}
    }
    private async Task<TablePage038<T>> Table038<T>(string script,string? previous,string requested,CancellationToken token)
    {
        for(var i=0;i<90;i++)
        {
            token.ThrowIfCancellationRequested();
            if(new Uri(view.CoreWebView2.Source).AbsolutePath=="/login")throw new TicketException("Ticket PRO: требуется вход в настройках. Последние данные сохранены.");
            var raw=JsonSerializer.Deserialize<string>(await view.ExecuteScriptAsync(script));
            var page=raw is null?null:JsonSerializer.Deserialize<TablePage038<T>>(raw,Json038);
            if(page is {Ready:true}&&page.Key!=previous&&MatchesRequestedDocument040(requested,page.Url))return page;
            await Task.Delay(500,token);
        }
        throw new TicketException("Ticket PRO: список сеансов не загрузился полностью. Последние данные сохранены.");
    }
    private async Task<T[]> AllPages038<T>(string script,string requested,CancellationToken token)
    {
        var rows=new List<T>();string? previous=null;var visited=new HashSet<string>();
        for(var index=0;index<100;index++)
        {
            var page=await Table038<T>(script,previous,requested,token);
            if(!visited.Add(page.Key))throw new TicketException("Ticket PRO: повтор страницы списка. Последние данные сохранены.");
            rows.AddRange(page.Items);
            if(!page.Next)return rows.ToArray();
            previous=page.Key;
            await view.ExecuteScriptAsync("(()=>{const b="+NextButton038+";if(b)b.click();})()");
        }
        throw new TicketException("Ticket PRO: список слишком большой. Последние данные сохранены.");
    }
    internal async Task<Discovery038> DiscoverShows038Async(CancellationToken token,long? eventId=null)
    {
        await NavigateDocument038("https://pro.ticketland.ru/management/show",token);
        var catalog=await AllPages038<CatalogRow038>(CatalogScript038,"https://pro.ticketland.ru/management/show",token);
        await RememberSignIn039Async();
        var result=new List<DiscoveredShow038>();
        var sessionErrors=new List<string>();
        var reads=catalog.DistinctBy(x=>x.Url).Where(x=>store.TicketProTitleIsRelevant038(x.Title,eventId)).Select(show=>(Func<Task>)(async()=>
        {
            if(!Uri.TryCreate(show.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="pro.ticketland.ru"||!uri.IsDefaultPort||!System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,@"^/management/show/[1-9]\d*$"))throw new TicketException("Ticket PRO: неизвестная ссылка мероприятия.");
            await NavigateDocument038(show.Url,token);
            var sessions=await AllPages038<SessionRow038>(SessionsScript038,show.Url,token);
            var sessionReads=sessions.Select(session=>(Func<Task>)(()=>
            {
                if(!TicketPro037.IsSchemeUrl(session.Url)||!DateTime.TryParseExact(session.Date,["dd.MM.yyyy HH:mm","dd.MM.yyyy HH:mm:ss"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var at))throw new TicketException("Ticket PRO: не удалось прочитать дату сеанса.");
                var id=store.MatchTicketProShow038(show.Title,at,session.Venue);
                if(id.HasValue&&(!eventId.HasValue||id==eventId))result.Add(new(id.Value,session.Url));
                return Task.CompletedTask;
            }));
            sessionErrors.AddRange(await TicketProBatch038.RunAsync(sessionReads,token));
        }));
        var errors=await TicketProBatch038.RunAsync(reads,token);
        return new(result,errors.Concat(sessionErrors).ToArray());
    }
}
