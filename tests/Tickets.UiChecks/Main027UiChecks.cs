using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Main027UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var previousEvent=typeof(MainWindow).GetField("current",Private)!.GetValue(main) as Tickets.Core.EventInfo;
        var venue=store.CreateHall("UI027 main "+Guid.NewGuid(),"",new[]{new DraftSeat{Sector="Партер",Row="7",Number="1",X=0,Y=0},new DraftSeat{Sector="Партер",Row="7",Number="2",X=40,Y=0},new DraftSeat{Sector="Партер",Row="7",Number="3",X=80,Y=0}});
        var ids=store.VenueSeats(venue).Select(s=>s.Id).ToArray();var plan=store.SavePlan(venue,"UI027 plan",ids.Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
        var eventId=store.CreateEvent(store.SavePerformance("UI027 main event"),venue,plan,DateTime.Today.AddDays(9));
        typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);main.SelectEvent(eventId);Pump();
        var search=(TextBox)main.FindName("SearchBox");var brand=(TextBlock)main.FindName("AppBrand");
        Check(ReferenceEquals(search.Parent,brand.Parent),"search is next to brand in top band");
        Check(!Walk((DependencyObject)main.FindName("HomePanel")).OfType<Button>().Any(),"home has no duplicate creation or search commands");
        var root=(FrameworkElement)main.Content;var transform=root.LayoutTransform;
        var width=main.Width;var height=main.Height;main.Width=1420;main.Height=880;
        try{foreach(var scale in new[]{1d,1.25,1.5}){
            root.LayoutTransform=new ScaleTransform(scale,scale);Pump();main.UpdateLayout();Pump();
            var normal=(Button)main.FindName("ExtendInvoiceButton");var print=(Button)main.FindName("PrintTicketsButton");var issue=(Button)main.FindName("IssueButton");var tabs=(TabControl)main.FindName("WorkspaceTabs");
            var top=normal.TranslatePoint(new Point(),main).Y;
            Check(Math.Abs(print.TranslatePoint(new Point(),main).Y-top)<.5&&Math.Abs(issue.TranslatePoint(new Point(),main).Y-top)<.5,"square commands align to ordinary commands at "+scale);
            var bottom=normal.TranslatePoint(new Point(0,normal.ActualHeight),main).Y;
            var tabTop=tabs.TranslatePoint(new Point(),main).Y;
            Check(tabTop>=bottom-1&&tabTop-bottom<=(7+(((ScrollViewer)main.FindName("OrdinaryActionsScroll")).ComputedHorizontalScrollBarVisibility==Visibility.Visible?SystemParameters.HorizontalScrollBarHeight:0))*scale,"hall and journal tabs sit immediately below ordinary commands at "+scale);
            foreach(var button in new[]{print,issue}){button.BringIntoView();Pump();var enabled=button.IsEnabled;button.IsEnabled=true;try{var point=button.TranslatePoint(new Point(36,36),root);var hit=root.InputHitTest(point) as DependencyObject;Check(hit is not null&&Visuals(button).Contains(hit),"square command remains hit testable at "+scale);}finally{button.IsEnabled=enabled;}}
            if(scale==1)Corrections025UiChecks.Capture(main,"027-main-layout");
        }}finally{root.LayoutTransform=transform;main.Width=width;main.Height=height;Pump();}
        var ticketIssue=store.Issue(eventId,[ids[0]],"UI027 journal","R027",1);store.Return([store.History(eventId).Single(t=>t.IssueId==ticketIssue).Id]);
        typeof(MainWindow).GetMethod("LoadJournal",Private)!.Invoke(main,null);var workspace=(TabControl)main.FindName("WorkspaceTabs");var originalTab=workspace.SelectedIndex;workspace.SelectedIndex=1;Pump();
        try{
            var history=(DataGrid)main.FindName("HistoryGrid");history.UpdateLayout();Pump();
            var toggle=Visuals(history).OfType<TextBlock>().Single(t=>t.Text=="Возврат: 1");var windows=Application.Current.Windows.Count;
            toggle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});Pump();
            var row=Visuals(history).OfType<DataGridRow>().Single(r=>ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(r),history)&&r.Item.GetType().GetProperty("Id")!.GetValue(r.Item) is long n&&n==ticketIssue);
            Check(row.DetailsVisibility==Visibility.Visible&&Visuals(row).OfType<DataGrid>().Any(g=>g.Items.Count==1&&g.Columns.Count==6),"journal return count expands actual returned places inline");
            Check(Application.Current.Windows.Count==windows,"journal return click opens no extra window");
            var details=Visuals(row).OfType<DataGrid>().Single(g=>g.Columns.Count==6);
            Check(ReturnedTicketDetails027.IsToggleSource(details),"nested return detail cells are excluded from issuance opening");
            details.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});Pump();
            Check(Application.Current.Windows.Count==windows,"clicking inside return details opens no issuance");
            toggle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});Pump();Check(row.DetailsVisibility==Visibility.Collapsed,"journal return details toggle closed");
        }finally{workspace.SelectedIndex=originalTab;Pump();}
        HallGestures();
        if(previousEvent is not null){main.SelectEvent(previousEvent.Id);Pump();}
    }
    private static void HallGestures()
    {
        var hall=new HallView{SectorEditingEnabled=true,AllowUnavailableSelection=true};var window=new Window{Content=hall,Width=640,Height=440};window.Show();Pump();window.Activate();
        var seats=new[]{0d,20,60,80,130,150}.Select((x,i)=>new SeatInfo(i+1,"Партер","7",(i+1).ToString(),x,0,0,"#ABCDEF",null,"","",null)).Append(new SeatInfo(7,"Партер","8","1",0,35,0,"#ABCDEF",null,"","",null)).ToArray();
        try{
            hall.Load(seats,[new SectorInfo("Партер",0,-26)],true);Pump();hall.SetSelection([1,2]);
            Move(hall,new Point(hall.ActualWidth-10,hall.ActualHeight-10));Click();Check(hall.Selected.Count==0,"empty hall click clears all selected places");
            var labels=(System.Collections.ICollection)typeof(HallView).GetField("rowHits",Private)!.GetValue(hall)!;Check(labels.Count==6,"both edge numbers and every aisle number are drawn");
            var captionMethod=typeof(HallView).GetMethod("SectorCaptionOrigin",BindingFlags.Static|BindingFlags.NonPublic)!;
            var point=(Point)captionMethod.Invoke(null,[new Rect(0,0,40,15),seats,seats,Array.Empty<Rect>(),new Point()])!;Check(point.Y+15==-5,"sector title is exactly five units above first row");
            var obstacle=new SeatInfo(99,"Другой","1","1",75,-20,0,"#ABCDEF",null,"","",null);
            var clear=(Point)captionMethod.Invoke(null,[new Rect(0,0,40,15),seats,seats.Append(obstacle).ToArray(),Array.Empty<Rect>(),new Point()])!;
            var ink=new Rect(clear,new Size(40,15));Check(clear.Y+15==-5&&!ink.IntersectsWith(new Rect(obstacle.X,obstacle.Y,16,16)),"dense hall caption keeps five pixel gap without covering a neighboring sector");
            Move(hall,new Point(200,150));hall.EditRow("Партер","7");Pump();IReadOnlyList<SeatInfo>? changed=null;hall.RowTransformCompleted+=value=>changed=value;
            Move(hall,new Point(240,210));Click();
            Console.WriteLine("027 row move result: "+(changed is null?"no completion":string.Join("; ",changed.Select(s=>$"{s.Id}: X={s.X:R} Y={s.Y:R} dx={s.X-seats[(int)s.Id-1].X:R}"))));
            Check(changed is {Count:6}&&changed.All(s=>s.Y==0)&&changed.All(s=>Math.Abs(s.X-seats[(int)s.Id-1].X-changed[0].X)<.00001)&&changed[0].X>0,"row move changes only horizontal coordinates of its own places");
            changed=null;Move(hall,new Point(200,150));hall.EditRow("Партер","7");Move(hall,new Point(250,220));hall.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(hall),0,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent});Pump();Check(changed is null,"Escape cancels pending row move");
            Corrections025UiChecks.Capture(window,"027-hall-caption-and-aisles");
            var fixedAnchor=new HallEditing025.RowAnchor(108,8,true);
            hall.Load(new[]{120d,140,160}.Select((x,i)=>new SeatInfo(i+1,"Партер","7",(i+1).ToString(),x,0,0,"#ABCDEF",null,"","",null){RowAisleAnchors=new[]{fixedAnchor}}).ToArray(),[new SectorInfo("Партер",120,-26)],true);Pump();
            var targets=(List<(Rect Bounds,long Id)>)typeof(HallView).GetField("rowHits",Private)!.GetValue(hall)!;
            Check(targets.Count==3&&Math.Abs(targets[1].Bounds.X+targets[1].Bounds.Width/2-fixedAnchor.X)<.001,"aisle label stays fixed after deleting the whole block on one side");
            Check(!targets[0].Bounds.IntersectsWith(targets[1].Bounds),"outer row number stays clear of the fixed aisle number after edge deletion");
        }finally{window.Close();}
    }
    private static void Move(FrameworkElement target,Point point){var screen=target.PointToScreen(point);SetCursorPos((int)screen.X,(int)screen.Y);PumpInput();}
    private static void Click(){mouse_event(2,0,0,0,UIntPtr.Zero);PumpInput();mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();}
    private static void PumpInput(){var until=DateTime.UtcNow.AddMilliseconds(65);while(DateTime.UtcNow<until){Pump();Thread.Sleep(5);}}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS 027 "+text);}
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var nested in Walk(child))yield return nested;}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject node){yield return node;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Visuals(VisualTreeHelper.GetChild(node,i)))yield return child;}
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
}
