using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

public partial class MainWindow
{
    private void CollapseReturnedDetails028()
    {
        foreach(var item in HistoryGrid.Items)
            if(HistoryGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                row.DetailsVisibility=Visibility.Collapsed;
    }

    private void ReturnCountLoaded027(object sender,RoutedEventArgs e)
    {
        if(sender is not ContentControl host||host.DataContext is not JournalIssueRow issue)return;
        if(issue.ReturnedCount==0){host.Content=new TextBlock{Text="0"};return;}
        host.Content=ReturnedTicketDetails027.CreateToggle(issue.ReturnedCount,()=>{
            if(current is null)return;
            DependencyObject? node=host;while(node is not null&&node is not DataGridRow)node=VisualTreeHelper.GetParent(node);
            if(node is not DataGridRow row)return;
            if(row.DetailsVisibility==Visibility.Visible){row.DetailsVisibility=Visibility.Collapsed;return;}
            var factory=new FrameworkElementFactory(typeof(ContentControl));
            factory.AddHandler(FrameworkElement.LoadedEvent,new RoutedEventHandler((control,_)=>{
                if(control is ContentControl details&&details.DataContext is JournalIssueRow item&&current is not null)
                    details.Content=ReturnedTicketDetails027.CreateTable(store,current.Id,item.Id);
            }));
            row.DetailsTemplate=new DataTemplate{VisualTree=factory};row.DetailsVisibility=Visibility.Visible;
        });
    }
}
