using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;
public partial class MainWindow
{
    private void AddStatusLegend030(string text,Brush brush,HallStatus030 status)
    {
        var active=Hall.FocusStatus030==status;
        var panel=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,12,4),Cursor=Cursors.Hand,Background=Brushes.Transparent,Tag=status};
        panel.Children.Add(new Border{Background=brush,Width=12,Height=12,CornerRadius=new CornerRadius(2),Margin=new Thickness(0,0,5,0),BorderBrush=active?Brushes.Black:null,BorderThickness=new Thickness(active?2:0)});
        panel.Children.Add(new TextBlock{Text=text,FontSize=14.3,VerticalAlignment=VerticalAlignment.Center,FontWeight=active?FontWeights.Bold:FontWeights.Normal});
        panel.MouseLeftButtonDown+=(_,e)=>{
            Hall.FocusStatus030=Hall.FocusStatus030==status?null:status;
            if(current is not null)RenderHallLegend(store.Seats(current.Id));
            e.Handled=true;
        };
        StatusLegendPanel.Children.Add(panel);
    }

    private void RefreshHallTotals030(IReadOnlyList<SeatInfo> seats)
    {
        var total=current is null?null:store.TicketlandSourceTotals030().SingleOrDefault(t=>t.EventId==current.Id);
        HallTotals.Text=$"Всего мест: {seats.Count} · В продаже с ценой: {seats.Count(s=>s.Amount.HasValue&&!s.IsBlocked)} · Общая сумма: {seats.Where(s=>!s.IsBlocked).Sum(s=>(decimal)(s.Amount??0)):N0} ₽"+
            $"\nПродано Ticketland: {total?.TicketCount??0} · Сумма Ticketland: {total?.Nominal??0:N2} ₽";
    }
}
