using Tickets.Core;
using Microsoft.Data.Sqlite;
internal static class Hall024Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("024 delete renumbers while preserving exact row geometry",_=>{
            var seats=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=6},new HallRowLayout{Label="2",SeatCount=6}},"1",true,0).ToList();
            HallEditing023.Rotate(seats,17);var kept=seats.Where(s=>s.Row!="1"||s.Number is not ("2" or "4")).ToDictionary(s=>s,s=>(s.X,s.Y));
            HallEditing024.Delete(seats,seats.Where(s=>s.Row=="1"&&s.Number is "2" or "4").ToArray());
            if(seats.Count!=10||seats.Any(s=>kept[s]!=(s.X,s.Y)))throw new Exception("Seats moved");
            if(!seats.Where(s=>s.Row=="1").Select(s=>s.Number).Order().SequenceEqual(new[]{"1","2","3","4"}))throw new Exception("Numbering has gaps");
            if(!seats.Where(s=>s.Row=="2").Select(s=>s.Number).SequenceEqual(new[]{"6","5","4","3","2","1"}))throw new Exception("Unselected row changed");
            var letters=HallLayout.Generate("Ложа",new[]{new HallRowLayout{Label="А",SeatCount=4}},"А",false,0).ToList();HallEditing024.Delete(letters,new[]{letters[1]});
            if(!letters.Select(s=>s.Number).SequenceEqual(new[]{"А","Б","В"})||letters[1].X!=40)throw new Exception("Letter deletion");
        });
        run("024 custom labels delete without moving or losing prefix",_=>{
            var seats=Enumerable.Range(1,4).Select(n=>new DraftSeat{Sector="VIP",Row="A",Number="VIP-"+n,X=n*20}).ToList();
            HallEditing024.Delete(seats,new[]{seats[1]});
            if(!seats.Select(s=>s.Number).SequenceEqual(new[]{"VIP-1","VIP-2","VIP-3"})||seats[1].X!=60)throw new Exception("Custom deletion failed");
        });
        run("024 mixed row aisle respects saved row order through rotation",store=>{
            var rows=new[]{"А","1","2"}.Select(label=>new HallRowLayout{Label=label,SeatCount=3}).ToArray();
            foreach(var angle in new[]{0d,90d,180d,270d})
            {
                var draft=HallLayout.Generate("Партер",rows,"1",true,0).ToList();HallEditing023.Rotate(draft,angle);
                var venue=store.CreateHall("024 mixed "+angle,"",draft);var seats=store.HallDraft(venue);var old=seats.ToDictionary(s=>s,s=>(s.X,s.Y));
                HallEditing024.MakeRowAisle(seats,new[]{seats.First(s=>s.Row=="1")},30);
                var dx=-Math.Sin(angle*Math.PI/180)*30;var dy=Math.Cos(angle*Math.PI/180)*30;
                foreach(var seat in seats){var move=seat.Row=="2";if(Math.Abs(seat.X-old[seat].X-(move?dx:0))>.001||Math.Abs(seat.Y-old[seat].Y-(move?dy:0))>.001)throw new Exception("Wrong mixed row moved at "+angle);}
            }
        });
        run("024 hide persists labels geometry and excludes sellable seats",store=>{
            var draft=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=5}},"1",false,0).ToList();draft[1].Hidden=true;draft[3].Hidden=true;
            var venue=store.CreateHall("024 hidden", "",draft);var loaded=new Store(store.Path).HallDraft(venue);
            if(loaded.Count!=5||loaded.Count(s=>s.Hidden)!=2||loaded.Any(s=>s.X!=draft.Single(p=>p.Number==s.Number).X))throw new Exception("Hidden layout lost");
            if(!store.VenueSeats(venue).Select(s=>s.Number).SequenceEqual(new[]{"1","3","5"}))throw new Exception("Hidden seat visible");
            using var c=new SqliteConnection($"Data Source={store.Path};Pooling=false");c.Open();using var cmd=c.CreateCommand();cmd.CommandText=$"SELECT id FROM seat WHERE venue_id={venue} AND number='2'";var hidden=Convert.ToInt64(cmd.ExecuteScalar());
            var evt=store.CreateEvent(store.SavePerformance("024 hidden show"),venue,store.SavePlan(venue,"024 plan",store.VenueSeats(venue).Select(s=>new PriceValue(s.Id,1000,"#FF0000")).ToArray()),new DateTime(2027,1,1,19,0,0));
            cmd.CommandText=$"INSERT INTO event_price(event_id,seat_id,amount,color) VALUES({evt},{hidden},1000,'#FF0000')";cmd.ExecuteNonQuery();
            try{store.Issue(evt,new[]{hidden},"024 recipient","H024",1);throw new Exception("Hidden seat issued");}catch(TicketException){}
            if(store.Seats(evt).Count!=3||store.Events().Single(e=>e.Id==evt).SeatCount!=3)throw new Exception("Hidden counted");
        });
        run("024 row aisle follows rotated sector and preserves other sector",_=>{
            var seats=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=3},new HallRowLayout{Label="2",SeatCount=3},new HallRowLayout{Label="3",SeatCount=3}},"1",false,0).ToList();HallEditing023.Rotate(seats,90);
            seats.Add(new(){Sector="Балкон",Row="1",Number="1",X=500,Y=500});var old=seats.ToDictionary(s=>s,s=>(s.X,s.Y));
            HallEditing024.MakeRowAisle(seats,new[]{seats[0]},30);
            foreach(var s in seats){var moved=s.Sector=="Партер"&&s.Row!="1";if(Math.Abs(s.X-old[s].X-(moved?-30:0))>.001||Math.Abs(s.Y-old[s].Y)>.001)throw new Exception("Wrong row aisle geometry");}
        });
    }
}
