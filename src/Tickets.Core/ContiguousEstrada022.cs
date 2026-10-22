using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureContiguousEstrada022()
    {
        using var c=Open();
        using(var create=Command(c,"CREATE TABLE IF NOT EXISTS hall_layout_migration_022(venue_id INTEGER PRIMARY KEY,applied_at TEXT NOT NULL)"))create.ExecuteNonQuery();
        var ids=new List<long>();
        using(var query=Command(c,"SELECT v.id FROM venue v WHERE NOT EXISTS(SELECT 1 FROM hall_layout_migration_022 m WHERE m.venue_id=v.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='venue' AND h.entity_key=CAST(v.id AS TEXT)) AND (SELECT count(*) FROM seat s WHERE s.venue_id=v.id)=1002"))
        {using var r=query.ExecuteReader();while(r.Read())ids.Add(r.GetInt64(0));}
        if(ids.Count==0)return;
        static string Key(string zone,string row,string number)=>zone+"\u001f"+row+"\u001f"+number;
        var old=VenueLayouts019.EstradaSeats(legacyPerimeter:true).ToDictionary(s=>Key(s.Sector,s.Row,s.Number));
        var next=VenueLayouts019.EstradaSeats().ToDictionary(s=>Key(s.Sector,s.Row,s.Number));
        static bool Near(double a,double b)=>Math.Abs(a-b)<.05;
        var pending=new List<(long Venue,List<(long Id,DraftSeat Target)> Seats,List<(long Id,double X,double Y)> Sectors)>();
        foreach(var venue in ids)
        {
            var targetSeats=new List<(long Id,DraftSeat Target)>();var valid=true;var seen=new HashSet<string>();
            using(var q=Command(c,"SELECT s.id,z.name,s.row_label,s.number,s.x,s.y FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v","$v",venue))
            {
                using var r=q.ExecuteReader();while(r.Read())
                {
                    var key=Key(r.GetString(1),r.GetString(2),r.GetString(3));
                    if(!seen.Add(key)||!old.TryGetValue(key,out var before)||!Near(r.GetDouble(4),before.X)||!Near(r.GetDouble(5),before.Y)){valid=false;break;}
                    targetSeats.Add((r.GetInt64(0),next[key]));
                }
            }
            if(!valid||targetSeats.Count!=1002)continue;
            var targets=next.Values.GroupBy(s=>s.Sector).ToDictionary(g=>g.Key,g=>(X:g.Min(s=>s.X),Y:g.Min(s=>s.Y)-26));
            var prior=old.Values.GroupBy(s=>s.Sector).ToDictionary(g=>g.Key,g=>(X:g.Min(s=>s.X),Y:g.Min(s=>s.Y)-26));
            var sectors=new List<(long Id,double X,double Y)>();
            using(var q=Command(c,"SELECT id,name,x,y FROM sector WHERE venue_id=$v","$v",venue))
            {using var r=q.ExecuteReader();while(r.Read()){var name=r.GetString(1);if(!prior.TryGetValue(name,out var p)||!Near(r.GetDouble(2),p.X)||!Near(r.GetDouble(3),p.Y)){valid=false;break;}var target=targets[name];sectors.Add((r.GetInt64(0),target.X,target.Y));}}
            if(valid&&sectors.Count==prior.Count)pending.Add((venue,targetSeats,sectors));
        }
        if(pending.Count==0)return;
        var backup=Path+".before-v022-parter.db";if(!File.Exists(backup))Backup(backup);
        using var tx=c.BeginTransaction(deferred:false);
        foreach(var item in pending)
        {
            foreach(var seat in item.Seats){using var q=Command(c,"UPDATE seat SET x=$x,y=$y WHERE id=$i AND venue_id=$v","$x",seat.Target.X,"$y",seat.Target.Y,"$i",seat.Id,"$v",item.Venue);q.Transaction=tx;q.ExecuteNonQuery();}
            foreach(var sector in item.Sectors){using var q=Command(c,"UPDATE sector SET x=$x,y=$y WHERE id=$i AND venue_id=$v","$x",sector.X,"$y",sector.Y,"$i",sector.Id,"$v",item.Venue);q.Transaction=tx;q.ExecuteNonQuery();}
            using var mark=Command(c,"INSERT INTO hall_layout_migration_022 VALUES($v,$at)","$v",item.Venue,"$at",DateTimeOffset.Now.ToString("O"));mark.Transaction=tx;mark.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
