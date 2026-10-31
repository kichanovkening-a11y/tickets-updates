using Tickets.Core;

internal static class DocumentArchive044Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("044 document archive keeps mixed dates active and stored documents unchanged",store=>{
            var venue=store.CreateHall("Archive044","",[new DraftSeat{Number="1"}]);
            var seat=store.VenueSeats(venue).Single().Id;
            var plan=store.SavePlan(venue,"Archive044",[new PriceValue(seat,1000,"#FFFF00")]);
            var now=new DateTime(2035,5,15,12,0,0);
            var past=store.CreateEvent(store.SavePerformance("Archive past044"),venue,plan,now.AddDays(-2));
            var future=store.CreateEvent(store.SavePerformance("Archive future044"),venue,plan,now.AddDays(2));
            var a=store.Issue(past,[seat],"Archive044","ARCH044",1);
            var b=store.Issue(future,[seat],"Archive044","ARCH044",2);
            var mixed=store.CreateRecipientPackage("Archive044",[(past,a),(future,b)]);
            var old=store.CreateRecipientPackage("Archive044",[(past,a)]);
            var bytes=store.GetRecipientPackageDocument(mixed.Id,"invoice").SourceDocument!;
            var pastBytes=store.GetRecipientPackageDocument(old.Id,"invoice").SourceDocument!;
            var invoice=store.Invoice(past,a);
            var dates=store.DocumentArchive(now);
            Check(dates.Contains(old)&&!dates.Contains(mixed),"mixed-date document prematurely archived");
            Check(dates.Contains(past,invoice.EventDate)&&!dates.Contains(future,""),"individual dates classified incorrectly");
            Check(!store.DocumentArchive(now.AddDays(2)).Contains(mixed),"show archived at its exact start instead of after it");
            Check(store.DocumentArchive(now.AddDays(2).AddSeconds(1)).Contains(mixed),"all-past package missing from archive");
            var reopened=new Store(store.Path);
            Check(reopened.RecipientHistory("Archive044").Count==2&&reopened.Invoice(past,a).Tickets.Count==1,"past issue lost");
            Check(reopened.GetRecipientPackageDocument(mixed.Id,"invoice").SourceDocument!.SequenceEqual(bytes),"mixed document bytes changed");
            Check(reopened.GetRecipientPackageDocument(old.Id,"invoice").SourceDocument!.SequenceEqual(pastBytes),"archived document bytes changed");
            store.DeleteVenue(venue);
            Check(store.DocumentArchive(now).Contains(mixed),"all archived dates should archive package even before their start");
            Check(store.GetRecipientPackageDocument(mixed.Id,"invoice").SourceDocument!.SequenceEqual(bytes),"hidden catalog rewrote saved document");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
