using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class ArchivePricingChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("deleted imported show pricing remains reusable after source removal",s=>{
            var source=s.Events().First(e=>s.Seats(e.Id).Count(x=>x.Amount.HasValue)>=2);
            var before=s.Seats(source.Id).Where(x=>x.Amount.HasValue).Select(x=>new PriceValue(x.Id,x.Amount!.Value,x.Color)).OrderBy(x=>x.SeatId).ToArray();
            var performance=s.Performances().First(p=>CatalogText.Key(p.Title)==CatalogText.Key(source.Title));
            var template=s.GetOrCreateEventPlan(source.Id);
            s.DeletePerformance(performance.Id);
            Require(!s.Events(true).Any(e=>e.Id==source.Id),"deleted source show remains");
            Require(s.Plans(source.VenueId).Any(p=>p.Id==template),"archived source template not offered");
            var copied=s.CopyPlanPrices(template,source.VenueId).OrderBy(x=>x.SeatId).ToArray();
            Require(copied.SequenceEqual(before),"archived prices or category colors changed");
            var newPerformance=s.SavePerformance("Новый спектакль с архивными ценами");
            var created=s.CreateEvent(newPerformance,source.VenueId,template,new DateTime(2099,1,1,19,0,0));
            Require(s.Seats(created).Where(x=>x.Amount.HasValue).OrderBy(x=>x.Id).Select(x=>x.Amount).SequenceEqual(before.Select(x=>(long?)x.Amount)),"new showing did not copy archive prices");
            var edited=copied.Select((p,i)=>i==0?p with{Amount=123456789,Color="#123456"}:p).ToArray();
            s.SavePlan(source.VenueId,"Изменённая копия архивных цен",edited);
            Require(!s.Events(true).Any(e=>e.Id==source.Id),"deleted showing was resurrected by copying prices");
            Require(s.PlanPrices(template).OrderBy(x=>x.SeatId).SequenceEqual(before),"saving edited copy changed source template");
        });
        test("event templates deduplicate exact seat patterns and respect deletion across reopen",s=>{
            var venue=s.CreateHall("Архивные варианты цен","",new[]{new DraftSeat{Number="1"},new DraftSeat{Number="2",X=20},new DraftSeat{Number="3",X=40}});
            var seats=s.VenueSeats(venue).Select(x=>x.Id).ToArray();
            var prices=new[]{new PriceValue(seats[0],1000,"#123456"),new PriceValue(seats[1],2000,"#654321")};
            var first=Source(s,venue,"Архивный импорт 1",1,prices);
            var second=Source(s,venue,"Архивный импорт 2",2,prices);
            var different=Source(s,venue,"Другие места",3,new[]{prices[0],prices[1] with{SeatId=seats[2]}});
            var partial=Source(s,venue,"Неполные цены",4,new[]{prices[0]});
            var saved=s.SavePlan(venue,"Готовая расценовка",prices);
            var plans=s.Plans(venue);
            Require(plans.Count==3,"identical source patterns created duplicate templates or different assignments merged");
            Require(s.GetOrCreateEventPlan(first)==saved&&s.GetOrCreateEventPlan(second)==saved,"existing matching plan was not reused");
            Require(s.GetOrCreateEventPlan(different)!=saved&&s.GetOrCreateEventPlan(partial)!=saved,"different seat patterns merged");
            Require(s.Plans(venue).Count==3,"repeated listing created duplicate templates");
            s.DeletePlan(saved);
            var reopened=new Store(s.Path);
            Require(reopened.GetOrCreateEventPlan(first)==saved,"deleted source mapping was discarded");
            Require(reopened.Plans(venue).Count==2&&!reopened.AllPlans().Any(p=>p.Id==saved),"deleted plan resurrected from prior show");
        });
    }

    private static long Source(Store store,long venue,string title,int day,IReadOnlyList<PriceValue> prices)
    {
        using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=store.Path,Pooling=false}.ToString());c.Open();
        using var tx=c.BeginTransaction();
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="INSERT INTO event(venue_id,title,starts_at,legacy_date) VALUES($v,$t,$d,$d) RETURNING id";
        cmd.Parameters.AddWithValue("$v",venue);cmd.Parameters.AddWithValue("$t",title);cmd.Parameters.AddWithValue("$d",$"2000-01-{day:00}T19:00:00");
        var id=Convert.ToInt64(cmd.ExecuteScalar());
        foreach(var p in prices)
        {
            using var add=c.CreateCommand();add.Transaction=tx;add.CommandText="INSERT INTO event_price(event_id,seat_id,amount,color) VALUES($e,$s,$a,$c)";
            add.Parameters.AddWithValue("$e",id);add.Parameters.AddWithValue("$s",p.SeatId);add.Parameters.AddWithValue("$a",p.Amount);add.Parameters.AddWithValue("$c",p.Color);add.ExecuteNonQuery();
        }
        tx.Commit();return id;
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
