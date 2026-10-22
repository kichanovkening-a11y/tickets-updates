using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record HallReplacement019(string Venue,int OldSeats,int NewSeats,int ArchivedTickets,int CopiedPrices,int UnpricedEventSeats);

public sealed partial class Store
{
    /// <summary>Accounting for replacements performed when this Store was opened. Original halls,
    /// showings, tickets and documents are retained under a hidden archival venue.</summary>
    public IReadOnlyList<HallReplacement019> HallReplacements019 {get;private set;}=[];

    private void EnsureVenueLayouts019()
    {
        using var c=Open();
        using(var table=Command(c,"CREATE TABLE IF NOT EXISTS hall_layout_migration_019(old_venue_id INTEGER PRIMARY KEY,new_venue_id INTEGER NOT NULL,details TEXT NOT NULL,applied_at TEXT NOT NULL)"))table.ExecuteNonQuery();
        var targets=new List<(long Id,string Name,string Address,IReadOnlyList<DraftSeat> Seats)>();
        foreach(var (originalId,name,seats) in new[]{(25L,VenueLayouts019.RussianSong,VenueLayouts019.RussianSongSeats()),(14L,VenueLayouts019.Estrada,VenueLayouts019.EstradaSeats())})
        {
            // Only the two exact imported hall identities are replaced. Custom or similarly named
            // halls (including «Новая сцена театра Русская песня») must remain untouched.
            using var query=Command(c,"SELECT id,name,address FROM venue WHERE id=$original AND name=$n AND id NOT IN(SELECT old_venue_id FROM hall_layout_migration_019 UNION SELECT new_venue_id FROM hall_layout_migration_019)","$n",name,"$original",originalId);
            using var r=query.ExecuteReader();while(r.Read())targets.Add((r.GetInt64(0),r.GetString(1),r.GetString(2),seats));
        }
        if(targets.Count==0)return;
        var backup=Path+".before-v019-halls.db";
        if(!System.IO.File.Exists(backup))Backup(backup);
        var report=new List<HallReplacement019>();
        using var tx=c.BeginTransaction(deferred:false);
        EnsureEventPlanSources(c,tx);
        // Imported showings can predate slot validation and contain duplicate dates. Reproduce
        // those historical rows verbatim, then restore the original insert guard in this transaction.
        string? slotTrigger;
        using(var q=Command(c,"SELECT sql FROM sqlite_master WHERE type='trigger' AND name='event_slot_insert'")){q.Transaction=tx;slotTrigger=q.ExecuteScalar() as string;}
        using(var q=Command(c,"DROP TRIGGER IF EXISTS event_slot_insert")){q.Transaction=tx;q.ExecuteNonQuery();}
        object? Scalar(string sql,params object[] pairs){using var q=Command(c,sql,pairs);q.Transaction=tx;return q.ExecuteScalar();}
        int Exec(string sql,params object[] pairs){using var q=Command(c,sql,pairs);q.Transaction=tx;return q.ExecuteNonQuery();}
        foreach(var target in targets)
        {
            if(Convert.ToInt32(Scalar("SELECT count(*) FROM hall_layout_migration_019 WHERE old_venue_id=$v","$v",target.Id))!=0)continue;
            var oldCount=Convert.ToInt32(Scalar("SELECT count(*) FROM seat WHERE venue_id=$v","$v",target.Id));
            var tickets=Convert.ToInt32(Scalar("SELECT count(*) FROM ticket t JOIN event e ON e.id=t.event_id WHERE e.venue_id=$v","$v",target.Id));
            var newId=Convert.ToInt64(Scalar("INSERT INTO venue(name,address) VALUES($n,$a) RETURNING id","$n",target.Name,"$a",target.Address));
            long zid=0;
            foreach(var group in target.Seats.GroupBy(s=>s.Sector))
            {
                zid++;
                Exec("INSERT INTO sector(venue_id,id,name,x,y) VALUES($v,$z,$n,$x,$y)","$v",newId,"$z",zid,"$n",group.Key,"$x",group.Min(s=>s.X),"$y",group.Min(s=>s.Y)-26);
                foreach(var s in group)Exec("INSERT INTO seat(venue_id,sector_id,row_label,number,x,y) VALUES($v,$z,$r,$n,$x,$y)","$v",newId,"$z",zid,"$r",s.Row,"$n",s.Number,"$x",s.X,"$y",s.Y);
            }
            // Temporary exact label map. Sector spelling aliases only reconcile typography, never
            // infer a missing row/place or inherit prices by nearest position.
            Exec("CREATE TEMP TABLE seat_map_019(old_id INTEGER PRIMARY KEY,new_id INTEGER NOT NULL)");
            static string SectorKey(string value)=>value.Replace("VIP №","VIP ").Replace("Бельэтаж","Бель-этаж")
                .Replace("Ложа партера (правая сторона)","Ложа партера правая").Replace("Ложа партера (левая сторона)","Ложа партера левая")
                .Replace(" (правая сторона)"," прав.сторона").Replace(" (левая сторона)"," лев.сторона");
            var oldSeats=new Dictionary<string,long>(StringComparer.Ordinal);
            using(var q=Command(c,"SELECT s.id,z.name,s.row_label,s.number FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v","$v",target.Id))
            {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())oldSeats[SectorKey(r.GetString(1))+"\u001f"+r.GetString(2)+"\u001f"+r.GetString(3)]=r.GetInt64(0);}
            var matches=new List<(long Old,long New)>();
            using(var q=Command(c,"SELECT s.id,z.name,s.row_label,s.number FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v","$v",newId))
            {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())if(oldSeats.TryGetValue(SectorKey(r.GetString(1))+"\u001f"+r.GetString(2)+"\u001f"+r.GetString(3),out var old))matches.Add((old,r.GetInt64(0)));}
            foreach(var pair in matches)Exec("INSERT INTO seat_map_019 VALUES($o,$n)","$o",pair.Old,"$n",pair.New);
            var plans=new Dictionary<long,long>();
            var oldPlans=new List<(long Id,string Name,string Key)>();
            using(var q=Command(c,"SELECT id,name,name_key FROM pricing_plan WHERE venue_id=$v","$v",target.Id)){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())oldPlans.Add((r.GetInt64(0),r.GetString(1),r.GetString(2)));}
            var copied=0;
            foreach(var p in oldPlans)
            {
                var id=Convert.ToInt64(Scalar("INSERT INTO pricing_plan(venue_id,name,name_key) VALUES($v,$n,$k) RETURNING id","$v",newId,"$n",p.Name,"$k",p.Key));plans[p.Id]=id;
                copied+=Exec("INSERT INTO plan_price SELECT $n,m.new_id,p.amount,p.color FROM plan_price p JOIN seat_map_019 m ON m.old_id=p.seat_id WHERE p.plan_id=$o","$n",id,"$o",p.Id);
                Exec("INSERT INTO plan_seat_block SELECT $n,m.new_id FROM plan_seat_block p JOIN seat_map_019 m ON m.old_id=p.seat_id WHERE p.plan_id=$o","$n",id,"$o",p.Id);
                Exec("INSERT OR IGNORE INTO catalog_hidden SELECT kind,CAST($n AS TEXT) FROM catalog_hidden WHERE kind='plan' AND entity_key=CAST($o AS TEXT)","$n",id,"$o",p.Id);
            }
            var events=new List<(long Id,string Title,string Date,string Legacy)>();
            using(var q=Command(c,"SELECT id,title,starts_at,legacy_date FROM event WHERE venue_id=$v","$v",target.Id)){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())events.Add((r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3)));}
            var unpriced=0;
            foreach(var e in events)
            {
                var id=Convert.ToInt64(Scalar("INSERT INTO event(venue_id,title,starts_at,legacy_date) VALUES($v,$t,$d,$l) RETURNING id","$v",newId,"$t",e.Title,"$d",e.Date,"$l",e.Legacy));
                copied+=Exec("INSERT INTO event_price SELECT $n,m.new_id,p.amount,p.color FROM event_price p JOIN seat_map_019 m ON m.old_id=p.seat_id WHERE p.event_id=$o","$n",id,"$o",e.Id);
                Exec("INSERT INTO event_seat_block SELECT $n,m.new_id,p.changed_at FROM event_seat_block p JOIN seat_map_019 m ON m.old_id=p.seat_id WHERE p.event_id=$o","$n",id,"$o",e.Id);
                foreach(var p in plans)Exec("INSERT INTO event_plan_source SELECT $n,$p FROM event_plan_source WHERE event_id=$o AND plan_id=$oldp","$n",id,"$p",p.Value,"$o",e.Id,"$oldp",p.Key);
                unpriced+=target.Seats.Count-Convert.ToInt32(Scalar("SELECT count(*) FROM event_price WHERE event_id=$e","$e",id));
            }
            Exec("INSERT OR IGNORE INTO catalog_hidden(kind,entity_key) VALUES('venue',CAST($v AS TEXT))","$v",target.Id);
            Exec("UPDATE venue SET name=name||' [архив схемы до 0.19]' WHERE id=$v","$v",target.Id);
            var details=new HallReplacement019(target.Name,oldCount,target.Seats.Count,tickets,copied,unpriced);report.Add(details);
            Exec("INSERT INTO hall_layout_migration_019 VALUES($o,$n,$d,$t)","$o",target.Id,"$n",newId,"$d",System.Text.Json.JsonSerializer.Serialize(details),"$t",DateTimeOffset.Now.ToString("O"));
            Exec("DROP TABLE seat_map_019");
        }
        if(slotTrigger is not null)Exec(slotTrigger);
        tx.Commit();HallReplacements019=report;
    }
}
