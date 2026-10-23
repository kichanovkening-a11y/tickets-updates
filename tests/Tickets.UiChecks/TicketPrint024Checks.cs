using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tickets.Core;

internal static class TicketPrint024Checks
{
    internal static void Run(Store store,Type type,long eventId,long seatId)
    {
        var before=store.Issues(eventId).Count;
        store.RememberTicketBlank(null);
        var window=(Window)Activator.CreateInstance(type,store,eventId,new[]{seatId},"UI024","PRINT024",1L)!;
        try
        {
            var controls=Logical(window).OfType<FrameworkElement>().ToArray();
            Check(controls.OfType<TextBlock>().Any(t=>t.Text=="Принтер")&&controls.OfType<TextBlock>().Any(t=>t.Text=="Бумага")&&controls.OfType<TextBlock>().Any(t=>t.Text=="Копий"),"first ticket window includes printer, paper and copy settings");
            var orientation=controls.OfType<ComboBox>().Single(c=>c.Items.Cast<object>().Any(i=>Equals(i,"Альбомная")));
            Check(orientation.SelectedIndex==0,"ticket settings start in portrait");
            orientation.SelectedIndex=1;
            var page=Logical(window).OfType<DocumentPageView>().First();
            var size=page.DocumentPaginator.GetPage(0).Size;
            Check(size.Width>size.Height,"orientation updates ticket preview inside the same window");
            var copies=controls.OfType<TextBox>().Single(t=>t.Text=="1");copies.Text="3";
            Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Обновить просмотр")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Logical(window).OfType<TextBlock>().Any(t=>t.Text.Contains("Копий: 3")),"copy settings update in the same preview before issuance");
            Check(Logical(window).OfType<Button>().Count(b=>Equals(b.Content,"Печатать"))==1,"ticket window has one final print action");
            Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Отмена")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        finally{window.Close();}
        Check(store.Issues(eventId).Count==before&&!store.Seats(eventId).Single(s=>s.Id==seatId).TicketId.HasValue,"cancelling consolidated settings does not issue tickets");
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
