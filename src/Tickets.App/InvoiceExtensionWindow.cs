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
        hall.SeatSelectionRejected+=seat=>summary.Text=$"{seat.Label}: {seat.Status}. Место не выбирается.";
        hall.SelectionChanged+=()=>{var selected=hall.SelectedSeats;summary.Text=$"Мест: {selected.Count} · Сумма: {selected.Sum(s=>(decimal)(s.Amount??0)):N0} ₽";apply.IsEnabled=selected.Count>0&&selected.All(s=>s.Available);};
        root.Children.Add(hall);hall.Load(store.Seats(eventId),store.Sectors(show.VenueId),true);
    }
}

internal sealed class InvoiceChoiceWindow:EditorWindow
{
    public long SelectedIssueId{get;private set;}
    internal InvoiceChoiceWindow(Store store,long eventId):base(store,"Выберите получателя и накладную для дополнения",850,650)
    {
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var heading=new TextBlock{Text="Сначала выберите получателя, затем его накладную текущего показа.",TextWrapping=TextWrapping.Wrap};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var issues=store.Issues(eventId).Where(i=>i.Count>i.ReturnedCount&&i.ReservedCount==0).ToArray();
        var recipients=new ComboBox{Margin=new Thickness(0,8,0,8),MinHeight=36,ItemsSource=issues.Select(i=>i.Recipient).Distinct().OrderBy(x=>x).ToArray(),ToolTip="Получатель накладной"};RecipientTypography.Apply(recipients);DockPanel.SetDock(recipients,Dock.Top);root.Children.Add(recipients);
        var search=new TextBox{Margin=new Thickness(0,0,0,8),ToolTip="Поиск по номеру накладной"};DockPanel.SetDock(search,Dock.Top);root.Children.Add(search);
        var select=new Button{Content="Выбрать",Margin=new Thickness(0,8,0,0),IsEnabled=false};DockPanel.SetDock(select,Dock.Bottom);root.Children.Add(select);
        var list=new ListBox{DisplayMemberPath="Caption"};root.Children.Add(list);
        void Filter()
        {
            var recipient=recipients.SelectedItem as string;
            list.ItemsSource=issues.Where(i=>recipient is not null&&CatalogText.Key(i.Recipient)==CatalogText.Key(recipient)&&(string.IsNullOrWhiteSpace(search.Text)||i.Id.ToString().Contains(search.Text.Trim()))).Select(i=>new Choice(i.Id,$"№ {i.Id} · {i.Recipient} · {i.Count-i.ReturnedCount} бил. · {i.Total:N0} ₽")).ToArray();
            select.IsEnabled=false;
        }
        recipients.SelectionChanged+=(_,_)=>Filter();search.TextChanged+=(_,_)=>Filter();list.SelectionChanged+=(_,_)=>select.IsEnabled=list.SelectedItem is Choice;
        void Choose(){if(list.SelectedItem is Choice choice){SelectedIssueId=choice.Id;Finish(true);}}
        select.Click+=(_,_)=>Choose();list.MouseDoubleClick+=(_,_)=>Choose();Filter();
    }
    private sealed record Choice(long Id,string Caption);
}
