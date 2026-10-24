using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class DeletedVenue028Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        foreach(var (id,name) in new[]{(14L,"\"Московский государственный театр Эстрады\""),(25L,"Театр «Русская Песня»")})
        run($"028 deleted pending imported hall {id} stays deleted on startup",store=>{
            // Reproduce a database upgraded from before 019: exact imported hall identity,
            // existing history, no migration marker, then an explicit catalogue deletion.
            using(var c=new SqliteConnection($"Data Source={store.Path}"))
            {
                c.Open();using var q=c.CreateCommand();
                q.CommandText="DELETE FROM hall_layout_migration_019 WHERE old_venue_id=$id; UPDATE venue SET name=$name WHERE id=$id; DELETE FROM catalog_hidden WHERE kind='venue' AND entity_key=CAST($id AS TEXT)";
                q.Parameters.AddWithValue("$name",name);q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();
            }
            Check(store.Venues().Any(v=>v.Id==id&&v.Name==name),"pending imported hall fixture is not active");
            store.DeleteVenue(id);
            var before=Counts(store.Path);var reopened=new Store(store.Path);
            Check(Counts(store.Path)==before,"startup recreated a deleted hall or changed retained historical records");
            Check(!reopened.Venues().Any(v=>v.Id==id),"deleted imported hall became active");
            Check(reopened.HallReplacements019.Count==0,"deleted hall was migrated to a visible replacement");
        });
        run("028 normal hall deletion survives reopening with history",store=>{
            var venue=store.CreateHall("028 deleted hall","",new[]{new DraftSeat{Sector="Партер",Row="1",Number="1",X=0,Y=0}});
            var seat=store.VenueSeats(venue).Single().Id;
            var plan=store.SavePlan(venue,"028 price",new[]{new PriceValue(seat,500,"#FF0000")});
            var evt=store.CreateEvent(store.SavePerformance("028 retained history"),venue,plan,DateTime.Today.AddDays(20));
            var issue=store.Issue(evt,new[]{seat},"028 recipient","DEL028",1);
            store.DeleteVenue(venue);var before=Counts(store.Path);var reopened=new Store(store.Path);
            Check(!reopened.Venues().Any(v=>v.Id==venue),"deleted hall became active after reopening");
            Check(Counts(store.Path)==before&&reopened.History(evt).Single(t=>t.IssueId==issue).SeatId==seat,"deletion or reopening removed historical data");
            Check(reopened.Events(true).Any(e=>e.Id==evt)&&reopened.IsEventArchived(evt),"historical show was lost or reactivated");
        });
    }
    private static string Counts(string path)
    {
        using var c=new SqliteConnection($"Data Source={path}");c.Open();using var q=c.CreateCommand();
        q.CommandText="SELECT (SELECT count(*) FROM venue)||':'||(SELECT count(*) FROM event)||':'||(SELECT count(*) FROM issue_batch)||':'||(SELECT count(*) FROM ticket)||':'||(SELECT count(*) FROM seat)";
        return (string)q.ExecuteScalar()!;
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
