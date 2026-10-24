using Tickets.Core;
using Microsoft.Data.Sqlite;

internal static class TicketlandStore028Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("028 Ticketland snapshot persists idempotently without local accounting",s=>{
            var f=Fixture(s);var now=DateTimeOffset.UtcNow;var sale=Sale(f,"1","remote-1");
            var report=new TicketlandReport028(new[]{sale,sale},now);
            var result=s.ApplyTicketlandReport028(report,false);Check(result.Matched==1&&result.Unmatched==0,"duplicate report row not deduplicated");
            s.ApplyTicketlandReport028(report,false);var reopened=new Store(s.Path);var seat=reopened.Seats(f.Event).Single(x=>x.Id==f.Seats[0]);
            Check(seat.IsTicketlandSold&&!seat.Available&&!SeatSelection.CanSelect(seat)&&seat.Status=="Продано Ticketland","external sale is selectable or absent after reopening");
            Check(reopened.TicketlandLastSync028==now&&reopened.History(f.Event).Count==0&&reopened.Issues(f.Event).Count==0,"external sales polluted local history or lost sync timestamp");
            Check(Scalar(s,"SELECT count(*) FROM ticketland_sale_028")==1,"repeat fetch duplicated persisted sale");
        });
        run("028 Ticketland prevents local issue reservation and invoice extension",s=>{
            var f=Fixture(s);var issue=s.Issue(f.Event,new[]{f.Seats[1]},"local","TL028",1);
            s.ApplyTicketlandReport028(new(new[]{Sale(f,"1","remote-2")},DateTimeOffset.UtcNow),false);
            Reject(()=>s.Issue(f.Event,new[]{f.Seats[0]},"local","TL028",2));
            Reject(()=>s.Reserve(f.Event,new[]{f.Seats[0]},"local","TL028",2));
            Reject(()=>s.ExtendInvoice(f.Event,issue,new[]{f.Seats[0]}));
            Check(s.History(f.Event).Count==1&&s.Issues(f.Event).Count==1,"rejected external seat created local printable accounting");
        });
        run("028 Ticketland collision blocks reserved ticket printing and confirmation atomically",s=>{
            var f=Fixture(s);var issue=s.Reserve(f.Event,new[]{f.Seats[0],f.Seats[1]},"local","TLPRINT028",1);
            var before=s.History(f.Event).ToArray();var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{Sale(f,"2","reserved-collision")},at),true);
            Reject(()=>s.TicketReprint(f.Event,new[]{f.Seats[0],f.Seats[1]}));
            Reject(()=>s.ConfirmReservation(f.Event,issue));
            Reject(()=>s.MarkTicketsPrinted(f.Event,before.OrderBy(t=>t.SeatId).Select(t=>t.Id).ToArray()));
            Check(s.History(f.Event).SequenceEqual(before)&&s.History(f.Event).All(t=>t.IsReserved&&!t.IsPrinted),"collision changed original reservation or partly marked tickets printed");
            Check(s.Invoice(f.Event,issue).Tickets.Count==2,"collision hid historical invoice accounting");
            s.ApplyTicketlandReport028(new(Array.Empty<TicketlandSale028>(),at.AddSeconds(1)),true);
            s.ConfirmReservation(f.Event,issue);var printable=s.TicketReprint(f.Event,new[]{f.Seats[0],f.Seats[1]});
            Check(printable.Tickets.Count==2&&printable.Tickets.All(t=>!t.IsReserved),"resolved collision still blocked confirmation or printable snapshot");
            s.MarkTicketsPrinted(f.Event,printable.Tickets.Select(t=>t.Id).ToArray());
            Check(s.History(f.Event).All(t=>t.IsPrinted&&!t.IsReserved)&&s.Issues(f.Event).Single().Id==issue,"normal printing after external return lost the original issue");
        });
        run("028 Ticketland conflicts remain occupied and replacement preserves local data",s=>{
            var f=Fixture(s);var issue=s.Issue(f.Event,new[]{f.Seats[0]},"local","TLC028",1);s.SetSeatsBlocked(f.Event,new[]{f.Seats[1]},true);
            var result=s.ApplyTicketlandReport028(new(new[]{Sale(f,"1","conflict"),Sale(f,"2","blocked"),Sale(f,"3","remote")},DateTimeOffset.UtcNow),false);
            Check(result.Matched==3&&result.Conflicts==1,"local collision not counted");
            var collision=s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]);Check(collision.IsTicketlandSold&&collision.TicketId.HasValue&&collision.Status.Contains("Конфликт"),"collision silently discarded local or external sale");
            s.ApplyTicketlandReport028(new(Array.Empty<TicketlandSale028>(),DateTimeOffset.UtcNow.AddSeconds(1)),true);
            var seats=s.Seats(f.Event);Check(seats.Single(x=>x.Id==f.Seats[2]).Available,"explicit replacement did not release remote-only sale");
            Check(seats.Single(x=>x.Id==f.Seats[1]).IsBlocked&&!seats.Single(x=>x.Id==f.Seats[1]).Available&&s.History(f.Event).Single().IssueId==issue,"replacement freed manual block or local issue");
        });
        run("028 Ticketland rejects malformed contradictory and stale snapshots atomically",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;var sale=Sale(f,"1","atomic");s.ApplyTicketlandReport028(new(new[]{sale},at),false);
            Reject(()=>s.ApplyTicketlandReport028(new(new[]{Sale(f,"2","valid"),Sale(f,"3","")},at.AddSeconds(1)),true));
            Reject(()=>s.ApplyTicketlandReport028(new(new[]{sale,sale with{Seat="2"}},at.AddSeconds(1)),true));
            Reject(()=>s.ApplyTicketlandReport028(new(new[]{sale with{Seat="2"}},at.AddSeconds(1)),false));
            Reject(()=>s.ApplyTicketlandReport028(new(Array.Empty<TicketlandSale028>(),at.AddSeconds(-1)),true));
            Check(s.TicketlandLastSync028==at&&Scalar(s,"SELECT count(*) FROM ticketland_sale_028")==1&&s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"failed import changed last successful snapshot");
            s.ApplyTicketlandReport028(new(Array.Empty<TicketlandSale028>(),at.AddSeconds(2)),false);Check(s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"append import freed missing remote sale");
        });
        run("028 Ticketland write failure rolls back replacement and last sync",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{Sale(f,"1","old")},at),false);
            using(var c=new SqliteConnection($"Data Source={s.Path}"))
            {
                c.Open();using var q=c.CreateCommand();q.CommandText="CREATE TRIGGER ticketland_fail_028 BEFORE INSERT ON ticketland_sale_028 WHEN NEW.barcode='fail' BEGIN SELECT RAISE(ABORT,'test database write failure'); END";q.ExecuteNonQuery();
            }
            var rejected=false;try{s.ApplyTicketlandReport028(new(new[]{Sale(f,"2","new"),Sale(f,"3","fail")},at.AddSeconds(1)),true);}catch(SqliteException){rejected=true;}
            Check(rejected&&s.TicketlandLastSync028==at&&Scalar(s,"SELECT count(*) FROM ticketland_sale_028 WHERE barcode='old'")==1&&Scalar(s,"SELECT count(*) FROM ticketland_sale_028")==1,"write failure committed a partial replacement or changed last sync");
        });
        run("028 Ticketland exact matching skips ambiguous shows and general admission",s=>{
            var f=Fixture(s);var other=Fixture(s,f.Title,f.At);var sale=Sale(f,"1","ambiguous");
            var summary=s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="general",Row="--",Seat="--"},sale with{Barcode="unmatched",Title="unknown"}},DateTimeOffset.UtcNow),false);
            Check(summary.Matched==0&&summary.Unmatched==2&&summary.GeneralAdmission==1,"ambiguous or general admission row guessed a seat");
            Check(!s.Seats(f.Event).Any(x=>x.IsTicketlandSold)&&!s.Seats(other.Event).Any(x=>x.IsTicketlandSold)&&Scalar(s,"SELECT count(*) FROM ticketland_sale_028")==3,"unmapped raw sales lost or falsely seated");
        });
        run("028 Ticketland normalized exact keys map without fuzzy matching",s=>{
            var f=Fixture(s);var sale=Sale(f,"1","normalized") with{Title="  "+f.Title.ToUpperInvariant()+"  ",Sector=" ПАРТЕР ",Row=" 1 ",Seat=" 1 "};
            var summary=s.ApplyTicketlandReport028(new(new[]{sale,sale with{Barcode="wrongtime",StartsAt=f.At.AddSeconds(1)},sale with{Barcode="wrongsector",Sector="Партер левый",Seat="2"}},DateTimeOffset.UtcNow),false);
            Check(summary.Matched==2&&summary.Unmatched==1,"normalization failed or wrong time accepted");
        });
        run("028 Ticketland append retains occupied markers when show becomes ambiguous",s=>{
            var f=Fixture(s);var sale=Sale(f,"1","retain");var at=DateTimeOffset.UtcNow;
            s.ApplyTicketlandReport028(new(new[]{sale},at),false);Fixture(s,f.Title,f.At);
            var result=s.ApplyTicketlandReport028(new(new[]{sale},at.AddSeconds(1)),false);
            Check(result.Unmatched==1&&s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"append ambiguity silently freed a previously matched sale");
        });
        run("028 Ticketland complete snapshot preserves known sale after local sector rename",s=>{
            var f=Fixture(s);var sale=Sale(f,"1","renamed");var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale},at),true);
            var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;var hall=s.Venues().Single(v=>v.Id==venue);var draft=s.HallDraft(venue);
            HallEditing027.RenameSector(draft,"Партер","Новый партер");s.SaveEditedHall(venue,hall.Name,hall.Address,draft);
            var result=s.ApplyTicketlandReport028(new(new[]{sale},at.AddSeconds(1)),true);
            Check(result.Matched==0&&result.Unmatched==1&&s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"full snapshot freed a live barcode after local rename");
            Reject(()=>s.Issue(f.Event,new[]{f.Seats[0]},"local","TLREN028",1));
            s.ApplyTicketlandReport028(new(Array.Empty<TicketlandSale028>(),at.AddSeconds(2)),true);
            Check(s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).Available,"authoritative missing barcode did not release returned sale");
        });
        run("028 Ticketland complete snapshot preserves known sale after local show ambiguity",s=>{
            var f=Fixture(s);var sale=Sale(f,"1","ambiguous-live");var at=DateTimeOffset.UtcNow;s.ApplyTicketlandReport028(new(new[]{sale},at),true);
            var other=Fixture(s,f.Title,f.At);var result=s.ApplyTicketlandReport028(new(new[]{sale},at.AddSeconds(1)),true);
            Check(result.Matched==0&&result.Unmatched==1&&s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"full snapshot freed a live barcode after local ambiguity");
            Check(!s.Seats(other.Event).Any(x=>x.IsTicketlandSold),"full snapshot guessed another ambiguous showing");
            Reject(()=>s.Reserve(f.Event,new[]{f.Seats[0]},"local","TLAMB028",1));
        });
        run("028 Ticketland rechecks previously unmapped raw rows on next fetch",s=>{
            var title="TL028 unmapped "+Guid.NewGuid();var at=DateTimeOffset.UtcNow;var date=new DateTime(2030,8,4,18,0,0);
            var sale=new TicketlandSale028(title,date,"Партер","1","1","remap",500);
            Check(s.ApplyTicketlandReport028(new(new[]{sale},at),false).Unmatched==1,"unknown showing guessed a seat");
            var f=Fixture(s,title,date);Check(s.ApplyTicketlandReport028(new(new[]{sale},at.AddSeconds(1)),false).Matched==1&&s.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"next fetch failed to map cached raw sale");
        });
        run("028 Ticketland explicit name binding persists and remaps cached exact seats",s=>{
            var f=Fixture(s);var source="Ticketland other title "+Guid.NewGuid();var at=DateTimeOffset.UtcNow;
            var sale=Sale(f,"1","bound") with{Title=source};s.ApplyTicketlandReport028(new(new[]{sale},at),true);
            var before=s.Events().Single(e=>e.Id==f.Event);var raw=s.TicketlandShows028().Single();
            Check(raw.EventId is null&&raw.Tickets==1,"different title was guessed before explicit binding");
            s.BindTicketlandShow028(source,f.At,f.Event);var reopened=new Store(s.Path);
            Check(reopened.TicketlandShows028().Single().EventId==f.Event,"explicit binding lost on reopening");
            var cached=reopened.CachedTicketlandReport028();Check(cached.FetchedAt==at&&cached.Sales.Count==1,"cached report changed its successful fetch time");
            Check(reopened.ApplyTicketlandReport028(cached,true).Matched==1&&reopened.Seats(f.Event).Single(x=>x.Id==f.Seats[0]).IsTicketlandSold,"explicit source binding did not map exact seat");
            Check(reopened.Events().Single(e=>e.Id==f.Event)==before&&reopened.History(f.Event).Count==0&&reopened.Issues(f.Event).Count==0,"binding changed local catalogue or accounting");
        });
        run("028 Ticketland explicit binding rejects wrong time and deleted shows",s=>{
            var f=Fixture(s);var wrong=Fixture(s,at:f.At.AddMinutes(1));var source="Ticketland binding reject "+Guid.NewGuid();
            s.ApplyTicketlandReport028(new(new[]{Sale(f,"1","bind-reject") with{Title=source}},DateTimeOffset.UtcNow),true);
            Reject(()=>s.BindTicketlandShow028(source,f.At,wrong.Event));
            var venue=s.Events().Single(e=>e.Id==f.Event).VenueId;s.DeleteVenue(venue);
            Reject(()=>s.BindTicketlandShow028(source,f.At,f.Event));
            Check(s.TicketlandShows028().Single().EventId is null,"invalid explicit binding was persisted");
        });
        run("028 Ticketland explicit override resolves ambiguous showing without altering seat labels",s=>{
            var f=Fixture(s);var other=Fixture(s,f.Title,f.At);var at=DateTimeOffset.UtcNow;
            s.ApplyTicketlandReport028(new(new[]{Sale(f,"1","override")},at),true);
            var issue=s.Issue(f.Event,new[]{f.Seats[0]},"local","TLREBIND028",1);
            s.BindTicketlandShow028(f.Title,f.At,f.Event);s.ApplyTicketlandReport028(s.CachedTicketlandReport028(),true);
            s.BindTicketlandShow028(f.Title,f.At,other.Event);Check(s.ApplyTicketlandReport028(s.CachedTicketlandReport028(),false).Matched==1,"explicit override ignored in ambiguous catalogue");
            Check(!s.Seats(f.Event).Any(x=>x.IsTicketlandSold)&&s.Seats(other.Event).Single(x=>x.Id==other.Seats[0]).IsTicketlandSold,"explicit override marked wrong showing");
            Check(s.History(f.Event).Single().IssueId==issue,"explicit external rebinding discarded original local accounting");
        });
        run("028 Ticketland explicit show and sector binding preserves local hall",s=>{
            var f=Fixture(s);var source="Ticketland remote sector "+Guid.NewGuid();var at=DateTimeOffset.UtcNow;
            var sale=Sale(f,"1","sector-bound") with{Title=source,Sector="ПАРТЕР ЦЕНТР"};
            var before=s.Seats(f.Event).Select(x=>(x.Id,x.Sector,x.Row,x.Number,x.X,x.Y)).ToArray();
            s.ApplyTicketlandReport028(new(new[]{sale},at),true);s.BindTicketlandShow028(source,f.At,f.Event);
            Check(s.TicketlandSectors028(source,f.At).SequenceEqual(new[]{"ПАРТЕР ЦЕНТР"}),"raw source sectors unavailable");
            Reject(()=>s.BindTicketlandSector028(source,f.At,"ПАРТЕР ЦЕНТР",f.Event,"does not exist"));
            s.BindTicketlandSector028(source,f.At,"ПАРТЕР ЦЕНТР",f.Event,"Партер");var reopened=new Store(s.Path);
            Check(reopened.ApplyTicketlandReport028(reopened.CachedTicketlandReport028(),true).Matched==1,"explicit sector mapping failed after reopening");
            Check(reopened.Seats(f.Event).Select(x=>(x.Id,x.Sector,x.Row,x.Number,x.X,x.Y)).SequenceEqual(before)&&reopened.Events().Single(e=>e.Id==f.Event).Title==f.Title,"explicit mapping modified approved local names or geometry");
            var other=Fixture(s,at:f.At);Reject(()=>s.BindTicketlandSector028(source,f.At,"ПАРТЕР ЦЕНТР",other.Event,"Партер"));
        });
        run("028 Ticketland external markers cascade with explicit event deletion",s=>{
            var f=Fixture(s);s.ApplyTicketlandReport028(new(new[]{Sale(f,"1","purge")},DateTimeOffset.UtcNow),false);
            s.BindTicketlandShow028(f.Title,f.At,f.Event);s.BindTicketlandSector028(f.Title,f.At,"Партер",f.Event,"Партер");
            s.DeleteEventsPermanently(new[]{f.Event});Check(Scalar(s,"SELECT count(*) FROM ticketland_sale_028")==0&&Scalar(s,"SELECT count(*) FROM ticketland_show_map_028")==0&&Scalar(s,"SELECT count(*) FROM ticketland_sector_map_028")==0,"event cleanup left external child rows or mappings");
        });
    }
    private sealed record Data(long Event,long[] Seats,string Title,DateTime At);
    private static Data Fixture(Store s,string? title=null,DateTime? at=null)
    {
        title??="TL028 "+Guid.NewGuid();var date=at??new DateTime(2030,5,7,19,0,0);var venue=s.CreateHall("TL028 "+Guid.NewGuid(),"",Enumerable.Range(1,3).Select(i=>new DraftSeat{Sector="Партер",Row="1",Number=i.ToString(),X=i*20,Y=0}).ToArray());
        var ids=s.VenueSeats(venue).Select(x=>x.Id).ToArray();var plan=s.SavePlan(venue,"TL028",ids.Select(id=>new PriceValue(id,500,"#FF0000")).ToArray());
        var evt=s.CreateEvent(s.SavePerformance(title),venue,plan,date);return new(evt,ids,title,date);
    }
    private static TicketlandSale028 Sale(Data f,string seat,string barcode)=>new(f.Title,f.At,"Партер","1",seat,barcode,500);
    private static long Scalar(Store s,string sql){using var c=new SqliteConnection($"Data Source={s.Path}");c.Open();using var q=c.CreateCommand();q.CommandText=sql;return Convert.ToInt64(q.ExecuteScalar());}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("expected TicketException");}
}
