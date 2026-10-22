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
        var window=(Window)Activator.CreateInstance(type,[store,"Партер"])!;
        var venuesBefore=store.Venues().Count;
        Exception? error=null;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try
            {
                ((TextBox)type.GetField("rows",flags)!.GetValue(window)!).Text="3";
                ((TextBox)type.GetField("places",flags)!.GetValue(window)!).Text="5";
                Click(window,"Создать сетку");Pump();
                var table=(DataGrid)type.GetField("table",flags)!.GetValue(window)!;
                Check(!table.CanUserReorderColumns,"legacy grid keeps geometric columns locked to their classification indices");
                Check(table.SelectionUnit==DataGridSelectionUnit.Cell&&table.SelectionMode==DataGridSelectionMode.Extended,"legacy draft supports rectangular cell selection");
                table.SelectedCells.Clear();
                for(var r=0;r<=1;r++)for(var c=2;c<=3;c++)table.SelectedCells.Add(new DataGridCellInfo(table.Items[r],table.Columns[c]));
                table.ContextMenu!.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Установить проход")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Click(window,"Применить");Pump();
                var preview=(HallView)type.GetField("preview",flags)!.GetValue(window)!;
                var seats=(List<SeatInfo>)typeof(HallView).GetField("seats",flags)!.GetValue(preview)!;
                Check(seats.Count==11&&seats.All(s=>s.X is >=0 and <=80),"rectangle classification removes exactly four cells without phantom caption seats");
                Check(((IReadOnlyList<DraftSeat>)type.GetProperty("Result")!.GetValue(window)!).Count==0&&store.Venues().Count==venuesBefore,"Apply previews without accepting or saving a hall");
                Click(window,"Принять места");
            }
            catch(Exception ex){error=ex;window.Close();}
        }));
        try
        {
            var accepted=(bool?)type.BaseType!.GetMethod("ShowSeparateDialog",flags)!.Invoke(window,null);
            if(error is not null)throw error;
            Check(accepted==true&&((IReadOnlyList<DraftSeat>)type.GetProperty("Result")!.GetValue(window)!).Count==11,"explicit acceptance returns only compiled seats");
            Check(store.Venues().Count==venuesBefore,"acceptance leaves database persistence to parent hall editor");
        }
        finally{window.Close();Pump();}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject node)
    {
        yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;
    }
    private static void Click(Window window,string text)=>Logical(window).OfType<Button>().Single(b=>Equals(b.Content,text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
