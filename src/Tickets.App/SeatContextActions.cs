using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private void TicketBlanksClick(object sender,RoutedEventArgs e)=>Run(()=>new TicketBlankWindow(store).ShowDialog());
    private void PriceKeyDown(object sender,KeyEventArgs e)
    {if(e.Key==Key.Enter&&ApplyPriceButton.IsEnabled){e.Handled=true;ApplyPriceClick(sender,new RoutedEventArgs());}}

    private void OpenSeatContext(long seatId,Point position)
    {
        if(current is null)return;
        var seat=store.Seats(current.Id).FirstOrDefault(s=>s.Id==seatId);
        if(seat is null)return;
        var menu=new ContextMenu{PlacementTarget=Hall,Placement=PlacementMode.RelativePoint,HorizontalOffset=position.X,VerticalOffset=position.Y};
        void Action(string label,bool enabled,System.Action perform)
        {if(!enabled)return;var item=new MenuItem{Header=label};item.Click+=(_,_)=>perform();menu.Items.Add(item);}
        if(!SeatSelection.CanSelect(seat))
        {
            StatusText.Text=$"{seat.Label}: {seat.Status}. Место не выбирается.";
            menu.Items.Add(new MenuItem{Header=seat.Status,IsEnabled=false});
            if(seat.IssueId.HasValue)Action("Открыть исходную накладную",true,()=>Run(()=>OpenInvoice(seat.IssueId.Value)));
            Action("Вернуть в продажу",seat.IsBlocked&&!currentArchived,()=>Run(()=>{
                if(MessageBox.Show(this,$"Вернуть в продажу: {seat.Label}?","Возврат в продажу",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                store.SetSeatsBlocked(current.Id,[seat.Id],false);LoadEvent(false);
            }));
            Hall.ContextMenu=menu;menu.IsOpen=true;return;
        }
        if(!Hall.Selected.Contains(seatId))Hall.SetSelection(new[]{seatId});
        UpdateSelection();
        Action(IssueButton.Content?.ToString()??"Выдать накладную",IssueButton.IsEnabled,()=>IssueClick(this,new RoutedEventArgs()));
        Action(PrintTicketsButton.Content?.ToString()??"Печать билетов",PrintTicketsButton.IsEnabled,()=>PrintTicketsClick(this,new RoutedEventArgs()));
        Action("Забронировать",ReserveButton.IsEnabled,()=>ReserveClick(this,new RoutedEventArgs()));
        Action("Дополнить накладную",ExtendInvoiceButton.IsEnabled,()=>ExtendInvoiceClick(this,new RoutedEventArgs()));
        if(menu.Items.Count>0)menu.Items.Add(new Separator());
        Action("Изменить цену",ApplyPriceButton.IsEnabled,()=>TogglePriceClick(this,new RoutedEventArgs()));
        Action(SaleStateButton.Content?.ToString()??"Снять с продажи",SaleStateButton.IsEnabled,()=>SaleStateClick(this,new RoutedEventArgs()));
        Action("Вернуть выбранные",ReturnButton.IsEnabled,()=>ReturnClick(this,new RoutedEventArgs()));
        var invoiceIds=Hall.SelectedSeats.Where(s=>s.IssueId.HasValue).Select(s=>s.IssueId!.Value).Distinct().ToArray();
        if(invoiceIds.Length==1)Action("Открыть накладную",true,()=>Run(()=>OpenInvoice(invoiceIds[0])));
        if(menu.Items.Count>0&&menu.Items[menu.Items.Count-1] is Separator)menu.Items.RemoveAt(menu.Items.Count-1);
        Hall.ContextMenu=menu;menu.IsOpen=menu.Items.Count>0;
    }
}
