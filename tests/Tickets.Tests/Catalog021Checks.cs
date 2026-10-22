using Tickets.Core;
internal static class Catalog021Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("021 retiring Extreme parter removes working seats but preserves issued history",store=>{
            var venue=store.CreateHall(VenueLayouts019.RussianSong,"Тест",new[]{new DraftSeat{Sector="Экстрим партер",Row="А",Number="1"},new DraftSeat{Sector="Партер",Row="1",Number="1",Y=36}});
            var seats=store.VenueSeats(venue);var price=store.PriceColors().First();
            var plan=store.SavePlan(venue,"Тест 021",seats.Select(s=>new PriceValue(s.Id,price.Amount,price.Color)).ToArray(),[]);
            var show=store.CreateEvent(store.Performances().First().Id,venue,plan,new DateTime(2091,10,22,19,0,0));
            var issuedSeat=seats.First(s=>s.Sector=="Экстрим партер");
            var id=store.Issue(show,[issuedSeat.Id],"История 021","Э021",1);
            var archived=store.CreateHall(VenueLayouts019.RussianSong,"Архив",new[]{new DraftSeat{Sector="Экстрим партер",Row="1",Number="1"},new DraftSeat{Sector="Партер",Row="1",Number="1",Y=36}});store.DeleteVenue(archived);
            var reopened=new Store(store.Path);
            Check(reopened.VenueSeats(archived).Count==2,"archived hall geometry changed");
            Check(reopened.VenueSeats(venue).Count==1&&!reopened.Sectors(venue).Any(s=>s.Name=="Экстрим партер"),"retired sector still offered");
            Check(reopened.Seats(show).Count==1&&reopened.PlanPrices(plan).Count==1,"retired prices or seats remain active");
            Check(reopened.Invoice(show,id).Tickets.Single().SeatId==issuedSeat.Id,"history erased");
            Check(new Store(store.Path).VenueSeats(venue).Count==1,"sector returned after restart");
        });
        test("021 identical plan prices allow independent unique names",store=>{
            var venue=store.CreateHall("Планы021","",new[]{new DraftSeat()});var seat=store.VenueSeats(venue).Single();var category=store.PriceColors().First();
            var values=new[]{new PriceValue(seat.Id,category.Amount,category.Color)};
            var a=store.SavePlan(venue,"Первый",values,[]);var b=store.SavePlan(venue,"Второй",values,[]);
            Check(a!=b&&store.PlanPrices(a).SequenceEqual(store.PlanPrices(b)),"new plan wasn't independent");
        });
    }
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
}
