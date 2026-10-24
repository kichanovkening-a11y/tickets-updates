using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.App;
using Tickets.Core;
internal static class Document027UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).ToArray();
        var issue=store.Issue(show.Id,seats.Select(s=>s.Id).ToArray(),"Документы027","UI027",1);var assembly=typeof(MainWindow).Assembly;
        var invoiceType=assembly.GetType("Tickets.App.InvoiceWindow")!;var invoice=(Window)Activator.CreateInstance(invoiceType,[store,show.Id,issue,false])!;invoice.Show();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        try{
            var box=(ComboBox)invoiceType.GetField("senderBox",Private)!.GetValue(invoice)!;
            Check(box.FontSize==18&&box.Height==34&&box.Width<=320,"027 sender field is narrow without font/height change");
            var labels=Walk(invoice).OfType<TextBlock>().ToArray();var sender=labels.First(t=>t.Text=="От кого");Check(sender.FontSize>=18&&sender.FontWeight==FontWeights.Bold,"027 sender caption is large and bold");
            var flag=labels.Single(t=>t.Text.Replace("\n","")=="Выдача №"+issue);Check(flag.LayoutTransform is not RotateTransform&&flag.FontSize>=16,"027 flag has upright enlarged letters and actual issue only");
        }finally{invoice.Close();}
        var printType=assembly.GetType("Tickets.App.TicketPrintWindow")!;
        void CheckTitle(long[] ids,string expected){var print=(Window)Activator.CreateInstance(printType,[store,show.Id,ids])!;try{Check(print.Title==expected,"027 print title follows prior print history: "+expected);}finally{print.Close();}}
        CheckTitle(seats.Select(s=>s.Id).ToArray(),"Печать билетов");
        store.MarkTicketsPrinted(show.Id,[store.Invoice(show.Id,issue).Tickets.Single(t=>t.SeatId==seats[0].Id).Id]);
        CheckTitle([seats[0].Id],"Повторная печать билетов");CheckTitle([seats[1].Id],"Печать билетов");CheckTitle(seats.Select(s=>s.Id).ToArray(),"Повторная печать билетов");
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
