using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.Core;

internal static class Journal017Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=4);
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(4).Select(s=>s.Id).ToArray();
        store.SaveDocumentSettings("ООО Журнал UI017",store.DocumentsDirectory);
        var first=store.Issue(show.Id,new[]{seats[0]},"Получатель UI017","JUI017",1);
        var second=store.Issue(show.Id,new[]{seats[1]},"Получатель UI017","JUI017",2);
        var otherRecipient=store.Issue(show.Id,new[]{seats[2]},"Получатель UI017 другой","JUI017",3);
        store.SaveDocumentSettings("ООО Журнал UI017 другой",store.DocumentsDirectory);
        var otherSender=store.Issue(show.Id,new[]{seats[3]},"Получатель UI017","JUI017",4);
        var main=new Tickets.App.MainWindow(store);
        try
        {
            main.Show();
            typeof(Tickets.App.MainWindow).GetField("current",Private)!.SetValue(main,show);
            typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",Private)!.Invoke(main,new object[]{false});
            var selection=(HashSet<long>)typeof(Tickets.App.MainWindow).GetField("journalSelection",Private)!.GetValue(main)!;
            var grid=(DataGrid)main.FindName("HistoryGrid");
            var detailColumns=grid.Columns.OfType<DataGridTextColumn>().Where(column=>column.Binding is System.Windows.Data.Binding binding&&binding.Path.Path is "ReturnedCount" or "Status").ToArray();
            Check(detailColumns.Length==2&&detailColumns.All(column=>column.Visibility==Visibility.Collapsed),"Returned count and status are hidden by default while remaining available as columns");
            Check(grid.Columns.Count(column=>column.Visibility==Visibility.Visible)==7,"Default journal contains six main columns and the selection checkbox");
            var merge=(Button)main.FindName("MergeInvoicesButton");
            var status=(TextBlock)main.FindName("JournalSelectionStatus");
            void Select(params long[] ids){selection.Clear();selection.UnionWith(ids);typeof(Tickets.App.MainWindow).GetMethod("UpdateJournalSelection",Private)!.Invoke(main,null);}
            void Click()=>typeof(Tickets.App.MainWindow).GetMethod("MergeInvoicesClick",Private)!.Invoke(main,new object[]{merge,new RoutedEventArgs()});
            Select(first,otherRecipient);
            Check(!merge.IsEnabled&&status.Text.Contains("получателя"),"Different recipients immediately disable inline merge with a reason");
            Click();Check(store.ResolveInvoiceId(otherRecipient)==otherRecipient,"Invalid recipient selection cannot merge via handler");
            Select(first,otherSender);
            Check(!merge.IsEnabled&&status.Text.Contains("отправителя"),"Different full sender names are rejected even with the same legal form");
            Select(first,long.MaxValue);
            Check(!merge.IsEnabled&&status.Text.Contains("мероприятия"),"Missing or cross-event selection immediately requests one event");
            Select(second,first);
            Check(merge.IsEnabled&&merge.Content.ToString()!.Contains($"№ {first}"),"Inline merge explicitly targets the oldest selected invoice regardless of click order");
            ConfirmMerge(Click,false);
            Check(store.ResolveInvoiceId(second)==second&&selection.Count==2,"Declining confirmation preserves both invoices and checked rows");
            ConfirmMerge(Click,true);
            Check(store.ResolveInvoiceId(second)==first&&selection.Count==0,"Accepting inline confirmation merges the checked invoices and clears selection");
            Check(status.Text.Contains($"№ {first}")&&status.Text.Contains("Документы обновлены"),"Successful inline merge refreshes documents and reports target invoice");
            Check(!Application.Current.Windows.Cast<Window>().Any(w=>w.GetType().Name=="InvoiceWindow"||w.GetType().Name=="InvoiceMergeWindow"),"Inline merge never opens an invoice or selection window");
        }
        finally{main.Close();}
    }
    private static void ConfirmMerge(Action click,bool accept)
    {
        var confirmed=false;var extraWindow=false;
        var timer=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(50)};
        timer.Tick+=(_,_)=>{
            var chooser=Application.Current.Windows.Cast<Window>().FirstOrDefault(w=>w.GetType().Name=="InvoiceMergeWindow");
            if(chooser is not null){extraWindow=true;timer.Stop();chooser.Close();return;}
            EnumWindows((window,_)=>{
                GetWindowThreadProcessId(window,out var process);if(process!=(uint)Environment.ProcessId)return true;
                var title=new StringBuilder(256);GetWindowText(window,title,title.Capacity);
                if(title.ToString()!="Объединение накладных")return true;
                confirmed=true;timer.Stop();PostMessage(window,0x111,new IntPtr(accept?6:7),IntPtr.Zero);return false;
            },IntPtr.Zero);
        };
        timer.Start();try{click();}finally{timer.Stop();}
        Check(confirmed&&!extraWindow,"Inline merge goes directly to the confirmation dialog");
    }
    private delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr window,StringBuilder text,int capacity);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
