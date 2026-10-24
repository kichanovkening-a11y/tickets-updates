using Tickets.Core;
internal static class HallCaption029Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("029 sector caption metadata preserves issued hall and seat identity",store=>{
            var draft=new[]{new DraftSeat{Sector="Балкон длинное название",Row="1",Number="1",X=30,Y=40},new DraftSeat{Sector="Балкон длинное название",Row="1",Number="2",X=50,Y=40}};
            var id=store.CreateHall("029 caption used","",draft);var before=store.VenueSeats(id);
            var plan=store.SavePlan(id,"029 caption prices",before.Select(s=>new PriceValue(s.Id,500,"#ABCDEF")).ToArray());
            var evt=store.CreateEvent(store.SavePerformance("029 caption show"),id,plan,new DateTime(2027,6,1,19,0,0));store.Issue(evt,[before[0].Id],"029 caption recipient","C029",1);
            var edits=store.HallDraft(id);var caption=new SectorCaption029("Балкон\nдлинное название",-120,-55,37);
            foreach(var seat in edits)seat.SectorCaption=caption;
            Check(store.SaveEditedHall(id,"029 caption used","",edits)==id,"caption created another hall");
            var reopened=new Store(store.Path);Check(reopened.HallDraft(id).All(s=>s.SectorCaption==caption),"draft lost caption");
            Check(reopened.Sectors(id).Single().Caption==caption,"render metadata lost caption");
            Check(reopened.VenueSeats(id).SequenceEqual(before),"caption changed seat records");
            Check(!reopened.Seats(evt).Single(s=>s.Id==before[0].Id).Available,"caption changed issuance");
            var copy=store.CreateHall("029 caption copied","",reopened.HallDraft(id));Check(store.Sectors(copy).Single().Caption==caption,"copied hall lost caption");
            var rename=reopened.HallDraft(id);HallEditing027.RenameSector(rename,"Балкон длинное название","Новый балкон");store.SaveEditedHall(id,"029 caption used","",rename);
            Check(store.Sectors(id).Single().Caption==caption,"sector rename lost caption placement");
            var geometry=store.HallDraft(id);geometry[0].X++;Reject(()=>store.SaveEditedHall(id,"029 caption used","",geometry));
        });
        run("029 default captions and invalid metadata",store=>{
            var draft=new[]{new DraftSeat{Sector="Партер",Row="1",Number="1"}};var id=store.CreateHall("029 caption default","",draft);
            Check(store.HallDraft(id).Single().SectorCaption is null&&store.Sectors(id).Single().Caption is null,"default acquired override");
            foreach(var caption in new[]{new SectorCaption029("caption",double.NaN,0,0),new SectorCaption029("caption",0,0,double.PositiveInfinity),new SectorCaption029("one\ntwo\nthree",0,0,0),new SectorCaption029("bad\ttext",0,0,0)})
            {var edits=store.HallDraft(id);edits[0].SectorCaption=caption;Reject(()=>store.SaveEditedHall(id,"029 caption default","",edits));}
            Check(store.Sectors(id).Single().Caption is null,"invalid caption committed");
        });
    }
    private static void Reject(Action action){try{action();throw new Exception("Expected rejection.");}catch(TicketException){}}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
