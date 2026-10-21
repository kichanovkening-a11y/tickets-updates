using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.Core;

internal static class Journal016Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=4);
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(4).Select(s=>s.Id).ToArray();
        store.SaveDocumentSettings("ООО Журнал UI",store.DocumentsDirectory);
        var first=store.Issue(show.Id,new[]{seats[0]},"Оля UI016","JUI016",1);
        store.SaveDocumentSettings("ИП Журнал UI",store.DocumentsDirectory);
        var second=store.Issue(show.Id,new[]{seats[1]},"Оля UI016","JUI016",2);
        var unrelated=store.Issue(show.Id,new[]{seats[2]},"Оля UI016 Пупкин","JUI016",3);
        var main=new Tickets.App.MainWindow(store){Width=1280,Height=820,WindowState=WindowState.Normal};
        try
        {
            main.Show();
            typeof(Tickets.App.MainWindow).GetField("current",Private)!.SetValue(main,show);
            typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",Private)!.Invoke(main,new object[]{false});
            ((FrameworkElement)main.FindName("HomePanel")).Visibility=Visibility.Collapsed;
            ((FrameworkElement)main.FindName("Workspace")).Visibility=Visibility.Visible;
            var grid=(DataGrid)main.FindName("HistoryGrid");
            var journal=Ancestors(grid).OfType<TabItem>().First();journal.IsSelected=true;Pump();
            var sender=grid.Columns.OfType<DataGridTextColumn>().Single(c=>Equals(c.Header,"От"));
            string Sender(long id){var row=grid.Items.Cast<object>().Single(item=>(long)item.GetType().GetProperty("Id")!.GetValue(item)! ==id);return (string)row.GetType().GetProperty("SenderKind")!.GetValue(row)!;}
            Check(Sender(first)=="ООО"&&Sender(second)=="ИП","Journal shows each historical sender's legal form only");
            var recipient=grid.Columns.Single(c=>Equals(c.Header,"Получатель"));recipient.Width=new DataGridLength(237);
            var before=grid.TransformToAncestor(main).Transform(new Point());
            var selection=(HashSet<long>)typeof(Tickets.App.MainWindow).GetField("journalSelection",Private)!.GetValue(main)!;
            selection.Add(first);selection.Add(second);typeof(Tickets.App.MainWindow).GetMethod("UpdateJournalSelection",Private)!.Invoke(main,null);Pump();
            var after=grid.TransformToAncestor(main).Transform(new Point());
            Check(Math.Abs(before.Y-after.Y)<.1&&Math.Abs(before.X-after.X)<.1,"Second checkbox does not move journal rows");
            Check(((Button)main.FindName("MergeInvoicesButton")).Visibility==Visibility.Visible,"Merge appears for two selected invoices");
            selection.Remove(second);typeof(Tickets.App.MainWindow).GetMethod("UpdateJournalSelection",Private)!.Invoke(main,null);Pump();
            Check(((Button)main.FindName("MergeInvoicesButton")).Visibility!=Visibility.Visible,"Merge hides below two selected invoices");
            Check(Math.Abs(recipient.ActualWidth-237)<1,"Selection preserves user column width");
            var path=Path.Combine(Path.GetDirectoryName(store.Path)!,"journal-columns.json");
            var timeout=DateTime.UtcNow.AddSeconds(2);
            while((!File.Exists(path)||!File.ReadAllText(path).Contains("\"Width\":237"))&&DateTime.UtcNow<timeout){Pump();Thread.Sleep(10);}
            Check(File.Exists(path)&&File.ReadAllText(path).Contains("\"Width\":237"),"Column width is saved before closing the application");
            var otherShow=store.Events().First(e=>e.Id!=show.Id);
            typeof(Tickets.App.MainWindow).GetField("current",Private)!.SetValue(main,otherShow);
            typeof(Tickets.App.MainWindow).GetMethod("LoadJournal",Private)!.Invoke(main,null);Pump();
            Check(Math.Abs(recipient.Width.Value-237)<.1,"Column width is shared across shows");
            var reopened=new Tickets.App.MainWindow(new Store(store.Path));
            try{var columns=((DataGrid)reopened.FindName("HistoryGrid")).Columns;Check(Math.Abs(columns.Single(c=>Equals(c.Header,"Получатель")).Width.Value-237)<.1,"Column width survives opening another window");}finally{reopened.Close();}
            var choiceType=typeof(Tickets.App.MainWindow).Assembly.GetType("Tickets.App.InvoiceChoiceWindow")!;
            var choice=(Window)Activator.CreateInstance(choiceType,Private,null,new object[]{store,show.Id,"Оля UI016"},null)!;
            try
            {
                var list=Logical(choice).OfType<ListBox>().Single();
                var ids=list.Items.Cast<object>().Select(item=>(long)item.GetType().GetProperty("Id")!.GetValue(item)!).ToArray();
                Check(ids.Contains(first)&&ids.Contains(second)&&!ids.Contains(unrelated),"Extension chooser offers only the exact selected recipient");
            }
            finally{choice.Close();}
        }
        finally{main.Close();}
    }
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject child){for(var node=LogicalTreeHelper.GetParent(child);node is not null;node=LogicalTreeHelper.GetParent(node))yield return node;}
    private static IEnumerable<DependencyObject> Logical(DependencyObject parent){yield return parent;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var descendant in Logical(child))yield return descendant;}
    private static void Pump(){var frame=new DispatcherFrame();Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
