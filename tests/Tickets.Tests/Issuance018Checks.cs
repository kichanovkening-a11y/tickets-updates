using Tickets.Core;

internal static class Issuance018Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("018 occupied states cannot enter click rectangle or stale selection",_=>{
            var free=new SeatInfo(1,"S","1","1",0,0,100,"#123456",null,"","",null);
            var seats=new[]{free,free with{Id=2,X=20,TicketId=77,Recipient="Получатель"},
                free with{Id=3,X=40,IsReserved=true},free with{Id=4,X=60,IsBlocked=true},
                free with{Id=5,X=80,Amount=null}};
            var clicked=new HashSet<long>();foreach(var seat in seats)SeatSelection.Toggle(clicked,seat);
            if(!clicked.SetEquals(new long[]{1,5}))throw new Exception("occupied click accepted or unpriced price-edit seat excluded");
            var editable=SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),-1,-1,100,20,false,true);
            if(editable.Count!=5)throw new Exception("pricing editor must retain selection of withdrawn places");
            var dragged=SeatSelection.ApplyRectangle(seats,new long[]{2,3,4},-1,-1,100,20,false);
            if(!dragged.SetEquals(new long[]{1,5}))throw new Exception("occupied rectangle or stale selection accepted");
            if(!SeatSelection.ApplyRectangle(seats,dragged,-1,-1,100,20,true).SetEquals(Array.Empty<long>()))throw new Exception("free seat rectangle removal failed");
        });
        test("018 original invoice reprint preserves recipient numbers and invoice",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);
            var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).ToArray();
            var issue=store.Issue(show.Id,[seats[0].Id],"Получатель018","И018",1);
            var printed=store.TicketReprint(show.Id,[seats[0].Id]);
            if(printed.Id!=issue||printed.Recipient!="Получатель018"||printed.Tickets.Single().Number!=1)throw new Exception("original invoice changed");
            try{store.Issue(show.Id,[seats[0].Id,seats[1].Id],"Другой018","И018",2);throw new Exception("occupied seat reissued");}catch(TicketException){}
            if(!store.Seats(show.Id).Single(s=>s.Id==seats[1].Id).Available)throw new Exception("rejected mixed issuance consumed free seat");
        });
    }
}
