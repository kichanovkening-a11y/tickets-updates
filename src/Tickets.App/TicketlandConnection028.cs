using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private sealed record TicketlandSettings028(string ConnectionId,bool Enabled);
    private TicketlandSettings028 ticketlandSettings028=new(Guid.NewGuid().ToString("D"),false);
    private readonly CancellationTokenSource ticketlandLifetime028=new();
    private readonly SemaphoreSlim ticketlandGate028=new(1,1);
    private DispatcherTimer? ticketlandTimer028;
    internal Func<TicketlandLogin028,CancellationToken,Task<TicketlandReport028>>? TicketlandFetch028;
    internal string TicketlandStatus028{get;private set;}="Ticketland не подключён.";
    internal event Action? TicketlandStatusChanged028;
    internal bool TicketlandIsUpdating028=>ticketlandGate028.CurrentCount==0;
    private string TicketlandSettingsPath028=>Path.Combine(Path.GetDirectoryName(store.Path)!,"ticketland-connection.json");

    private void InitializeTicketland028()
    {
        try
        {
            if(File.Exists(TicketlandSettingsPath028))
            {
                var saved=JsonSerializer.Deserialize<TicketlandSettings028>(File.ReadAllText(TicketlandSettingsPath028));
                if(saved is not null&&Guid.TryParse(saved.ConnectionId,out _))ticketlandSettings028=saved;
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException)
        {SetTicketlandStatus028("Ticketland: не удалось прочитать настройки подключения.");}
        ticketlandTimer028=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};
        ticketlandTimer028.Tick+=async(_,_)=>await RefreshTicketland028();
        Loaded+=async(_,_)=>{if(ticketlandSettings028.Enabled){ticketlandTimer028.Start();await RefreshTicketland028();}};
        Closed+=(_,_)=>{ticketlandTimer028.Stop();ticketlandLifetime028.Cancel();};
    }

    private void SaveTicketlandSettings028(TicketlandSettings028 settings)
    {
        var path=TicketlandSettingsPath028;
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(settings));File.Move(path+".tmp",path,true);
        ticketlandSettings028=settings;
    }
    private void SetTicketlandStatus028(string text)
    {
        if(!Dispatcher.CheckAccess())
        {
            if(!Dispatcher.HasShutdownStarted)Dispatcher.Invoke(()=>SetTicketlandStatus028(text));
            return;
        }
        TicketlandStatus028=text;
        if(ready)StatusText.Text=text;
        TicketlandStatusChanged028?.Invoke();
    }

    internal async Task ConnectTicketland028(TicketlandLogin028 login,CancellationToken cancellation)
    {
        if(string.IsNullOrWhiteSpace(login.Username)||string.IsNullOrEmpty(login.Password))throw new TicketException("Укажите логин и пароль Ticketland.");
        await SyncTicketland028(login,true,cancellation);
    }
    internal async Task RefreshTicketland028()
    {
        if(!ticketlandSettings028.Enabled||ticketlandLifetime028.IsCancellationRequested)return;
        try
        {
            var login=TicketlandCredentials028.Read(ticketlandSettings028.ConnectionId);
            if(login is null){SetTicketlandStatus028("Ticketland: войдите в настройках на этом компьютере.");return;}
            await SyncTicketland028(login,false,ticketlandLifetime028.Token);
        }
        catch(OperationCanceledException){}
        catch(TicketException){}
        catch(Exception){SetTicketlandStatus028("Ticketland: обновление не выполнено. Сохранены последние полученные данные.");}
    }
    private async Task SyncTicketland028(TicketlandLogin028 login,bool connect,CancellationToken cancellation)
    {
        if(!await ticketlandGate028.WaitAsync(0,cancellation))throw new TicketException("Обновление Ticketland уже выполняется.");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,ticketlandLifetime028.Token);
        var applied=false;
        try
        {
            SetTicketlandStatus028("Ticketland: получаю отчёт…");
            TicketlandReport028 report;
            if(TicketlandFetch028 is not null)report=await TicketlandFetch028(login,linked.Token);
            else
            {
                using var client=new TicketlandClient028{Progress0281=SetTicketlandStatus028};
                var today=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"Russian Standard Time").Date;
                var period=store.TicketlandPeriod0281(today);
                if(period is {} dates)report=await client.FetchCurrentAsync(login.Username,login.Password,dates.From,dates.To,linked.Token);
                else{await client.AuthenticateCurrentAsync(login.Username,login.Password,linked.Token);report=new([],DateTimeOffset.UtcNow);}
            }
            linked.Token.ThrowIfCancellationRequested();
            var summary=await Task.Run(()=>store.ApplyTicketlandReport028(report,replace:true),linked.Token);
            applied=true;
            await Dispatcher.InvokeAsync(()=>
            {
                if(current is not null){var ids=Hall.SelectedSeats.Select(s=>s.Id).ToArray();LoadEvent(false);Hall.SetSelection(ids);}
                if(connect)
                {
                    TicketlandCredentials028.Save(ticketlandSettings028.ConnectionId,login);
                    SaveTicketlandSettings028(ticketlandSettings028 with{Enabled=true});
                    ticketlandTimer028?.Start();
                }
                SetTicketlandStatus028($"Ticketland: {report.FetchedAt.LocalDateTime:dd.MM.yyyy HH:mm} · Мест: {summary.Matched} · Не сопоставлено: {summary.Unmatched} · Совпадений с выдачами: {summary.Conflicts} · Без мест: {summary.GeneralAdmission}");
            });
        }
        catch(OperationCanceledException){SetTicketlandStatus028("Ticketland: получение отчёта остановлено. Предыдущие данные сохранены.");throw;}
        catch(TicketException ex){SetTicketlandStatus028(ex.Message);throw;}
        catch(Exception){SetTicketlandStatus028(applied?"Отчёт Ticketland загружен. Не удалось сохранить подключение на этом компьютере.":"Ticketland: не удалось получить отчёт. Предыдущие данные сохранены.");throw new TicketException(TicketlandStatus028);}
        finally
        {
            ticketlandGate028.Release();
            if(!Dispatcher.HasShutdownStarted)await Dispatcher.InvokeAsync(()=>{UpdateTicketlandRefreshState029();TicketlandStatusChanged028?.Invoke();});
        }
    }
    internal void DisconnectTicketland028()
    {
        if(ticketlandGate028.CurrentCount==0)throw new TicketException("Дождитесь окончания обновления Ticketland.");
        SaveTicketlandSettings028(ticketlandSettings028 with{Enabled=false});ticketlandTimer028?.Stop();
        TicketlandCredentials028.Delete(ticketlandSettings028.ConnectionId);
        SetTicketlandStatus028("Ticketland отключён. Ранее полученные продажи сохранены.");
    }
    internal void ReapplyTicketlandMappings028()
    {
        if(ticketlandGate028.CurrentCount==0)throw new TicketException("Дождитесь окончания обновления Ticketland.");
        var report=store.CachedTicketlandReport028();var summary=store.ApplyTicketlandReport028(report,replace:true);
        if(current is not null){var ids=Hall.SelectedSeats.Select(s=>s.Id).ToArray();LoadEvent(false);Hall.SetSelection(ids);}
        SetTicketlandStatus028($"Ticketland: мест сопоставлено {summary.Matched} · Не сопоставлено: {summary.Unmatched} · Совпадений с выдачами: {summary.Conflicts} · Без мест: {summary.GeneralAdmission}");
    }
}

internal sealed class TicketlandConnectionWindow028:EditorWindow
{
    internal TicketlandConnectionWindow028(Store store,MainWindow main):base(store,"Ticketland",650,500)
    {
        var cancel=new CancellationTokenSource();Closed+=(_,_)=>cancel.Cancel();
        var form=new StackPanel{Margin=new Thickness(20)};Content=form;
        form.Children.Add(new TextBlock{Text="Автоматическое получение продаж Ticketland",TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Bold});
        var login=Field(form,"Логин");
        ((TextBlock)form.Children[1]).TextAlignment=TextAlignment.Center;
        form.Children.Add(new TextBlock{Text="Пароль",TextAlignment=TextAlignment.Center,Margin=new Thickness(0,10,0,4)});
        var password=new PasswordBox{MinHeight=32,Padding=new Thickness(5)};form.Children.Add(password);
        var status=new TextBlock{Text=main.TicketlandStatus028,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,12,0,8)};form.Children.Add(status);
        form.Children.Add(new TextBlock{Text="После подключения программа обновляет отчёт при запуске и каждые 5 минут. Вход запоминается Windows на этом компьютере.",TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap});
        var commands=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};form.Children.Add(commands);
        var connect=Button(commands,"Подключить",()=>{});
        var refresh=Button(commands,"Обновить",()=>{});
        var disconnect=Button(commands,"Отключить",()=>Try(main.DisconnectTicketland028));
        Button(form,"Расхождения",()=>Try(()=>new TicketlandMappingWindow028(store,main).ShowDialog()));
        void Changed()=>status.Text=main.TicketlandStatus028;
        main.TicketlandStatusChanged028+=Changed;Closed+=(_,_)=>main.TicketlandStatusChanged028-=Changed;
        connect.Click+=async(_,_)=>{
            connect.IsEnabled=refresh.IsEnabled=disconnect.IsEnabled=false;
            try{await main.ConnectTicketland028(new(login.Text.Trim(),password.Password),cancel.Token);password.Clear();}
            catch(OperationCanceledException){}
            catch(TicketException ex){status.Text=ex.Message;}
            finally{connect.IsEnabled=refresh.IsEnabled=disconnect.IsEnabled=true;}
        };
        refresh.Click+=async(_,_)=>await main.RefreshTicketland028();
    }
}
