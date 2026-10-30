using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class TicketPro041UiChecks
{
    private static object? Invoke(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static void Wait(Func<Task> factory)
    {
        var task=Application.Current.Dispatcher.InvokeAsync(factory).Task.Unwrap();
        var frame=new DispatcherFrame();
        _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Logical(child))yield return node;}
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    internal static void Run(string seed)
    {
        var work=Path.Combine(Path.GetTempPath(),"Tickets-selected041-"+Guid.NewGuid());Directory.CreateDirectory(work);
        var path=Path.Combine(work,"tickets.db");File.Copy(seed,path);var store=new Store(path);
        var venue=store.CreateHall("PRO041 "+Guid.NewGuid(),"",Enumerable.Range(1,3).Select(n=>new DraftSeat{Sector="Партер",Row="1",Number=n.ToString(),X=n*22,Y=0}).ToArray());
        var seats=store.VenueSeats(venue).Select(s=>s.Id).ToArray();var plan=store.SavePlan(venue,"041",seats.Select(id=>new PriceValue(id,1000,"#FFFF00")).ToArray());
        var at=new DateTime(2030,11,1,19,0,0);
        long Create(string title,int day)=>store.CreateEvent(store.SavePerformance(title),venue,plan,at.AddDays(day));
        var a=Create("Selected A041",0);var b=Create("Selected B041",1);var legacy=Create("Legacy C041",2);
        string Raw(long id,bool fresh=true)
        {
            var show=store.Events().Single(e=>e.Id==id);
            return JsonSerializer.Serialize(new{url=$"https://pro.ticketland.ru/management/show/1/ticket-control/{id}",title=show.Title,date=DateTime.Parse(show.SortDate,CultureInfo.InvariantCulture).ToString("dd.MM.yyyy HH:mm:ss"),expectedCount=3,
                places=Enumerable.Range(1,3).Select(n=>new{id="p"+n,sector="Партер",row="1",seat=n.ToString(),status=!fresh?"В продаже":n==1?"Продано":n==2?"Забронировано":"В продаже",fund="1",price="1000"}).ToArray()});
        }
        var old=DateTimeOffset.UtcNow.AddDays(-1);
        store.ApplyTicketPro037(a,TicketPro037.Parse(Raw(a,false),old));store.ApplyTicketPro037(b,TicketPro037.Parse(Raw(b,false),old));
        var issue=store.Reserve(a,[seats[0]],"fixture recipient","P041",1);
        var main=new MainWindow(store){WindowState=WindowState.Normal,WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false};
        var previousContext=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(main.Dispatcher));
        try
        {
            var fetch=typeof(MainWindow).GetField("TicketProFetch041",BindingFlags.Instance|BindingFlags.NonPublic)??throw new Exception("Main window refresh is still tied to an all-show batch");
            var calls=new List<long>();
            void Fetch(Func<long,CancellationToken,Task<string>> value)=>fetch.SetValue(main,value);
            Task Refresh()=>main.RefreshTicketland028();
            Fetch((id,_)=>{calls.Add(id);return Task.FromResult(Raw(id));});
            Invoke(main,"SaveTicketProEnabled037",true);main.Show();
            Wait(Refresh);Check(calls.Count==0,"startup and home do not request any showing");
            main.SelectEvent(a);Wait(Refresh);
            Check(calls.SequenceEqual(new[]{a}),"selecting a known showing reads it once without other showings");
            Check(store.TicketProBindings037().Single(x=>x.EventId==b).FetchedAt==old,"unselected showing data is unchanged");
            Check(store.Seats(a)[0].IsTicketlandSold&&store.Seats(a)[1].IsTicketlandOccupied,"selected scheme is applied before refresh completes");
            Wait(Refresh);Check(calls.SequenceEqual(new[]{a,a}),"manual refresh reads only the open showing");
            Check(!main.TicketlandIsUpdating028&&((Button)main.FindName("RefreshTicketlandButton029")).IsEnabled,"refresh button is released after one showing");

            var legend=Logical((DependencyObject)main.FindName("StatusLegendPanel")).OfType<TextBlock>().Select(t=>t.Text).ToArray();
            Check(legend.Contains("Продано Ticket PRO")&&!legend.Any(t=>t.Contains("Ticketland")),"PRO legend names the actual source");
            Check(store.Seats(a)[0].Status.Contains("Ticket PRO")&&!store.Seats(a)[0].Status.Contains("Ticketland"),"sold and conflicting seat status names PRO");
            var status=(TextBlock)main.FindName("StatusText");
            Check(status.ToolTip?.ToString()?.Contains("Ticket PRO")==true&&!status.ToolTip.ToString()!.Contains("Без ряда"),"PRO status tooltip does not explain legacy report coordinates");
            Check(((Button)main.FindName("TicketlandConflictsButton033")).ToolTip.ToString()!.Contains("Ticket PRO"),"PRO conflict button tooltip names PRO");
            var conflicts=new TicketlandConflictsWindow033(store,a);
            Check(conflicts.Title.Contains("Ticket PRO")&&!Logical(conflicts).OfType<TextBlock>().Any(t=>t.Text.Contains("Ticketland")),"conflict window and explanation name the selected source");conflicts.Close();
            void RejectPro(Action action)
            {try{action();throw new Exception("Expected rejection");}catch(TicketException ex){Check(ex.Message.Contains("Ticket PRO")&&!ex.Message.Contains("Ticketland"),"sale conflict message names PRO");}}
            RejectPro(()=>store.ConfirmReservation(a,issue));
            RejectPro(()=>store.MarkTicketsPrinted(a,[store.History(a).Single().Id]));
            RejectPro(()=>store.TicketReprint(a,[seats[0]]));

            calls.Clear();CancellationToken previous=default;
            Fetch(async(id,token)=>{calls.Add(id);if(id==a){previous=token;await Task.Delay(Timeout.Infinite,token);}return Raw(id);});
            var pending=Refresh();main.SelectEvent(b);Wait(Refresh);Wait(()=>pending);
            Check(previous.IsCancellationRequested&&calls.SequenceEqual(new[]{a,b}),"switching the actual screen cancels the previous showing and loads the new one");
            Check(main.TicketlandStatus028.Contains("Selected B041"),"old cancellation does not overwrite the newly selected status");
            calls.Clear();main.SelectEvent(a);Invoke(main,"HomeClick",null,new RoutedEventArgs());Wait(Refresh);
            Check(calls.SequenceEqual(new[]{a})&&!main.TicketlandIsUpdating028,"Home cancels the actual screen refresh without scanning others");

            var before=store.TicketProBindings037().Single(x=>x.EventId==a).FetchedAt;
            Fetch((id,_)=>Task.FromException<string>(new TicketException("fixture source failure")));
            main.SelectEvent(a);Wait(Refresh);
            Check(store.TicketProBindings037().Single(x=>x.EventId==a).FetchedAt==before&&!main.TicketlandIsUpdating028,"failure preserves the previous snapshot and releases the refresh gate");
            Fetch((id,_)=>Task.FromResult(Raw(id)));Wait(Refresh);
            Check(!main.TicketlandStatus028.Contains("fixture source failure"),"successful retry clears the failed request status");
            HallStatus030UiChecks.CheckLayout(main);
            Corrections025UiChecks.Capture(main,"041-hall-normal");
            var toggle=(Button)main.FindName("ToggleHallButton038");
            toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(((FrameworkElement)main.FindName("WorkspaceNavigation")).Visibility==Visibility.Collapsed&&Equals(toggle.ToolTip,"Свернуть зал"),"green expansion icon hides navigation and offers restore");
            Corrections025UiChecks.Capture(main,"041-hall-expanded");
            toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(((FrameworkElement)main.FindName("WorkspaceNavigation")).Visibility==Visibility.Visible&&Equals(toggle.ToolTip,"Раскрыть зал"),"green expansion icon restores navigation");

            Invoke(main,"SaveTicketProEnabled037",false);
            var show=store.Events().Single(e=>e.Id==legacy);
            store.ApplyTicketlandReport028(new([new TicketlandSale028(show.Title,at.AddDays(2),"Партер","1","1","legacy041",1000){Venue=show.Venue}],DateTimeOffset.UtcNow),true);
            main.SelectEvent(legacy);
            Check(store.Seats(legacy)[0].Status.Contains("Ticketland"),"legacy report seats retain their actual Ticketland source");
            Check(status.ToolTip?.ToString()?.Contains("Без ряда")==true,"legacy report retains its matching explanation");
            Check(store.TicketProTitleIsRelevant038("Selected A041")&&!store.TicketProTitleIsRelevant038("unrelated fixture"),"legacy title relevance still works");
            Check(store.TicketProTitleIsRelevant038("Selected A041",a)&&!store.TicketProTitleIsRelevant038("Selected B041",a),"selected discovery excludes other performance titles");
        }
        finally{main.Close();SynchronizationContext.SetSynchronizationContext(previousContext);}
    }
}
