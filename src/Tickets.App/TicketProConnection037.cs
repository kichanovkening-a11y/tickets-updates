using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private TicketProSession037? ticketProSession037;
    private bool ticketProEnabled037;
    private string ticketProLastError037="";
    private string TicketProSettingsPath037=>Path.Combine(Path.GetDirectoryName(store.Path)!,"ticketpro-settings.json");
    private void InitializeTicketPro037()
    {
        try{if(File.Exists(TicketProSettingsPath037))ticketProEnabled037=JsonSerializer.Deserialize<bool>(File.ReadAllText(TicketProSettingsPath037));}
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){SetTicketlandStatus028("Ticket PRO: не удалось прочитать настройки. Последние состояния сохранены.");}
        Loaded+=async(_,_)=>{if(ticketProEnabled037&&!ticketlandLifetime028.IsCancellationRequested){ticketlandTimer028?.Start();await RefreshTicketPro037();}};
        Closed+=(_,_)=>ticketProSession037?.DisposeSession();
    }
    private void SaveTicketProEnabled037(bool enabled)
    {
        File.WriteAllText(TicketProSettingsPath037+".tmp",JsonSerializer.Serialize(enabled));File.Move(TicketProSettingsPath037+".tmp",TicketProSettingsPath037,true);ticketProEnabled037=enabled;
    }
    private TicketProSession037 ProSession037()=>ticketProSession037??=new TicketProSession037(this,store,ticketlandSettings028.ConnectionId,ConnectTicketPro037,()=>{
        SaveTicketProEnabled037(false);ticketlandTimer028?.Stop();SetTicketlandStatus028("Ticket PRO: обновление отключено. Последние состояния мест сохранены.");
    });
    internal async void OpenTicketPro037()
    {
        try
        {
            if(ticketlandGate028.CurrentCount==0)throw new TicketException("Дождитесь окончания обновления.");
            var session=ProSession037();await session.PrepareAsync(false,ticketlandLifetime028.Token);session.SelectEvent(current?.Id);session.ShowDialog();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){MessageBox.Show(this,ex is TicketException?ex.Message:"Не удалось открыть Ticket PRO. Проверьте наличие Microsoft Edge WebView2 Runtime на этом компьютере.","Ticket PRO",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    private async Task ConnectTicketPro037(long eventId,string json)
    {
        if(!await ticketlandGate028.WaitAsync(0,ticketlandLifetime028.Token))throw new TicketException("Обновление уже выполняется.");
        try
        {
            var snapshot=TicketPro037.Parse(json,DateTimeOffset.UtcNow);
            await Task.Run(()=>store.ApplyTicketPro037(eventId,snapshot),ticketlandLifetime028.Token);
            SaveTicketlandSettings028(ticketlandSettings028 with{Enabled=false});SaveTicketProEnabled037(true);ticketlandTimer028?.Start();ticketProLastError037="";
            if(current?.Id==eventId)LoadEvent(false);
            UpdateSelectedProStatus037();
        }
        finally{ticketlandGate028.Release();UpdateTicketlandRefreshState029();}
    }
    private async Task RefreshTicketPro037()
    {
        if(!ticketProEnabled037||ticketlandLifetime028.IsCancellationRequested)return;
        if(!await ticketlandGate028.WaitAsync(0,ticketlandLifetime028.Token))return;
        try
        {
            var bindings=store.TicketProBindings037().Where(b=>!store.IsEventArchived(b.EventId)).ToArray();
            if(bindings.Length==0){SetTicketlandStatus028("Ticket PRO: выберите показ и подключите его схему в настройках.");return;}
            SetTicketlandStatus028("Ticket PRO: обновляю состояния мест…");
            var session=ProSession037();
            if(session.IsVisible){SetTicketlandStatus028("Ticket PRO: закройте окно подключения для фонового обновления.");return;}
            await session.PrepareAsync(true,ticketlandLifetime028.Token);
            foreach(var binding in bindings)
            {
                var json=await session.ReadSchemeAsync(binding.SourceUrl,ticketlandLifetime028.Token);
                var snapshot=TicketPro037.Parse(json,DateTimeOffset.UtcNow);
                await Task.Run(()=>store.ApplyTicketPro037(binding.EventId,snapshot),ticketlandLifetime028.Token);
            }
            ticketProLastError037="";if(current is not null)LoadEvent(false);UpdateSelectedProStatus037();
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            ticketProLastError037=ex is TicketException?ex.Message:"Ticket PRO: обновление не выполнено. Последние состояния сохранены.";
            if(current is not null)LoadEvent(false);SetTicketlandStatus028(ticketProLastError037+LastProTime037());
        }
        finally{ticketlandGate028.Release();if(!Dispatcher.HasShutdownStarted)UpdateTicketlandRefreshState029();}
    }
    private string LastProTime037()
    {
        var binding=store.TicketProBindings037().FirstOrDefault(b=>b.EventId==current?.Id);
        return binding is null?"":$" Последнее обновление: {binding.FetchedAt.LocalDateTime:dd.MM.yyyy HH:mm}.";
    }
    private bool UpdateSelectedProStatus037()
    {
        var binding=store.TicketProBindings037().FirstOrDefault(b=>b.EventId==current?.Id);
        if(binding is null)return false;
        var seats=store.Seats(binding.EventId);var conflicts=seats.Count(s=>s.IsTicketlandSold&&s.TicketId.HasValue);UpdateConflictCount033(conflicts);
        SetTicketlandStatus028($"Ticket PRO · {current!.Title}, {current.Date} · {binding.FetchedAt.LocalDateTime:dd.MM.yyyy HH:mm} · Продано: {seats.Count(s=>s.IsTicketlandSold)} · Занято: {seats.Count(s=>s.IsTicketlandOccupied)} · В продаже: {seats.Count(s=>s.Available)}"+(ticketProLastError037.Length>0?" · "+ticketProLastError037:""));return true;
    }
}

/// <summary>A separate local profile. Only rendered seat attributes are read; no cookies or credentials are extracted.</summary>
internal sealed class TicketProSession037:Window
{
    private readonly WebView2 view=new();
    private readonly ComboBox shows=new(){DisplayMemberPath="Caption",MinWidth=240,MaxWidth=650,HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
    private readonly Button connect=new(){Content="Подключить этот сеанс",Width=230,Height=38,ContentTemplate=null,Margin=new Thickness(5)};
    private readonly Func<long,string,Task> apply;
    private readonly Store store;
    private readonly string profile;
    private bool initialized,disposing,busy;
    private Task? prepare;
    internal TicketProSession037(Window owner,Store store,string connectionId,Func<long,string,Task> apply,Action disconnect)
    {
        this.apply=apply;this.store=store;Owner=owner;Title="Ticket PRO · подключение мест";Width=Math.Min(1250,SystemParameters.WorkArea.Width);Height=Math.Min(850,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Tickets","TicketPRO",connectionId);
        var panel=new DockPanel();Content=panel;
        var commands=new StackPanel();DockPanel.SetDock(commands,Dock.Top);panel.Children.Add(commands);
        commands.Children.Add(new TextBlock{Text="Выберите показ нашей программы, войдите в Ticket PRO и откройте управление билетами нужного сеанса.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
        shows.ItemsSource=store.Events().Where(e=>!store.IsEventArchived(e.Id)).ToArray();commands.Children.Add(shows);
        var buttons=new WrapPanel();commands.Children.Add(buttons);
        var catalog=new Button{Content="Мероприятия",Width=150,Height=38,ContentTemplate=null,Margin=new Thickness(5)};buttons.Children.Add(catalog);buttons.Children.Add(connect);
        var off=new Button{Content="Отключить обновление",Width=220,Height=38,ContentTemplate=null,Margin=new Thickness(5)};buttons.Children.Add(off);
        var close=new Button{Content="Закрыть",Width=120,Height=38,ContentTemplate=null,Margin=new Thickness(5)};buttons.Children.Add(close);
        commands.Children.Add(status);panel.Children.Add(view);
        catalog.Click+=(_,_)=>{if(!busy&&initialized)view.Source=new Uri("https://pro.ticketland.ru/management/show");};
        off.Click+=(_,_)=>{if(!busy){disconnect();status.Text="Обновление отключено. Сохранённые состояния остаются в программе.";}};
        close.Click+=(_,_)=>Close();
        connect.Click+=async(_,_)=>{
            if(busy)return;
            busy=true;connect.IsEnabled=catalog.IsEnabled=off.IsEnabled=false;view.IsEnabled=false;
            try
            {
                if(shows.SelectedItem is not Tickets.Core.EventInfo selected)throw new TicketException("Выберите показ нашей программы.");
                var json=await ReadCurrentAsync();await apply(selected.Id,json);status.Text="Сеанс подключён. Можно закрыть это окно: состояния обновляются при запуске и каждые 5 минут. При истечении входа потребуется войти снова.";
            }
            catch(Exception ex){status.Text=ex is TicketException?ex.Message:"Не удалось прочитать схему. Последние данные сохранены.";}
            finally{busy=false;connect.IsEnabled=catalog.IsEnabled=off.IsEnabled=true;view.IsEnabled=true;}
        };
        Closing+=(_,e)=>{if(!disposing){e.Cancel=true;Hide();}};
    }
    internal void SelectEvent(long? id){shows.ItemsSource=store.Events().Where(e=>!store.IsEventArchived(e.Id)).ToArray();if(id.HasValue)shows.SelectedItem=shows.Items.Cast<Tickets.Core.EventInfo>().FirstOrDefault(e=>e.Id==id);}
    internal async Task PrepareAsync(bool hidden,CancellationToken token)
    {
        if(initialized)return;
        if(prepare is not null){await prepare;return;}
        prepare=PrepareCoreAsync(hidden,token);
        try{await prepare;}finally{prepare=null;}
    }
    private async Task PrepareCoreAsync(bool hidden,CancellationToken token)
    {
        // WebView2 requires a loaded WPF host. Keep the preparation window off-screen.
        ShowActivated=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.Manual;Left=-10000;Top=-10000;Show();
        try
        {
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:profile);
            token.ThrowIfCancellationRequested();await view.EnsureCoreWebView2Async(environment);
            view.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;
            view.CoreWebView2.Settings.IsGeneralAutofillEnabled=false;
            view.CoreWebView2.Settings.AreDevToolsEnabled=false;
            view.CoreWebView2.NewWindowRequested+=(_,e)=>e.Handled=true;
            view.CoreWebView2.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            view.CoreWebView2.DownloadStarting+=(_,e)=>e.Cancel=true;
            view.CoreWebView2.NavigationStarting+=(_,e)=>{
                if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var u)||u.Scheme!="https"||u.Host!="pro.ticketland.ru"||!u.IsDefaultPort)e.Cancel=true;
            };
            // Sign-in submits credentials normally. After sign-in, reject mutating HTTP requests.
            view.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            view.CoreWebView2.WebResourceRequested+=(_,e)=>{
                var method=e.Request.Method;
                if(method is "GET" or "HEAD" or "OPTIONS")return;
                var login=Uri.TryCreate(view.CoreWebView2.Source,UriKind.Absolute,out var page)&&page.Host=="pro.ticketland.ru"&&page.AbsolutePath=="/login";
                var same=Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var destination)&&destination.Scheme=="https"&&destination.Host=="pro.ticketland.ru"&&destination.IsDefaultPort;
                if(!login||!same)e.Response=environment.CreateWebResourceResponse(null,403,"Read-only connection","");
            };
            view.Source=new Uri("https://pro.ticketland.ru/management/show");initialized=true;
        }
        finally
        {
            Hide();ShowActivated=true;WindowStartupLocation=WindowStartupLocation.CenterOwner;Left=Math.Max(0,Owner.Left+(Owner.ActualWidth-Width)/2);Top=Math.Max(0,Owner.Top+(Owner.ActualHeight-Height)/2);
        }
    }
    private const string SnapshotScript="""
        (()=>{
          const main=document.querySelector('main');
          const places=[...document.querySelectorAll('#hall-scheme-svg [section][row][seat]')].map(e=>({id:e.id,sector:e.getAttribute('section'),row:e.getAttribute('row'),seat:e.getAttribute('seat'),status:e.getAttribute('data-status-name'),fund:e.getAttribute('data-fund-id'),price:e.getAttribute('data-price')}));
          const text=main?.innerText||'';
          const date=text.match(/(\d{2}\.\d{2}\.\d{4})[^\d]*?(\d{2}:\d{2}(?::\d{2})?)/);
          const total=text.match(/Оценено:[ \u00a0\u202f]*[\d \u00a0\u202f]+из[ \u00a0\u202f]*([\d \u00a0\u202f]+)/);
          return JSON.stringify({url:location.origin+location.pathname,title:(main?.querySelector('h1')?.textContent||'').replace(/\s*[—–-]\s*Управление билетами\s*$/,'').trim(),date:date?date[1]+' '+date[2]:'',expectedCount:total?Number(total[1].replace(/\D/g,'')):0,places});
        })()
        """;
    internal async Task<string> ReadCurrentAsync()
    {
        if(!initialized)throw new TicketException("Ticket PRO ещё загружается.");
        if(new Uri(view.CoreWebView2.Source).AbsolutePath=="/login")throw new TicketException("Ticket PRO: требуется вход. Последние данные сохранены.");
        return JsonSerializer.Deserialize<string>(await view.ExecuteScriptAsync(SnapshotScript))??throw new TicketException("Ticket PRO: схема не загружена.");
    }
    internal async Task<string> ReadSchemeAsync(string url,CancellationToken token)
    {
        if(!TicketPro037.IsSchemeUrl(url))throw new TicketException("Ticket PRO: неверная ссылка на сеанс.");
        var completed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong? navigationId=null;
        void Starting(object? _,CoreWebView2NavigationStartingEventArgs e){if(e.Uri==url)navigationId=e.NavigationId;}
        void Done(object? _,CoreWebView2NavigationCompletedEventArgs e){if(navigationId==e.NavigationId)completed.TrySetResult(e.IsSuccess);}
        view.NavigationStarting+=Starting;view.NavigationCompleted+=Done;
        try
        {
            view.CoreWebView2.Navigate(url);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(45));
            if(!await completed.Task.WaitAsync(timeout.Token))throw new TicketException("Ticket PRO: страница не загрузилась. Последние данные сохранены.");
            for(var i=0;i<70;i++)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var json=await ReadCurrentAsync();
                using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
                if(root.GetProperty("url").GetString()==url&&root.GetProperty("expectedCount").GetInt32()>0&&root.GetProperty("places").GetArrayLength()==root.GetProperty("expectedCount").GetInt32())return json;
                await Task.Delay(500,timeout.Token);
            }
            throw new TicketException("Ticket PRO: схема не загрузилась полностью. Последние данные сохранены.");
        }
        finally{view.NavigationStarting-=Starting;view.NavigationCompleted-=Done;}
    }
    internal void DisposeSession(){disposing=true;view.Dispose();Close();}
}
