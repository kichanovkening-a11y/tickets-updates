using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tickets.Core;
namespace Tickets.App;
public partial class MainWindow
{
    internal void UpdateConflictCount033(int count)
    {
        TicketlandConflictsButton033.Visibility=Visibility.Visible;
        TicketlandConflictsButton033.Content=count==0?"Конфликтов нет":$"Конфликтов: {count}";
        var source=store.TicketSalesSource041(current?.Id);
        TicketlandConflictsButton033.ToolTip=$"Продано в {source} и одновременно выдано или забронировано в программе. Нажмите, чтобы посмотреть список.";
        StatusText.ToolTip=source=="Ticket PRO"?"Ticket PRO: состояния мест выбранного показа. «Продано» — проданные билеты. «Занято» — места со статусом «Забронировано» или «Не использовать»; они не считаются продажами.":"Итоги Ticketland только по выбранному показу. «Без ряда или места» — в исходном отчёте не указаны координаты билета; это не свободные места.";
    }
    private void TicketlandConflictsClick033(object sender,RoutedEventArgs e)=>Run(()=>new TicketlandConflictsWindow033(store,current?.Id).ShowDialog());
}
internal sealed class TicketlandConflictsWindow033:EditorWindow
{
    internal TicketlandConflictsWindow033(Store store,long? eventId=null):base(store,"Конфликты "+store.TicketSalesSource041(eventId),1100,650)
    {
        var panel=new DockPanel{Margin=new Thickness(16)};Content=panel;var rows=store.TicketlandConflicts033().Where(row=>eventId.HasValue&&row.EventId==eventId.Value).ToArray();
        var heading=new TextBlock{Text=rows.Length==0?"Конфликтов нет":$"Конфликтов: {rows.Length}. Эти места проданы в {store.TicketSalesSource041(eventId)} и выданы или забронированы в программе.",TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
        var grid=new DataGrid{ItemsSource=rows,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false};
        foreach(var (title,path) in new[]{("Спектакль","Title"),("Дата и время","StartsAt"),("Площадка","Venue"),("Сектор","Sector"),("Ряд","Row"),("Место","Seat"),("Получатель","Recipient")})
            grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=path=="StartsAt"?new Binding(path){StringFormat="dd.MM.yyyy HH:mm"}:new Binding(path),Width=DataGridLength.SizeToCells});
        panel.Children.Add(grid);
    }
}
