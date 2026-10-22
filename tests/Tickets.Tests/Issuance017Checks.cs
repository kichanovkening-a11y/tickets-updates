using Tickets.Core;
internal static class Issuance017Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("last issuance series persists without reusing issued ticket numbers",store=>{
            store.RememberIssueSeries("НОВАЯ17");var reopened=new Store(store.Path);
            if(reopened.LastIssueSeries!="НОВАЯ17")throw new Exception("series lost on restart");
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
            store.Issue(show.Id,[seat.Id],"Серия17",reopened.LastIssueSeries,1);
            if(new Store(store.Path).NextNumber("НОВАЯ17")!=2)throw new Exception("remembering series reused a ticket number");
            try{store.RememberIssueSeries(new string('А',33));throw new Exception("oversized series accepted");}catch(TicketException){}
            if(new Store(store.Path).LastIssueSeries!="НОВАЯ17")throw new Exception("invalid series changed preference");
        });
    }
}
