using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

// Explicit opt-in: reads the existing app profile and writes only this harness's temporary DB copy.
internal static class TicketPro038LiveChecks
{
    private static T Wait<T>(Func<Task<T>> factory)
    {
        var task=Application.Current.Dispatcher.InvokeAsync(factory).Task.Unwrap();
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame();
            _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        return task.GetAwaiter().GetResult();
    }
    internal static void Run(Store store,MainWindow main,string connectionPath)
    {
        using var settings=JsonDocument.Parse(File.ReadAllText(connectionPath));
        var id=settings.RootElement.GetProperty("ConnectionId").GetString()!;
        var signedIn=new TaskCompletionSource<bool>();
        TicketProSession037? session=null;
        session=new TicketProSession037(main,store,id,()=>{signedIn.TrySetResult(true);session!.Hide();return Task.CompletedTask;},()=>{});
        try
        {
            Wait(Prepare);
            TicketProSession037.Discovery038 discovery;
            try{discovery=Wait(()=>session.DiscoverShows038Async(CancellationToken.None));}
            catch(TicketException ex) when(ex.Message.Contains("требуется вход"))
            {
                Console.WriteLine("LIVE sign-in required in the Ticket PRO window; original database is unchanged");
                session.Show();
                Wait(()=>signedIn.Task.WaitAsync(TimeSpan.FromMinutes(5)));
                discovery=Wait(()=>session.DiscoverShows038Async(CancellationToken.None));
            }
            foreach(var error in discovery.Errors)Console.WriteLine("LIVE discovery error: "+error);
            var shows=discovery.Shows;
            Console.WriteLine("LIVE discovered shows: "+shows.Count);
            if(shows.Count==0)throw new Exception("No local shows discovered in live Ticket PRO");
            foreach(var show in shows)
            {
                var e=store.Events().Single(e=>e.Id==show.EventId);
                var raw=Wait(()=>session.ReadSchemeAsync(show.Url,CancellationToken.None));
                store.ApplyTicketPro037(e.Id,TicketPro037.Parse(raw,DateTimeOffset.UtcNow));
                var seats=store.Seats(e.Id);
                Console.WriteLine($"LIVE {e.Caption}: {seats.Count} local; sold={seats.Count(x=>x.IsTicketlandSold)}, occupied={seats.Count(x=>x.IsTicketlandOccupied)}");
                if(e.Title.Contains("Филадельфия")&&e.SortDate.StartsWith("2026-11-01"))
                {
                    var invitation=seats.Single(x=>x.Sector.Equals("Партер",StringComparison.OrdinalIgnoreCase)&&x.Row=="5"&&x.Number=="20");
                    if(!invitation.IsTicketlandOccupied||invitation.IsTicketlandSold)throw new Exception("Live invitation is still sold");
                    Console.WriteLine("PASS LIVE invitation row 5 seat 20 is occupied, not sold");
                }
            }
            Console.WriteLine("PASS LIVE automatic discovery and complete scheme application on isolated copy");
        }
        finally{session.DisposeSession();}
        async Task<bool> Prepare(){await session.PrepareAsync(true,CancellationToken.None);return true;}
    }
}
