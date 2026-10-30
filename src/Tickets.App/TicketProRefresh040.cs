namespace Tickets.App;

internal static class TicketProRefresh040
{
    public static async Task<IReadOnlyList<string>> RunAsync(Dictionary<long,string> targets,Func<long?> preferred,Func<Task> discover,Func<long,string,int,Task> read,CancellationToken token)
    {
        var errors=new List<string>();var attempted=new HashSet<long>();var updated=0;
        Task Read(long id,string url){attempted.Add(id);return read(id,url,++updated);}
        var selected=preferred();
        if(selected.HasValue&&targets.TryGetValue(selected.Value,out var selectedUrl))
            errors.AddRange(await TicketProBatch038.RunAsync([()=>Read(selected.Value,selectedUrl)],token));
        errors.AddRange(await TicketProBatch038.RunAsync([discover],token));
        selected=preferred();
        var remaining=targets.Where(p=>!attempted.Contains(p.Key)).OrderBy(p=>p.Key==selected?0:1)
            .Select(p=>(Func<Task>)(()=>Read(p.Key,p.Value)));
        errors.AddRange(await TicketProBatch038.RunAsync(remaining,token));
        return errors;
    }
}
