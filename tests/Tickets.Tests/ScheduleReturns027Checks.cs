using Tickets.Core;

internal static class ScheduleReturns027Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("027 returned details preserve merged invoice scope and ticket data",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=5);
            var seats=store.Seats(show.Id).Where(s=>s.Available).Take(5).ToArray();
            var first=store.Issue(show.Id,seats.Take(2).Select(s=>s.Id).ToArray(),"Возвраты027","RET027",1);
            var second=store.Issue(show.Id,seats.Skip(2).Take(2).Select(s=>s.Id).ToArray(),"Возвраты027","RET027",3);
            var other=store.Issue(show.Id,[seats[4].Id],"Другой027","OTHER027",1);
            var merged=store.MergeInvoices(show.Id,[first,second]);
            var tickets=store.History(show.Id);
            var expected=tickets.Where(t=>t.IssueId==first||t.IssueId==second).GroupBy(t=>t.IssueId).Select(g=>g.First()).ToArray();
            store.Return(expected.Select(t=>t.Id).Append(tickets.Single(t=>t.IssueId==other).Id).ToArray());
            var returned=new Store(store.Path).ReturnedTicketsForIssue(show.Id,merged);
            if(!returned.Select(t=>t.Id).ToHashSet().SetEquals(expected.Select(t=>t.Id)))throw new Exception("Merged returns missing or another issue's return included");
            foreach(var ticket in returned)
            {
                var original=expected.Single(t=>t.Id==ticket.Id);
                if(ticket.ReturnedAt is null||ticket.Sector!=original.Sector||ticket.Row!=original.Row||ticket.Seat!=original.Seat||ticket.Code!=original.Code||ticket.Amount!=original.Amount)
                    throw new Exception("Returned seat, ticket number, amount or timestamp lost");
            }
            if(store.ReturnedTicketsForIssue(show.Id,first).Count!=1)throw new Exception("Historical source scope changed after merge");
            if(store.ReturnedTicketsForIssue(show.Id,-1).Count!=0)throw new Exception("Unknown issue exposed returns");
        });
    }
}
