using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallStatus030UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var previous=typeof(MainWindow).GetField("current",Private)!.GetValue(main) as Tickets.Core.EventInfo;
        var search=(TextBox)main.FindName("SearchBox");var oldSearch=search.Text;
        var name="Статусы030 "+Guid.NewGuid().ToString("N");
        var venue=store.CreateHall(name,"",Enumerable.Range(1,6).Select(n=>new DraftSeat{Sector="Партер",Row="1",Number=n.ToString(),X=n*40,Y=40}).ToArray());
        var ids=store.VenueSeats(venue).OrderBy(s=>s.Number).Select(s=>s.Id).ToArray();
        var plan=store.SavePlan(venue,name,ids.Where((_,i)=>i!=3).Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
        var at=DateTime.Today.AddDays(54).AddSeconds(Random.Shared.Next(10000));var eventId=store.CreateEvent(store.SavePerformance(name),venue,plan,at);
        store.Reserve(eventId,[ids[0]],name,"S030",1);
        var issue=store.Issue(eventId,[ids[1]],name,"S030",2);store.MarkTicketsPrinted(eventId,store.Invoice(eventId,issue).Tickets.Select(t=>t.Id).ToArray());
        store.SetSeatsBlocked(eventId,[ids[2]],true);
        store.ApplyTicketlandReport028(new([new(name,at,"Партер","1","5",name,125.50m){Venue=name},new(name,at,"Входной","--","--",name+"GA",44.25m){Venue=name}],(store.TicketlandLastSync028??DateTimeOffset.UtcNow).AddMinutes(1)),true);
        try
        {
            search.Clear();typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);main.SelectEvent(eventId);Pump();
            CheckLayout(main);
            var hall=(HallView)main.FindName("Hall");var legend=(WrapPanel)main.FindName("StatusLegendPanel");
            hall.SetSelection([ids[5]]);var original=store.Seats(eventId).ToArray();
            Check(legend.Children.Count==5,"all five status entries are present");
            foreach(var status in Enum.GetValues<HallStatus030>())
            {
                var entry=legend.Children.OfType<FrameworkElement>().Single(e=>Equals(e.Tag,status));
                entry.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});Pump();
                Check(hall.FocusStatus030==status&&hall.FocusPrice is null,"legend activates visual focus: "+status);
                var drawing=(DrawingGroup)typeof(HallView).GetField("drawing",Private)!.GetValue(hall)!;
                foreach(var seat in original)
                {
                    var fill=drawing.Children.OfType<GeometryDrawing>().Single(d=>d.Geometry is RectangleGeometry&&d.Geometry.Bounds==new Rect(seat.X,seat.Y,16,16));
                    Check(Equals(fill.Brush,Brushes.White)==!HallFocus030.Matches(seat,status),"only matching seats retain their fill: "+status+" / "+seat.Number);
                }
                Check(hall.Selected.SetEquals([ids[5]])&&store.Seats(eventId).SequenceEqual(original),"status focus preserves selection and stored state: "+status);
                ((EventHandler)typeof(HallView).GetField("EmptySpaceClicked",Private)!.GetValue(hall)!)(hall,EventArgs.Empty);Pump();
                Check(hall.FocusStatus030 is null&&hall.FocusPrice is null,"empty hall click clears focus: "+status);
            }
            var totals=((TextBlock)main.FindName("HallTotals")).Text;
            Check(totals.Contains("Продано Ticketland: 2")&&totals.Contains(169.75m.ToString("N2")),"hall totals use cached nominal including GA, not local 500 price");
            hall.FocusPrice=500;var statusEntry=legend.Children.OfType<FrameworkElement>().First();statusEntry.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
            Check(hall.FocusPrice is null,"status focus replaces price focus");
            typeof(MainWindow).GetMethod("SetPriceFocus",Private)!.Invoke(main,new object?[]{500m});Check(hall.FocusStatus030 is null&&hall.FocusPrice==500,"price focus replaces status focus");
        }
        finally{if(previous is not null)main.SelectEvent(previous.Id);search.Text=oldSearch;Pump();}
    }
    internal static void CheckLayout(MainWindow main)
    {
        var width=main.Width;var height=main.Height;var state=main.WindowState;
        try
        {
            main.WindowState=WindowState.Normal;
            foreach(var size in new[]{new Size(1420,880),new Size(900,600)})
            {
                main.Width=size.Width;main.Height=size.Height;Pump();main.UpdateLayout();Pump();
                var toolbar=(Grid)main.FindName("HallToolbar030");var refresh=(Button)main.FindName("RefreshTicketlandButton029");
                var fit=Visuals(toolbar).OfType<Button>().Single(b=>Equals(b.Content,"Весь зал"));
                Check(Math.Abs(refresh.TranslatePoint(new Point(),toolbar).Y-fit.TranslatePoint(new Point(),toolbar).Y)<.5,"refresh shares hall toolbar baseline at "+size.Width);
                Check(Math.Abs(refresh.TranslatePoint(new Point(refresh.ActualWidth,0),toolbar).X-toolbar.ActualWidth)<.5,"refresh is against hall toolbar right edge at "+size.Width);
                Check(refresh.ActualHeight==fit.ActualHeight&&refresh.FontSize==14,"refresh fits existing hall toolbar height and ordinary typography");
                var square=(StackPanel)main.FindName("SquareShowActions");Check(square.Children.OfType<Button>().Count()==2&&square.Children.OfType<Button>().All(b=>b.Width==72&&b.Height==72),"two original square actions retain their dimensions");
                var root=(FrameworkElement)main.Content;var hit=root.InputHitTest(refresh.TranslatePoint(new Point(refresh.ActualWidth/2,refresh.ActualHeight/2),root)) as DependencyObject;
                Check(hit is not null&&Visuals(refresh).Contains(hit),"refresh remains reachable at "+size.Width);
            }
        }
        finally{main.Width=width;main.Height=height;main.WindowState=state;Pump();}
    }
    private static IEnumerable<DependencyObject> Visuals(DependencyObject node){yield return node;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Visuals(VisualTreeHelper.GetChild(node,i)))yield return child;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 030 "+message);}
}
