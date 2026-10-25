using Tickets.Core;
internal static class TicketlandMatching031Checks
{
 internal static void Register(Action<string,Action<Store>> run)
 {
  run("031 show diagnostics distinguish all four criteria without changing snapshot",s=>{
   var f=Fixture(s,"Партер");var now=DateTimeOffset.UtcNow;var same=Sale(f,"Партер","diagnostic-exact");
   var sales=new[]{same,same with{Barcode="diagnostic-title",Title="Владимир Винокур. Другая программа"},same with{Barcode="diagnostic-date",StartsAt=f.At.AddDays(1)},same with{Barcode="diagnostic-time",StartsAt=f.At.AddMinutes(1)},same with{Barcode="diagnostic-venue",Venue="Другая сцена"},same with{Barcode="diagnostic-unrelated",Title="Совсем другой концерт",StartsAt=f.At.AddHours(2)}};
   s.ApplyTicketlandReport028(new(sales,now),true);var before=s.CachedTicketlandReport028();
   var diagnostic=s.TicketlandShowDiagnostics031(f.Event);
   Check(diagnostic.SourceTicketCount==5&&diagnostic.ExactTicketCount==1&&diagnostic.Differences.Count==5,"diagnostic candidate scope is wrong");
   Check(diagnostic.Differences.Single(x=>!x.TitleMatches).DateMatches&&diagnostic.Differences.Single(x=>!x.TitleMatches).TimeMatches&&diagnostic.Differences.Single(x=>!x.TitleMatches).VenueMatches,"title mismatch hidden");
   Check(diagnostic.Differences.Single(x=>!x.DateMatches).TitleMatches,"date mismatch hidden");
   Check(diagnostic.Differences.Single(x=>!x.TimeMatches).TitleMatches,"time mismatch hidden");
   Check(diagnostic.Differences.Single(x=>!x.VenueMatches).TitleMatches,"venue mismatch hidden");
   Check(s.CachedTicketlandReport028().Sales.SequenceEqual(before.Sales)&&s.TicketlandLastSync028==now&&s.Seats(f.Event).Single().IsTicketlandSold,"diagnostics mutated snapshot");
  });
  run("031 normalized markers and approvals survive portable copy and failed refresh",s=>{
   var automatic=Fixture(s,"Ложа правая партера");var approved=Fixture(s,"Ложа левая бельэтажа");var now=DateTimeOffset.UtcNow;
   var sales=new[]{Sale(automatic,"ЛОЖА ПАРТЕРА ПРАВАЯ","portable-auto"),Sale(approved,"Ложа левая бельэтажа Источник","portable-approved")};
   s.ApplyTicketlandReport028(new(sales,now),true);var review=s.TicketlandSectorReviews030().Single();s.AcceptTicketlandSector030(review.Key,"Ложа левая бельэтажа");
   s.ApplyTicketlandReport028(s.CachedTicketlandReport028(),true);
   var directory=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!,"another-usb-folder");Directory.CreateDirectory(directory);var copy=System.IO.Path.Combine(directory,"tickets.db");s.Backup(copy);var reopened=new Store(copy);
   Check(reopened.ApplyTicketlandReport028(reopened.CachedTicketlandReport028(),true).Matched==2,"portable copy lost mapping or approval");
   try{reopened.ApplyTicketlandReport028(new(new[]{sales[0] with{Barcode=""}},now.AddSeconds(1)),true);throw new Exception("invalid refresh accepted");}catch(TicketException){}
   var restarted=new Store(copy);Check(restarted.TicketlandLastSync028==now&&restarted.CachedTicketlandReport028().Sales.Count==2&&restarted.TicketlandSectorReviews030().Count==0,"failed refresh altered cache or decisions");
   Check(restarted.Seats(automatic.Event).Single().IsTicketlandSold&&restarted.Seats(approved.Event).Single().IsTicketlandSold,"restart or failed refresh lost sold markers");
  });
  run("031 old explicit sector conflicts cannot survive decisions or proven markers",s=>{
   foreach(var pair in new[]{("Партер правый","Партер левый"),("Ложа партера правая","Партер"),("Балкон 1 левый","Балкон 2 левый")})
   {
    var f=Fixture(s,pair.Item2);var sale=Sale(f,pair.Item1,Guid.NewGuid().ToString());var now=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale},now),true);var key=s.TicketlandSectorReviews030().Single().Key;var seat=s.Seats(f.Event).Single().Id;
    using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}"))
    {
     c.Open();using var q=c.CreateCommand();q.CommandText="INSERT OR REPLACE INTO ticketland_sector_decision_030(issue_key,rejected,local_sector) VALUES($key,0,$sector); UPDATE ticketland_sale_028 SET event_id=$event,seat_id=$seat WHERE barcode=$barcode";q.Parameters.AddWithValue("$key",key);q.Parameters.AddWithValue("$sector",pair.Item2);q.Parameters.AddWithValue("$event",f.Event);q.Parameters.AddWithValue("$seat",seat);q.Parameters.AddWithValue("$barcode",sale.Barcode);q.ExecuteNonQuery();
    }
    var reopened=new Store(s.Path);Check(reopened.Seats(f.Event).Single().IsTicketlandSold,"legacy marker fixture missing");
    var result=reopened.ApplyTicketlandReport028(reopened.CachedTicketlandReport028(),true);
    Check(result.Matched==0&&result.Unmatched==1&&!reopened.Seats(f.Event).Single().IsTicketlandSold,"legacy explicit conflict resurrected: "+pair.Item1+" -> "+pair.Item2);
    Check(reopened.TicketlandSectorReviews030().Single().Candidates.Count==0,"legacy explicit conflict remains actionable");
   }
  });
  run("031 box inflections and word order match automatically",s=>{
   var f=Fixture(s,"Ложа правая партера");var now=DateTimeOffset.UtcNow;
   foreach(var sector in new[]{"ЛОЖА ПАРТЕРА ПРАВАЯ","Ложа партер правая","правая партера ложа"})
   {
    var sale=Sale(f,sector,Guid.NewGuid().ToString());
    Check(s.ApplyTicketlandReport028(new(new[]{sale},now),true).Matched==1,"box equivalent not automatic: "+sector);now=now.AddSeconds(1);
   }
  });
  run("031 incompatible sector family side and number never offered",s=>{
   foreach(var local in new[]{"Ложа партера левая","Партер","Ложа партера правая 2"})
   {
    var f=Fixture(s,local);var sale=Sale(f,"Ложа партера правая 1",Guid.NewGuid().ToString());
    Check(s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true).Matched==0,"incompatible sector matched");
    Check(s.TicketlandSectorReviews030().Single().Candidates.Count==0,"incompatible sector group offered: "+local);
    Check(s.TicketlandIssues029().Single().Candidates.Count==0,"incompatible seat offered: "+local);
   }
  });
  run("031 opposing explicit parter sides cannot match while combined parter can",s=>{
   var f=Fixture(s,"Партер левый");var sale=Sale(f,"Партер правый","opposite");
   Check(s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true).Matched==0,"opposite side auto matched");
   Check(s.TicketlandSectorReviews030().Single().Candidates.Count==0,"opposite side manually offered");
   var combined=Fixture(s,"Партер");Check(s.ApplyTicketlandReport028(new(new[]{Sale(combined,"Правая часть партера","combined")},DateTimeOffset.UtcNow.AddSeconds(1)),true).Matched==1,"combined parter stopped resolving");
  });
  run("031 equivalent labels do not create false source collision",s=>{
   var f=Fixture(s,"Ложа правая партера");var sale=Sale(f,"ЛОЖА ПАРТЕРА ПРАВАЯ","same1");
   var result=s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="same2",Sector="Ложа партер правая"}},DateTimeOffset.UtcNow),true);
   Check(result.Matched==2&&s.TicketlandSectorReviews030().Count==0,"equivalent source labels treated as different seats");
  });
 }
 private static (long Event,DateTime At,string Venue,string Title) Fixture(Store s,string sector)
 {
  var title="031 show "+Guid.NewGuid();var name="031 hall "+Guid.NewGuid();var venue=s.CreateHall(name,"",new[]{new DraftSeat{Sector=sector,Row="1",Number="1"}});
  var plan=s.SavePlan(venue,"p",s.VenueSeats(venue).Select(x=>new PriceValue(x.Id,500,"#FF0000")).ToArray());var at=new DateTime(2030,5,7,19,0,0);
  return(s.CreateEvent(s.SavePerformance(title),venue,plan,at),at,name,title);
 }
 private static TicketlandSale028 Sale((long Event,DateTime At,string Venue,string Title) f,string sector,string barcode)=>new(f.Title,f.At,sector,"1","1",barcode,70){Venue=f.Venue};
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
