using System.IO;
using System.Text.Json;
using Tickets.Core;

namespace Tickets.App;

internal sealed partial class TicketProSession037
{
    private TicketlandLogin028? pendingLogin050;
    private bool attemptedLogin050;
    private string CredentialsPath050=>Path.Combine(profile,"credentials050.bin");
    internal static bool IsLoginPage050(string source)=>Uri.TryCreate(source,UriKind.Absolute,out var uri)&&
        uri.Scheme=="https"&&uri.Host=="pro.ticketland.ru"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&uri.AbsolutePath=="/login";
    private const string LoginFields050="""
        const passwords=[...document.querySelectorAll('input[type=password]')].filter(x=>!x.disabled&&x.getClientRects().length);
        if(passwords.length!==1)return false;
        const password=passwords[0],form=password.closest('form');if(!form)return false;
        const users=[...form.querySelectorAll('input')].filter(x=>!x.disabled&&x.getClientRects().length&&['text','email'].includes(x.type));
        const username=users.find(x=>x.autocomplete==='username')||(users.length===1?users[0]:null);
        if(!username)return false;
        """;
    internal static readonly string CaptureLogin050="""
        (()=>{
          if(window!==window.top)return;
          document.addEventListener('submit',event=>{
            if(location.origin!=='https://pro.ticketland.ru'||location.pathname!=='/login')return;
        """+LoginFields050+"""
            if(event.target!==form)return;
            if(username.value&&password.value)window.chrome.webview.postMessage({kind:'tickets-login050',Username:username.value,Password:password.value});
          },true);
        })();
        """;
    internal static string FillLogin050(TicketlandLogin028 login)=>"""
        (()=>{
          if(window!==window.top||location.origin!=='https://pro.ticketland.ru'||location.pathname!=='/login')return false;
        """+LoginFields050+"\nconst login="+JsonSerializer.Serialize(login)+";\n"+"""
          const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;
          for(const [field,value] of [[username,login.Username],[password,login.Password]]){
            set.call(field,value);field.dispatchEvent(new Event('input',{bubbles:true}));field.dispatchEvent(new Event('change',{bubbles:true}));
          }
          if(!form.checkValidity())return false;
          form.requestSubmit();return true;
        })()
        """;
    private async Task InitializeLogin050()
    {
        await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(CaptureLogin050);
        view.CoreWebView2.WebMessageReceived+=(_,e)=>{
            if(!IsLoginPage050(e.Source))return;
            try
            {
                if(e.WebMessageAsJson.Length>12000)return;
                using var doc=JsonDocument.Parse(e.WebMessageAsJson);
                if(!doc.RootElement.TryGetProperty("kind",out var kind)||kind.GetString()!="tickets-login050")return;
                var login=doc.RootElement.Deserialize<TicketlandLogin028>();
                if(TicketProCredentials050.Valid(login))pendingLogin050=login;
            }
            catch(JsonException){}
        };
    }
    // Called only after a protected page has positively confirmed sign-in.
    private void RememberCredentials050()
    {
        attemptedLogin050=false;
        if(pendingLogin050 is not {} login)return;
        TicketProCredentials050.Save(CredentialsPath050,login);pendingLogin050=null;
    }
    private async Task Reauthenticate050(string requested,CancellationToken token)
    {
        if(!IsLoginPage050(view.CoreWebView2.Source))return;
        var login=TicketProCredentials050.Read(CredentialsPath050);
        if(login is null||attemptedLogin050)throw new TicketException("Ticket PRO: требуется вход в настройках. Последние данные сохранены.");
        attemptedLogin050=true;
        // Never retry rejected credentials on each refresh. A successful or manual login resets this guard.
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var submitted=false;
            for(var i=0;i<40&&!submitted;i++)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if(await IsSignedIn038())break;
                submitted=await view.ExecuteScriptAsync(FillLogin050(login))=="true";
                if(!submitted)await Task.Delay(250,timeout.Token);
            }
            while(!await IsSignedIn038())await Task.Delay(250,timeout.Token);
            await RememberSignIn039Async();
            await NavigateDocument038(requested,timeout.Token);
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested)
        {pendingLogin050=null;throw new TicketException("Ticket PRO: автоматический вход не выполнен. Войдите в настройках. Последние данные сохранены.");}
    }
}
