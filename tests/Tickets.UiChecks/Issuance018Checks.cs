using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Issuance018Checks
{
    internal static void Run(Store store,MainWindow main)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);
        main.SelectEvent(show.Id);Pump();
        var recipient=(ComboBox)main.FindName("RecipientBox");
        Check(recipient.Text=="","opening a show requires recipient choice");
        var hall=(HallView)main.FindName("Hall");
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).ToArray();
        recipient.Text="Проверка018";hall.SetSelection([seats[0].Id]);Pump();
        Check(((Button)main.FindName("IssueButton")).IsEnabled,"explicit recipient enables issuance");
        typeof(MainWindow).GetMethod("ClearIssuanceRecipient",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);Pump();
        Check(recipient.Text==""&&!((Button)main.FindName("IssueButton")).IsEnabled,"new issuance recipient reset disables issue");
        Check(((Button)main.FindName("ExtendInvoiceButton")).IsEnabled,"invoice extension does not require sidebar recipient");
        var id=store.Issue(show.Id,[seats[1].Id],"Исходный018","ЮИ018",store.NextNumber("ЮИ018"));
        typeof(MainWindow).GetMethod("LoadEvent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[false]);
        hall.SetSelection([seats[1].Id]);Pump();
        Check(hall.SelectedSeats.Count==0&&!((Button)main.FindName("PrintTicketsButton")).IsEnabled,"issued seats cannot be selected for generic printing");
        var original=store.Invoice(show.Id,id);Check(store.TicketReprint(show.Id,[seats[1].Id]).Tickets.Single().Id==original.Tickets.Single().Id,"original invoice reprint keeps ticket");
        var choiceType=typeof(MainWindow).Assembly.GetType("Tickets.App.InvoiceChoiceWindow")!;
        var choice=(Window)Activator.CreateInstance(choiceType,BindingFlags.Instance|BindingFlags.NonPublic,null,[store,show.Id],null)!;
        var dock=(DockPanel)choice.Content;var picker=dock.Children.OfType<ComboBox>().Single();var list=dock.Children.OfType<ListBox>().Single();
        Check(picker.SelectedItem is null&&list.Items.Count==0,"invoice extension starts with explicit recipient choice");
        picker.SelectedItem="Исходный018";Check(list.Items.Count>0,"recipient choice reveals current show invoices");choice.Close();
        store.Return(original.Tickets.Select(t=>t.Id).ToArray());
        typeof(MainWindow).GetMethod("LoadEvent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[true]);
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
