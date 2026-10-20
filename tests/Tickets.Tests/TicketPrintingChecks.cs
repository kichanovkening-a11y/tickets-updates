using Tickets.Core;
internal static class TicketPrintingChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("ticket print preview is read only, validates selection and preserves numbering order",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=3);var seats=s.Seats(e.Id).Where(x=>x.Available).Take(3).Select(x=>x.Id).ToArray();
            var before=s.History(e.Id).Count;var issues=s.Issues(e.Id).Count;
            var preview=Preview(s,e.Id,new[]{seats[1],seats[0]},"Печать"," draft ",9);
            Check(preview.Id==0&&preview.Tickets.Count==2&&preview.Tickets[0].SeatId==seats[1]&&preview.Tickets[0].Number==9&&preview.Tickets[1].Number==10,"draft order or number changed");
            Check(preview.Tickets.All(t=>t.Id==0&&t.IssueId==0&&t.Series=="DRAFT"),"draft has issued identity");
            Reject(()=>Preview(s,e.Id,Array.Empty<long>(),"Печать","DRAFT",1));
            Reject(()=>Preview(s,e.Id,new[]{seats[0],seats[0]},"Печать","DRAFT",1));
            Reject(()=>Preview(s,e.Id,new[]{seats[0]},"","DRAFT",1));
            Reject(()=>Preview(s,e.Id,new[]{seats[0]},"Печать","",1));
            Reject(()=>Preview(s,e.Id,seats,"Печать","DRAFT",long.MaxValue));
            Reject(()=>Preview(s,e.Id,new[]{-1L},"Печать","DRAFT",1));
            s.SetSeatsBlocked(e.Id,new[]{seats[2]},true);Reject(()=>Preview(s,e.Id,new[]{seats[2]},"Печать","DRAFT",1));
            Check(s.History(e.Id).Count==before&&s.Issues(e.Id).Count==issues,"preview wrote issued tickets");
            Check(!s.Recipients().Contains("Печать"),"preview wrote recipient");
        });
        test("ticket reprint selects active tickets across issues without new issuance",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=3);var seats=s.Seats(e.Id).Where(x=>x.Available).Take(3).Select(x=>x.Id).ToArray();
            var a=s.Issue(e.Id,new[]{seats[0]},"Первый","REPRINTA",1);var b=s.Issue(e.Id,new[]{seats[1]},"Второй","REPRINTB",2);
            var info=Reprint(s,e.Id,new[]{seats[1],seats[0]});
            Check(info.Tickets.Count==2&&info.Tickets[0].Series=="REPRINTB"&&info.Tickets[0].IssueId==b&&info.Tickets[1].IssueId==a,"reprint codes or order changed");
            Check(info.Recipient.Contains("Первый")&&info.Recipient.Contains("Второй"),"recipient aggregation missing");
            Reject(()=>Reprint(s,e.Id,new[]{seats[2]}));Reject(()=>Reprint(s,e.Id,new[]{seats[0],seats[0]}));Reject(()=>Preview(s,e.Id,new[]{seats[0]},"Печать","DRAFT",1));
            s.Return(new[]{info.Tickets[0].Id});Reject(()=>Reprint(s,e.Id,new[]{seats[1]}));
            Reject(()=>Preview(s,e.Id,new[]{seats[2]},"Печать","REPRINTA",1));
            s.DeleteVenue(e.VenueId);
            Check(Reprint(s,e.Id,new[]{seats[0]}).Tickets[0].Code=="REPRINTA 000001","archive reprint lost issued code");
            Reject(()=>Preview(s,e.Id,new[]{seats[2]},"Печать","DRAFT",1));
            Check(s.History(e.Id).Count==2&&s.Issues(e.Id).Count==2,"reprint created issuance");
        });
    }
    private static InvoiceInfo Preview(Store s,long e,IReadOnlyCollection<long> seats,string recipient,string series,long first)=>Invoke(s,"TicketPrintPreview",e,seats,recipient,series,first);
    private static InvoiceInfo Reprint(Store s,long e,IReadOnlyCollection<long> seats)=>Invoke(s,"TicketReprint",e,seats);
    private static InvoiceInfo Invoke(Store s,string name,params object[] args)
    {
        var method=typeof(Store).GetMethod(name)??throw new Exception(name+" API missing");
        try{return (InvoiceInfo)method.Invoke(s,args)!;}catch(System.Reflection.TargetInvocationException ex){throw ex.InnerException!;}
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("invalid printing operation accepted");}
}
