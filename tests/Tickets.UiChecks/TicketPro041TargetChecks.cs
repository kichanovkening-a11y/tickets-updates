using Tickets.App;
using Tickets.Core;

internal static class TicketPro041TargetChecks
{
    internal static void Run()=>RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        const string selected="https://pro.ticketland.ru/management/show/1/ticket-control/2";
        const string duplicate="https://pro.ticketland.ru/management/show/3/ticket-control/4";
        var read=0;var discovery=0;
        Task<string> Read(string url,CancellationToken _){read++;if(url!=selected)throw new Exception("Read another showing");return Task.FromResult("snapshot");}
        var result=await TicketProTarget041.ReadAsync(2,selected,_=>{discovery++;throw new Exception("Known binding must not rediscover");},Read,default);
        if(result!="snapshot"||read!=1||discovery!=0)throw new Exception("Known binding performed extra work");
        Console.WriteLine("PASS known selected binding reads once without catalog discovery");
        read=0;
        await TicketProTarget041.ReadAsync(2,null,_=>Task.FromResult(new TicketProSession037.Discovery038([new(2,selected),new(9,duplicate)],[])),Read,default);
        if(read!=1)throw new Exception("Complete discovery did not select exactly one showing");
        Console.WriteLine("PASS complete discovery selects only the requested showing");
        read=0;
        try
        {
            await TicketProTarget041.ReadAsync(2,null,_=>Task.FromResult(new TicketProSession037.Discovery038([new(2,selected)],["Another matching performance failed to load"])),Read,default);
            throw new Exception("Incomplete discovery accepted a candidate without ruling out an ambiguous source");
        }
        catch(TicketException){}
        if(read!=0)throw new Exception("Incomplete discovery read and could apply a candidate");
        Console.WriteLine("PASS incomplete discovery cannot read or bind the successful partial candidate");
        foreach(var shows in new TicketProSession037.DiscoveredShow038[][]{[],[new(2,selected),new(2,duplicate)]})
        {
            read=0;
            try{await TicketProTarget041.ReadAsync(2,null,_=>Task.FromResult(new TicketProSession037.Discovery038(shows,[])),Read,default);throw new Exception("Missing or ambiguous source accepted");}catch(TicketException){}
            if(read!=0)throw new Exception("Missing or ambiguous source was read");
        }
        Console.WriteLine("PASS missing and ambiguous sources do not read any scheme");
        using var cancelled=new CancellationTokenSource();read=0;
        try{await TicketProTarget041.ReadAsync(2,null,_=>{cancelled.Cancel();return Task.FromResult(new TicketProSession037.Discovery038([new(2,selected)],[]));},Read,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        if(read!=0)throw new Exception("Cancelled discovery continued into a scheme read");
        Console.WriteLine("PASS cancelled discovery does not start a scheme read");
    }
}
