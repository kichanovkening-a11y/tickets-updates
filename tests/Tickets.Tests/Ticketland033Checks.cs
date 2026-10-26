using Tickets.Core;
internal static class Ticketland033Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("033 selected show summary excludes other sessions gifts and conflicts",s=>{
            var hall=s.CreateHall("033 scope venue","",Enumerable.Range(1,2).Select(n=>new DraftSeat{Sector="Партер",Row="1",Number=n.ToString(),X=n*40}).ToArray());
            var seats=s.VenueSeats(hall);var plan=s.SavePlan(hall,"033 scope plan",seats.Select(x=>new PriceValue(x.Id,1000,"#123456")).ToArray());
            var at=new DateTime(2027,8,10,14,0,0);var title="033 scope show";var perf=s.SavePerformance(title);
            var first=s.CreateEvent(perf,hall,plan,at);var other=s.CreateEvent(perf,hall,plan,at.AddHours(3));
            s.Issue(other,[seats[0].Id],"other recipient","SCOPE033",1);
            TicketlandSale028 Sale(DateTime time,string row,string seat,string code,string name="033 scope show")=>new(name,time,"Партер",row,seat,code,1000){Venue="033 scope venue"};
            s.ApplyTicketlandReport028(new([Sale(at,"1","1","scope-ok"),Sale(at,"1","9","scope-missing"),Sale(at,"--","--","scope-general"),Sale(at.AddHours(3),"1","1","scope-other"),Sale(at.AddHours(7),"1","8","scope-absent"),Sale(at,"1","1","scope-gift","Зона подарков. 033 scope show")],DateTimeOffset.UtcNow),true);
            var method=typeof(Store).GetMethod("TicketlandShowSummary034");Check(method is not null,"selected-show summary is missing");
            var summary=(TicketlandSyncSummary028)method!.Invoke(s,[first])!;
            Check(summary==new TicketlandSyncSummary028(1,1,0,1),"other sessions or gifts leaked into selected show summary");
            var second=(TicketlandSyncSummary028)method.Invoke(s,[other])!;Check(second==new TicketlandSyncSummary028(1,0,1,0),"selected show conflict count incorrect");
            Check(s.TicketlandSectorReviews030().Where(x=>x.EventId==first).Single().MissingSeats.Any(x=>x.Contains("место 9")),"actual selected-show discrepancy hidden");
        });
        run("033 conflict list explains active issuance and disappears after local return",s=>{
            var hall=s.CreateHall("033 conflicts venue","",[new DraftSeat{Sector="Партер"}]);var seats=s.VenueSeats(hall);var plan=s.SavePlan(hall,"033 conflicts plan",[new(seats[0].Id,1000,"#123456")]);
            var at=new DateTime(2027,8,1,19,0,0);var evt=s.CreateEvent(s.SavePerformance("033 conflict show"),hall,plan,at);
            s.Issue(evt,[seats[0].Id],"Получатель 033","X033",1);
            var sale=new TicketlandSale028("033 conflict show",at,"Партер","1","1","033-sold",800){Venue="033 conflicts venue"};s.ApplyTicketlandReport028(new([sale],DateTimeOffset.UtcNow),true);
            var conflict=s.TicketlandConflicts033().Single(x=>x.EventId==evt);Check(conflict.Recipient=="Получатель 033"&&conflict.Row=="1"&&conflict.Seat=="1"&&conflict.Sector=="Партер"&&conflict.StartsAt==at,"wrong conflict details");
            var before=s.Seats(evt).Single();s.TicketlandConflicts033();Check(s.Seats(evt).Single()==before,"reading changed issuance");
            s.Return([before.TicketId!.Value]);Check(!s.TicketlandConflicts033().Any(x=>x.EventId==evt),"returned issue still conflicts");
        });
        run("033 missing position diagnostic says whether row or seat is absent",s=>{
            var hall=s.CreateHall("033 diagnostic venue","",[new DraftSeat{Sector="Партер"}]);var seat=s.VenueSeats(hall).Single();var plan=s.SavePlan(hall,"033 diagnostic plan",[new(seat.Id,1000,"#123456")]);var at=new DateTime(2027,8,2,19,0,0);s.CreateEvent(s.SavePerformance("033 diagnostic show"),hall,plan,at);
            var sale=new TicketlandSale028("033 diagnostic show",at,"Партер","1","6","033-missing",800){Venue="033 diagnostic venue"};s.ApplyTicketlandReport028(new([sale],DateTimeOffset.UtcNow),true);
            var reason=s.TicketlandSectorReviews030().Single().Reason;Check(reason.Contains("отсутствует место 6 в ряду 1")&&!reason.Contains("или неоднозначно"),"missing seat remains ambiguous");
        });
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
