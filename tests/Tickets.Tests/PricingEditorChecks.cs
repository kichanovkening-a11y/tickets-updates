using Tickets.Core;

internal static class PricingEditorChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("pricing editor requires exact coverage and preserves explicit withdrawn seats",s=>{
            var venue=s.CreateHall("Проверка расценовки","",new[]{new DraftSeat{Sector="Балкон",Row="А",Number="7"},new DraftSeat{Sector="Балкон",Row="А",Number="8",X=20},new DraftSeat{Sector="Партер",Row="Б",Number="9",X=40}});
            var seats=s.VenueSeats(venue);var priced=new[]{new PriceValue(seats[0].Id,87123,"#123ABC")};
            try{s.SavePlan(venue,"Неполная",priced,new[]{seats[1].Id});throw new Exception("incomplete editor plan saved");}
            catch(TicketException ex){Require(ex.Message.Contains(seats[2].Label)&&!ex.Message.Contains(seats[1].Label),"missing-seat warning did not identify exact unassigned seat");}
            var plan=s.SavePlan(venue,"Полная",priced,new[]{seats[1].Id,seats[2].Id});
            Require(s.PlanBlockedSeats(plan).Order().SequenceEqual(new[]{seats[1].Id,seats[2].Id}.Order()),"withdrawn seats were not saved");
            Require(s.PlanPrices(plan).Count==1,"withdrawn seats gained prices");
            var reopened=new Store(s.Path);Require(reopened.CopyPlanBlockedSeats(plan,venue).Count==2,"withdrawn seats not reusable after reopen");
            var second=s.SavePlan(venue,"Весь зал снят",Array.Empty<PriceValue>(),seats.Select(x=>x.Id).ToArray());Require(s.PlanBlockedSeats(second).Count==3,"all-withdrawn plan rejected");
            Reject(()=>s.SavePlan(venue,"Пересечение",priced,seats.Select(x=>x.Id).ToArray()));
        });
        test("pricing editor color changes propagate across saved schemes",s=>{
            var venue=s.CreateHall("Общие цвета расценовок","",new[]{new DraftSeat{Number="1"}});var id=s.VenueSeats(venue).Single().Id;
            var first=s.SavePlan(venue,"Первый вариант",new[]{new PriceValue(id,918273,"#0A1B2C")},Array.Empty<long>());
            var perf=s.SavePerformance("Общий цвет схем");var showing=s.CreateEvent(perf,venue,first,new DateTime(2098,12,10,19,0,0));
            s.SavePlan(venue,"Второй вариант",new[]{new PriceValue(id,918273,"#3D4E5F")},Array.Empty<long>());
            Require(s.PlanPrices(first).Single().Color=="#3D4E5F"&&s.Seats(showing).Single().Color=="#3D4E5F","global price color did not update prior schemes");
        });
        test("event plan templates preserve withdrawals separately from missing prices",s=>{
            var venue=s.CreateHall("Шаблон со снятыми местами","",new[]{new DraftSeat{Number="1"},new DraftSeat{Number="2",X=20},new DraftSeat{Number="3",X=40}});var ids=s.VenueSeats(venue).Select(x=>x.Id).ToArray();
            var original=s.SavePlan(venue,"Три цены",ids.Select(id=>new PriceValue(id,516273,"#92A3B4")).ToArray(),Array.Empty<long>());
            var perf=s.SavePerformance("Снятые места в шаблоне");var first=s.CreateEvent(perf,venue,original,new DateTime(2098,11,1,19,0,0));
            s.SetSeatsBlocked(first,new[]{ids[1]},true);
            var template=s.GetOrCreateEventPlan(first);Require(template!=original,"blocked and unblocked patterns were merged");
            Require(s.PlanBlockedSeats(template).SequenceEqual(new[]{ids[1]})&&s.PlanPrices(template).All(p=>p.SeatId!=ids[1]),"blocked source price not normalized");
            var created=s.CreateEvent(perf,venue,template,new DateTime(2098,11,2,19,0,0));Require(s.Seats(created).Single(x=>x.Id==ids[1]).IsBlocked,"new event lost template withdrawal");
            Require(s.GetOrCreateEventPlan(created)==template,"same block pattern did not deduplicate");
            var all=s.SavePlan(venue,"Все сняты",Array.Empty<PriceValue>(),ids);var allEvent=s.CreateEvent(perf,venue,all,new DateTime(2098,11,3,19,0,0));
            Require(s.GetOrCreateEventPlan(allEvent)==all,"all-withdrawn template was not reusable");
        });
        test("pricing editor palette contains black gray and distinct saturated choices",s=>{
            Require(PricePalette.Colors.Contains("#000000")&&PricePalette.Colors.Contains("#808080"),"neutral color options missing");
            Require(PricePalette.Colors.Count>=30&&PricePalette.Colors.Distinct(StringComparer.OrdinalIgnoreCase).Count()==PricePalette.Colors.Count,"expanded palette invalid");
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("invalid pricing plan accepted");}
}
