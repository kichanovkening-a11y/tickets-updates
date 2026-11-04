using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class PriceFocus049UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool ok,string message){if(!ok)throw new Exception("049 "+message);Console.WriteLine("PASS 049 "+message);}
    private static void Click(UIElement element)
    {
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseUpEvent});Pump();
    }
    private static void PumpInput(int milliseconds=55)
    {var until=DateTime.UtcNow.AddMilliseconds(milliseconds);while(DateTime.UtcNow<until){Pump();Thread.Sleep(5);}}
    private static void NativeClick()
    {mouse_event(2,0,0,0,UIntPtr.Zero);PumpInput();mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();}
    private static void NativeDoubleClick()
    {
        // Queue both clicks before pumping WPF: rendering and CI console I/O
        // must not stretch the native interval between the button-down events.
        mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);
        mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();
    }
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    [DllImport("user32.dll")]private static extern uint GetDoubleClickTime();
    internal static void Run(string seed)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Tickets-price049-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        var db=Path.Combine(folder,"tickets.db");File.Copy(seed,db);var store=new Store(db);
        var main=new MainWindow(store){WindowState=WindowState.Normal,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        main.Show();var ev=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Amount.HasValue&&!s.IsBlocked));main.SelectEvent(ev.Id);Pump();
        try
        {
            var hall=(HallView)main.FindName("Hall");var legend=(Grid)main.FindName("LegendPanel");
            var seats=store.Seats(ev.Id).Where(s=>s.Amount.HasValue&&!s.IsBlocked).ToArray();hall.SetSelection([seats[0].Id]);
            hall.TogglePriceFocus(seats[0].Amount!.Value);Pump();
            Check(legend.Children.OfType<Border>().Any(p=>p.BorderBrush==Brushes.Black),"price focus paints its active plaque");
            var recipient=(ComboBox)main.FindName("RecipientBox");
            Click(recipient);
            Check(hall.FocusPrices.Count==1,"input interaction keeps price focus");
            // Complete the control interaction before testing a click on the hall.
            // A ComboBox popup consumes the first outside native click to dismiss itself.
            recipient.IsDropDownOpen=false;Pump();
            Click((Border)main.FindName("SelectionSidebar"));
            Check(hall.FocusPrices.Count==0&&hall.Selected.SetEquals([seats[0].Id]),"blank sidebar click clears prices and preserves selected seats");
            Check(legend.Children.OfType<Border>().All(p=>p.BorderBrush==Brushes.Gray),"blank click removes every active price border");
            hall.TogglePriceFocus(seats[0].Amount!.Value);
            var blank=new Border{Background=Brushes.White};
            var separate=new Window{Content=blank,Width=300,Height=200,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
            separate.Show();Pump();
            try
            {
                Click(blank);
                Check(hall.FocusPrices.Count==0&&hall.Selected.SetEquals([seats[0].Id]),"blank click in a separate window without a hall clears main price focus and preserves seats");
                Check(legend.Children.OfType<Border>().All(p=>p.BorderBrush==Brushes.Gray),"separate-window click updates main price plaque borders");
            }
            finally{separate.Close();}
            hall.TogglePriceFocus(seats[0].Amount!.Value);
            var tabs=(TabControl)main.FindName("WorkspaceTabs");var originalTab=tabs.SelectedItem;
            tabs.SelectedItem=main.FindName("JournalTab");Pump();
            Check(hall.FocusPrices.Count==1,"switching to journal preserves price focus until an empty click");
            Click((Grid)main.FindName("JournalHeader"));
            Check(hall.FocusPrices.Count==0&&hall.Selected.SetEquals([seats[0].Id]),"blank journal click clears unloaded hall price focus and preserves seats");
            tabs.SelectedItem=originalTab;Pump();
            main.Left=0;main.Top=0;main.Activate();PumpInput();
            var hit=typeof(HallView).GetMethod("Hit",Private)!;
            var empty=(from y in Enumerable.Range(1,Math.Max(1,(int)hall.ActualHeight/20-1))
                from x in Enumerable.Range(1,Math.Max(1,(int)hall.ActualWidth/20-1))
                let point=new Point(x*20,y*20)
                where hit.Invoke(hall,[point]) is null select point).First();
            var screen=hall.PointToScreen(empty);
            Check(SetCursorPos((int)screen.X,(int)screen.Y),"native cursor moves to an empty visible hall point");PumpInput();
            hall.TogglePriceFocus(seats[0].Amount!.Value);
            var clicks=new List<int>();
            string State()=> $"focus=[{string.Join(',',hall.FocusPrices)}] selected=[{string.Join(',',hall.Selected)}] expected={seats[0].Id} mouse={Mouse.GetPosition(hall)} hit={hit.Invoke(hall,[Mouse.GetPosition(hall)])} inputHit={main.InputHitTest(Mouse.GetPosition(main))?.GetType().Name} captured={Mouse.Captured?.GetType().Name} active={main.IsActive} loaded={hall.IsLoaded} visible={hall.IsVisible} dropdown={recipient.IsDropDownOpen} clicks=[{string.Join(',',clicks)}]";
            void CheckNative(bool ok,string message)
            {if(!ok)throw new Exception("049 "+message+"; "+State());Check(true,message);}
            MouseButtonEventHandler track=(_,e)=>clicks.Add(e.ClickCount);
            CheckNative(Mouse.Captured is null&&!recipient.IsDropDownOpen,"native hall click starts without an open dropdown or prior mouse capture");
            CheckNative(ReferenceEquals(main.InputHitTest(Mouse.GetPosition(main)),hall)&&hit.Invoke(hall,[Mouse.GetPosition(hall)]) is null,"native cursor hit-tests to empty hall background");
            hall.AddHandler(Mouse.MouseDownEvent,track,true);
            try
            {
                NativeClick();
                CheckNative(hall.FocusPrices.Count==0&&hall.Selected.SetEquals([seats[0].Id]),"blank hall click clears price focus without clearing selected seats");
                PumpInput((int)GetDoubleClickTime()+40);
                hall.SetSelection([seats[0].Id]);hall.FocusPrice=seats[0].Amount!.Value;Pump();clicks.Clear();
                NativeDoubleClick();
                CheckNative(clicks.Contains(2),"native second click is delivered as a double-click");
                CheckNative(hall.FocusPrices.Count==0&&hall.Selected.SetEquals([seats[0].Id]),"second click of price-dismissal double-click preserves selected seats");
                PumpInput((int)GetDoubleClickTime()+40);
                NativeClick();
                CheckNative(hall.Selected.Count==0,"blank hall click without price focus retains the existing selection-clear behavior");
            }
            finally{hall.RemoveHandler(Mouse.MouseDownEvent,track);}
            foreach(var cell in legend.Children.OfType<Border>())
            {
                var label=(CompactText043)cell.Child;Check(label.FontSize==13.44,"price font grows by two points");
                var baseline=new CompactText043{Text=label.Text,FontSize=11.44,FontWeight=label.FontWeight,FontFamily=label.FontFamily,TextWrapping=label.TextWrapping};
                baseline.Measure(new Size(cell.ActualWidth-10,double.PositiveInfinity));
                Check(cell.ActualHeight+0.6>=baseline.DesiredSize.Height+15,"price plaque gains five pixels over previous text and insets");
            }
        }
        finally{main.Close();}
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.PriceEditorWindow")!;
        var venue=store.Venues().First(v=>store.Plans(v.Id).Count>0);
        var plan=store.Plans(venue.Id).First();
        var editor=(Window)Activator.CreateInstance(type,[store,(long?)venue.Id,(long?)plan.Id])!;
        editor.Left=-10000;editor.Top=-10000;editor.Show();Pump();
        try
        {
            var hall=(HallView)type.GetField("preview",Private)!.GetValue(editor)!;
            var legend=(Grid)type.GetField("priceSummary",Private)!.GetValue(editor)!;
            hall.SetSelection([store.VenueSeats(venue.Id).First().Id]);
            var plaque=legend.Children.OfType<Border>().First();
            plaque.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});Pump();
            Check(hall.FocusPrices.Count==1&&plaque.BorderBrush==Brushes.Black,"editor price click highlights plaque and seats");
            var selection=hall.Selected.ToArray();Click((UIElement)editor.Content);
            Check(hall.FocusPrices.Count==0&&hall.Selected.SetEquals(selection)&&plaque.BorderBrush==Brushes.Gray,"blank editor area clears price focus and keeps seat selection");
        }
        finally{editor.Close();}
    }
}
