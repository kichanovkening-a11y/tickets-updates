using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureCompactVenueLayouts020()
    {
        using var c=Open();
        using(var table=Command(c,"CREATE TABLE IF NOT EXISTS hall_layout_migration_020(venue_id INTEGER PRIMARY KEY,applied_at TEXT NOT NULL)"))table.ExecuteNonQuery();
        var candidates=new List<(long Id,int Count)>();
        using(var query=Command(c,"SELECT venue_id,count(*) FROM seat WHERE venue_id NOT IN(SELECT venue_id FROM hall_layout_migration_020) GROUP BY venue_id HAVING count(*) IN(1122,1002)"))
        {using var r=query.ExecuteReader();while(r.Read())candidates.Add((r.GetInt64(0),r.GetInt32(1)));}
        if(candidates.Count==0)return;
        static string Key(string sector,string row,string number)=>sector+"\u001f"+row+"\u001f"+number;
        static bool Near(double a,double b)=>double.IsFinite(a)&&Math.Abs(a-b)<=.05;
        var sources=new Dictionary<int,(IReadOnlyList<DraftSeat> Raw,IReadOnlyList<DraftSeat> Compact)>{
            [1122]=(VenueLayouts019.RussianSongSeats(false),VenueLayouts019.RussianSongSeats()),
            [1002]=(VenueLayouts019.EstradaSeats(false),VenueLayouts019.EstradaSeats())};
        var pending=new List<(long Venue,bool Update,List<(long Id,DraftSeat Target)> Seats,List<(long Id,double X,double Y)> Sectors)>();
        foreach(var candidate in candidates)
        {
            var source=sources[candidate.Count];
            var raw=source.Raw.ToDictionary(s=>Key(s.Sector,s.Row,s.Number));
            var compact=source.Compact.ToDictionary(s=>Key(s.Sector,s.Row,s.Number));
            var seats=new List<(long Id,DraftSeat Target)>();var seen=new HashSet<string>();
            var isRaw=true;var isCompact=true;
            using(var query=Command(c,"SELECT s.id,z.name,s.row_label,s.number,s.x,s.y FROM seat s JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id WHERE s.venue_id=$v","$v",candidate.Id))
            {
                using var r=query.ExecuteReader();while(r.Read())
                {
                    var key=Key(r.GetString(1),r.GetString(2),r.GetString(3));
                    if(!seen.Add(key)||!raw.TryGetValue(key,out var old)||!compact.TryGetValue(key,out var target)){isRaw=isCompact=false;break;}
                    isRaw&=Near(r.GetDouble(4),old.X)&&Near(r.GetDouble(5),old.Y);
                    isCompact&=Near(r.GetDouble(4),target.X)&&Near(r.GetDouble(5),target.Y);
                    seats.Add((r.GetInt64(0),target));
                }
            }
            if(seats.Count!=candidate.Count||(!isRaw&&!isCompact))continue;
            var oldSectors=source.Raw.GroupBy(s=>s.Sector).ToDictionary(g=>g.Key,g=>(X:g.Min(s=>s.X),Y:g.Min(s=>s.Y)-26));
            var newSectors=source.Compact.GroupBy(s=>s.Sector).ToDictionary(g=>g.Key,g=>(X:g.Min(s=>s.X),Y:g.Min(s=>s.Y)-26));
            var sectors=new List<(long Id,double X,double Y)>();var sectorNames=new HashSet<string>();
            using(var query=Command(c,"SELECT id,name,x,y FROM sector WHERE venue_id=$v","$v",candidate.Id))
            {
                using var r=query.ExecuteReader();while(r.Read())
                {
                    var name=r.GetString(1);
                    if(!sectorNames.Add(name)||!oldSectors.TryGetValue(name,out var old)||!newSectors.TryGetValue(name,out var target)){isRaw=isCompact=false;break;}
                    isRaw&=Near(r.GetDouble(2),old.X)&&Near(r.GetDouble(3),old.Y);
                    isCompact&=Near(r.GetDouble(2),target.X)&&Near(r.GetDouble(3),target.Y);
                    sectors.Add((r.GetInt64(0),target.X,target.Y));
                }
            }
            if(sectors.Count==oldSectors.Count&&(isRaw||isCompact))pending.Add((candidate.Id,isRaw,seats,sectors));
        }
        if(pending.Count==0)return;
        // Existing seats are moved in place: pricing, issued tickets and all references keep their IDs.
        if(pending.Any(p=>p.Update))
        {var backup=Path+".before-v020-compact-halls.db";if(!File.Exists(backup))Backup(backup);}
        using var tx=c.BeginTransaction(deferred:false);
        foreach(var item in pending)
        {
            if(item.Update)
            {
                foreach(var seat in item.Seats)
                {using var cmd=Command(c,"UPDATE seat SET x=$x,y=$y WHERE id=$i AND venue_id=$v","$x",seat.Target.X,"$y",seat.Target.Y,"$i",seat.Id,"$v",item.Venue);cmd.Transaction=tx;cmd.ExecuteNonQuery();}
                foreach(var sector in item.Sectors)
                {using var cmd=Command(c,"UPDATE sector SET x=$x,y=$y WHERE id=$i AND venue_id=$v","$x",sector.X,"$y",sector.Y,"$i",sector.Id,"$v",item.Venue);cmd.Transaction=tx;cmd.ExecuteNonQuery();}
            }
            using var mark=Command(c,"INSERT OR IGNORE INTO hall_layout_migration_020(venue_id,applied_at) VALUES($v,$t)","$v",item.Venue,"$t",DateTimeOffset.Now.ToString("O"));mark.Transaction=tx;mark.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
