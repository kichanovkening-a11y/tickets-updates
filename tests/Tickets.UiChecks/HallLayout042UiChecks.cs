using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallLayout042UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS 042 "+text);}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject node)
    {yield return node;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Visuals(VisualTreeHelper.GetChild(node,i)))yield return child;}
    internal static void CheckLayout(MainWindow main)
    {
        var root=(FrameworkElement)main.Content;var workspace=(Grid)main.FindName("Workspace");
        var refresh=(Button)main.FindName("RefreshTicketlandButton029");var toggle=(Button)main.FindName("ToggleHallButton038");
        var hall=(HallView)main.FindName("Hall");
        Check(refresh.TranslatePoint(new Point(0,refresh.ActualHeight),workspace).Y<=toggle.TranslatePoint(new Point(),workspace).Y,"refresh is above expansion");
        Check(toggle.TranslatePoint(new Point(0,toggle.ActualHeight),workspace).Y<=hall.TranslatePoint(new Point(),workspace).Y,"both icons remain outside seating");
        Check(refresh.ActualWidth==toggle.ActualWidth&&refresh.ActualHeight==toggle.ActualHeight,"icons have equal square bounds");
        var print=(Button)main.FindName("PrintTicketsButton");var issue=(Button)main.FindName("IssueButton");
        var tab=(TabItem)((TabControl)main.FindName("WorkspaceTabs")).Items[0];
        Check(Math.Abs(print.TranslatePoint(new Point(),workspace).Y-tab.TranslatePoint(new Point(),workspace).Y)<3&&Math.Abs(issue.ActualHeight-tab.ActualHeight)<3,"tabs and document commands share one compact row");
        foreach(var button in new[]{print,issue,refresh,toggle})
        {
            button.BringIntoView();Pump();var enabled=button.IsEnabled;button.IsEnabled=true;
            try{var hit=root.InputHitTest(button.TranslatePoint(new Point(button.ActualWidth/2,button.ActualHeight/2),root)) as DependencyObject;Check(hit is not null&&Visuals(button).Contains(hit),"command remains reachable: "+button.Name);}
            finally{button.IsEnabled=enabled;}
        }
        Check(hall.ActualHeight>=100,$"usable hall height remains: window={main.Width}x{main.Height}, scale={root.LayoutTransform.Value.M11}, hall={hall.ActualWidth}x{hall.ActualHeight}, workspace={workspace.ActualHeight}");
        var legend=(Grid)main.FindName("LegendPanel");var sidebar=(Border)main.FindName("SelectionSidebar");
        Check(legend.IsDescendantOf(sidebar),"price plaques are in the right sidebar");
        var plaques=legend.Children.OfType<FrameworkElement>().ToArray();
        for(var i=1;i<plaques.Length;i++)Check(plaques[i].TranslatePoint(new Point(),legend).Y>=plaques[i-1].TranslatePoint(new Point(0,plaques[i-1].ActualHeight),legend).Y,"price plaques do not overlap vertically");
    }
    internal static void Run(string seed)
    {
        var work=Path.Combine(Path.GetTempPath(),"Tickets-layout042-"+Guid.NewGuid());Directory.CreateDirectory(work);
        var db=Path.Combine(work,"tickets.db");File.Copy(seed,db);var store=new Store(db);
        var main=new MainWindow(store){WindowState=WindowState.Normal,WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            main.Show();main.SelectEvent(store.Events().First().Id);Pump();
            var toggle=(Button)main.FindName("ToggleHallButton038");var root=(FrameworkElement)main.Content;
            foreach(var size in new[]{new Size(1420,880),new Size(900,600)})
            foreach(var scale in new[]{1d,1.25,1.5}.Where(s=>size.Width/s>=main.MinWidth&&size.Height/s>=main.MinHeight))
            {
                main.Width=size.Width;main.Height=size.Height;root.LayoutTransform=new ScaleTransform(scale,scale);Pump();
                for(var mode=0;mode<2;mode++)
                {
                    CheckLayout(main);
                    if(mode==1)Check(((TextBlock)main.FindName("EventSubtitle")).Visibility==Visibility.Collapsed,"expanded hall omits upper totals");
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                }
            }
            root.LayoutTransform=Transform.Identity;main.Width=1536;main.Height=800;Pump();
            var hall=(HallView)main.FindName("Hall");var prices=(Grid)main.FindName("LegendPanel");
            var plaque=prices.Children.OfType<Border>().First();var selection=hall.Selected.ToArray();
            plaque.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});Pump();
            Check(hall.FocusPrice.HasValue&&hall.Selected.SetEquals(selection),"moved price plaque filters without changing selection");
            typeof(MainWindow).GetMethod("SetPriceFocus",Private)!.Invoke(main,[null]);
            Corrections025UiChecks.Capture(main,"042-hall-normal");
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Corrections025UiChecks.Capture(main,"042-hall-expanded");
            CheckOccupiedDrawing(hall);
            // Popup placement can bring a menu back onto the user's monitor.
            // Exercise real context menus on the CI desktop only.
            if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")CheckContextCommands(store,main);
        }
        finally{main.Close();}
    }
    private static void CheckOccupiedDrawing(HallView hall)
    {
        var seat=new SeatInfo(1,"Партер","1","1",0,0,1000,"#FFFF00",null,"","",null){IsTicketlandOccupied=true};
        hall.Load([seat],[],true);Pump();
        var drawing=(DrawingGroup)typeof(HallView).GetField("drawing",Private)!.GetValue(hall)!;
        IEnumerable<GeometryDrawing> Shapes(Drawing item)
        {if(item is GeometryDrawing shape)yield return shape;if(item is DrawingGroup group)foreach(var child in group.Children)foreach(var nested in Shapes(child))yield return nested;}
        var shapes=Shapes(drawing).ToArray();
        Check(shapes.Any(s=>s.Geometry is RectangleGeometry&&s.Geometry.Bounds==new Rect(0,0,16,16)&&Equals(s.Brush,Brushes.Black))&&shapes.Count(s=>s.Geometry is LineGeometry&&Equals(s.Pen?.Brush,Brushes.Red))>=4,"externally occupied seat renders black with red hatching");
        Check(!seat.Available&&!seat.IsTicketlandSold,"occupied rendering keeps the original seat status");
    }
    private static void CheckContextCommands(Store store,MainWindow main)
    {
        var venue=store.CreateHall("Layout042 "+Guid.NewGuid(),"",Enumerable.Range(1,3).Select(n=>new DraftSeat{Sector="Партер",Row="1",Number=n.ToString(),X=n*22,Y=0}).ToArray());
        var ids=store.VenueSeats(venue).Select(s=>s.Id).ToArray();var plan=store.SavePlan(venue,"042",ids.Select(id=>new PriceValue(id,1000,"#FFFF00")).ToArray());
        var show=store.CreateEvent(store.SavePerformance("Layout042"),venue,plan,new DateTime(2030,11,1,19,0,0));
        store.SetSeatsBlocked(show,[ids[1]],true);store.Issue(show,[ids[2]],"042 recipient","042",1);
        typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);main.SelectEvent(show);Pump();
        var hall=(HallView)main.FindName("Hall");
        string[] Menu(long id)
        {
            hall.SetSelection([id]);typeof(MainWindow).GetMethod("OpenSeatContext",Private)!.Invoke(main,[id,new Point()]);Pump();
            try{return hall.ContextMenu!.Items.OfType<MenuItem>().Where(m=>m.IsEnabled).Select(m=>m.Header.ToString()!).ToArray();}
            finally{hall.ContextMenu!.IsOpen=false;}
        }
        var free=Menu(ids[0]);
        Check(new[]{"Изменить цену","Дополнить накладную","Заблокировать"}.All(label=>free.Count(x=>x==label)==1),"removed toolbar commands remain once in free-seat context menu");
        Check(Menu(ids[1]).Contains("Разблокировать"),"blocked seat keeps its unblock command");
        Check(Menu(ids[2]).Contains("Вернуть место"),"issued seat keeps its return command");
        ((ComboBox)main.FindName("RecipientBox")).Text="042 recipient";hall.SetSelection([ids[0]]);Pump();
        Check(((Button)main.FindName("PrintTicketsButton")).IsEnabled&&((Button)main.FindName("IssueButton")).IsEnabled,"compact document buttons retain eligibility for a free seat and recipient");
        hall.SetSelection([ids[2]]);Pump();
        Check(!((Button)main.FindName("PrintTicketsButton")).IsEnabled&&!((Button)main.FindName("IssueButton")).IsEnabled,"issued seats cannot be issued again through compact buttons");
    }
}
