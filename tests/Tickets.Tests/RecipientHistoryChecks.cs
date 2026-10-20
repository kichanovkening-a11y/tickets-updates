using Tickets.Core;

internal static class RecipientHistoryChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("organization history keeps all events and returns without mixing recipients",s=>{
            var choices=s.Events().Select(e=>(Event:e,Seats:s.Seats(e.Id).Where(x=>x.Available).Take(2).Select(x=>x.Id).ToArray())).Where(x=>x.Seats.Length==2).Take(2).ToArray();
            if(choices.Length<2)throw new Exception("No two events with free seats");
            var first=s.Issue(choices[0].Event.Id,choices[0].Seats,"ООО История ёлка","ORG",1);
            var second=s.Issue(choices[1].Event.Id,new[]{choices[1].Seats[0]},"ООО История ёлка","ORG",3);
            s.Issue(choices[1].Event.Id,new[]{choices[1].Seats[1]},"Другой получатель","OTHER",1);
            s.Return(new[]{s.History(choices[0].Event.Id).First().Id});
            var reopened=new Store(s.Path);var rows=reopened.RecipientHistory("  ооо история елка  ");
            Require(rows.Count==2&&rows.Select(x=>x.Issue.Id).ToHashSet().SetEquals(new[]{first,second}),"organization batches lost or mixed");
            Require(rows.Sum(x=>x.Issue.Count)==3&&rows.Sum(x=>x.Issue.ReturnedCount)==1,"return history missing");
            foreach(var row in rows)Require(reopened.Invoice(row.Issue.EventId,row.Issue.Id).Tickets.Count==row.Issue.Count,"invoice event scope wrong");
            var entry=reopened.RecipientDirectory().Single(x=>x.Name=="ООО История ёлка");
            Require(entry.IssueCount==2&&entry.TicketCount==3&&entry.ReturnedCount==1,"directory totals wrong");
        });
        test("organization directory includes legacy recipients without issuance",s=>{
            var saved=s.Recipients();var directory=s.RecipientDirectory();
            Require(saved.Count>0&&saved.All(name=>directory.Any(x=>CatalogText.Key(x.Name)==CatalogText.Key(name))),"saved recipient omitted");
            Require(directory.Any(x=>x.IssueCount==0),"empty legacy recipient omitted");
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
