using System.Reflection;
using Tickets.App;

internal static class TicketPro041QueueChecks
{
    internal static void Run()=>CheckAsync().GetAwaiter().GetResult();
    private static async Task CheckAsync()
    {
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.TicketProSelection041")??throw new Exception("Refresh still queues all showings instead of the selected showing");
        object Queue(Func<long,CancellationToken,Task> read,CancellationToken token=default)=>Activator.CreateInstance(type,[read,token])!;
        Task Request(object queue,long? id)=>(Task)type.GetMethod("RequestAsync")!.Invoke(queue,[id])!;
        var calls=new List<long>();
        var queue=Queue((id,_)=>{calls.Add(id);return Task.CompletedTask;});
        await Request(queue,null);if(calls.Count!=0)throw new Exception("Home starts a refresh");
        await Request(queue,2);if(!calls.SequenceEqual(new long[]{2}))throw new Exception("Other showings were requested");
        Console.WriteLine("PASS idle home does not refresh and a request reads only its selected showing");

        var release=new TaskCompletionSource();calls.Clear();
        queue=Queue((id,_)=>{calls.Add(id);return release.Task;});
        var first=Request(queue,1);var duplicate=Request(queue,1);
        if(!ReferenceEquals(first,duplicate)||calls.Count!=1)throw new Exception("Same showing is read twice concurrently");
        release.SetResult();await first;
        Console.WriteLine("PASS repeated requests for the active showing coalesce");

        release=new();calls.Clear();CancellationToken oldToken=default;var active=0;var max=0;
        queue=Queue(async(id,token)=>{active++;max=Math.Max(max,active);calls.Add(id);try{if(id==1){oldToken=token;await release.Task;}}finally{active--;}});
        first=Request(queue,1);var middle=Request(queue,2);var last=Request(queue,3);
        if(!oldToken.IsCancellationRequested||calls.Count!=1)throw new Exception("Old request was not cancelled or a second navigation overlapped");
        release.SetResult();await Task.WhenAll(first,middle,last);
        if(max!=1||!calls.SequenceEqual(new long[]{1,3}))throw new Exception("Obsolete middle selection was read");
        Console.WriteLine("PASS rapid selection cancels old work and runs only the latest pending showing serially");

        calls.Clear();queue=Queue(async(id,token)=>{calls.Add(id);await Task.Delay(Timeout.Infinite,token);});
        first=Request(queue,4);await Request(queue,null);await first;
        if(calls.Count!=1)throw new Exception("Home queued another read");
        Console.WriteLine("PASS returning home cancels the current read");

        using var lifetime=new CancellationTokenSource();calls.Clear();
        queue=Queue(async(id,token)=>{calls.Add(id);await Task.Delay(Timeout.Infinite,token);},lifetime.Token);
        first=Request(queue,5);lifetime.Cancel();await first;await Request(queue,6);
        if(!calls.SequenceEqual(new long[]{5}))throw new Exception("Work starts after close");
        Console.WriteLine("PASS lifetime cancellation stops work and prevents a new read");

        calls.Clear();queue=Queue((id,_)=>{calls.Add(id);return id==1?Task.FromException(new InvalidOperationException("fixture failure")):Task.CompletedTask;});
        try{await Request(queue,1);throw new Exception("Failure hidden");}catch(InvalidOperationException){}
        await Request(queue,2);
        if(!calls.SequenceEqual(new long[]{1,2}))throw new Exception("Failure prevents the next selected showing");
        Console.WriteLine("PASS a failed request does not block the next showing");
    }
}
