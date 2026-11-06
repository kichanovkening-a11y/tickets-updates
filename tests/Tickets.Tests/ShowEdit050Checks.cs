using System.Globalization;
using System.Reflection;
using Tickets.Core;

internal static class ShowEdit050Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("050 editing a show preserves identity seats issues and saved documents",s=>{
            var (id,other,seat)=Create(s);
            var issue=s.Issue(id,[seat],"Получатель 050","EDIT050",1);
            var path=s.SaveInvoiceDocument(id,issue);var bytes=File.ReadAllBytes(path);
            var before=s.Seats(id).ToArray();var otherBefore=s.Events().Single(x=>x.Id==other);
            Edit(s,id,"Н. Шацкая, О. Кабо. Память о солнце",new TimeSpan(18,30,15));
            var after=s.Events().Single(x=>x.Id==id);
            Require(after.Title=="Н. Шацкая, О. Кабо. Память о солнце","title not saved");
            Require(DateTime.Parse(after.SortDate).TimeOfDay==new TimeSpan(18,30,15),"time not saved");
            Require(DateTime.Parse(after.SortDate).Date==DateTime.Parse(otherBefore.SortDate).Date,"date changed");
            Require(after.Date==DateTime.Parse(after.SortDate).ToString("dd.MM.yyyy HH:mm:ss",CultureInfo.InvariantCulture),"legacy display date stale");
            Require(s.Seats(id).SequenceEqual(before),"seats or issuance changed");
            Require(File.ReadAllBytes(path).SequenceEqual(bytes),"saved document changed");
            Require(s.Events().Single(x=>x.Id==other)==otherBefore,"another show changed");
            Require(s.Performances().Any(x=>x.Title==after.Title),"renamed show missing from directory");
        });
        test("050 editing rejects conflicting time invalid title and archived show atomically",s=>{
            var(id,other,_)=Create(s);var before=s.Events().Single(x=>x.Id==id);
            Reject(()=>Edit(s,id,"Нельзя",DateTime.Parse(s.Events().Single(x=>x.Id==other).SortDate).TimeOfDay));
            Reject(()=>Edit(s,id," ",TimeSpan.FromHours(17)));
            Reject(()=>Edit(s,id,"Название",TimeSpan.FromHours(25)));
            Require(s.Events().Single(x=>x.Id==id)==before,"failed edit changed show");
            using(var c=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+s.Path))
            {c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE event SET starts_at='2020-01-01T19:00:00' WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
            Reject(()=>Edit(s,id,"Архив",TimeSpan.FromHours(19)));
        });
    }
    private static (long,long,long) Create(Store s)
    {
        var venue=s.CreateHall("Зал 050","Адрес",[new DraftSeat()]);var seat=s.VenueSeats(venue).Single().Id;
        var plan=s.SavePlan(venue,"Цена 050",[new PriceValue(seat,1000,"#FFCC00")]);var title=s.SavePerformance("Название 050");
        var date=DateTime.Now.Date.AddDays(30);
        return(s.CreateEvent(title,venue,plan,date.AddHours(19)),s.CreateEvent(title,venue,plan,date.AddHours(21)),seat);
    }
    private static void Edit(Store s,long id,string title,TimeSpan time)
    {
        var method=typeof(Store).GetMethod("EditShow050")??throw new Exception("EditShow050 is missing");
        try{method.Invoke(s,[id,title,time]);}catch(TargetInvocationException e) when(e.InnerException is TicketException inner){throw inner;}
    }
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("invalid edit accepted");}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
