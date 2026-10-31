using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;
public partial class MainWindow
{
    private bool hallExpanded038;
    private void ToggleHallClick038(object sender,RoutedEventArgs e)
    {
        hallExpanded038=!hallExpanded038;
        UpdateCompactLayout();
    }
    private void WorkspaceTabChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ReferenceEquals(e.OriginalSource,WorkspaceTabs))return;
        if(e.RemovedItems.Contains(JournalTab)){saveJournalLayout?.Invoke();ClearJournalSelection();}
    }
    private void UpdateCompactLayout()
    {
        EventSubtitle.Visibility=hallExpanded038||(Workspace.ActualHeight>0&&Workspace.ActualHeight<550)?Visibility.Collapsed:Visibility.Visible;
        if(Workspace.Parent is Grid layout&&layout.ActualWidth>0)
        {
            var expanded=Workspace.Visibility==Visibility.Visible&&hallExpanded038;
            var toggleName=expanded?"Свернуть зал":"Раскрыть зал";
            ToggleHallButton038.ToolTip=toggleName;
            System.Windows.Automation.AutomationProperties.SetName(ToggleHallButton038,toggleName);
            var navigation=expanded?0:layout.ActualWidth<1050?236d:280d;
            WorkspaceNavigation.Visibility=expanded?Visibility.Collapsed:Visibility.Visible;
            if(layout.ColumnDefinitions[0].Width.Value!=navigation)layout.ColumnDefinitions[0].Width=new GridLength(navigation);
            layout.ColumnDefinitions[1].Width=new GridLength(expanded?0:8);
        }
    }
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
            var text=new TextBlock{Text=FormattableString.Invariant($"{price:0.##}₽×{group.Count()}={group.Sum(s=>(decimal)s.Amount!.Value):0.##}₽"),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Left,FontSize=11.44,FontWeight=FontWeights.Bold,Foreground=(Brush)new BrushConverter().ConvertFromString(Pricing012Presentation.Foreground(group.First().Color))!};
            var active=Hall.FocusPrice==price;
            var button=new Border{Child=text,HorizontalAlignment=HorizontalAlignment.Stretch,Background=HallView.SeatBrush(group.First().Color),Padding=new Thickness(active?2:4),Margin=new Thickness(0,0,0,i==groups.Count-1?0:3),BorderThickness=new Thickness(active?3:1),BorderBrush=active?Brushes.Black:Brushes.Gray,Cursor=System.Windows.Input.Cursors.Hand};
            button.MouseLeftButtonDown+=(_,e)=>{SetPriceFocus(Hall.FocusPrice==price?null:price);e.Handled=true;};
            LegendPanel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(button,i);LegendPanel.Children.Add(button);
        }
        AddStatusLegend030("Бронь / накладная",Brushes.Black,HallStatus030.Reserved);AddStatusLegend030("Распечатаны",Brushes.Red,HallStatus030.Printed);AddStatusLegend030("× Заблокировано",Brushes.Gray,HallStatus030.Blocked);AddStatusLegend030("Нет цены",Brushes.Gainsboro,HallStatus030.NoPrice);
        AddStatusLegend030("Продано "+store.TicketSalesSource041(current?.Id),Brushes.Red,HallStatus030.TicketlandSold);
        if(seats.Any(s=>s.IsTicketlandOccupied))AddStatusLegend030("Занято Ticket PRO",OccupiedLegendBrush042(),HallStatus030.TicketlandOccupied);
    }
}
