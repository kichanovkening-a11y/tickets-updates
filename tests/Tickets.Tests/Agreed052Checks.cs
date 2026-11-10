using Tickets.Core;
using Microsoft.Data.Sqlite;
using System.Reflection;

internal static class Agreed052Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("052 new halls have square 18-unit cells and preserve explicit aisles",_=>{
            var seats=HallLayout.Generate("S",[new(){Label="A",SeatCount=3,AisleAfter=1},new(){Label="B",SeatCount=3,RowGapAfter=12},new(){Label="C",SeatCount=1}],"1",false,0);
            Check(SeatSelection.SeatSize==18,"seat cell must be 18");
            Check(seats[2].X-seats[1].X==18&&seats[1].X-seats[0].X==54,"horizontal step or explicit aisle changed");
            Check(seats[3].Y==18&&seats[6].Y==48,"vertical step or row aisle changed");
            Check(SeatSelection.Hit(seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"",null,"","",null)),17.9,17.9)?.Id==1,"dense seat corner cannot be selected");
        });
        run("052 existing halls compact without changing issued tickets and only once",store=>{
            var draft=new List<DraftSeat>();
            for(var r=0;r<2;r++)for(var c=0;c<3;c++)draft.Add(new(){Sector="S",Row=(r+1).ToString(),Number=(c+1).ToString(),X=c*20+(c>0?36:0),Y=r*22,Hidden=r==1&&c==2});
            var venue=store.CreateHall("052 existing","",draft);
            var ids=store.VenueSeats(venue).Select(s=>s.Id).ToArray();
            var plan=store.SavePlan(venue,"052 prices",ids.Select(id=>new PriceValue(id,1350,"#FF0000")).ToArray());
            var evt=store.CreateEvent(store.SavePerformance("052 show"),venue,plan,new DateTime(2027,7,1,19,0,0));
            var issue=store.Issue(evt,[ids[0]],"052 recipient","AB",52);
            var before=store.Invoice(evt,issue);
            using(var c=new SqliteConnection("Data Source="+store.Path)){c.Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM hall_compaction_052 WHERE venue_id="+venue;q.ExecuteNonQuery();}
            var reopened=new Store(store.Path);var seats=reopened.HallDraft(venue);
            Check(seats[1].X-seats[0].X==54&&seats[2].X-seats[1].X==18&&seats[3].Y-seats[0].Y==18,"saved hall did not compact");
            Check(reopened.VenueSeats(venue).Select(s=>s.Id).SequenceEqual(ids),"seat identities changed");
            Check(reopened.Invoice(evt,issue).Tickets.SequenceEqual(before.Tickets)&&reopened.Invoice(evt,issue).Total==1350,"accounting changed");
            Check(seats.Count(s=>s.Hidden)==1,"hidden seats lost");
            Check(new Store(store.Path).HallDraft(venue).Select(s=>(s.X,s.Y)).SequenceEqual(seats.Select(s=>(s.X,s.Y))),"reopening compacts twice");
        });
        run("052 compaction preserves rotation explicit gaps and already dense layouts",_=>{
            foreach(var angle in new[]{0d,17d,90d,180d,270d})
            {
                var seats=new List<DraftSeat>();
                for(var r=0;r<3;r++)for(var c=0;c<4;c++)seats.Add(new(){Sector="S",Row=r.ToString(),Number=c.ToString(),X=c*20+(c>1?36:0),Y=r*22+(r>1?30:0)});
                HallEditing023.Rotate(seats,angle);var labels=seats.Select(s=>(s.Row,s.Number)).ToArray();
                HallCompaction052.Compact(seats);
                double Distance(DraftSeat a,DraftSeat b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));
                Check(Math.Abs(Distance(seats[0],seats[1])-18)<.01,"rotated row spacing");
                Check(Math.Abs(Distance(seats[1],seats[2])-54)<.01,"explicit horizontal aisle");
                Check(Math.Abs(Distance(seats[0],seats[4])-18)<.01&&Math.Abs(Distance(seats[4],seats[8])-48)<.01,"rotated row gap");
                Check(seats.Select(s=>(s.Row,s.Number)).SequenceEqual(labels),"labels changed");
                var once=seats.Select(s=>(s.X,s.Y)).ToArray();HallCompaction052.Compact(seats);Check(seats.Select(s=>(s.X,s.Y)).SequenceEqual(once),"dense layout compacted twice");
            }
        });
        run("052 migrated aisle anchors update even when the first seat stays put",store=>{
            var draft=new[]{0d,20,76,96}.Select((x,i)=>new DraftSeat{Sector="S",Row="1",Number=(i+1).ToString(),X=x,Y=0,RowAisleAnchors=new[]{new HallEditing025.RowAnchor(56,8,true)}}).ToArray();
            var venue=store.CreateHall("052 anchors","",draft);
            using(var c=new SqliteConnection("Data Source="+store.Path)){c.Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM hall_compaction_052 WHERE venue_id="+venue;q.ExecuteNonQuery();}
            var loaded=new Store(store.Path).HallDraft(venue);
            Check(loaded.All(s=>s.RowAisleAnchors!.Single()==new HallEditing025.RowAnchor(54,9,true)),"unchanged first seat retained stale aisle centre");
        });
        run("052 rotating a row keeps its aisle label attached to axis-aligned seat centres",_=>{
            var draft=new[]{0d,18,72,90}.Select((x,i)=>new DraftSeat{Sector="S",Row="1",Number=(i+1).ToString(),X=x,Y=0,RowAisleAnchors=new[]{new HallEditing025.RowAnchor(54,9,true)}}).ToArray();
            HallEditing023.Rotate(draft,90);var anchor=draft[0].RowAisleAnchors!.Single();
            Check(Math.Abs(anchor.X-((draft[1].X+draft[2].X)/2+9))<.001&&Math.Abs(anchor.Y-((draft[1].Y+draft[2].Y)/2+9))<.001,"rotated label detached");
        });
        run("052 no-price legacy bindings hide amount and stub price without changing accounting",_=>{
            var ticket=new TicketInfo(1,1,"S","2","3","R","AB",987,1350,"2026-11-10T00:00:00Z",null);
            var invoice=new InvoiceInfo(1,1,"Show","Venue","01.07.2027 19:00","R",ticket.IssuedAt,[ticket]);
            var overload=typeof(LegacyTicketTemplates).GetMethods().SingleOrDefault(m=>m.Name=="Value"&&m.GetParameters().Length==7);
            Check(overload is not null,"price visibility option missing");
            string Text(string binding,bool hide)=>(string)overload!.Invoke(null,[binding,invoice,ticket,"address",new DateTime(2026,11,10),1,hide])!;
            Check(Text("ticket_price",true)==""&&Text("ticket_amount",true)=="","price-only field remains");
            Check(!Text("small_info1",true).Contains("1350")&&!Text("small_info1",true).Contains("Цена")&&Text("small_info1",true).Contains("000987"),"tear-off lost identity or kept price");
            Check(Text("ticket_price",false).Contains("1350")&&invoice.Total==1350&&ticket.Amount==1350,"ordinary printing or accounting changed");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}