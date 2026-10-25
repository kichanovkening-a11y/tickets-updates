using Tickets.Core;
internal static class TicketlandMatching032Checks
{
 internal static void Register(Action<string,Action<Store>> run)
 {
  run("032 actual Vinokur title order punctuation and known descriptor match strictly",s=>{
   var f=Fixture(s,"Владимир Винокур «Винокур-Шоу»",new DraftSeat{Sector="Партер",Row="1",Number="1"});
   var sale=Sale(f,"Партер") with{Title="Владимир Винокур. \"Винокур - Шоу\" . Новогодняя программа."};
   Check(s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true).Matched==1,"actual Vinokur descriptor mismatch");
   var reversed=sale with{Barcode="reverse",Title="Новогодняя программа. Шоу Винокур Владимир Винокур"};
   Check(s.ApplyTicketlandReport028(new(new[]{reversed},DateTimeOffset.UtcNow.AddSeconds(1)),true).Matched==1,"known prefix / word order mismatch");
   foreach(var wrong in new[]{sale with{Title="Владимир Винокур Другая программа"},sale with{StartsAt=f.At.AddDays(1)},sale with{StartsAt=f.At.AddMinutes(1)},sale with{Venue=""},sale with{Venue="Другая площадка"}})
    Check(s.ApplyTicketlandReport028(new(new[]{wrong with{Barcode=Guid.NewGuid().ToString()}},DateTimeOffset.UtcNow.AddMinutes(1)),true).Matched==0,"show identifier bypass");
  });
  run("032 sector resolved before row and seat including missing rows",s=>{
   var f=Fixture(s,"sector first",new DraftSeat{Sector="Партер левый",Row="1",Number="1"},new DraftSeat{Sector="Партер правый",Row="2",Number="2"});
   var result=s.ApplyTicketlandReport028(new(new[]{Sale(f,"Партер", "2","2")},DateTimeOffset.UtcNow),true);
   Check(result.Matched==0,"ambiguous sector selected using position");
   var review=s.TicketlandSectorReviews030().Single();Check(review.Candidates.Count==0&&review.Reason.Contains("неоднознач",StringComparison.OrdinalIgnoreCase),"ambiguous name not explained");
   var combined=Fixture(s,"combined first",new DraftSeat{Sector="Партер",Row="1",Number="1"},new DraftSeat{Sector="Партер левый",Row="2",Number="2"});
   s.ApplyTicketlandReport028(new(new[]{Sale(combined,"Правая часть партера","2","2")},DateTimeOffset.UtcNow.AddSeconds(1)),true);
   review=s.TicketlandSectorReviews030().Single();Check(review.MissingSeats.Single().Contains("2")&&review.Reason.Contains("ряд",StringComparison.OrdinalIgnoreCase)&&review.Candidates.Count==0,"combined missing row scanned wrong sector");
  });
  run("032 balcony aliases right left and VIP remain separate",s=>{
   var f=Fixture(s,"balconies",new DraftSeat{Sector="Балкон 2 правый",Row="1",Number="1"},new DraftSeat{Sector="Балкон 2 левый",Row="1",Number="1"},new DraftSeat{Sector="VIP 2",Row="1",Number="1"});
   var now=DateTimeOffset.UtcNow;
   foreach(var alias in new[]{"Балкон2 правая сторона", "Балкон 2 правая сторона","Балкон 2 прав. сторона","Балкон справа 2","Балкона второго правая сторона","Правая сторона 2"})
   {
    Check(s.ApplyTicketlandReport028(new(new[]{Sale(f,alias)},now),true).Matched==1,"right alias failed: "+alias);
    Check(s.Seats(f.Event).Single(x=>x.IsTicketlandSold).Sector=="Балкон 2 правый","right selected wrong sector");now=now.AddSeconds(1);
   }
   foreach(var alias in new[]{"VIP2","ВИП2","VIP №2","ВИП 2"})
   {Check(s.ApplyTicketlandReport028(new(new[]{Sale(f,alias)},now),true).Matched==1&&s.Seats(f.Event).Single(x=>x.IsTicketlandSold).Sector=="VIP 2","VIP alias failed: "+alias);now=now.AddSeconds(1);}
   s.ApplyTicketlandReport028(new(new[]{Sale(f,"Балконон правый 2")},now),true);now=now.AddSeconds(1);
   Check(s.TicketlandSectorReviews030().Single().Candidates.Count==0,"unknown family word became contextual VIP/balcony guess");
   var glued=Fixture(s,"glued VIP",new DraftSeat{Sector="ВИП2",Row="1",Number="1"},new DraftSeat{Sector="Балкон2 правый",Row="1",Number="1"});
   Check(s.ApplyTicketlandReport028(new(new[]{Sale(glued,"VIP №2")},now),true).Matched==1&&s.Seats(glued.Event).Single(x=>x.IsTicketlandSold).Sector=="ВИП2","local glued type mismatch");
  });
  run("032 abbreviated sector ambiguous across families never uses position",s=>{
   var f=Fixture(s,"abbreviated",new DraftSeat{Sector="Балкон 2 правый",Row="1",Number="1"},new DraftSeat{Sector="Ложа 2 правая",Row="2",Number="2"});
   Check(s.ApplyTicketlandReport028(new(new[]{Sale(f,"Правая сторона 2")},DateTimeOffset.UtcNow),true).Matched==0,"abbreviation used position to disambiguate");
   Check(s.TicketlandSectorReviews030().Single().Candidates.Count==0,"ambiguous abbreviation offered");
  });
  run("032 short left and right box aliases preserve distinct overlapping positions",s=>{
   var f=Fixture(s,"boxes",new DraftSeat{Sector="Левая ложа",Row="1",Number="1"},new DraftSeat{Sector="Правая ложа",Row="1",Number="1"});
   var left=Sale(f,"Ложа партера левая");var right=Sale(f,"Ложа партера правая") with{Barcode="right"};
   Check(s.ApplyTicketlandReport028(new(new[]{left,right},DateTimeOffset.UtcNow),true).Matched==2,"short boxes failed");Check(s.Seats(f.Event).Count(x=>x.IsTicketlandSold)==2,"box markers merged");
  });
  run("032 unknown labels never propose whole hall by coincident positions",s=>{
   var f=Fixture(s,"unknown",new DraftSeat{Sector="Партер",Row="1",Number="1"});
   s.ApplyTicketlandReport028(new(new[]{Sale(f,"Источник A")},DateTimeOffset.UtcNow),true);
   var review=s.TicketlandSectorReviews030().Single();Check(review.Candidates.Count==0&&review.Reason.Contains("Источник A",StringComparison.OrdinalIgnoreCase)&&review.Reason.Contains("назван",StringComparison.OrdinalIgnoreCase),"unknown alias guessed or unexplained");
  });
  run("032 resolved sector missing seat is scoped despite another sector having position",s=>{
   var f=Fixture(s,"missing seat",new DraftSeat{Sector="Балкон 2 правый",Row="1",Number="1"},new DraftSeat{Sector="Балкон 2 левый",Row="1",Number="2"});
   s.ApplyTicketlandReport028(new(new[]{Sale(f,"Балкон 2 правая сторона","1","2")},DateTimeOffset.UtcNow),true);
   var review=s.TicketlandSectorReviews030().Single();Check(review.Candidates.Count==0&&review.MissingSeats.Single().Contains("место 2")&&review.Reason.Contains("место",StringComparison.OrdinalIgnoreCase),"missing seat hidden by opposite side");
  });
  run("032 cached reapply remaps offline without changing source timestamp or local accounting",s=>{
   var f=Fixture(s,"Владимир Винокур «Винокур-Шоу»",new DraftSeat{Sector="Партер",Row="1",Number="1"});
   var sale=Sale(f,"Партер") with{Title="Владимир Винокур. \"Винокур - Шоу\" . Новогодняя программа."};var at=DateTimeOffset.UtcNow;
   var seat=s.Seats(f.Event).Single();s.Issue(f.Event,new[]{seat.Id},"Получатель032","P032",1);
   s.ApplyTicketlandReport028(new(new[]{sale},at),true);
   using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}")){c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE ticketland_sale_028 SET event_id=NULL,seat_id=NULL";q.ExecuteNonQuery();}
   var before=s.CachedTicketlandReport028();var reopened=new Store(s.Path);var result=reopened.ReapplyCachedTicketland032();var after=reopened.Seats(f.Event).Single();
   Check(result?.Matched==1&&after.IsTicketlandSold&&after.TicketId.HasValue,"cache marker did not remap");
   Check(after.Status.Contains("Получатель032")&&after.Amount==seat.Amount&&reopened.TicketlandLastSync028==at&&reopened.CachedTicketlandReport028().Sales.SequenceEqual(before.Sales),"cached reapply changed local issue source price or timestamp");
  });
  run("032 unconfirmed arbitrary legacy same-family wrong-row marker never bypass resolver",s=>{
   var f=Fixture(s,"legacy guarded",new DraftSeat{Sector="Партер",Row="1",Number="1"});var sale=Sale(f,"Источник A","99","99");
   s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);var key=s.TicketlandSectorReviews030().Single().Key;var seat=s.Seats(f.Event).Single().Id;
   using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}")){c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE ticketland_sale_028 SET event_id=$event,seat_id=$seat";q.Parameters.AddWithValue("$key",key);q.Parameters.AddWithValue("$event",f.Event);q.Parameters.AddWithValue("$seat",seat);q.ExecuteNonQuery();}
   Check(s.ReapplyCachedTicketland032()?.Matched==0&&!s.Seats(f.Event).Single().IsTicketlandSold&&s.TicketlandSectorReviews030().Single().Candidates.Count==0,"legacy arbitrary marker bypassed names");
  });

  run("032 legacy title key preserves accepted name knowledge without new unknown guesses",s=>{
   var f=Fixture(s,"Legacy title multiple words",new DraftSeat{Sector="Партер",Row="1",Number="1"});
   var sale=Sale(f,"Партер Источник");s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);
   var seat=s.Seats(f.Event).Single();string T(string text)=>System.Text.RegularExpressions.Regex.Replace(text.Normalize().ToUpperInvariant().Replace('Ё','Е'),@"[^\p{L}\p{Nd}]+"," ").Trim();
   var source=$"{f.Event}|{T(f.Title)}|{f.At:O}|{T(f.Venue)}|{T(sale.Sector)}|{seat.Id}:{seat.Sector}:{seat.Row}:{seat.Number}";
   var old=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
   using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}")){c.Open();using var q=c.CreateCommand();q.CommandText="INSERT OR REPLACE INTO ticketland_sector_decision_030 VALUES($key,0,'Партер')";q.Parameters.AddWithValue("$key",old);q.ExecuteNonQuery();}
   var reopened=new Store(s.Path);Check(reopened.ReapplyCachedTicketland032()?.Matched==1&&reopened.Seats(f.Event).Single().IsTicketlandSold,"legacy multiword title decision lost");
  });

  run("032 retained contextual sector proof cannot cross balcony to VIP",s=>{
   var f=Fixture(s,"proof family",new DraftSeat{Sector="Балкон 2 правый",Row="1",Number="1"});var sale=Sale(f,"Правая сторона 2");var at=DateTimeOffset.UtcNow;
   Check(s.ApplyTicketlandReport028(new(new[]{sale},at),true).Matched==1,"proof fixture did not resolve");
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,"Балкон 2 правый","VIP 2 правый");s.SaveEditedHall(venue,hall.Name,hall.Address,draft);
   for(var i=0;i<3;i++){s.ReapplyCachedTicketland032();Check(!s.Seats(f.Event).Single().IsTicketlandSold,"context proof crossed balcony to VIP");}
  });
  run("032 proof retained markers participate in source collision before storing",s=>{
   var f=Fixture(s,"proof collision",new DraftSeat{Sector="Партер",Row="1",Number="1"});var sale=Sale(f,"Партер");var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale},at),true);
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,"Партер","Партер Источник");s.SaveEditedHall(venue,hall.Name,hall.Address,draft);
   var result=s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="proof-new",Sector="Партер Источник"}},at.AddSeconds(1)),true);
   Check(result.Matched==0&&result.Unmatched==2&&!s.Seats(f.Event).Single().IsTicketlandSold,"retained marker escaped source collision");
   Check(s.TicketlandSectorReviews030().All(x=>x.Candidates.Count==0),"retained collision still actionable");
   for(var i=0;i<2;i++){s.ReapplyCachedTicketland032();Check(!s.Seats(f.Event).Single().IsTicketlandSold,"repeated cache reapply selected collision winner");}
  });

  run("032 known abbreviated sector family survives weaker captions new barcodes and returns",s=>{
   var f=Fixture(s,"source identity knowledge",new DraftSeat{Sector="Балкон 2 правый",Row="1",Number="1"});var sale=Sale(f,"Правая сторона 2");var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale},at),true);
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);
   void Rename(string from,string to){var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,from,to);s.SaveEditedHall(venue,hall.Name,hall.Address,draft);}
   Rename("Балкон 2 правый","Правая сторона 2");Check(s.ReapplyCachedTicketland032()?.Matched==1,"compatible weak caption lost established place");
   Rename("Правая сторона 2","VIP 2 правый");var added=sale with{Barcode="new-known-source"};
   Check(s.ApplyTicketlandReport028(new(new[]{sale,added},at.AddSeconds(1)),true).Matched==0&&!s.Seats(f.Event).Single().IsTicketlandSold,"new barcode forgot original balcony family");
   s.ApplyTicketlandReport028(new(Array.Empty<TicketlandSale028>(),at.AddSeconds(2)),true);
   Check(s.ApplyTicketlandReport028(new(new[]{sale with{Barcode="after-return"}},at.AddSeconds(3)),true).Matched==0&&!s.Seats(f.Event).Single().IsTicketlandSold,"returns erased established source-sector identity");
  });

  run("032 append enriched source identity also guards older weaker barcode proofs",s=>{
   var f=Fixture(s,"append knowledge",new DraftSeat{Sector="Правая сторона 2",Row="1",Number="1"});var sale=Sale(f,"Правая сторона 2");var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale},at),true);
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);
   void Rename(string from,string to){var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,from,to);s.SaveEditedHall(venue,hall.Name,hall.Address,draft);}
   Rename("Правая сторона 2","Балкон 2 правый");s.ApplyTicketlandReport028(new(new[]{sale with{Barcode="append-enrichment"}},at.AddSeconds(1)),false);
   Rename("Балкон 2 правый","VIP 2");
   Check(s.ReapplyCachedTicketland032()?.Matched==0&&!s.Seats(f.Event).Single().IsTicketlandSold,"old weak barcode proof ignored enriched source family");
  });

 }
 private static (long Event,DateTime At,string Venue,string Title) Fixture(Store s,string title,params DraftSeat[] seats)
 {
  var name="032 hall "+Guid.NewGuid();var venue=s.CreateHall(name,"",seats);var plan=s.SavePlan(venue,"p",s.VenueSeats(venue).Select(x=>new PriceValue(x.Id,500,"#FF0000")).ToArray());var at=new DateTime(2027,1,3,20,0,0);
  return(s.CreateEvent(s.SavePerformance(title),venue,plan,at),at,name,title);
 }
 private static TicketlandSale028 Sale((long Event,DateTime At,string Venue,string Title) f,string sector,string row="1",string seat="1")=>new(f.Title,f.At,sector,row,seat,Guid.NewGuid().ToString(),70){Venue=f.Venue};
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
