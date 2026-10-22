using Tickets.Core;
using Microsoft.Data.Sqlite;

internal static class Hall022Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("Estrada row 17 stays a contiguous numbered row with all genuine seats",_=>{
            var all=VenueLayouts019.EstradaSeats();var row=all.Where(s=>s.Sector=="Партер"&&s.Row=="17").OrderBy(s=>s.X).ToArray();
            Require(row.Length==40&&row.Select(s=>s.Number).SequenceEqual(Enumerable.Range(1,40).Select(n=>n.ToString())),"row 17 identities changed");
            var gaps=row.Zip(row.Skip(1),(a,b)=>b.X-a.X).ToArray();Require(gaps.Count(g=>Math.Abs(g-18)<.01)==38&&gaps.Count(g=>Math.Abs(g-54)<.01)==1,"row 17 is scattered around other rows");
        });
        test("022 Estrada migration preserves seat IDs prices tickets and custom coordinates",store=>{
            var venue=store.CreateHall("Эстрада схема022","",VenueLayouts019.EstradaSeats(legacyPerimeter:true).ToArray());
            var old=store.VenueSeats(venue).ToDictionary(s=>s.Id);var category=store.PriceColors().First();
            var plan=store.SavePlan(venue,"План022",old.Values.Select(s=>new PriceValue(s.Id,category.Amount,category.Color)).ToArray());
            var show=store.CreateEvent(store.SavePerformance("Тест схемы022"),venue,plan,new DateTime(2097,1,1));
            var issue=store.Issue(show,new[]{old.Keys.First()},"Оля022","H022",1);
            var custom=store.CreateHall("Ручная схема022","",VenueLayouts019.EstradaSeats(legacyPerimeter:true).ToArray());
            using(var c=new SqliteConnection("Data Source="+store.Path)){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE seat SET x=x+1 WHERE venue_id=$v AND id=(SELECT min(id) FROM seat WHERE venue_id=$v)";cmd.Parameters.AddWithValue("$v",custom);cmd.ExecuteNonQuery();}
            var customBefore=store.VenueSeats(custom);
            var reopened=new Store(store.Path);var expected=VenueLayouts019.EstradaSeats().ToDictionary(s=>(s.Sector,s.Row,s.Number));
            Require(reopened.VenueSeats(venue).All(s=>old.ContainsKey(s.Id)&&expected[(s.Sector,s.Row,s.Number)].X==s.X&&expected[(s.Sector,s.Row,s.Number)].Y==s.Y),"corrected geometry failed or IDs replaced");
            Require(reopened.PlanPrices(plan).Count==1002&&reopened.Invoice(show,issue).Tickets.Count==1,"accounting changed");
            Require(reopened.VenueSeats(custom).SequenceEqual(customBefore),"custom hall overwritten");
        });
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
