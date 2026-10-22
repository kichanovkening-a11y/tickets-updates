using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.Core;
internal static class Merge018Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).Select(s=>s.Id).ToArray();
        var first=store.Issue(show.Id,new[]{seats[0]},"UI merge 018","UIM018",1);
        var second=store.Issue(show.Id,new[]{seats[1]},"UI merge 018","UIM018",2);
        var window=new Tickets.App.MainWindow(store);
        try
        {
            window.Show();
            typeof(Tickets.App.MainWindow).GetField("current",Private)!.SetValue(window,show);
            typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",Private)!.Invoke(window,new object[]{false});
            ((FrameworkElement)window.FindName("HomePanel")).Visibility=Visibility.Collapsed;
            ((FrameworkElement)window.FindName("Workspace")).Visibility=Visibility.Visible;
            var tabs=(TabControl)window.FindName("WorkspaceTabs");
            var journal=(TabItem)window.FindName("JournalTab");journal.IsSelected=true;Pump();
            var selection=(HashSet<long>)typeof(Tickets.App.MainWindow).GetField("journalSelection",Private)!.GetValue(window)!;
            selection.UnionWith(new[]{first,second});
            typeof(Tickets.App.MainWindow).GetMethod("LoadJournal",Private)!.Invoke(window,null);Pump();
            var grid=(DataGrid)window.FindName("HistoryGrid");
            Check(grid.Items.Cast<object>().Count(Selected)==2,"Journal checkbox state was not loaded");
            var merge=(Button)window.FindName("MergeInvoicesButton");
            Check(merge.IsEnabled&&((TextBlock)window.FindName("JournalSelectionStatus")).Text.Contains("новая накладная"),"Merge status does not explain new identity");
            tabs.SelectedIndex=0;Pump();
            Check(selection.Count==0&&grid.Items.Cast<object>().All(row=>!Selected(row)),"Leaving journal retained checked invoices");
            journal.IsSelected=true;Pump();
            Check(selection.Count==0&&merge.Visibility!=Visibility.Visible,"Returning to journal restored stale merge selection");
        }
        finally{window.Close();}
    }
    private static bool Selected(object row)=>(bool)row.GetType().GetProperty("IsSelected")!.GetValue(row)!;
    private static void Pump(){var frame=new DispatcherFrame();Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
