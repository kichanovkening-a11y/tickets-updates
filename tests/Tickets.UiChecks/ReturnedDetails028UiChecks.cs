using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class ReturnedDetails028UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var venue=store.CreateHall("UI028 returns "+Guid.NewGuid(),"",new[]{new DraftSeat{Sector="Партер",Row="1",Number="1",X=0,Y=0}});
        var seat=store.VenueSeats(venue).Single().Id;
        var plan=store.SavePlan(venue,"UI028 returns",new[]{new PriceValue(seat,500,"#FF0000")});
        var evt=store.CreateEvent(store.SavePerformance("UI028 returns"),venue,plan,DateTime.Today.AddDays(10));
        var recipient="UI028 returns "+Guid.NewGuid();store.SaveOrganizationDetails(recipient,false,new(Name:recipient));
        var issue=store.Issue(evt,new[]{seat},recipient,"UIRET028",1);store.Return(new[]{store.History(evt).Single(t=>t.IssueId==issue).Id});
        typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);
        typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);main.SelectEvent(evt);Pump();
        var tabs=(TabControl)main.FindName("WorkspaceTabs");var previous=tabs.SelectedIndex;tabs.SelectedIndex=1;Pump();
        try
        {
            var history=(DataGrid)main.FindName("HistoryGrid");history.UpdateLayout();Pump();
            var toggle=Visuals(history).OfType<TextBlock>().Single(t=>t.Text=="Возврат: 1");Click(toggle);Pump();
            var row=(DataGridRow)history.ItemContainerGenerator.ContainerFromIndex(0);
            var table=Visuals(row).OfType<DataGrid>().Single();var windows=Application.Current.Windows.Count;
            Click(table);Pump();Check(row.DetailsVisibility==Visibility.Visible&&Application.Current.Windows.Count==windows,"journal table click retains details and opens no invoice");
            Click(history);Pump();Check(row.DetailsVisibility==Visibility.Collapsed&&Application.Current.Windows.Count==windows,"journal empty click closes details without invoice");
            Click(toggle);Pump();Check(row.DetailsVisibility==Visibility.Visible,"journal count reopens details after empty click");
            Click(toggle);Pump();Check(row.DetailsVisibility==Visibility.Collapsed,"journal count still closes details");
        }
        finally{tabs.SelectedIndex=previous;Pump();}
        var window=new RecipientsWindow(store);window.Show();Pump();
        try
        {
            var organizations=Logical(window).OfType<ListBox>().Single();organizations.SelectedItem=organizations.Items.OfType<RecipientDirectoryInfo>().Single(r=>r.Name==recipient);Pump();
            var history=Logical(window).OfType<TreeView>().Single();var toggle=Logical(window).OfType<TextBlock>().Single(t=>t.Text=="Возврат: 1");Click(toggle);Pump();
            var table=Logical(window).OfType<DataGrid>().Single();var windows=Application.Current.Windows.Count;
            Click(table);Pump();Check(table.Visibility==Visibility.Visible&&Application.Current.Windows.Count==windows,"recipient table click retains details and opens no invoice");
            Click(history);Pump();Check(table.Visibility==Visibility.Collapsed&&Application.Current.Windows.Count==windows,"recipient empty click closes details without invoice");
            Click(toggle);Pump();Check(table.Visibility==Visibility.Visible,"recipient count reopens details after empty click");
            Click(toggle);Pump();Check(table.Visibility==Visibility.Collapsed,"recipient count still closes details");
        }
        finally{window.Close();}
    }
    private static void Click(UIElement element)=>element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS 028 "+message);}
    private static IEnumerable<DependencyObject> Logical(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root){yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Visuals(VisualTreeHelper.GetChild(root,i)))yield return item;}
}
