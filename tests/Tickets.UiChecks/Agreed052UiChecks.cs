using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Tickets.Core;

internal static class Agreed052UiChecks
{
    internal static void Run(Store store)
    {

        var assembly=typeof(Tickets.App.MainWindow).Assembly;
        var type=assembly.GetType("Tickets.App.HallEditorWindow")!;
        var hall=(Window)Activator.CreateInstance(type,[store,null])!;
        try
        {
            hall.Show();Pump();
            var buttons=Tree(hall).OfType<Button>().Select(b=>b.Content?.ToString()).ToArray();
            Check(buttons.Contains("Добавить сектор"),"manual creation missing");
            Check(!buttons.Any(b=>b is not null&&(b.Contains("скан",StringComparison.OrdinalIgnoreCase)||b.Contains("Сетка как")||b=="Весь зал")),"redundant hall creation or fit button remains");
        }
        finally{hall.Close();}
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);var amount=seat.Amount;
        var issue=store.Issue(show.Id,[seat.Id],"052 UI","UI",52);
        var invoice=store.Invoice(show.Id,issue);
        var printType=assembly.GetType("Tickets.App.TicketPrintWindow")!;
        var priceCaption=printType.GetMethod("IsPriceCaption",BindingFlags.Static|BindingFlags.NonPublic)!;
        Check((bool)priceCaption.Invoke(null,["руб."])!&&(bool)priceCaption.Invoke(null,["р. "])!,"price currency remains on main ticket or tear-off");
        var editable=TicketBlankEditing.CreateA4From030879();editable.Name="052 editable";store.SaveTicketBlank(editable);
        var custom=TicketBlankEditing.CreateA4();custom.Name="052 custom";store.SaveTicketBlank(custom);
        foreach(var blank in new TicketBlank?[]{null,store.TicketBlanks().First(b=>b.Legacy is not null),editable,custom})
        {
            store.RememberTicketBlank(blank?.Id);
            var print=(Window)Activator.CreateInstance(printType,[store,show.Id,new long[]{seat.Id}])!;
            try
            {
                print.Show();Pump();
                var toggle=Tree(print).OfType<CheckBox>().SingleOrDefault(c=>Equals(c.Content,"Печатать без цены"));
                Check(toggle is not null&&toggle.IsChecked!=true,"per-print price checkbox missing or persisted");
                string Preview()=>string.Join("\n",Tree(((FixedDocument)Tree(print).OfType<DocumentPageView>().First().DocumentPaginator.Source).Pages[0].Child).OfType<TextBlock>().Select(t=>t.Text));
                Check(Preview().Contains("Цена:")||Preview().Contains(InvoiceDocuments.Money(invoice.Tickets[0].Amount)),"normal preview has no price");
                toggle!.IsChecked=true;Pump();Check(!Preview().Contains("Цена:")&&!Preview().Split('\n').Any(t=>t.Trim() is "руб." or "р." or "₽"),"price or currency remains in ticket/tear-off");
                toggle.IsChecked=false;Pump();Check(Preview().Contains("Цена:")||Preview().Contains(InvoiceDocuments.Money(invoice.Tickets[0].Amount)),"price cannot be restored");
            }
            finally{print.Close();}
        }
        Check(store.Invoice(show.Id,issue).Tickets.SequenceEqual(invoice.Tickets)&&store.Invoice(show.Id,issue).Total==amount,"print option mutated accounting");
        UpdaterProgress052Checks.Run();
        Console.WriteLine("PASS 052 hall commands and per-window no-price preview");
    }
    internal static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Tree(child))yield return item;}
    private static void Pump()=>System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}