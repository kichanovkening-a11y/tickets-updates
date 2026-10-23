using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class LegacyHallGrid022Checks
{
    internal static void Run(Store store)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.LegacyHallGridWindow")!;
        var window=(Window)Activator.CreateInstance(type,[store,"Партер"])!;var venuesBefore=store.Venues().Count;Exception? error=null;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try{
                var fields=Logical(window).OfType<TextBox>().ToArray();fields[1].Text="3";fields[2].Text="5";
                Click(window,"Создать сетку");Pump();
                Check(!Logical(window).OfType<DataGrid>().Any(),"legacy hall contains no upper table");
                var preview=(HallView)type.GetField("preview",flags)!.GetValue(window)!;
                var seats=(List<SeatInfo>)typeof(HallView).GetField("seats",flags)!.GetValue(preview)!;
                Check(seats.Count==15,"legacy scheme contains generated places");
                preview.SetSelection(new long[]{2,3,7,8});
                var callback=typeof(HallView).GetField("SeatContextRequested",flags)!.GetValue(preview) as Action<long,Point>;callback!(2,new Point());
                preview.ContextMenu!.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Сделать проход")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();
                seats=(List<SeatInfo>)typeof(HallView).GetField("seats",flags)!.GetValue(preview)!;
                Check(seats.Count==11,"scheme passage removes selected places without table");Click(window,"Принять места");
            }catch(Exception ex){error=ex;window.Close();}
        }));
        try{var accepted=(bool?)type.BaseType!.GetMethod("ShowSeparateDialog",flags)!.Invoke(window,null);if(error is not null)throw error;
            Check(accepted==true&&((IReadOnlyList<DraftSeat>)type.GetProperty("Result")!.GetValue(window)!).Count==11,"legacy acceptance returns edited scheme");
            Check(store.Venues().Count==venuesBefore,"legacy acceptance does not save a hall by itself");
        }finally{window.Close();Pump();}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Click(Window window,string text)=>Logical(window).OfType<Button>().Single(b=>Equals(b.Content,text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
