using Tickets.Core;
internal static class SaleStateChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("withdrawn seats persist and cannot be issued; restoration is scoped",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=3);var seats=s.Seats(e.Id).Where(x=>x.Available).Take(3).ToArray();
            var method=typeof(Store).GetMethod("SetSeatsBlocked");if(method is null)throw new Exception("withdrawal API missing");
            void Set(long eventId,long[] ids,bool blocked){try{method.Invoke(s,new object[]{eventId,ids,blocked});}catch(System.Reflection.TargetInvocationException ex){throw ex.InnerException!;}}
            Set(e.Id,new[]{seats[0].Id},true);var withdrawn=new Store(s.Path).Seats(e.Id).Single(x=>x.Id==seats[0].Id);
            if(withdrawn.Available||withdrawn.Status!="Снят с продажи")throw new Exception("withdrawal not persisted");
            Reject(()=>s.Issue(e.Id,new[]{seats[0].Id,seats[1].Id},"Касса","WITHDRAW",1));
            if(s.History(e.Id).Count!=0)throw new Exception("blocked mixed issue wrote ticket");
            var other=s.Events().FirstOrDefault(x=>x.Id!=e.Id&&x.VenueId==e.VenueId);
            if(other is not null&&s.Seats(other.Id).Single(x=>x.Id==seats[0].Id).Status=="Снят с продажи")throw new Exception("withdrawal leaked to other show");
            Set(e.Id,new[]{seats[0].Id},false);if(!s.Seats(e.Id).Single(x=>x.Id==seats[0].Id).Available)throw new Exception("restore failed");
            s.Issue(e.Id,new[]{seats[1].Id},"Касса","WITHDRAW",1);
            Reject(()=>Set(e.Id,new[]{seats[2].Id,seats[1].Id},true));
            if(!s.Seats(e.Id).Single(x=>x.Id==seats[2].Id).Available)throw new Exception("invalid withdrawal partially applied");
            Reject(()=>Set(e.Id,new[]{seats[0].Id,-1L},true));Reject(()=>Set(e.Id,new[]{seats[0].Id,seats[0].Id},true));
        });
    }
    private static void Reject(Action a){try{a();}catch(TicketException){return;}throw new Exception("invalid operation accepted");}
}
