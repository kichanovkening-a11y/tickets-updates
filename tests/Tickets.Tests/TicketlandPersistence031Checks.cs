using Tickets.Core;

internal static class TicketlandPersistence031Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("031 cached sales reopen offline then add new sales and apply returns",store=>{
            var title="Persistence031 "+Guid.NewGuid();var venue=store.CreateHall(title,"",Enumerable.Range(1,3).Select(n=>new DraftSeat{Sector="Ложа правая партера",Row="1",Number=n.ToString(),X=n*20}).ToArray());
            var seats=store.VenueSeats(venue);var plan=store.SavePlan(venue,title,seats.Select(s=>new PriceValue(s.Id,500,"#FF0000")).ToArray());var at=new DateTime(2030,1,3,20,0,0);
            var evt=store.CreateEvent(store.SavePerformance(title),venue,plan,at);var fetched=DateTimeOffset.UtcNow;
            TicketlandSale028 Sale(string number)=>new(title,at,"ЛОЖА ПАРТЕРА ПРАВАЯ","1",number,"persist031-"+number,700){Venue=title};
            store.Issue(evt,[seats.Single(s=>s.Number=="1").Id],"Получатель031","P031",1);
            store.ApplyTicketlandReport028(new([Sale("1")],fetched),true);
            var reopened=new Store(store.Path);Check(reopened.Seats(evt).Count(s=>s.IsTicketlandSold)==1&&reopened.Seats(evt).Single(s=>s.Number=="1").Status.Contains("Получатель031"),"saved conflict absent before any network update");
            reopened.ApplyTicketlandReport028(new([Sale("1"),Sale("2")],fetched.AddSeconds(1)),true);
            Check(reopened.Seats(evt).Count(s=>s.IsTicketlandSold)==2,"new sales not added");
            reopened.ApplyTicketlandReport028(new([Sale("2")],fetched.AddSeconds(2)),true);
            var final=new Store(store.Path);Check(!final.Seats(evt).Single(s=>s.Number=="1").IsTicketlandSold&&final.Seats(evt).Single(s=>s.Number=="1").TicketId.HasValue&&final.Seats(evt).Single(s=>s.Number=="2").IsTicketlandSold,"return removed local issue or remaining source sale");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
