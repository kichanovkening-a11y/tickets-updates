using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Transfer033UiChecks
{
    internal static void Run(Store store,MainWindow main)
    {
        var settings=new SettingsWindow(store,main);settings.Show();Pump();
        Check(Walk(settings).OfType<Button>().Count(b=>Equals(b.Content,"Импорт"))==1,"one Import entry in settings");
        Check(Walk(settings).OfType<Button>().Count(b=>Equals(b.Content,"Экспорт"))==1,"one Export entry in settings");settings.Close();
        var export=new TransferWindow033(store);export.Show();Pump();
        var combos=Walk(export).OfType<ComboBox>().ToArray();Check(combos.Length==2,"export has type and item selectors");
        Check(combos[0].Items.Count==4,"all supported transfer types are available");
        Check(combos[1].Items.Count>0,"hall export choices loaded");export.Close();
        var hall=store.Venues().First();var before=store.HallDraft(hall.Id);var bytes=store.ExportTransfer033("hall",hall.Id.ToString());
        var import=new TransferImportWindow033(store,bytes);import.Show();Pump();
        Check(Walk(import).OfType<TextBlock>().Any(t=>t.Text=="Зал: "+hall.Name),"import identifies contents before saving");
        Check(Walk(import).OfType<Button>().Any(b=>Equals(b.Content,"Добавить копию")),"duplicate hall explicitly offers a new copy");
        Check(Walk(import).OfType<TextBox>().Single().Text!=hall.Name,"duplicate gets a separate name");import.Close();
        Check(store.HallDraft(hall.Id).Count==before.Count,"cancelled import leaves existing hall intact");
        main.UpdateConflictCount033(0);var button=(Button)main.FindName("TicketlandConflictsButton033");
        Check(Equals(button.Content,"Конфликтов нет"),"zero conflicts is unambiguous");main.UpdateConflictCount033(3);Check(Equals(button.Content,"Конфликтов: 3"),"nonzero conflicts count is clickable");
        var conflicts=new TicketlandConflictsWindow033(store);conflicts.Show();Pump();
        Check(Walk(conflicts).OfType<DataGrid>().Single().Columns.Count==7,"conflict list includes show, time, venue, sector, row, place and recipient");conflicts.Close();
        var cached=store.TicketlandLastSync028;
        if(!cached.HasValue)store.ApplyTicketlandReport028(new([],DateTimeOffset.UtcNow),true);
        var offline=new MainWindow(store);offline.Show();Pump();
        var cachedButton=(Button)offline.FindName("TicketlandConflictsButton033");
        Check(cachedButton.Visibility==Visibility.Visible,"cached conflict entry survives restart without network");offline.Close();Pump();
        Corrections025UiChecks.Capture(main,"033-import-and-conflict-status");
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {
        yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;
    }
    static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS UI033 "+message);}
}
