using Tickets.Core;

internal static class Hall012Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("row aisles coexist with seat aisles and preserve mixed reversed labels after save",store=>{
            var rows=new[]{
                new HallRowLayout{Label="А",SeatCount=3,AisleAfter=1,RowGapAfter=36},
                new HallRowLayout{Label="1",SeatCount=2,FirstSeat="Б, 4",RowGapAfter=12.5},
                new HallRowLayout{Label="2",SeatCount=1}};
            var seats=HallLayout.Generate("Партер",rows,"А, 1",true,26).ToList();
            Require(seats.Select(s=>s.Number).SequenceEqual(new[]{"2","1","А","4","Б","А"}),"Mixed and reverse labels must not depend on gaps");
            Require(seats[1].X-seats[0].X==56,"Existing horizontal aisle remains available");
            Require(seats[3].Y==84&&seats[5].Y==118.5,"Each row adds its own extra gap to later rows");
            seats.RemoveAt(1);
            var hall=store.CreateHall("Hall 0.12 row gaps","",seats);
            var saved=store.HallDraft(hall);
            Require(saved.Count==seats.Count,"Removed seats must stay removed");
            foreach(var seat in seats)
                Require(saved.Any(s=>s.Sector==seat.Sector&&s.Row==seat.Row&&s.Number==seat.Number&&s.X==seat.X&&s.Y==seat.Y),"Saving must retain exact labels and coordinates, including fractional gaps");
        });
        test("row aisle validation rejects invalid spacing without changing default layout",_=>{
            foreach(var gap in new[]{-1d,double.NaN,double.PositiveInfinity,1001d})
            {
                try{HallLayout.Generate("Партер",new[]{new HallRowLayout{RowGapAfter=gap}},"1",false,0);throw new Exception("Invalid row gap accepted");}
                catch(TicketException){}
            }
            var seats=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=1},new HallRowLayout{Label="2",SeatCount=1}},"1",false,26);
            Require(seats[0].Y==26&&seats[1].Y==48,"Existing halls retain their original row pitch");
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
