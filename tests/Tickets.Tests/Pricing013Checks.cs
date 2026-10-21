using Tickets.Core;
using Microsoft.Data.Sqlite;
internal static class Pricing013Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("013 repairs confusable 1500 and 5000 colors once without changing seats",s=>{
            s.SetPriceColor(1500,"#12AE34");s.SetPriceColor(5000,"#13AF35");
            var evt=s.Events().First();var before=s.EventPrices(evt.Id,s.Seats(evt.Id).Select(x=>x.Id).ToArray()).ToArray();
            var reopened=new Store(s.Path);var colors=reopened.PriceColors();
            var low=colors.Single(x=>x.Amount==1500).Color;var high=colors.Single(x=>x.Amount==5000).Color;
            Require(low=="#12AE34"&&high!="#13AF35","confusable pair not repaired");
            Require(new Store(s.Path).PriceColors().SequenceEqual(colors),"repair is not stable");
            Require(reopened.EventPrices(evt.Id,before.Select(x=>x.SeatId).ToArray()).Select(x=>(x.SeatId,x.Amount)).SequenceEqual(before.Select(x=>(x.SeatId,x.Amount))),"repair changed amounts");
        });
        test("013 imports after v6 receive missing global colors",s=>{
            var evt=s.Events().First();var seat=s.Seats(evt.Id).First();const long price=87654321;
            using(var c=new SqliteConnection("Data Source="+s.Path))
            {c.Open();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE event_price SET amount=$a WHERE event_id=$e AND seat_id=$s; PRAGMA user_version=6";cmd.Parameters.AddWithValue("$a",price);cmd.Parameters.AddWithValue("$e",evt.Id);cmd.Parameters.AddWithValue("$s",seat.Id);cmd.ExecuteNonQuery();}
            var reopened=new Store(s.Path);Require(reopened.PriceColors().Any(x=>x.Amount==price),"imported amount has no mapping");
            Require(reopened.Seats(evt.Id).Single(x=>x.Id==seat.Id).Amount==price,"repair changed imported amount");
        });
        test("013 typed recipient is persisted only on successful issuance",s=>{
            const string recipient="Новая организация 013";var evt=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(evt.Id).First(x=>x.Available);
            Require(!s.Recipients().Contains(recipient),"fixture recipient already exists");
            s.Issue(evt.Id,new[]{seat.Id},recipient,"NEW013",1);
            Require(new Store(s.Path).Recipients().Contains(recipient),"new recipient not persisted");
            try{s.Issue(evt.Id,new[]{seat.Id},"Неудачная организация 013","NEW013",2);}catch(TicketException){}
            Require(!s.Recipients().Contains("Неудачная организация 013"),"failed issuance created recipient");
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
