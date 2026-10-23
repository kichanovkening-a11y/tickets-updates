using Tickets.Core;
using Microsoft.Data.Sqlite;
internal static class Cleanup023Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("023 purge removes shared documents tied to deleted shows",store=>{
            var venue=store.CreateHall("Cleanup package023 "+Guid.NewGuid(),"",new[]{new DraftSeat()});
            var seat=store.VenueSeats(venue).Single();var plan=store.SavePlan(venue,"Cleanup023",new[]{new PriceValue(seat.Id,1000,"#FF0000")});
            var performance=store.SavePerformance("Cleanup doc023 "+Guid.NewGuid());var showing=store.CreateEvent(performance,venue,plan,new DateTime(2098,1,1));
            store.Issue(showing,new[]{seat.Id},"Cleanup recipient023","CL23",1);
            var issue=store.Issues(showing).Single();var package=store.CreateRecipientPackage("Cleanup recipient023",new[]{(showing,issue.Id)});
            store.GetRecipientPackageDocument(package.Id,"invoice");store.DeleteEventsPermanently(new[]{showing});
            if(store.RecipientPackages("Cleanup recipient023").Count!=0)throw new Exception("Shared deleted-show documents remain");
        });
        run("023 permanent event cleanup preserves all halls and active shows",store=>{
            var all=store.Events(true);var target=all[0];var other=all.First(e=>e.Id!=target.Id);
            var hall=store.HallDraft(target.VenueId).Select(s=>(s.Sector,s.Row,s.Number,s.X,s.Y)).ToArray();
            var plans=store.Plans(target.VenueId).Select(p=>p.Id).ToArray();
            store.DeleteEventsPermanently(new[]{target.Id});
            var reopened=new Store(store.Path);
            if(reopened.Events(true).Any(e=>e.Id==target.Id)||!reopened.Events(true).Any(e=>e.Id==other.Id))throw new Exception("Wrong event deletion");
            if(!hall.SequenceEqual(reopened.HallDraft(target.VenueId).Select(s=>(s.Sector,s.Row,s.Number,s.X,s.Y))))throw new Exception("Hall geometry removed");
            if(!plans.SequenceEqual(reopened.Plans(target.VenueId).Select(p=>p.Id)))throw new Exception("Plans removed");
            using var c=new SqliteConnection("Data Source="+store.Path);c.Open();using var q=c.CreateCommand();q.CommandText="PRAGMA foreign_key_check";using var r=q.ExecuteReader();if(r.Read())throw new Exception("Broken FK");
            if(!Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(store.Path)!,"Backups"),"tickets.db",SearchOption.AllDirectories).Any())throw new Exception("No backup");
        });
    }
}
