using Tickets.Core;
using Microsoft.Data.Sqlite;

internal static class Compact020Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("new hall diagrams use compact Taganka seat spacing and close sector gaps",_=>{
            foreach(var seats in new[]{VenueLayouts019.RussianSongSeats(),VenueLayouts019.EstradaSeats()})
            {
                foreach(var row in seats.Where(s=>s.Sector is "Партер" or "Амфитеатр" or "Бельэтаж").GroupBy(s=>(s.Sector,s.Row)))
                {
                    if(row.Key==("Партер","17")&&seats.Count==1002)continue;
                    var sorted=row.OrderBy(s=>s.X).ToArray();
                    for(var i=1;i<sorted.Length;i++)
                    {
                        var distance=sorted[i].X-sorted[i-1].X;
                        Require(distance>=17.99&&distance<=54.01,"seat spacing must be 18 with compact aisles: "+row.Key+" gap "+distance);
                    }
                }
                var boundsWidth=seats.Max(s=>s.X)-seats.Min(s=>s.X)+16;
                Require(boundsWidth<=1300,"diagram wastes horizontal space: "+boundsWidth);
                for(var i=0;i<seats.Count;i++)for(var j=i+1;j<seats.Count;j++)
                    Require(Math.Abs(seats[i].X-seats[j].X)>=15.99||Math.Abs(seats[i].Y-seats[j].Y)>=15.99,"overlapping compact seats");
            }
            var estrada=VenueLayouts019.EstradaSeats();
            var gap=estrada.Where(s=>s.Sector=="Бельэтаж").Min(s=>s.Y)-estrada.Where(s=>s.Sector=="Партер").Max(s=>s.Y)-16;
            Require(gap>=32&&gap<=44,"Estrada sector gap must be compact: "+gap);
        });
        test("020 existing source halls and copies compact in place without changing accounting",store=>{
            var raw=VenueLayouts019.RussianSongSeats(false);
            var venue=store.CreateHall("Сохранённый зал020","Адрес",raw.ToArray());
            var copy=store.CreateHall("Копия Эстрады020","Адрес",VenueLayouts019.EstradaSeats(false).ToArray());
            var customRaw=VenueLayouts019.RussianSongSeats(false);customRaw[0].X+=1;
            var custom=store.CreateHall("Изменённый вручную020","Адрес",customRaw.ToArray());
            var seats=store.VenueSeats(venue);var ids=seats.ToDictionary(Key,s=>s.Id);
            var plan=store.SavePlan(venue,"Расценовка020",seats.Select(s=>new PriceValue(s.Id,1234,"#123456")).ToArray());
            var show=store.CreateEvent(store.Performances().First().Id,venue,plan,new DateTime(2035,7,8,19,0,0));
            var issue=store.Issue(show,[seats[0].Id],"Получатель компакт020","К020",1);
            var invoice=store.Invoice(show,issue);var beforeCustom=store.VenueSeats(custom);
            var reopened=new Store(store.Path);
            Require(File.Exists(store.Path+".before-v020-compact-halls.db"),"compact migration backup missing");
            var expected=VenueLayouts019.RussianSongSeats().ToDictionary(Key);
            foreach(var seat in reopened.VenueSeats(venue))
            {
                Require(ids[Key(seat)]==seat.Id,"compacting replaced a seat identity");
                var target=expected[Key(seat)];Require(Math.Abs(target.X-seat.X)<.05&&Math.Abs(target.Y-seat.Y)<.05,"saved source hall was not compacted");
            }
            var expectedCopy=VenueLayouts019.EstradaSeats().ToDictionary(Key);
            Require(reopened.VenueSeats(copy).All(s=>Math.Abs(s.X-expectedCopy[Key(s)].X)<.05&&Math.Abs(s.Y-expectedCopy[Key(s)].Y)<.05),"copied source hall was not compacted");
            Require(reopened.VenueSeats(custom).SequenceEqual(beforeCustom),"manual coordinate edit was overwritten");
            Require(reopened.PlanPrices(plan).Count==seats.Count&&reopened.PlanPrices(plan).All(p=>p.Amount==1234&&p.Color=="#123456"),"plan prices changed during coordinate migration");
            Require(reopened.Invoice(show,issue)==invoice||reopened.Invoice(show,issue).Tickets.SequenceEqual(invoice.Tickets),"issued ticket data changed during coordinate migration");
            Require(reopened.Events().Any(e=>e.Id==show&&e.VenueId==venue)&&reopened.History(show).Any(t=>t.IssueId==issue),"event or ticket history changed");
            using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=store.Path,Pooling=false}.ToString()))
            {
                c.Open();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE seat SET x=x+3 WHERE id=$i";cmd.Parameters.AddWithValue("$i",seats[0].Id);cmd.ExecuteNonQuery();
            }
            var afterEdit=reopened.VenueSeats(venue).ToDictionary(s=>s.Id);
            Require(new Store(store.Path).VenueSeats(venue).All(s=>s.X==afterEdit[s.Id].X&&s.Y==afterEdit[s.Id].Y),"repeat migration overwrote a later manual edit");
        });
    }
    private static string Key(SeatInfo s)=>s.Sector+"|"+s.Row+"|"+s.Number;
    private static string Key(DraftSeat s)=>s.Sector+"|"+s.Row+"|"+s.Number;
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
