using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class InvoiceExtensionWindow:EditorWindow
{
    private readonly HallView hall=new();
    private readonly TextBlock summary=new(){FontWeight=FontWeights.Bold,Foreground=Brushes.Firebrick,Margin=new Thickness(8)};
    internal InvoiceExtensionWindow(Store store,long eventId,long issueId):base(store,$"Дополнить накладную № {issueId}",1150,850)
    {
        var invoice=store.Invoice(eventId,issueId);var show=store.Events().FirstOrDefault(e=>e.Id==eventId)??throw new TicketException("Показ не доступен для дополнения.");
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;
        var heading=new TextBlock{Text=$"{invoice.Title} · {invoice.EventDate}\nНакладная № {issueId} · Получатель: {invoice.Recipient}\nВыберите дополнительные свободные места. Они сохранятся в этой накладной.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var controls=new WrapPanel();DockPanel.SetDock(controls,Dock.Bottom);root.Children.Add(controls);
        Button(controls,"Весь зал",()=>hall.Fit());
        var apply=Button(controls,"Дополнить накладную",()=>Try(()=>{
            var selected=hall.SelectedSeats;
            if(selected.Count==0||selected.Any(s=>!s.Available))throw new TicketException("Для дополнения выберите свободные места с назначенной ценой.");
            store.ExtendInvoice(eventId,issueId,selected.Select(s=>s.Id).ToArray());
            try{store.SaveAllDocuments(eventId,issueId);}catch(Exception ex){MessageBox.Show(DialogOwner,"Накладная дополнена в базе. Не удалось обновить файлы: "+ex.Message,"Документы",MessageBoxButton.OK,MessageBoxImage.Warning);}
            Finish(true);
        }));apply.IsEnabled=false;apply.Margin=new Thickness(8,10,0,0);
        controls.Children.Add(summary);
        hall.SelectionChanged+=()=>{var selected=hall.SelectedSeats;summary.Text=$"Мест: {selected.Count} · Сумма: {selected.Sum(s=>(decimal)(s.Amount??0)):N0} ₽";apply.IsEnabled=selected.Count>0&&selected.All(s=>s.Available);};
        root.Children.Add(hall);hall.Load(store.Seats(eventId),store.Sectors(show.VenueId),true);
    }
}

internal sealed class InvoiceChoiceWindow:EditorWindow
{
    public long SelectedIssueId{get;private set;}
    internal InvoiceChoiceWindow(Store store,long eventId,string recipient):base(store,"Выберите накладную для дополнения",850,650)
    {
        if(string.IsNullOrWhiteSpace(recipient))throw new TicketException("Выберите получателя для дополнения накладной.");
        var recipientKey=CatalogText.Key(recipient);
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var heading=new TextBlock{Text="Получатель: "+recipient,TextWrapping=TextWrapping.Wrap};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var search=new TextBox{Margin=new Thickness(0,8,0,8),ToolTip="Поиск получателя"};DockPanel.SetDock(search,Dock.Top);root.Children.Add(search);
        var select=new Button{Content="Выбрать",Margin=new Thickness(0,8,0,0),IsEnabled=false};DockPanel.SetDock(select,Dock.Bottom);root.Children.Add(select);
        var issues=store.Issues(eventId).Where(i=>i.Count>i.ReturnedCount&&i.ReservedCount==0&&CatalogText.Key(i.Recipient)==recipientKey).ToArray();
        var list=new ListBox{DisplayMemberPath="Caption"};root.Children.Add(list);
        void Filter()=>list.ItemsSource=issues.Where(i=>SearchText.Matches(i.Recipient,search.Text)||i.Id.ToString()==search.Text.Trim()).Select(i=>new Choice(i.Id,$"№ {i.Id} · {i.Recipient} · {i.Count} бил. · {i.Total:N0} ₽")).ToArray();
        search.TextChanged+=(_,_)=>Filter();list.SelectionChanged+=(_,_)=>select.IsEnabled=list.SelectedItem is Choice;
        void Choose(){if(list.SelectedItem is Choice choice){SelectedIssueId=choice.Id;Finish(true);}}
        select.Click+=(_,_)=>Choose();list.MouseDoubleClick+=(_,_)=>Choose();Filter();
    }
    private sealed record Choice(long Id,string Caption);
}
