namespace Tickets.App;

// Called on the UI thread. A WebView has one document, so reads must be serial.
internal sealed class TicketProSelection041(Func<long,CancellationToken,Task> refresh,CancellationToken lifetime)
{
    private Task tail=Task.CompletedTask;
    private CancellationTokenSource? active;
    private long? selected;

    public Task RequestAsync(long? eventId)
    {
        if(lifetime.IsCancellationRequested)return Task.CompletedTask;
        if(selected==eventId&&!tail.IsCompleted)return tail;
        selected=eventId;active?.Cancel();
        if(!eventId.HasValue)return tail;
        var previous=tail;
        var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        active=cancellation;
        tail=RunAsync(previous,eventId.Value,cancellation);
        return tail;
    }

    private async Task RunAsync(Task previous,long eventId,CancellationTokenSource cancellation)
    {
        try
        {
            // A failed older request must not prevent switching to a different showing.
            try{await previous;}catch{}
            cancellation.Token.ThrowIfCancellationRequested();
            await refresh(eventId,cancellation.Token);
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested){}
        finally
        {
            if(ReferenceEquals(active,cancellation))active=null;
            cancellation.Dispose();
        }
    }
}
