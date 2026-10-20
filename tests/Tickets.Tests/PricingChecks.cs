using Tickets.Core;
using Microsoft.Data.Sqlite;

internal static class PricingChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("persistent event price survives reopen and is used at issue",s=>{
            var (e,ids)=Choice(s);s.SetEventPrices(e,ids,97531);
            var reopened=new Store(s.Path);Require(reopened.Seats(e).Where(x=>ids.Contains(x.Id)).All(x=>x.Amount==97531),"saved prices lost");
            reopened.Issue(e,ids,"Pricing check","PRICE",1);Require(reopened.History(e).All(x=>x.Amount==97531),"issue ignores saved price");
            Reject(()=>reopened.SetEventPrices(e,ids,111));Require(reopened.History(e).All(x=>x.Amount==97531),"issued snapshots changed");
        });
        test("mixed invalid pricing selection rolls back amount and global color",s=>{
            var (e,ids)=Choice(s);var before=s.EventPrices(e,ids);var colors=s.PriceColors().ToArray();
            Reject(()=>s.SetEventPrices(e,new[]{ids[0],-1L},234567,"#123456"));
            Require(s.EventPrices(e,ids).SequenceEqual(before)&&s.PriceColors().SequenceEqual(colors),"partial pricing update");
            s.Issue(e,new[]{ids[1]},"A","MIX",1);
            Reject(()=>s.SetEventPrices(e,ids,234567));Require(s.EventPrices(e,ids).SequenceEqual(before),"issued selection changed other seat");
        });
        test("pricing undo restores unpriced seats and guards concurrent changes",s=>{
            var (e,ids)=Choice(s);
            using(var c=new SqliteConnection("Data Source="+s.Path)){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM event_price WHERE event_id=$e AND seat_id=$s";cmd.Parameters.AddWithValue("$e",e);cmd.Parameters.AddWithValue("$s",ids[0]);cmd.ExecuteNonQuery();}
            var before=s.EventPrices(e,ids);var change=s.SetEventPrices(e,ids,654321);
            new Store(s.Path).UndoPriceChange(change);Require(s.EventPrices(e,ids).SequenceEqual(before),"undo did not restore exact values");
            var stale=s.SetEventPrices(e,ids,654321);s.SetEventPrices(e,new[]{ids[0]},654322);
            Reject(()=>s.UndoPriceChange(stale));Require(s.EventPrices(e,ids).First().Amount==654322,"undo overwrote newer change");
        });
        test("global price colors span halls and reject duplicate mappings",s=>{
            var (e,ids)=Choice(s);var amount=9876543L;s.SetEventPrices(e,new[]{ids[0]},amount);
            var venue=s.CreateHall("Цветовой зал","",new[]{new DraftSeat()});var sid=s.VenueSeats(venue).Single().Id;
            var plan=s.SavePlan(venue,"Цветовая цена",new[]{new PriceValue(sid,amount,"#ABCDEF")});
            var second=s.CreateEvent(s.Performances().First().Id,venue,plan,new DateTime(2098,1,1));
            s.SetPriceColor(amount,"#123456");Require(s.Seats(e).Single(x=>x.Id==ids[0]).Color=="#123456"&&s.Seats(second).Single().Color=="#123456","color not global");
            Require(s.PlanPrices(plan).Single().Color=="#123456","plan color not global");
            Reject(()=>s.SetPriceColor(amount+1,"#123456"));
            Require(new Store(s.Path).PriceColors().Single(x=>x.Amount==amount).Color=="#123456","global color not persisted");
        });
        test("plan colors update shared mappings and conflicting plans roll back",s=>{
            var venue=s.CreateHall("План цвета","",new[]{new DraftSeat{Number="1"},new DraftSeat{Number="2",X=26}});
            var ids=s.VenueSeats(venue).Select(x=>x.Id).ToArray();var amount=987654321L;
            var plan=s.SavePlan(venue,"Первый цвет",new[]{new PriceValue(ids[0],amount,"#123456")});
            s.SavePlan(venue,"Изменённый цвет",new[]{new PriceValue(ids[0],amount,"#234567")});
            Require(s.PlanPrices(plan).Single().Color=="#234567","plan color choice not global");
            var count=s.Plans(venue).Count;
            Reject(()=>s.SavePlan(venue,"Смешанные цвета",new[]{new PriceValue(ids[0],amount,"#345678"),new PriceValue(ids[1],amount,"#456789")}));
            Reject(()=>s.SavePlan(venue,"Повторный цвет",new[]{new PriceValue(ids[0],amount,"#345678"),new PriceValue(ids[1],amount+1,"#345678")}));
            Require(s.Plans(venue).Count==count&&s.PlanPrices(plan).Single().Color=="#234567","failed plan partially changed shared colors");
            var all=PricePalette.Colors.ToArray();var extra=PricePalette.NextAvailable(all);
            Require(!all.Contains(extra)&&extra!=PricePalette.NextAvailable(all.Append(extra)),"palette exhaustion repeats colors");
        });
        test("issue override maps actual amount globally and rollback leaves no mapping",s=>{
            var (e,ids)=Choice(s);const long amount=1234567890;
            s.Issue(e,new[]{ids[0]},"A","OVERCOLOR",1,new Dictionary<long,long>{{ids[0],amount}});
            var color=s.PriceColors().Single(x=>x.Amount==amount).Color;
            Require(s.Seats(e).Single(x=>x.Id==ids[0]).Color==color,"issued override uses base price color");
            const long failedAmount=1234567891;
            Reject(()=>s.Issue(e,new[]{ids[1],ids[0]},"A","FAILCOLOR",1,new Dictionary<long,long>{{ids[1],failedAmount}}));
            Require(!s.PriceColors().Any(x=>x.Amount==failedAmount)&&s.History(e).Count==1,"failed issue leaves orphan global mapping");
            var reopened=new Store(s.Path);Require(reopened.Seats(e).Single(x=>x.Id==ids[0]).Color==color,"override mapping lost after reload");
        });
        test("pricing undo after issue preserves ticket amount",s=>{
            var (e,ids)=Choice(s);var change=s.SetEventPrices(e,ids,54321);s.Issue(e,ids,"A","UNDO",1);
            Reject(()=>s.UndoPriceChange(change));s.SetPriceColor(54321,"#123456");
            Require(s.History(e).All(x=>x.Amount==54321),"ticket price snapshot modified");
        });
        test("v6 migration survives artificial downgrade and normalized duplicate plans",s=>{
            var original=s.PriceColors().ToArray();
            using(var c=new SqliteConnection("Data Source="+s.Path)){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA user_version=5";cmd.ExecuteNonQuery();}
            var reopened=new Store(s.Path);Require(File.Exists(s.Path+".before-v6.db"),"migration backup missing");Require(reopened.PriceColors().SequenceEqual(original),"migration changes existing colors");
            var venue=s.Venues().First().Id;var sid=s.VenueSeats(venue).First().Id;var prices=new[]{new PriceValue(sid,123,"#123456")};
            s.SavePlan(venue,"Тест   ёлка",prices);Reject(()=>s.SavePlan(venue,"  тест елка  ",prices));
        });
    }
    private static (long,long[]) Choice(Store s)
    {
        foreach(var e in s.Events())
        {var ids=s.Seats(e.Id).Where(x=>x.Available).Take(2).Select(x=>x.Id).ToArray();if(ids.Length==2)return(e.Id,ids);}
        throw new Exception("No event with two priced free seats");
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
