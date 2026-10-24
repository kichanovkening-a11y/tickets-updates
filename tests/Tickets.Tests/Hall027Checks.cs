using Tickets.Core;
internal static class Hall027Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("027 structural hall edits require a distinct new hall name",store=>{
            var id=store.CreateHall("027 original","address",Draft());var changed=store.HallDraft(id);changed[0].X+=12;
            Reject(()=>store.SaveEditedHall(id," 027 ORIGINAL ","address",changed));
            var created=store.SaveEditedHall(id,"027 changed","address",changed);
            Check(created!=id&&store.HallDraft(id)[0].X==0&&store.HallDraft(created)[0].X==12,"structural save overwrote original");
            var hidden=store.HallDraft(id);hidden[0].Hidden=true;Reject(()=>store.SaveEditedHall(id,"027 original","address",hidden));
            var numbered=store.HallDraft(id);numbered[0].Number="9";Reject(()=>store.SaveEditedHall(id,"027 original","address",numbered));
            var removed=store.HallDraft(id);removed.RemoveAt(0);Reject(()=>store.SaveEditedHall(id,"027 original","address",removed));
        });
        run("027 sector rename preserves issued seat identities and history",store=>{
            var id=store.CreateHall("027 issued","",Draft());var before=store.VenueSeats(id);
            var plan=store.SavePlan(id,"027 prices",before.Select(s=>new PriceValue(s.Id,1000,"#FF0000")).ToArray());
            var evt=store.CreateEvent(store.SavePerformance("027 performance"),id,plan,new DateTime(2027,4,1,19,0,0));
            store.Issue(evt,new[]{before[0].Id},"027 recipient","H027",1);
            var draft=store.HallDraft(id);foreach(var seat in draft)seat.Sector="Новый партер";
            Check(store.SaveEditedHall(id,"027 issued","",draft)==id,"rename created another hall");
            var after=store.Seats(evt);Check(after.Select(s=>s.Id).Order().SequenceEqual(before.Select(s=>s.Id).Order()),"seat identities changed");
            Check(after.All(s=>s.Sector=="Новый партер")&&!after.Single(s=>s.Id==before[0].Id).Available,"issuance lost");
            Check(store.PlanPrices(plan).Select(s=>s.SeatId).Order().SequenceEqual(before.Select(s=>s.Id).Order()),"plan identities changed");
        });
        run("027 sector rename rejects merging sectors and draft row shift preserves identity",store=>{
            var seats=Draft();seats.Add(new(){Sector="Балкон",Row="2",Number="1",X=80,Y=60});
            var id=store.CreateHall("027 sectors","",seats);var draft=store.HallDraft(id);
            HallEditing027.RenameSector(draft,"Партер","Амфитеатр");Check(draft.Take(2).All(s=>s.Sector=="Амфитеатр"),"rename missed seats");
            Reject(()=>HallEditing027.RenameSector(draft,"Амфитеатр","Балкон"));
            draft[1].Hidden=true;var snapshot=draft.Select(s=>(s.Sector,s.Row,s.Number,s.X,s.Y,s.Hidden)).ToArray();HallEditing027.MoveRow(draft,"Амфитеатр","1",35);
            for(var i=0;i<draft.Count;i++){var s=draft[i];var old=snapshot[i];Check(s.Hidden==old.Hidden&&s.Y==old.Y&&s.Number==old.Number&&s.X==old.X+(s.Sector=="Амфитеатр"?35:0),"row shift changed another value");}
        });
        run("027 aisle labels survive uneven deletion and saving reopening",store=>{
            var draft=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=10,AisleAfter=5}},"1",false,0).ToList();
            var before=Anchors(draft).Where(a=>a.Centre).ToArray();
            HallEditing024.Delete(draft,draft.Where(s=>s.Number is "1" or "2" or "5" or "6" or "10").ToArray());
            Check(Anchors(draft).Where(a=>a.Centre).SequenceEqual(before),"aisle label drifted when its neighbors or uneven edges were removed");
            var id=store.CreateHall("027 persisted anchors","",draft);var reopened=new Store(store.Path);var loaded=reopened.HallDraft(id);
            Check(Anchors(loaded).Where(a=>a.Centre).SequenceEqual(before),"draft aisle metadata lost across reopening");
            Check(HallEditing025.RowAnchors(reopened.VenueSeats(id)).Where(a=>a.Centre).SequenceEqual(before),"venue aisle metadata lost");
            var plan=store.SavePlan(id,"027 anchor price",store.VenueSeats(id).Select(s=>new PriceValue(s.Id,1000,"#FF0000")).ToArray());
            var evt=store.CreateEvent(store.SavePerformance("027 anchors"),id,plan,new DateTime(2027,5,1,19,0,0));
            Check(HallEditing025.RowAnchors(reopened.Seats(evt)).Where(a=>a.Centre).SequenceEqual(before),"event aisle metadata lost");
        });
        run("027 adding another aisle after deletion preserves previous centres",_=>{
            var draft=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=10,AisleAfter=5}},"1",false,0).ToList();
            var original=Anchors(draft).Single(a=>a.Centre);HallEditing024.Delete(draft,draft.Where(s=>s.Number is "1" or "2" or "5" or "6").ToArray());
            HallEditing023.MakeAisle(draft,new[]{draft[^1]});var centres=Anchors(draft).Where(a=>a.Centre).ToArray();
            Check(centres.Length==2&&centres.Contains(original),"creating another aisle recomputed the existing centre after deleting its neighbors");
        });
        run("027 deleting seats never invents aisle labels",store=>{
            var draft=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=8}},"1",false,0).ToList();
            HallEditing024.Delete(draft,draft.Where(s=>s.Number is "3" or "4" or "5").ToArray());
            Check(draft.All(s=>s.RowAisleAnchors is {Count:0})&&!Anchors(draft).Any(a=>a.Centre),"deletion gap became an aisle label");
            var id=store.CreateHall("027 empty anchors","",draft);Check(!HallEditing025.RowAnchors(new Store(store.Path).VenueSeats(id)).Any(a=>a.Centre),"empty frozen aisle list lost on save");
        });
        run("027 explicit aisles refresh centres and row sector transformations carry them",_=>{
            var draft=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=8}},"1",false,0).ToList();HallAnchors027.Capture(draft);
            HallEditing023.MakeAisle(draft,new[]{draft[3]});var before=Anchors(draft).Where(a=>a.Centre).ToArray();Check(before.Length==1,"explicit aisle did not refresh frozen metadata");
            HallEditing027.RenameSector(draft,"Партер","Новый сектор");HallEditing027.MoveRow(draft,"Новый сектор","1",25);
            Check(Anchors(draft).Where(a=>a.Centre).SequenceEqual(before.Select(a=>a with{X=a.X+25})),"rename or row move lost aisle position");
            var cx=draft.Average(s=>s.X);var cy=draft.Average(s=>s.Y);var shifted=Anchors(draft).Where(a=>a.Centre).ToArray();HallEditing023.Rotate(draft,90);
            Check(Anchors(draft).Where(a=>a.Centre).Zip(shifted,(a,b)=>Math.Abs(a.X-(cx-(b.Y-cy)))<.001&&Math.Abs(a.Y-(cy+(b.X-cx)))<.001).All(x=>x),"sector rotation lost aisle position");
        });
    }
    private static IReadOnlyList<HallEditing025.RowAnchor> Anchors(IReadOnlyList<DraftSeat> draft)=>HallEditing025.RowAnchors(draft.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"",null,"","",null){RowAisleAnchors=s.RowAisleAnchors}).ToArray());
    private static List<DraftSeat> Draft()=>[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0},new(){Sector="Партер",Row="1",Number="2",X=20,Y=0}];
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
