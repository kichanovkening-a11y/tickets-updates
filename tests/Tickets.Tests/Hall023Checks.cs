using Tickets.Core;
internal static class Hall023Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("023 sector transforms preserve labels and persist coordinates",store=>{
            var seats=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=5},new HallRowLayout{Label="2",SeatCount=5}},"1",false,0).ToList();
            var keys=seats.Select(s=>(s.Sector,s.Row,s.Number)).ToArray();
            HallEditing023.Rotate(seats,90);HallEditing023.Move(seats,40,70);
            if(!keys.SequenceEqual(seats.Select(s=>(s.Sector,s.Row,s.Number))))throw new Exception("Renumbered transform");
            var id=store.CreateHall("023 "+Guid.NewGuid(),"",seats);var saved=store.HallDraft(id);
            if(saved.Count!=10||saved.Any(s=>!seats.Any(x=>x.Row==s.Row&&x.Number==s.Number&&Math.Abs(x.X-s.X)<.001&&Math.Abs(x.Y-s.Y)<.001)))throw new Exception("Transform lost on save");
            var row=seats.Where(s=>s.Row=="1").ToArray();var numbers=row.Select(s=>s.Number).ToArray();
            HallEditing023.MakeAisle(seats,row.Where(s=>s.Number=="3").ToArray());
            if(!numbers.SequenceEqual(row.Select(s=>s.Number)))throw new Exception("Aisle renumbered seats");
        });
        run("023 Estrada omitted seats are display placeholders only",_=>{
            var actual=VenueLayouts019.EstradaSeats().Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,1000,"#F00",null,"","",null)).ToArray();
            var shown=HallEditing023.WithEstradaPlaceholders(actual);
            if(shown.Count!=actual.Length+9||shown.Count(s=>s.Id<0)!=9||shown.Any(s=>s.Id<0&&s.Available))throw new Exception("Inactive missing seats");
            if(!shown.Any(s=>s.Id<0&&s.Row=="2"&&s.Number=="12")||!shown.Any(s=>s.Id<0&&s.Row=="2"&&s.Number=="13"))throw new Exception("Missing row2 labels");
            if(HallEditing023.WithEstradaPlaceholders(actual.Take(10).ToArray()).Count!=10)throw new Exception("Other halls modified");
        });
    }
}
