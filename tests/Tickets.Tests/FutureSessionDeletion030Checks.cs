using Tickets.Core;
internal static class FutureSessionDeletion030Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("030 delete one future session retains sibling performance hall pricing and recoverable history",store=>{
            var venue=store.CreateHall("Future deletion030 "+Guid.NewGuid(),"",[new(){Sector="Партер",Row="1",Number="1",X=12,Y=34}]);
            var seat=store.VenueSeats(venue).Single().Id;
            var plan=store.SavePlan(venue,"Future030",[new(seat,700,"#FF0000")]);
            var performance=store.SavePerformance("Future deletion030 "+Guid.NewGuid());
            var target=store.CreateEvent(performance,venue,plan,DateTime.Today.AddYears(2));
            var sibling=store.CreateEvent(performance,venue,plan,DateTime.Today.AddYears(2).AddDays(1));
            var issue=store.Issue(target,[seat],"Future recipient030","F030",1);
            store.Return([store.History(target).Single().Id]);
            var document=store.SaveInvoiceDocument(target,issue);var bytes=File.ReadAllBytes(document);
            store.DeleteEventsPermanently([target]);
            var reopened=new Store(store.Path);
            if(reopened.Events(true).Any(e=>e.Id==target)||!reopened.Events().Any(e=>e.Id==sibling)||!reopened.Performances().Any(p=>p.Id==performance))throw new Exception("One-date deletion removed sibling or performance");
            if(reopened.Issues(target).Count!=0||reopened.History(target).Count!=0||File.Exists(document))throw new Exception("Deleted session history/documents remain active");
            if(reopened.VenueSeats(venue).Single().Id!=seat||!reopened.Plans(venue).Any(p=>p.Id==plan)||reopened.Seats(sibling).Single().Amount!=700)throw new Exception("Shared hall/pricing changed");
            var backup=Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(store.Path)!,"Backups"),"tickets.db",SearchOption.AllDirectories).Single();
            var restored=new Store(backup);
            if(!restored.Events().Any(e=>e.Id==target)||restored.History(target).Count!=1||restored.History(target).Single().ReturnedAt is null)throw new Exception("Backup cannot recover deleted future session and returned history");
            var saved=Directory.EnumerateFiles(Path.GetDirectoryName(backup)!,Path.GetFileName(document),SearchOption.AllDirectories).Single();
            if(!File.ReadAllBytes(saved).SequenceEqual(bytes))throw new Exception("Backup lost invoice document");
        });
    }
}
