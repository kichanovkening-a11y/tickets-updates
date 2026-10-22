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
            s.DeleteRecipient(recipient);s.DeleteVenue(e.VenueId);
            var reopened=new Store(s.Path);
            Require(!reopened.Events().Any(x=>x.VenueId==e.VenueId),"archived shows remain selectable");
            Require(reopened.Events(true).Any(x=>x.Id==e.Id)&&reopened.IsEventArchived(e.Id),"archived show cannot be opened explicitly");
            var archivedRecipient=reopened.RecipientDirectory(true).Single(x=>CatalogText.Key(x.Name)==CatalogText.Key(recipient));
            Require(archivedRecipient.IsDeleted&&archivedRecipient.IssueCount==2,"archived recipient cannot be opened explicitly");
            Require(reopened.Performances().Any(x=>x.Id==performance.Id)&&!reopened.Venues().Any(x=>x.Id==e.VenueId),"archive not persisted");
            Require(reopened.History(e.Id).Count==2&&reopened.Invoice(e.Id,issue).Tickets.Count==1,"issued audit lost");
            Require(reopened.RecipientHistory(recipient).Count==2,"recipient audit lost");
            Reject(()=>reopened.Issue(e.Id,new[]{seats[2]},recipient,"ARCHIVE",3));
            var ticket=reopened.History(e.Id).First().Id;reopened.Return(new[]{ticket});
            Require(reopened.History(e.Id).Single(x=>x.Id==ticket).ReturnedAt!=null,"archived event return failed");
        });
        test("deleting performance removes shows issues documents and preserves global invoice numbers",s=>{
            var venue=s.CreateHall("Зал удаления", "Адрес",new[]{new DraftSeat(),new DraftSeat{Number="2",X=20}});
            var places=s.VenueSeats(venue).Select(x=>x.Id).ToArray();
            var plan=s.SavePlan(venue,"Расценовка удаления",places.Select(x=>new PriceValue(x,1000,"#123456")).ToArray());
            var performance=s.SavePerformance("Полное удаление спектакля");
            var e=s.CreateEvent(performance,venue,plan,new DateTime(2098,1,1,19,0,0));
            var e2=s.CreateEvent(performance,venue,plan,new DateTime(2098,1,2,19,0,0));
            var other=s.SavePerformance("Сохраняемый спектакль");
            var keep=s.CreateEvent(other,venue,plan,new DateTime(2098,1,3,19,0,0));
            var keepIssue=s.Issue(keep,new[]{places[0]},"Получатель удаления","DEL-KEEP",1);
            var issue=s.Issue(e,places,"Получатель удаления","DEL",1);
            s.PrepareIssueDocuments(e,issue);var document=s.SaveInvoiceDocument(e,issue);
            s.Return(new[]{s.History(e).First().Id});
            var second=s.Issue(e2,new[]{places[0]},"Получатель удаления","DEL-2",1);
            s.SetSeatsBlocked(e2,new[]{places[1]},true);
            var stats=s.PerformanceDeletionSummary(performance);
            Require(stats.Shows==2&&stats.Issues==2&&stats.Tickets==3,"deletion confirmation counts are incomplete");
            var journal=Path.Combine(Path.GetDirectoryName(s.Path)!,"journal-columns-by-event.json");
            File.WriteAllText(journal,System.Text.Json.JsonSerializer.Serialize(new Dictionary<long,int>{{e,180},{e2,190},{keep,237}}));
            s.DeletePerformance(performance);var reopened=new Store(s.Path);
            using(var layouts=System.Text.Json.JsonDocument.Parse(File.ReadAllText(journal)))Require(!layouts.RootElement.TryGetProperty(e.ToString(),out _)&&!layouts.RootElement.TryGetProperty(e2.ToString(),out _)&&layouts.RootElement.GetProperty(keep.ToString()).GetInt32()==237,"deleted show widths remain or unrelated widths changed");
            Require(!reopened.Events(true).Any(x=>x.Id==e||x.Id==e2),"deleted shows remain in archive");
            Require(!reopened.Performances().Any(x=>x.Id==performance)&&reopened.History(e).Count==0&&reopened.Issues(e).Count==0,"deleted issuance data remains");
            Reject(()=>reopened.Invoice(e,issue));
            Require(!File.Exists(document),"managed deleted invoice remains in working documents");
            Require(Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(s.Path)!,"Backups"),"delete-performance-*").Any(),"deletion backup absent");
            Require(reopened.Invoice(keep,keepIssue).Tickets.Count==1&&reopened.Plans(venue).Any(x=>x.Id==plan),"unrelated show or shared plan damaged");
            Require(reopened.Recipients().Contains("Получатель удаления")&&reopened.Venues().Any(x=>x.Id==venue),"shared directory damaged");
            var next=reopened.Issue(keep,new[]{places[1]},"Получатель удаления","DEL-KEEP",2);
            Require(next>second,"global invoice number reused after deleting highest invoice");
            using var c=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+s.Path);c.Open();using var fk=c.CreateCommand();fk.CommandText="PRAGMA foreign_key_check";using var r=fk.ExecuteReader();Require(!r.Read(),"dangling foreign key after deletion");
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
