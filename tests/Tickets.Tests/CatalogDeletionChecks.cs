using Tickets.Core;

internal static class CatalogDeletionChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("catalog deletions preserve issued history across reopen",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=3);
            var seats=s.Seats(e.Id).Where(x=>x.Available).Take(3).Select(x=>x.Id).ToArray();
            var recipient="Удаляемый получатель ёлка";
            Require(!s.IsEventArchived(e.Id),"active show marked archived");
            var issue=s.Issue(e.Id,new[]{seats[0]},recipient,"ARCHIVE",1);
            var performance=s.Performances().First(x=>CatalogText.Key(x.Title)==CatalogText.Key(e.Title));
            s.DeleteRecipient("  удаляемый получатель елка ");
            Require(!s.Recipients().Any(x=>CatalogText.Key(x)==CatalogText.Key(recipient)),"recipient still offered");
            Require(!s.RecipientDirectory().Any(x=>CatalogText.Key(x.Name)==CatalogText.Key(recipient)),"history resurrected recipient");
            Require(s.RecipientHistory(recipient).Single().Issue.Id==issue,"explicit recipient history lost");
            s.Issue(e.Id,new[]{seats[1]},recipient,"ARCHIVE",2);
            Require(s.Recipients().Any(x=>CatalogText.Key(x)==CatalogText.Key(recipient)),"explicit issuance did not restore recipient");
            s.DeleteRecipient(recipient);s.DeletePerformance(performance.Id);s.DeleteVenue(e.VenueId);
            var reopened=new Store(s.Path);
            Require(!reopened.Events().Any(x=>x.VenueId==e.VenueId||CatalogText.Key(x.Title)==CatalogText.Key(e.Title)),"archived shows remain selectable");
            Require(reopened.Events(true).Any(x=>x.Id==e.Id)&&reopened.IsEventArchived(e.Id),"archived show cannot be opened explicitly");
            var archivedRecipient=reopened.RecipientDirectory(true).Single(x=>CatalogText.Key(x.Name)==CatalogText.Key(recipient));
            Require(archivedRecipient.IsDeleted&&archivedRecipient.IssueCount==2,"archived recipient cannot be opened explicitly");
            Require(!reopened.Performances().Any(x=>x.Id==performance.Id)&&!reopened.Venues().Any(x=>x.Id==e.VenueId),"archive not persisted");
            Require(reopened.History(e.Id).Count==2&&reopened.Invoice(e.Id,issue).Tickets.Count==1,"issued audit lost");
            Require(reopened.RecipientHistory(recipient).Count==2,"recipient audit lost");
            Reject(()=>reopened.Issue(e.Id,new[]{seats[2]},recipient,"ARCHIVE",3));
            var ticket=reopened.History(e.Id).First().Id;reopened.Return(new[]{ticket});
            Require(reopened.History(e.Id).Single(x=>x.Id==ticket).ReturnedAt!=null,"archived event return failed");
        });
        test("archived catalog IDs cannot create shows or pricing plans",s=>{
            var venue=s.CreateHall("Удаляемый зал","Адрес",new[]{new DraftSeat()});
            var seat=s.VenueSeats(venue).Single().Id;
            var price=new[]{new PriceValue(seat,100,"#123456")};
            var plan=s.SavePlan(venue,"Удаляемая расценовка",price);
            var performance=s.SavePerformance("Удаляемый спектакль");
            s.DeletePlan(plan);
            Require(!s.Plans(venue).Any(x=>x.Id==plan)&&!s.AllPlans().Any(x=>x.Id==plan),"plan still offered");
            Reject(()=>s.CreateEvent(performance,venue,plan,new DateTime(2030,1,1)));
            var activePlan=s.SavePlan(venue,"Другая расценовка",price);
            s.DeletePerformance(performance);Reject(()=>s.CreateEvent(performance,venue,activePlan,new DateTime(2030,1,2)));
            s.SavePerformance("Удаляемый спектакль");
            s.DeleteVenue(venue);Reject(()=>s.CreateEvent(performance,venue,activePlan,new DateTime(2030,1,3)));
            Reject(()=>s.SavePlan(venue,"Ещё расценовка",price));
            Require(s.Plans(venue).Count==0&&!s.AllPlans().Any(x=>x.VenueId==venue),"plans of archived venue offered");
            var unusedRecipient=s.RecipientDirectory().First(x=>x.IssueCount==0).Name;
            s.DeleteRecipient(unusedRecipient);
            Require(s.RecipientDirectory(true).Single(x=>x.Name==unusedRecipient).IsDeleted,"unused archived recipient omitted");
            Reject(()=>s.DeleteVenue(long.MaxValue));Reject(()=>s.DeletePerformance(long.MaxValue));
            Reject(()=>s.DeletePlan(long.MaxValue));Reject(()=>s.DeleteRecipient("Несуществующий получатель"));
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
