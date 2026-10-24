using Tickets.Core;
internal static class TicketlandMatching029Checks
{
 internal static void Register(Action<string,Action<Store>> run)
 {
  run("029 normalized show and sector aliases match unique seat",s=>{
   var f=Fixture(s,"Ёлка: Новый год", "Партер");
   var sale=new TicketlandSale028("Спектакль «Елка — Новый год»",f.At,"Партер левый","1","1","alias",500);
   var result=s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);
   Check(result.Matched==1,"normalized aliases did not match");
  });
  run("029 supplied venue distinguishes shows and survives cache",s=>{
   var f=Fixture(s,"Venue show","Партер");var other=Fixture(s,"Venue show","Партер");
   var venue=s.Venues().Single(v=>v.Id==s.Events().Single(e=>e.Id==f.Event).VenueId).Name;
   var sale=new TicketlandSale028("Venue show",f.At,"Партер","1","1","venue",500){Venue=venue,SourceChannel="Vendor"};
   Check(s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true).Matched==1,"supplied venue ignored");
   Check(s.Seats(f.Event).Single().IsTicketlandSold&&!s.Seats(other.Event).Single().IsTicketlandSold,"wrong venue sold");
   var cached=s.CachedTicketlandReport028().Sales.Single();Check(cached.Venue==venue.ToUpperInvariant()&&cached.SourceChannel=="Vendor","provenance lost");
  });
  run("029 canonical sector fallback refuses multiple real seats",s=>{
   var venue=s.CreateHall("Ambiguous hall "+Guid.NewGuid(),"",new[]{new DraftSeat{Sector="Партер левый",Row="1",Number="1"},new DraftSeat{Sector="Партер правый",Row="1",Number="1"}});
   var ids=s.VenueSeats(venue).Select(x=>x.Id).ToArray();var plan=s.SavePlan(venue,"p",ids.Select(id=>new PriceValue(id,500,"#FF0000")).ToArray());
   var at=new DateTime(2030,5,7,19,0,0);var title="Ambiguous seats "+Guid.NewGuid();var evt=s.CreateEvent(s.SavePerformance(title),venue,plan,at);
   var sale=new TicketlandSale028(title,at,"Партер","1","1","amb",500);s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);
   var issue=s.TicketlandIssues029().Single();Check(!s.Seats(evt).Any(x=>x.IsTicketlandSold)&&issue.Candidates.Count==2,"canonical aliases collapsed seats");
   s.AcceptTicketlandIssue029(issue.Key,evt,ids[1]);s=new Store(s.Path);Check(s.TicketlandIssues029().Count==0,"approval not persisted");
   s.ApplyTicketlandReport028(s.CachedTicketlandReport028(),true);Check(s.Seats(evt).Single(x=>x.Id==ids[1]).IsTicketlandSold&&!s.Seats(evt).Single(x=>x.Id==ids[0]).IsTicketlandSold,"approved wrong ambiguous seat");
  });
  run("029 approvals and refusals persist until source or local criteria change",s=>{
   var f=Fixture(s,"Local name","Партер");var sale=new TicketlandSale028("Remote name",f.At.AddMinutes(1),"Партер","1","1","approval",500){Venue=s.Venues().Single(v=>v.Id==s.Events().Single(e=>e.Id==f.Event).VenueId).Name};
   s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);var issue=s.TicketlandIssues029().Single();var candidate=issue.Candidates.Single(x=>x.EventId==f.Event&&x.SeatId.HasValue);
   Check(candidate.Differences.Contains("название")&&candidate.Differences.Contains("время"),"field explanations missing");
   var oldKey=issue.Key;s.RejectTicketlandIssue029(issue.Key);s=new Store(s.Path);Check(s.TicketlandIssues029().Count==0,"refusal repeated");
   var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);var draft=s.HallDraft(venue);HallEditing027.RenameSector(draft,"Партер","Партер левый");s.SaveEditedHall(venue,hall.Name,hall.Address,draft);
   issue=s.TicketlandIssues029().Single();Check(issue.Key!=oldKey,"local edit did not reopen issue");
   var current=issue.Candidates.Single(x=>x.EventId==f.Event&&x.SeatId.HasValue);s.AcceptTicketlandIssue029(issue.Key,current.EventId,current.SeatId);s.ApplyTicketlandReport028(s.CachedTicketlandReport028(),true);
   Check(s.Seats(f.Event).Single().IsTicketlandSold,"approved differing datetime not applied");
  });
  run("029 numbered row mismatch offers explicit seat correction",s=>{
   var f=Fixture(s,"Row correction","Партер");var sale=new TicketlandSale028("Row correction",f.At,"Партер","2","1","row-correction",500);
   s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);var issue=s.TicketlandIssues029().Single();
   Check(issue.Candidates.Any(x=>x.SeatId.HasValue&&x.Differences.Contains("ряд:")),"row mismatch has no explicit correction");
  });

  run("029 balcony order preserves level and side",s=>{
   var f=Fixture(s,"Balcony show","Балкон 2 левый");
   var sales=new[]{new TicketlandSale028("Balcony show",f.At,"2-й балкон левая сторона","1","1","balcony",500),new TicketlandSale028("Balcony show",f.At,"Балкон 1 левый","1","1","wrong-level",500),new TicketlandSale028("Balcony show",f.At,"Балкон 2 правый","1","1","wrong-side",500)};
   var result=s.ApplyTicketlandReport028(new(sales,DateTimeOffset.UtcNow),true);Check(result.Matched==1&&result.Unmatched==2,"balcony number/side identity incorrect");
  });

  run("029 distinct source sectors cannot silently collapse onto one local seat",s=>{
   var f=Fixture(s,"Source collision","Партер");var sale=new TicketlandSale028("Source collision",f.At,"Партер левый","1","1","left",500);
   var result=s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="right",Sector="Партер правый"}},DateTimeOffset.UtcNow),true);
   Check(result.Matched==0&&result.Unmatched==2&&!s.Seats(f.Event).Single().IsTicketlandSold&&s.TicketlandIssues029().Count==2,"different source seats collapsed");
  });

  run("029 append checks cached source seats before canonical fallback",s=>{
   var f=Fixture(s,"Append collision","Партер");var sale=new TicketlandSale028("Append collision",f.At,"Партер левый","1","1","append-left",500);var at=DateTimeOffset.UtcNow;
   s.ApplyTicketlandReport028(new(new[]{sale},at),true);
   var result=s.ApplyTicketlandReport028(new(new[]{sale with{Barcode="append-right",Sector="Партер правый"}},at.AddSeconds(1)),false);
   Check(result.Matched==0&&result.Unmatched==1&&s.TicketlandIssues029().Count==2,"append silently collapsed another cached source seat");
  });

  run("029 amphitheatre and bel etage source parts resolve unique seats",s=>{
   var amph=Fixture(s,"Amphitheatre parts","Амфитеатр");var at=DateTimeOffset.UtcNow;var index=0;
   foreach(var sector in new[]{"Амфитеатр середина","Амфитеатр средний","Амфитеатр центральная часть"})
   {
    var sale=new TicketlandSale028("Amphitheatre parts",amph.At,sector,"1","1","amph-"+(index++),500);
    Check(s.ApplyTicketlandReport028(new(new[]{sale},at.AddSeconds(index)),true).Matched==1,"amphitheatre part failed: "+sector);
   }
   var bel=Fixture(s,"Bel etage parts","Бель-этаж");
   foreach(var sector in new[]{"Бельэтаж левый","Бельэтаж правый","Бельэтаж центр"})
   {
    var sale=new TicketlandSale028("Bel etage parts",bel.At,sector,"1","1","bel-"+(index++),500);
    Check(s.ApplyTicketlandReport028(new(new[]{sale},at.AddSeconds(index)),true).Matched==1,"bel etage part failed: "+sector);
   }
  });

  run("029 observed source venue aliases match only their established stages",s=>{
   var pairs=new[]{("Театр Эстрады","Московский государственный театр Эстрады"),("Театр Русская Песня Олимпийский пр., д. 14","Театр «Русская Песня»"),("Центральный дом культуры железнодорожников","Театрально-концертный зал ЦДКЖ")};var index=0;var now=DateTimeOffset.UtcNow;
   foreach(var pair in pairs)
   {
    var title="Grounded venue "+index;var f=Fixture(s,title,"Партер",pair.Item2);var sale=new TicketlandSale028(title,f.At,"Партер","1","1","venue-alias-"+index,500){Venue=pair.Item1};
    Check(s.ApplyTicketlandReport028(new(new[]{sale},now.AddSeconds(index++)),true).Matched==1,"known venue alias failed: "+pair.Item1);
   }
   var main=Fixture(s,"Unknown stage","Партер","Театр «Русская Песня»");var source=new TicketlandSale028("Unknown stage",main.At,"Партер","1","1","unknown-stage",500){Venue="Театр Русская Песня Новая сцена"};
   Check(s.ApplyTicketlandReport028(new(new[]{source},now.AddSeconds(index++)),true).Unmatched==1,"different stage guessed");
   Check(s.ApplyTicketlandReport028(new(new[]{source with{Barcode="unknown-address",Venue="Театр Русская Песня Олимпийский пр., д. 12"}},now.AddSeconds(index++)),true).Unmatched==1,"unknown venue address guessed");
   var duplicate=Fixture(s,"Unknown stage","Партер","Театр Русская Песня Олимпийский пр., д. 14");
   Check(s.ApplyTicketlandReport028(new(new[]{source with{Barcode="duplicate-alias",Venue="Театр Русская Песня Олимпийский пр., д. 14"}},now.AddSeconds(index++)),true).Unmatched==1,"two local alias halls were not ambiguous");
  });

  run("029 old empty source venue retains proven seat only at supplied known venue",s=>{
   var title="Venue upgrade "+Guid.NewGuid();var f=Fixture(s,title,"Партер","Театр «Русская Песня»");var at=DateTimeOffset.UtcNow;
   var sale=new TicketlandSale028(title,f.At,"Партер","1","1","venue-upgrade",500);s.ApplyTicketlandReport028(new(new[]{sale},at),true);
   var duplicate=Fixture(s,title,"Партер","Театр Русская Песня Олимпийский пр., д. 14");
   var result=s.ApplyTicketlandReport028(new(new[]{sale with{Venue="Театр Русская Песня Олимпийский пр., д. 14"}},at.AddSeconds(1)),true);
   Check(result.Unmatched==1&&s.Seats(f.Event).Single().IsTicketlandSold&&!s.Seats(duplicate.Event).Single().IsTicketlandSold,"known old seat lost on venue metadata upgrade");
  });

  run("029 hidden performance is neither an automatic match nor a proposal",s=>{
   var title="Hidden performance "+Guid.NewGuid();var hidden=Fixture(s,title,"Партер");var active=Fixture(s,"Active alternative "+Guid.NewGuid(),"Партер");
   using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}"))
   {
    c.Open();using var q=c.CreateCommand();q.CommandText="INSERT INTO catalog_hidden(kind,entity_key) VALUES('performance',$key)";q.Parameters.AddWithValue("$key",CatalogText.Key(title));q.ExecuteNonQuery();
   }
   Check(!s.Events().Any(e=>e.Id==hidden.Event),"fixture performance is still active");
   var sale=new TicketlandSale028(title,hidden.At,"Партер","1","1","hidden-performance",500);
   var result=s.ApplyTicketlandReport028(new(new[]{sale},DateTimeOffset.UtcNow),true);
   Check(result.Matched==0&&result.Unmatched==1&&!s.Seats(hidden.Event).Single().IsTicketlandSold,"hidden performance matched automatically");
   var issue=s.TicketlandIssues029().Single();Check(issue.Candidates.All(c=>c.EventId!=hidden.Event)&&issue.Candidates.Any(c=>c.EventId==active.Event),"hidden performance remains a proposal");
  });

 }
 private static (long Event,DateTime At) Fixture(Store s,string title,string sector,string? venueName=null)
 {
  var venue=s.CreateHall(venueName??("Hall "+Guid.NewGuid()),"",new[]{new DraftSeat{Sector=sector,Row="1",Number="1"}});
  var plan=s.SavePlan(venue,"p",s.VenueSeats(venue).Select(x=>new PriceValue(x.Id,500,"#FF0000")).ToArray());
  var at=new DateTime(2030,5,7,19,0,0);return(s.CreateEvent(s.SavePerformance(title),venue,plan,at),at);
 }
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
