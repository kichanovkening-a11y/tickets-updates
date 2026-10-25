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
        TicketlandConflictsButton033.ToolTip="Продано в Ticketland и одновременно выдано или забронировано в программе. Нажмите, чтобы посмотреть список.";
        StatusText.ToolTip="Итоги Ticketland по всем показам. «Без ряда или места» — в исходном отчёте не указаны координаты билета; это не свободные места.";
    }
    private void TicketlandConflictsClick033(object sender,RoutedEventArgs e)=>Run(()=>new TicketlandConflictsWindow033(store).ShowDialog());
}
internal sealed class TicketlandConflictsWindow033:EditorWindow
{
    internal TicketlandConflictsWindow033(Store store):base(store,"Конфликты Ticketland",1100,650)
    {
        var panel=new DockPanel{Margin=new Thickness(16)};Content=panel;var rows=store.TicketlandConflicts033();
        var heading=new TextBlock{Text=rows.Count==0?"Конфликтов нет":$"Конфликтов: {rows.Count}. Эти места проданы в Ticketland и выданы или забронированы в программе.",TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
        var grid=new DataGrid{ItemsSource=rows,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false};
        foreach(var (title,path) in new[]{("Спектакль","Title"),("Дата и время","StartsAt"),("Площадка","Venue"),("Сектор","Sector"),("Ряд","Row"),("Место","Seat"),("Получатель","Recipient")})
            grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=path=="StartsAt"?new Binding(path){StringFormat="dd.MM.yyyy HH:mm"}:new Binding(path),Width=DataGridLength.SizeToCells});
        panel.Children.Add(grid);
    }
}
