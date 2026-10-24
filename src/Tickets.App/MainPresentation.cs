using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;
public partial class MainWindow
{
    private void WorkspaceTabChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ReferenceEquals(e.OriginalSource,WorkspaceTabs))return;
        if(e.RemovedItems.Contains(JournalTab)){saveJournalLayout?.Invoke();ClearJournalSelection();}
    }
    private void UpdateCompactLayout()
    {
        EventSubtitle.Visibility=Workspace.ActualHeight>0&&Workspace.ActualHeight<550?Visibility.Collapsed:Visibility.Visible;
        EventSummaryHeader.Margin=new Thickness(8,0,0,5);
        OrdinaryActionsScroll.Margin=new Thickness(8,0,0,0);
        Canvas.SetLeft(SquareShowActions,8+ChangePriceButton.ActualWidth+ChangePriceButton.Margin.Left+ChangePriceButton.Margin.Right+
            UndoPriceButton.ActualWidth+UndoPriceButton.Margin.Left+UndoPriceButton.Margin.Right+
            ExtendInvoiceButton.ActualWidth+ExtendInvoiceButton.Margin.Left+ExtendInvoiceButton.Margin.Right-OrdinaryActionsScroll.HorizontalOffset);
        Canvas.SetTop(SquareShowActions,0);
        if(Workspace.Parent is Grid layout&&layout.ActualWidth>0)
        {
            var navigation=layout.ActualWidth<1050?236d:280d;
            var selection=layout.ActualWidth<900?270d:300d;
            if(layout.ColumnDefinitions[0].Width.Value!=navigation)layout.ColumnDefinitions[0].Width=new GridLength(navigation);
            if(Workspace.ColumnDefinitions[1].Width.Value!=selection)Workspace.ColumnDefinitions[1].Width=new GridLength(selection);
        }
    }
    private void ActionScrollChanged027(object sender,ScrollChangedEventArgs e)=>UpdateCompactLayout();
    private static TreeViewItem? NearestTreeItem(DependencyObject? source)
    {
        while(source is not null)
        {
            if(source is TreeViewItem item)return item;
            source=source is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
    private void SelectedViewChanged(object sender,SelectionChangedEventArgs e)
    {
        if(SelectedList is null||SelectedPriceSummary is null)return;
        var summary=SelectedViewBox.SelectedIndex==1;
        SelectedList.Visibility=summary?Visibility.Collapsed:Visibility.Visible;
        SelectedPriceSummary.Visibility=summary?Visibility.Visible:Visibility.Collapsed;
    }
    private void SaveIssuedDocuments(long eventId,long issueId)
    {
        try{store.SaveAllDocuments(eventId,issueId);}
        catch(Exception ex){StatusText.Text="Выдача сохранена в базе. Не удалось обновить документы: "+ex.Message;}
    }
    private void SetPriceFocus(decimal? price)
    {
        Hall.FocusPrice=price;
        if(current is not null)RenderHallLegend(store.Seats(current.Id));
    }
    private void RenderHallLegend(IReadOnlyList<SeatInfo> seats)
    {
        LegendPanel.Children.Clear();LegendPanel.RowDefinitions.Clear();LegendPanel.ColumnDefinitions.Clear();StatusLegendPanel.Children.Clear();
        var groups=seats.Where(s=>s.Amount.HasValue&&!s.IsBlocked).GroupBy(s=>s.Amount!.Value).OrderByDescending(g=>g.Key).ToList();
        for(var i=0;i<groups.Count;i++)
        {
            var group=groups[i];var price=group.Key;
            var text=new TextBlock{Text=FormattableString.Invariant($"{price:0.##}₽×{group.Count()}={group.Sum(s=>(decimal)s.Amount!.Value):0.##}₽"),TextWrapping=TextWrapping.NoWrap,FontSize=14.3,FontWeight=FontWeights.Bold,Foreground=(Brush)new BrushConverter().ConvertFromString(Pricing012Presentation.Foreground(group.First().Color))!};
            var button=new Border{Child=text,HorizontalAlignment=HorizontalAlignment.Left,Background=HallView.SeatBrush(group.First().Color),Padding=new Thickness(7,4,7,4),Margin=new Thickness(0,0,8,4),BorderThickness=new Thickness(Hall.FocusPrice==price?3:1),BorderBrush=Hall.FocusPrice==price?Brushes.Black:Brushes.Gray,Cursor=System.Windows.Input.Cursors.Hand};
            button.MouseLeftButtonDown+=(_,e)=>{SetPriceFocus(Hall.FocusPrice==price?null:price);e.Handled=true;};
            LegendPanel.Children.Add(button);
        }
        PriceSummaryLayout.Arrange(LegendPanel);
        AddLegend("Бронь / накладная",Brushes.Black);AddLegend("Распечатаны",Brushes.Red);AddLegend("× Заблокировано",Brushes.Gray);AddLegend("Нет цены",Brushes.Gainsboro);
        if(seats.Any(s=>s.IsTicketlandSold))AddLegend("Продано Ticketland",Brushes.Red);
    }
}
