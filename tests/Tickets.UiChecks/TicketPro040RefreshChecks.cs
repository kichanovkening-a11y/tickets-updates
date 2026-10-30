using System.Reflection;
using Tickets.App;
using Tickets.Core;

internal static class TicketPro040RefreshChecks
{
    internal static void Run()=>RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.TicketProRefresh040")??throw new Exception("Selected showing still waits for whole catalog discovery");
        var method=type.GetMethod("RunAsync")!;
        Task<IReadOnlyList<string>> Run(Dictionary<long,string> targets,Func<long?> preferred,Func<Task> discover,Func<long,string,int,Task> read,CancellationToken token=default)
            =>(Task<IReadOnlyList<string>>)method.Invoke(null,[targets,preferred,discover,read,token])!;
        var trace=new List<string>();
        var targets=new Dictionary<long,string>{{1,"one"},{2,"two"}};
        var errors=await Run(targets,()=>2,()=>{trace.Add("discover");targets[3]="three";return Task.CompletedTask;},(id,url,n)=>{trace.Add("read"+id);if(n!=trace.Count(x=>x.StartsWith("read")))throw new Exception("Incorrect progress");return Task.CompletedTask;});
        if(errors.Count!=0||!trace.SequenceEqual(new[]{"read2","discover","read1","read3"}))throw new Exception("Known selected showing was delayed or read twice");
        Console.WriteLine("PASS known selected showing updates before catalog discovery");
        trace.Clear();targets=new(){{1,"one"},{2,"two"}};
        errors=await Run(targets,()=>3,()=>{trace.Add("discover");targets[3]="three";return Task.CompletedTask;},(id,url,n)=>{trace.Add("read"+id);return Task.CompletedTask;});
        if(errors.Count!=0||!trace.SequenceEqual(new[]{"discover","read3","read1","read2"}))throw new Exception("New selected showing was delayed behind other shows");
        Console.WriteLine("PASS newly discovered selected showing updates first");
        trace.Clear();targets=new(){{1,"one"},{2,"two"}};
        errors=await Run(targets,()=>2,()=>{trace.Add("discover");throw new TicketException("Catalog failed");},(id,url,n)=>{trace.Add("read"+id);return id==2?Task.FromException(new TicketException("Selected failed")):Task.CompletedTask;});
        if(errors.Count!=2||!trace.SequenceEqual(new[]{"read2","discover","read1"}))throw new Exception("Individual failures prevented remaining show updates");
        Console.WriteLine("PASS failed selected showing and discovery preserve other updates without duplicate reads");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();trace.Clear();
        try{await Run(targets,()=>2,()=>{trace.Add("discover");return Task.CompletedTask;},(id,url,n)=>{trace.Add("read");return Task.CompletedTask;},cancelled.Token);throw new Exception("Cancellation ignored");}
        catch(OperationCanceledException){if(trace.Count>0)throw new Exception("Work started after cancellation");}
        Console.WriteLine("PASS refresh cancellation prevents new reads");
    }
}
