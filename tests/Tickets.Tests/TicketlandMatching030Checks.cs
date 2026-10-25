using Tickets.Core;
internal static class TicketlandMatching030Checks
{
 internal static void Register(Action<string,Action<Store>> run)
 {
  run("030 unrelated shows and gifts never create review proposals",s=>{
   var f=Fixture(s);var sale=new TicketlandSale028("Another show",f.At,"Unknown","1","1","unrelated",70){Venue=f.Venue};
   s.ApplyTicketlandReport028(new(new[]{sale,sale with{Title="Ёлка",Barcode="gift",Row="--",Seat="--"}},DateTimeOffset.UtcNow),true);
   Check(s.TicketlandIssues029().Count==0,"unrelated show or gift produced actionable issue");
  });
  run("030 premiere prefix exact match requires venue",s=>{
   var f=Fixture(s);var sale=new TicketlandSale028("Премьера спектакля «Елка»",f.At,"Партер","1","1","premiere",70){Venue=f.Venue};
   Check(s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true).Matched==1,"decorative prefix not normalized");
   Check(s.ApplyTicketlandReport028(new(new[]{sale with{Barcode="no-venue",Venue=""}},DateTimeOffset.UtcNow.AddSeconds(1)),true).Matched==0,"missing venue guessed");
  });
  run("030 sector approval covers group future barcodes and invalidates on layout edit",s=>{
   var f=Fixture(s);var sale=new TicketlandSale028("Ёлка",f.At,"Remote sector","1","1","group1",70){Venue=f.Venue};
   var now=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="group2",Seat="2",Nominal=90}},now),true);
   var group=s.TicketlandSectorReviews030().Single();Check(group.TicketCount==2&&group.Candidates.Single().Sector=="Партер","not one complete sector group");
   s.AcceptTicketlandSector030(group.Key,"Партер");s=new Store(s.Path);
   var future=sale with{Barcode="future"};Check(s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="group2",Seat="2",Nominal=90},future},now.AddSeconds(1)),true).Matched==3,"group approval not persistent for future tickets");
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,"Партер","New sector");s.SaveEditedHall(venue,hall.Name,hall.Address,draft);
   Check(s.TicketlandSectorReviews030().Single().Key!=group.Key,"layout change did not invalidate approval");
  });
  run("030 refusal persists across new source tickets but layout change reopens",s=>{
   var f=Fixture(s);var sale=new TicketlandSale028("Ёлка",f.At,"Remote sector","1","1","refuse1",70){Venue=f.Venue};var now=DateTimeOffset.UtcNow;
   s.ApplyTicketlandReport028(new(new[]{sale},now),true);var group=s.TicketlandSectorReviews030().Single();s.RejectTicketlandSector030(group.Key);s=new Store(s.Path);
   s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="refuse2",Seat="2"}},now.AddSeconds(1)),true);Check(s.TicketlandSectorReviews030().Count==0&&!s.Seats(f.Event).Any(x=>x.IsTicketlandSold),"refusal lost on new barcode");
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,"Партер","Updated");s.SaveEditedHall(venue,hall.Name,hall.Address,draft);
   Check(s.TicketlandSectorReviews030().Count==1,"layout change did not invalidate refusal");
  });
  run("030 missing seats never offer renumbering and totals include raw unmatched and gifts",s=>{
   var f=Fixture(s);var sale=new TicketlandSale028("Ёлка",f.At,"Партер","1","1","total1",70){Venue=f.Venue};
   var missing=sale with{Barcode="missing",Sector="Unknown",Row="99",Nominal=120};var gift=sale with{Barcode="totalgift",Row="--",Seat="--",Nominal=0};
   s.ApplyTicketlandReport028(new(new[]{sale,missing,gift,sale},DateTimeOffset.UtcNow),true);
   var group=s.TicketlandSectorReviews030().Single();Check(group.MissingSeats.Single().Contains("99")&&group.Candidates.Count==0,"invented missing row correction");
   var total=s.TicketlandSourceTotals030().Single(t=>t.EventId==f.Event);Check(total.TicketCount==3&&total.Nominal==190,"total used local price or dropped unmapped/gift rows");
  });
  run("030 legacy manual show binding cannot cross title time date venue",s=>{
   var f=Fixture(s);var original=new TicketlandSale028("Ёлка",f.At,"Партер","1","1","legacy",70){Venue=f.Venue};
   foreach(var sale in new[]{original with{Title="Other"},original with{StartsAt=f.At.AddMinutes(1)},original with{StartsAt=f.At.AddDays(1)},original with{Venue="Other hall"}})
   {
    s.ApplyTicketlandReport028(new(new[]{sale with{Barcode=Guid.NewGuid().ToString()}},DateTimeOffset.UtcNow),true);
    try{s.BindTicketlandShow028(sale.Title,sale.StartsAt,f.Event);throw new Exception("wrong show accepted");}catch(TicketException){}
    Check(s.TicketlandSectorReviews030().Count==0,"wrong show is actionable");
   }
  });

  run("030 distinct source sector collision remains unmapped after manual sector confirmation",s=>{
   var f=Fixture(s);var sale=new TicketlandSale028("Ёлка",f.At,"Remote","1","1","manual-collision",70){Venue=f.Venue};var now=DateTimeOffset.UtcNow;
   s.ApplyTicketlandReport028(new(new[]{sale},now),true);s.AcceptTicketlandSector030(s.TicketlandSectorReviews030().Single().Key,"Партер");
   var other=sale with{Barcode="auto-collision",Sector="Партер"};var result=s.ApplyTicketlandReport028(new(new[]{sale,other},now.AddSeconds(1)),true);
   Check(result.Matched==0&&result.Unmatched==2&&!s.Seats(f.Event).Any(x=>x.IsTicketlandSold),"manual sector mapped distinct source seats together");
   Check(s.TicketlandSectorReviews030().All(g=>g.Candidates.Count==0),"collision still has actionable mapping");
  });

  run("030 sector approval cannot claim a seat already proven for another source sector",s=>{
   var f=Fixture(s);var now=DateTimeOffset.UtcNow;var proven=new TicketlandSale028("Ёлка",f.At,"Партер","1","1","proven-sector",70){Venue=f.Venue};
   s.ApplyTicketlandReport028(new(new[]{proven},now),true);
   var unknown=proven with{Sector="Remote",Barcode="new-sector"};s.ApplyTicketlandReport028(new(new[]{proven,unknown},now.AddSeconds(1)),true);
   var group=s.TicketlandSectorReviews030().Single();Check(group.Candidates.Count==0,"occupied target offered for manual sector approval");
   try{s.AcceptTicketlandSector030(group.Key,"Партер");throw new Exception("occupied target accepted");}catch(TicketException){}
   Check(s.Seats(f.Event).Single(x=>x.Number=="1").IsTicketlandSold,"proven marker lost on rejected mapping");
  });

  run("030 reimport removes legacy wrong-show manual markers from current and cached snapshots",s=>{
   var f=Fixture(s);var source=new TicketlandSale028("Ёлка",f.At,"Партер","1","1","legacy-wrong-marker",70){Venue=f.Venue};var now=DateTimeOffset.UtcNow;var n=0;
   foreach(var wrong in new[]{source with{Title="Other title"},source with{StartsAt=f.At.AddMinutes(1)},source with{StartsAt=f.At.AddDays(1)},source with{Venue="Other venue"}})
   {
    var sale=wrong with{Barcode="legacy-wrong-"+(n++)};s.ApplyTicketlandReport028(new(new[]{sale},now.AddSeconds(n)),true);
    using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}"))
    {c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE ticketland_sale_028 SET event_id=$e,seat_id=$s WHERE barcode=$b";q.Parameters.AddWithValue("$e",f.Event);q.Parameters.AddWithValue("$s",s.Seats(f.Event).Single(x=>x.Number=="1").Id);q.Parameters.AddWithValue("$b",sale.Barcode);q.ExecuteNonQuery();}
    Check(s.Seats(f.Event).Any(x=>x.IsTicketlandSold),"legacy fixture marker missing");
    var result=s.ApplyTicketlandReport028(new(new[]{sale},now.AddSeconds(n+10)),true);
    Check(result.Matched==0&&!s.Seats(f.Event).Any(x=>x.IsTicketlandSold),"legacy wrong-show marker survived full snapshot");
    s.ApplyTicketlandReport028(s.CachedTicketlandReport028(),true);
    Check(!s.Seats(f.Event).Any(x=>x.IsTicketlandSold)&&s.CachedTicketlandReport028().Sales.Count==1,"cached reapply restored wrong-show marker or lost raw sale");
    now=now.AddMinutes(1);
   }
  });

 }
 private static (long Event,DateTime At,string Venue) Fixture(Store s)
 {
  var name="Hall "+Guid.NewGuid();var venue=s.CreateHall(name,"",new[]{new DraftSeat{Sector="Партер",Row="1",Number="1"},new DraftSeat{Sector="Партер",Row="1",Number="2"}});
  var plan=s.SavePlan(venue,"p",s.VenueSeats(venue).Select(x=>new PriceValue(x.Id,500,"#FF0000")).ToArray());var at=new DateTime(2030,5,7,19,0,0);
  return(s.CreateEvent(s.SavePerformance("Ёлка"),venue,plan,at),at,name);
 }
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
