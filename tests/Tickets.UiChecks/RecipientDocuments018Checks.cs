using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using Tickets.Core;

internal static class RecipientDocuments018Checks
{
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).ToArray();
        var id=store.Issue(show.Id,[seats[0].Id],"UI018 без реквизитов","Д018",1);
        var assembly=typeof(Tickets.App.App).Assembly;
        var type=assembly.GetType("Tickets.App.InvoiceWindow")!;
        var invoice=(Window)Activator.CreateInstance(type,store,show.Id,id,false)!;invoice.Show();Pump();
        try
        {
            Check(Logical(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Накладная")).IsEnabled,"invoice stays available without details");
            foreach(var caption in new[]{"Договор","Акт","ТТН","Счёт","Спецификация"})Check(!Logical(invoice).OfType<Button>().Single(b=>Equals(b.Content,caption)).IsEnabled,"missing details disables "+caption);
            Check(Logical(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Печать билетов накладной")).IsEnabled,"ticket printing stays available");
            store.SaveOrganizationDetails("UI018 без реквизитов",false,new(Name:"UI018 без реквизитов",Inn:"018"));store.RefreshInvoiceRecipient(show.Id,id);
            type.GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(invoice,null);Pump();
            Check(Logical(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Договор")).IsEnabled,"saved requisites enable business docs");
        }
        finally{invoice.Close();}
        var sber=store.Issue(show.Id,[seats[1].Id],"Сбер","С018",1);
        var custom=(Window)Activator.CreateInstance(assembly.GetType("Tickets.App.CustomIssueDocumentWindow")!,BindingFlags.Instance|BindingFlags.NonPublic,null,[store,show.Id,sber,"contract"],null)!;
        custom.Show();Pump();
        try
        {
            var viewer=Logical(custom).OfType<FlowDocumentScrollViewer>().Single();var text=new TextRange(viewer.Document.ContentStart,viewer.Document.ContentEnd).Text;
            Check(text.Contains("Профсоюз")&&text.Contains(show.Title)&&!text.Contains("{count}"),"Sber source rendered with actual event");
            var date=Logical(custom).OfType<DatePicker>().Single();date.SelectedDate=new DateTime(2026,7,20);Pump();
            Check(store.ContractDate(show.Id,sber)==new DateTime(2026,7,20),"contract date edit persisted");
        }
        finally{custom.Close();}
        Console.WriteLine("Documents018: missing requisites guard, original invoice printing, Sber form preview and editable contract date.");
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject p){yield return p;foreach(var c in LogicalTreeHelper.GetChildren(p).OfType<DependencyObject>())foreach(var d in Logical(c))yield return d;}
    private static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool v,string message){if(!v)throw new Exception(message);}
}
