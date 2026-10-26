using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class TicketlandReview031UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var token=Guid.NewGuid().ToString("N");var title="Дубровский UI031 "+token;var otherTitle="Любовь UI031 "+token;
        var venueName="Зал UI031 "+token;
        var venue=store.CreateHall(venueName,"",new[]{new DraftSeat{Sector="Партер",Row="1",Number="1",X=0,Y=0},new DraftSeat{Sector="Партер",Row="1",Number="2",X=40,Y=0}});
        var plan=store.SavePlan(venue,token,store.VenueSeats(venue).Select(seat=>new PriceValue(seat.Id,500,"#6EA8E0")).ToArray());
        var starts=DateTime.Today.AddDays(60).AddHours(19);var performance=store.SavePerformance(title);
        var first=store.CreateEvent(performance,venue,plan,starts);
        var second=store.CreateEvent(performance,venue,plan,starts.AddDays(1));
        var other=store.CreateEvent(store.SavePerformance(otherTitle),venue,plan,starts.AddDays(2));
        var clean=store.CreateEvent(store.SavePerformance("Винокур UI031 "+token),venue,plan,starts.AddDays(3));
        TicketlandSale028 Sale(string name,DateTime at,string suffix,string seat="1")=>new(name,at,"Партер Источник "+suffix,"1",seat,token+suffix,500){Venue=venueName};
        var sales=new[]{Sale(title,starts,"A"),Sale(title,starts.AddDays(1),"B"),Sale(otherTitle,starts.AddDays(2),"C")};
        var fetchedAt=(store.TicketlandLastSync028??DateTimeOffset.UtcNow).AddMinutes(1);
        TicketlandReport028 Report(TicketlandSale028[] rows)=>new(rows,fetchedAt=fetchedAt.AddMinutes(1));
        var previousFetch=main.TicketlandFetch028;
        var previousEvent=typeof(MainWindow).GetField("current",Private)!.GetValue(main) as Tickets.Core.EventInfo;
        var search=(TextBox)main.FindName("SearchBox");var previousSearch=search.Text;
        var settings=typeof(MainWindow).GetField("ticketlandSettings028",Private)!.GetValue(main)!;
        var connection=(string)settings.GetType().GetProperty("ConnectionId")!.GetValue(settings)!;
        try
        {
            search.Clear();typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);
            main.SelectEvent(first);Pump();
            main.TicketlandFetch028=(_,_)=>Task.FromResult(Report(sales));
            Wait(main.ConnectTicketland028(new("ui031-"+token,"test-password-"+token),CancellationToken.None));
            Check(store.TicketlandSectorReviews030().Count==3,"fixture contains three exact-show discrepancy groups");
            var scoped=new TicketlandMappingWindow028(store,main,first);var global=new TicketlandMappingWindow028(store,main);
            scoped.Show();global.Show();Pump();
            try
            {
                var scopedIssues=Issues(scoped);var globalIssues=Issues(global);
                Check(scopedIssues.Items.Cast<TicketlandSectorReview030>().Select(issue=>issue.EventId).SequenceEqual(new[]{first}),"scoped review contains only the requested exact show, excluding the same title on another date");
                Check(globalIssues.Items.Count==3&&global.EventId031 is null,"default review retains all shows");
                var scopedSearch=Walk(scoped).OfType<TextBox>().Single(box=>box.Name=="TicketlandSearch030");
                scopedSearch.Text="Любовь";Pump();Check(scopedIssues.Items.Count==0,"search cannot expose another performance in scoped review");scopedSearch.Clear();Pump();
                var expanded=sales.Append(Sale(otherTitle,starts.AddDays(2),"D","2")).ToArray();
                store.ApplyTicketlandReport028(Report(expanded),replace:true);main.ReapplyTicketlandMappings028();Pump();
                Check(scopedIssues.Items.Count==1&&scopedIssues.Items.Cast<TicketlandSectorReview030>().All(issue=>issue.EventId==first),"status notification reload preserves exact-show scope");
                Check(globalIssues.Items.Count==4&&globalIssues.Items.Cast<TicketlandSectorReview030>().Any(issue=>issue.EventId==other),"status notification reload keeps global review global");
            }
            finally{scoped.Close();global.Close();}

            var connectionWindow=new TicketlandConnectionWindow028(store,main);connectionWindow.Show();Pump();
            try
            {
                ObserveReview(main,()=>Walk(connectionWindow).OfType<Button>().Single(button=>Equals(button.Content,"Расхождения")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),window=>{
                    Check(window.EventId031==first&&Issues(window).Items.Count==1&&Issues(window).Items.Cast<TicketlandSectorReview030>().All(issue=>issue.EventId==first),"settings discrepancy command opens only selected show review");
                },1);
            }
            finally{connectionWindow.Close();}

            main.SelectEvent(first);Pump();
            ObserveReview(main,()=>ClickRefresh(main),window=>{
                Check(window.EventId031==first&&Issues(window).Items.Cast<TicketlandSectorReview030>().All(issue=>issue.EventId==first),"hall Refresh opens review for the selected exact show only");
            },1);
            main.SelectEvent(clean);Pump();
            ObserveReview(main,()=>ClickRefresh(main),_=>{},0);
            Check(store.TicketlandSectorReviews030().Any(issue=>issue.EventId==other),"unrelated discrepancies remain available when the selected show needs no popup");

            var diagnosticSource="Владимир Винокур. Новогодняя программа UI031 "+token;
            main.TicketlandFetch028=(_,_)=>Task.FromResult(Report(sales.Append(Sale(diagnosticSource,starts.AddDays(3),"diagnostic")).ToArray()));
            ObserveReview(main,()=>ClickRefresh(main),_=>{},0);
            var hallTotals=(TextBlock)main.FindName("HallTotals");
            Check(hallTotals.Text.Contains("нет точного совпадения")&&hallTotals.ToolTip is TextBlock explanation&&explanation.Text.Contains(diagnosticSource,StringComparison.OrdinalIgnoreCase)&&explanation.Text.Contains("Не совпадает: название"),"unmatched showing explains source title instead of a silent zero");

            var completion=new TaskCompletionSource<TicketlandReport028>();
            main.TicketlandFetch028=(_,_)=>completion.Task;
            main.SelectEvent(first);Pump();
            ObserveReview(main,()=>{
                ClickRefresh(main);Check(main.TicketlandIsUpdating028,"selection-switch check waits for an in-flight report");
                main.SelectEvent(second);Pump();completion.SetResult(Report(sales));
            },_=>{},0);
            Check(store.TicketlandSectorReviews030().Any(issue=>issue.EventId==first)&&store.TicketlandSectorReviews030().Any(issue=>issue.EventId==second),"switching shows suppresses stale popup despite discrepancies in both shows");
            store.DeleteVenue(venue);typeof(MainWindow).GetMethod("LoadEvent",Private)!.Invoke(main,[false]);Pump();
            Check(((TextBlock)main.FindName("HallTotals")).ToolTip is null,"archived showing remains viewable without active-catalog diagnostics");
        }
        finally
        {
            main.TicketlandFetch028=previousFetch;
            try{main.DisconnectTicketland028();}finally{TicketlandCredentials028.Delete(connection);}
            if(previousEvent is not null){main.SelectEvent(previousEvent.Id);Pump();}
            search.Text=previousSearch;
        }
    }
    private static void ClickRefresh(MainWindow main)=>((Button)main.FindName("RefreshTicketlandButton029")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static ListBox Issues(Window window)=>Walk(window).OfType<ListBox>().Single(list=>list.Name=="TicketlandIssues029");
    private static void ObserveReview(MainWindow main,Action action,Action<TicketlandMappingWindow028> inspect,int expected)
    {
        var opened=0;Exception? error=null;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(_,_)=>{
            foreach(var window in Application.Current.Windows.OfType<TicketlandMappingWindow028>().ToArray())
            {
                opened++;
                try{inspect(window);}catch(Exception ex){error=ex;}finally{window.Close();}
            }
        };
        timer.Start();
        try
        {
            action();var until=DateTime.UtcNow.AddSeconds(15);
            var button=(Button)main.FindName("RefreshTicketlandButton029");
            while((main.TicketlandIsUpdating028||!button.IsEnabled)&&DateTime.UtcNow<until){Pump();Thread.Sleep(5);}
            Pump();if(error is not null)throw error;
            Check(!main.TicketlandIsUpdating028&&button.IsEnabled,"review check finishes with Refresh enabled");
            Check(opened==expected,$"review popup count is {expected}");
        }
        finally{timer.Stop();}
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
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 031 "+message);}
}
