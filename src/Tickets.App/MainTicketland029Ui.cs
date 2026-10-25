using System.Windows;

namespace Tickets.App;

public partial class MainWindow
{
    private bool ticketlandUiInitialized029;
    private bool ticketlandManualRefresh029;

    private void InitializeTicketlandUi029(object sender,RoutedEventArgs e)
    {
        if(ticketlandUiInitialized029)return;
        ticketlandUiInitialized029=true;
        TicketlandStatusChanged028+=UpdateTicketlandRefreshState029;
        Closed+=(_,_)=>TicketlandStatusChanged028-=UpdateTicketlandRefreshState029;
        UpdateTicketlandRefreshState029();UpdateCompactLayout();
    }

    internal void UpdateTicketlandRefreshState029()=>RefreshTicketlandButton029.IsEnabled=!TicketlandIsUpdating028&&!ticketlandManualRefresh029;

    private async void RefreshTicketlandClick029(object sender,RoutedEventArgs e)
    {
        if(TicketlandIsUpdating028||ticketlandManualRefresh029)return;
        var eventId=current?.Id;
        ticketlandManualRefresh029=true;UpdateTicketlandRefreshState029();
        try
        {
            await RefreshTicketland028();
            if(!Dispatcher.HasShutdownStarted&&eventId.HasValue)await Dispatcher.InvokeAsync(()=>HandleTicketlandReview029(eventId.Value));
        }
        finally
        {
            if(!Dispatcher.HasShutdownStarted)await Dispatcher.InvokeAsync(()=>{ticketlandManualRefresh029=false;UpdateTicketlandRefreshState029();});
        }
    }

    internal void HandleTicketlandReview029(long eventId)
    {
        if(ticketlandLifetime028.IsCancellationRequested||!IsLoaded||TicketlandIsUpdating028||current?.Id!=eventId||!store.TicketlandSectorReviews030().Any(issue=>issue.EventId==eventId))return;
        var existing=Application.Current.Windows.OfType<TicketlandMappingWindow028>().FirstOrDefault(window=>window.EventId031==eventId);
        if(existing is not null){existing.Activate();return;}
        var window=new TicketlandMappingWindow028(store,this,eventId);
        if(IsVisible)window.Owner=this;
        window.ShowDialog();
    }
}
