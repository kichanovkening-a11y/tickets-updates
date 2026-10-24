using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Ticketland029UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void CheckLayout(MainWindow main)
    {
        var width=main.Width;var height=main.Height;var state=main.WindowState;
        var scroll=(ScrollViewer)main.FindName("OrdinaryActionsScroll");
        try
        {
            main.WindowState=WindowState.Normal;
            foreach(var size in new[]{new Size(1420,880),new Size(900,600)})
            {
                main.Width=size.Width;main.Height=size.Height;scroll.ScrollToHorizontalOffset(0);Pump();main.UpdateLayout();Pump();
                var square=(StackPanel)main.FindName("SquareShowActions");var refresh=(Button)main.FindName("RefreshTicketlandButton029");
                var ordinary=(Button)main.FindName("ExtendInvoiceButton");
                Check(square.Children.OfType<Button>().Count()==2,"only the two existing seat actions remain square");
                var issue=(Button)main.FindName("IssueButton");var print=(Button)main.FindName("PrintTicketsButton");
                Check(new[]{issue,print}.All(b=>b.Width==72&&b.Height==72),"existing square dimensions are unchanged");
                Check(Math.Abs(refresh.TranslatePoint(new Point(),main).Y-ordinary.TranslatePoint(new Point(),main).Y)<.5,"refresh aligns with ordinary commands at "+size.Width);
                Check(refresh.TranslatePoint(new Point(),main).X>=issue.TranslatePoint(new Point(issue.ActualWidth,0),main).X,"refresh is directly after issue at "+size.Width);
                Check(refresh.FontSize==ordinary.FontSize&&refresh.ActualHeight==ordinary.ActualHeight,"refresh uses ordinary button typography and height");
                refresh.BringIntoView();Pump();
                var root=(FrameworkElement)main.Content;var hit=root.InputHitTest(refresh.TranslatePoint(new Point(refresh.ActualWidth/2,refresh.ActualHeight/2),root)) as DependencyObject;
                Check(hit is not null&&Visuals(refresh).Contains(hit),"refresh is reachable after horizontal scrolling at "+size.Width);
                if(size.Width==1420)Corrections025UiChecks.Capture(main,"029-whole-hall-refresh");
            }
        }
        finally{main.Width=width;main.Height=height;main.WindowState=state;scroll.ScrollToHorizontalOffset(0);Pump();}
    }
    internal static void CheckRefreshButton(MainWindow main)
    {
        var fetch=main.TicketlandFetch028!;var completion=new TaskCompletionSource<TicketlandReport028>();var calls=0;
        main.TicketlandFetch028=async(login,cancel)=>{calls++;var report=await fetch(login,cancel);await completion.Task;return report;};
        var button=(Button)main.FindName("RefreshTicketlandButton029");
        try
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(calls==1&&!button.IsEnabled&&main.TicketlandIsUpdating028,"ordinary refresh starts exactly one fetch and disables during update");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Check(calls==1,"repeated refresh while fetching does not duplicate request");
            completion.SetResult(new([],DateTimeOffset.UtcNow));
            var until=DateTime.UtcNow.AddSeconds(15);while(!button.IsEnabled&&DateTime.UtcNow<until){Pump();Thread.Sleep(5);}
            Check(button.IsEnabled&&!main.TicketlandIsUpdating028,"ordinary refresh re-enables after completed fetch");
        }
        finally{completion.TrySetResult(new([],DateTimeOffset.UtcNow));main.TicketlandFetch028=fetch;}
    }
    internal static void CheckReview(Store store,MainWindow main)
    {
        var name="Review UI029 "+Guid.NewGuid().ToString("N");
        var venue=store.CreateHall(name,"",new[]{new DraftSeat{Sector="Партер",Row="1",Number="1",X=0,Y=0},new DraftSeat{Sector="Балкон",Row="1",Number="2",X=40,Y=0}});
        var ids=store.VenueSeats(venue).OrderBy(s=>s.Number).Select(s=>s.Id).ToArray();
        var plan=store.SavePlan(venue,name,ids.Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
        var starts=DateTime.Today.AddDays(45).AddSeconds(Random.Shared.Next(10000));
        var eventId=store.CreateEvent(store.SavePerformance(name),venue,plan,starts);
        var source=name+" другое название";
        store.ApplyTicketlandReport028(new([new(source,starts,"Источник партер","1","1",name+"A",500),new(source,starts,"Источник балкон","1","2",name+"B",500)],(store.TicketlandLastSync028??DateTimeOffset.UtcNow).AddMinutes(1)),replace:true);
        typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);main.SelectEvent(eventId);Pump();
        var window=new TicketlandMappingWindow028(store,main);window.Show();Pump();
        try
        {
            var controls=Walk(window).ToArray();
            Check(!controls.OfType<ComboBox>().Any(),"review has no routine show or sector mapping dropdowns");
            var lists=controls.OfType<ListBox>().ToArray();var issues=lists.Single(l=>l.Name=="TicketlandIssues029");var candidates=lists.Single(l=>l.Name=="TicketlandCandidates029");
            Check(issues.Items.Count==2,"review lists unresolved proposals");
            var issue=issues.Items.Cast<TicketlandIssue029>().Single(i=>i.Source.Contains(name+"A",StringComparison.Ordinal));issues.SelectedItem=issue;
            var candidate=issue.Candidates.Single(c=>c.EventId==eventId&&c.SeatId==ids[0]);candidates.SelectedItem=candidate;Pump();
            Check(controls.OfType<TextBlock>().Any(t=>t.Text==candidate.Differences&&t.Text.Contains("название",StringComparison.Ordinal)&&t.Text.Contains("сектор",StringComparison.Ordinal)),"review displays exact field differences");
            Corrections025UiChecks.Capture(window,"029-ticketland-discrepancies");
            controls.OfType<Button>().Single(b=>Equals(b.Content,"Согласиться")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            var hall=(HallView)main.FindName("Hall");hall.SetSelection([ids[0]]);Pump();
            Check(store.Seats(eventId).Single(s=>s.Id==ids[0]).IsTicketlandSold&&hall.SelectedSeats.Single().IsTicketlandSold,"agreement persists decision and refreshes open hall");
            Check(issues.Items.Count==1,"accepted proposal disappears from review");
            controls.OfType<Button>().Single(b=>Equals(b.Content,"Отказаться")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(issues.Items.Count==0&&store.TicketlandIssues029().Count==0,"refusal removes unresolved proposal");
            main.ReapplyTicketlandMappings028();Check(store.TicketlandIssues029().Count==0&&!store.Seats(eventId).Single(s=>s.Id==ids[1]).IsTicketlandSold,"refusal survives cached report reapplication");
        }
        finally{window.Close();}
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 029 "+message);}
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject node){yield return node;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var item in Visuals(VisualTreeHelper.GetChild(node,i)))yield return item;}
}
