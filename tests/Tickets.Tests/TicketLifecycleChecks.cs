using Tickets.Core;
internal static class TicketLifecycleChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("ticket lifecycle persists reservation, confirmation and successful print",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=3);
            var seats=s.Seats(e.Id).Where(x=>x.Available).Take(3).Select(x=>x.Id).ToArray();
            var issue=s.Reserve(e.Id,new[]{seats[0],seats[1]},"Бронь","LIFECYCLE",1);
            var reserved=s.History(e.Id).Where(t=>t.IssueId==issue).ToArray();
            Check(reserved.All(t=>t.IsReserved&&t.PrintedAt is null),"reservation state missing");
            Check(s.Issues(e.Id).Single(x=>x.Id==issue).ReservedCount==2,"journal reservation state missing");
            Check(s.Seats(e.Id).Where(x=>x.IssueId==issue).All(x=>x.IsReserved&&!x.Available&&!x.IsPrinted),"seat reservation state missing");
            Reject(()=>s.MarkTicketsPrinted(e.Id+999999,reserved.Select(t=>t.Id).ToArray()));
            s.ConfirmReservation(e.Id,issue);s.ConfirmReservation(e.Id,issue);
            Check(s.History(e.Id).Where(t=>t.IssueId==issue).All(t=>!t.IsReserved&&t.PrintedAt is null),"confirmation state incorrect");
            s.MarkTicketsPrinted(e.Id,new[]{reserved[0].Id});
            var printed=s.History(e.Id).Single(t=>t.Id==reserved[0].Id).PrintedAt;
            Check(s.Issues(e.Id).Single(x=>x.Id==issue) is { ReservedCount:0, PrintedCount:1 },"journal printed state missing");
            s.MarkTicketsPrinted(e.Id,new[]{reserved[0].Id});
            Check(printed is not null&&s.History(e.Id).Single(t=>t.Id==reserved[0].Id).PrintedAt==printed,"print timestamp not idempotent");
            var reopened=new Store(s.Path);
            Check(reopened.Seats(e.Id).Single(x=>x.TicketId==reserved[0].Id).IsPrinted,"print state did not persist");
            Reject(()=>s.MarkTicketsPrinted(e.Id,new[]{reserved[1].Id,-1L}));
            Check(s.History(e.Id).Single(t=>t.Id==reserved[1].Id).PrintedAt is null,"failed print marking partially committed");
            s.Return(new[]{reserved[1].Id});Reject(()=>s.MarkTicketsPrinted(e.Id,new[]{reserved[1].Id}));
        });
        test("invoice extension preserves original identity and snapshots, allocates series globally and rolls back",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=5);
            var seats=s.Seats(e.Id).Where(x=>x.Available).Take(5).Select(x=>x.Id).ToArray();
            var issue=s.Issue(e.Id,new[]{seats[0]},"Оригинал","EXTEND",10);var original=s.Invoice(e.Id,issue);
            s.Issue(e.Id,new[]{seats[1]},"Другой","EXTEND",99);
            Check(s.ExtendInvoice(e.Id,issue,new[]{seats[2]})==issue,"extension created a new invoice identity");
            var extended=s.Invoice(e.Id,issue);
            Check(extended.Tickets.Count==2&&extended.Tickets.Single(t=>t.SeatId==seats[2]).Number==100,"global series allocation failed");
            Check(extended.Recipient==original.Recipient&&extended.Sender==original.Sender&&extended.Title==original.Title&&extended.IssuedAt==original.IssuedAt,"invoice snapshots changed");
            Reject(()=>s.ExtendInvoice(e.Id,issue,new[]{seats[3],seats[1]}));
            Check(s.Seats(e.Id).Single(t=>t.Id==seats[3]).Available,"extension partially committed");
            Reject(()=>s.ExtendInvoice(e.Id+999999,issue,new[]{seats[3]}));
            s.Return(extended.Tickets.Select(t=>t.Id).ToArray());Reject(()=>s.ExtendInvoice(e.Id,issue,new[]{seats[3]}));
        });
        test("reservation failures are atomic and reservation cannot extend an invoice",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=3);
            var seats=s.Seats(e.Id).Where(x=>x.Available).Take(3).Select(x=>x.Id).ToArray();
            s.Issue(e.Id,new[]{seats[0]},"Выдано","RESERVED",1);
            Reject(()=>s.Reserve(e.Id,new[]{seats[1],seats[0]},"Бронь","RESERVED",2));
            Check(s.Seats(e.Id).Single(t=>t.Id==seats[1]).Available,"reservation partially committed");
            var reservation=s.Reserve(e.Id,new[]{seats[1]},"Бронь","RESERVED",2);
            Reject(()=>s.ExtendInvoice(e.Id,reservation,new[]{seats[2]}));
            Reject(()=>s.ConfirmReservation(e.Id+999999,reservation));
            var ticket=s.History(e.Id).Single(t=>t.IssueId==reservation);
            s.MarkTicketsPrinted(e.Id,new[]{ticket.Id});
            Check(s.History(e.Id).Single(t=>t.Id==ticket.Id) is { IsReserved:false, IsPrinted:true },"printing reservation did not confirm issuance");
        });
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("invalid lifecycle operation accepted");}
}
