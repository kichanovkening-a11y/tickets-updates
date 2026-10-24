using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class TicketlandConnection028UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var settingsPath=Path.Combine(Path.GetDirectoryName(store.Path)!,"ticketland-connection.json");
        Check(!File.Exists(settingsPath)&&!Directory.Exists(settingsPath+".tmp"),"integration check requires isolated temporary database settings");
        var settings=typeof(MainWindow).GetField("ticketlandSettings028",Private)!.GetValue(main)!;
        var connection=(string)settings.GetType().GetProperty("ConnectionId")!.GetValue(settings)!;
        Check(Guid.TryParse(connection,out _)&&TicketlandCredentials028.Read(connection) is null,"connection starts with a fresh credential target");
        var previousFetch=main.TicketlandFetch028;
        var search=(TextBox)main.FindName("SearchBox");var previousSearch=search.Text;
        var previousEvent=typeof(MainWindow).GetField("current",Private)!.GetValue(main) as Tickets.Core.EventInfo;
        var login=new TicketlandLogin028("test-login-"+Guid.NewGuid().ToString("N"),"test-password-"+Guid.NewGuid().ToString("N"));
        try
        {
            CheckConnectionWindow(store,main);
            var fixture="Ticketland UI028 "+Guid.NewGuid().ToString("N");
            var venue=store.CreateHall(fixture,"",Enumerable.Range(1,3).Select(n=>new DraftSeat{Sector="Партер",Row="1",Number=n.ToString(),X=n*40,Y=0}).ToArray());
            var ids=store.VenueSeats(venue).OrderBy(s=>s.Number).Select(s=>s.Id).ToArray();
            var plan=store.SavePlan(venue,fixture,ids.Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
            var starts=DateTime.Today.AddDays(20).AddHours(19);
            while(store.Events().Any(e=>DateTime.Parse(e.SortDate,CultureInfo.InvariantCulture)==starts))starts=starts.AddSeconds(1);
            var eventId=store.CreateEvent(store.SavePerformance(fixture),venue,plan,starts);
            store.SetSeatsBlocked(eventId,[ids[1]],true);
            var issue=store.Issue(eventId,[ids[2]],"Ticketland UI028 local","TL028",1);
            var local=store.Invoice(eventId,issue).Tickets.Single();
            search.Clear();
            typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);
            typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);
            main.SelectEvent(eventId);Pump();
            var hall=(HallView)main.FindName("Hall");
            var at=(store.TicketlandLastSync028??DateTimeOffset.UtcNow).AddMinutes(1);
            var report=new TicketlandReport028([new(fixture,starts,"Партер","1","1","TL028-"+Guid.NewGuid().ToString("N"),500)],at);
            var calls=0;
            main.TicketlandFetch028=(saved,_)=>{Check(saved==login,"fetch receives original login or login recovered from Windows vault");calls++;return Task.FromResult(report);};
            Wait(main.ConnectTicketland028(login,CancellationToken.None));
            Check(calls==1&&store.Seats(eventId).Single(s=>s.Id==ids[0]).IsTicketlandSold&&store.TicketlandLastSync028==at,"connect commits exact local seat and report timestamp");
            var json=File.ReadAllText(settingsPath);
            using(var document=JsonDocument.Parse(json))Check(document.RootElement.GetProperty("ConnectionId").GetString()==connection&&document.RootElement.GetProperty("Enabled").GetBoolean(),"nonsecret settings enable the same random connection");
            Check(!json.Contains(login.Username,StringComparison.Ordinal)&&!json.Contains(login.Password,StringComparison.Ordinal)&&!json.Contains("Username",StringComparison.OrdinalIgnoreCase)&&!json.Contains("Password",StringComparison.OrdinalIgnoreCase),"settings JSON contains no username or password");
            Check(TicketlandCredentials028.Read(connection)==login,"connect remembers login only in native vault");
            hall.SetSelection([ids[0]]);Pump();
            Check(hall.SelectedSeats.Single().IsTicketlandSold&&!hall.SelectedSeats.Single().Available&&hall.SelectedSeats.Single().Status=="Продано Ticketland","hall exposes remote seat as sold and unavailable");
            Check(!((Button)main.FindName("IssueButton")).IsEnabled,"existing issue command is disabled for remote seat");
            try{typeof(MainWindow).GetMethod("IssueSelected",Private)!.Invoke(main,[false]);throw new Exception("External seat was accepted for issuance.");}
            catch(TargetInvocationException ex) when(ex.InnerException is TicketException error&&error.Message.Contains("только свободные",StringComparison.Ordinal)){}
            Check(store.Invoice(eventId,issue).Tickets.Single().Id==local.Id&&store.Seats(eventId).Single(s=>s.Id==ids[0]).TicketId is null,"external issuance guard preserves local ticket and creates no remote ticket");

            report=new([],at.AddMinutes(1));Wait(main.RefreshTicketland028());
            Check(calls==2&&store.Seats(eventId).Single(s=>s.Id==ids[0]).Available,"refresh recovers saved credentials and frees sales absent from complete snapshot");
            Check(store.Seats(eventId).Single(s=>s.Id==ids[1]).IsBlocked&&store.Seats(eventId).Single(s=>s.Id==ids[2]).TicketId==local.Id,"snapshot replacement preserves manual block and local ticket");

            report=new([new(fixture,starts,"Партер","1","1","TL028-"+Guid.NewGuid().ToString("N"),500)],at.AddMinutes(2));
            Directory.CreateDirectory(settingsPath+".tmp");
            try{Wait(main.ConnectTicketland028(login,CancellationToken.None));throw new Exception("Expected settings persistence failure.");}
            catch(TicketException){Check(main.TicketlandStatus028.StartsWith("Отчёт Ticketland загружен. Не удалось сохранить подключение",StringComparison.Ordinal)&&!main.TicketlandStatus028.Contains("Предыдущие данные",StringComparison.Ordinal),"post-commit persistence failure accurately reports loaded report");}
            finally{Directory.Delete(settingsPath+".tmp");}
            hall.SetSelection([ids[0]]);Pump();
            Check(store.TicketlandLastSync028==report.FetchedAt&&store.Seats(eventId).Single(s=>s.Id==ids[0]).IsTicketlandSold&&hall.SelectedSeats.Single().IsTicketlandSold,"settings write failure preserves committed report and refreshed hall");
            main.TicketlandFetch028=(_,_)=>Task.FromException<TicketlandReport028>(new IOException("Synthetic fetch failure."));
            Wait(main.RefreshTicketland028());
            Check(store.TicketlandLastSync028==report.FetchedAt&&store.Seats(eventId).Single(s=>s.Id==ids[0]).IsTicketlandSold&&hall.SelectedSeats.Single().IsTicketlandSold,"failed fetch preserves last successful timestamp and markers");
            Check(main.TicketlandStatus028.Contains("обновление не выполнено",StringComparison.Ordinal)&&main.TicketlandStatus028.Contains("последние полученные данные",StringComparison.Ordinal),"failed automatic refresh explains that last report remains available");
            main.TicketlandFetch028=(_,_)=>Task.FromException<TicketlandReport028>(new TicketException("Вход в Ticketland выполнен. Превышено время ожидания отчёта текущих сеансов."));
            Wait(main.RefreshTicketland028());
            Check(main.TicketlandStatus028.StartsWith("Вход в Ticketland выполнен.",StringComparison.Ordinal),"automatic refresh preserves precise login/report stage error");
            CheckMappingWindow(store,main,eventId,starts,ids[0],report.FetchedAt.AddMinutes(1));
        }
        finally
        {
            main.TicketlandFetch028=previousFetch;
            if(Directory.Exists(settingsPath+".tmp"))Directory.Delete(settingsPath+".tmp");
            try{main.DisconnectTicketland028();}
            finally{TicketlandCredentials028.Delete(connection);}
            if(previousEvent is not null){main.SelectEvent(previousEvent.Id);Pump();}
            search.Text=previousSearch;
        }
    }
    private static void CheckConnectionWindow(Store store,MainWindow main)
    {
        var window=new TicketlandConnectionWindow028(store,main);window.Show();Pump();
        try
        {
            var controls=Walk(window).ToArray();
            Check(controls.OfType<TextBox>().Count()==1&&controls.OfType<PasswordBox>().Count()==1,"login form contains one login and one protected password input");
            var buttons=controls.OfType<Button>().Select(b=>b.Content?.ToString()).ToArray();
            Check(new[]{"Подключить","Обновить","Отключить","Сопоставление показов и секторов"}.All(b=>buttons.Count(x=>x==b)==1)&&buttons.Length==4,"login form has automatic connection and mapping commands without manual exports");
            Corrections025UiChecks.Capture(window,"028-ticketland-connection");
            foreach(var caption in new[]{"Логин","Пароль"})
            {
                var label=controls.OfType<TextBlock>().Single(t=>t.Text==caption);
                FrameworkElement input=caption=="Логин"?controls.OfType<TextBox>().Single():controls.OfType<PasswordBox>().Single();
                var labelCenter=label.TranslatePoint(new Point(label.ActualWidth/2,0),window).X;
                var inputCenter=input.TranslatePoint(new Point(input.ActualWidth/2,0),window).X;
                Check(label.TextAlignment==TextAlignment.Center&&Math.Abs(labelCenter-inputCenter)<.5,"connection input caption is centered over its field: "+caption);
            }
        }
        finally{window.Close();}
    }
    private static void CheckMappingWindow(Store store,MainWindow main,long eventId,DateTime starts,long seatId,DateTimeOffset at)
    {
        var sourceTitle="Ticketland source UI028 "+Guid.NewGuid().ToString("N");
        store.ApplyTicketlandReport028(new([new(sourceTitle,starts,"Ticketland Партер","1","1","TL028-MAP-"+Guid.NewGuid().ToString("N"),500)],at),replace:true);
        store.BindTicketlandShow028(sourceTitle,starts,eventId);
        var window=new TicketlandMappingWindow028(store,main);window.Show();Pump();
        try
        {
            var controls=Walk(window).ToArray();var combos=controls.OfType<ComboBox>().ToArray();
            Check(combos.Length==4,"mapping form has one source/local show pair and one source/local sector pair");
            Check(combos[0].Items.Count==1&&combos[0].SelectedItem is not null&&combos[1].Items.Count==1&&combos[1].SelectedItem is not null,"opening prebound show selects its sole exact-date local candidate");
            Check(combos[2].Items.Cast<string>().Any(s=>string.Equals(s,"Ticketland Партер",StringComparison.OrdinalIgnoreCase))&&combos[3].Items.Cast<string>().Contains("Партер"),"prebound local show populates sector choices immediately on opening");
            combos[2].SelectedIndex=0;combos[3].SelectedItem="Партер";Pump();
            Corrections025UiChecks.Capture(window,"028-ticketland-mapping");
            controls.OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(store.Seats(eventId).Single(s=>s.Id==seatId).IsTicketlandSold,"saving explicit show and sector mapping resolves different Ticketland labels");
            var hall=(HallView)main.FindName("Hall");hall.SetSelection([seatId]);Pump();
            Check(hall.SelectedSeats.Single().IsTicketlandSold,"saving mappings refreshes actual hall from cached report");
        }
        finally{window.Close();}
    }
    private static void Wait(Task task)
    {
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame();
            _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();Pump();
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 028 "+message);}
}
