using System.Text.Json;
namespace Tickets.Core;

/// <summary>Remove only identifiable old 20/22-unit padding, preserving explicit aisles, curved rows and seat identities.</summary>
public static class HallCompaction052
{
    public static void Compact(IReadOnlyList<DraftSeat> seats)
    {
        foreach(var sector in seats.GroupBy(s=>s.Sector))
        {
            var rows=sector.GroupBy(s=>s.Row).Select(g=>g.ToArray()).ToArray();
            var axis=Axis(rows.MaxBy(r=>r.Length)!);var ux=axis.X;var uy=axis.Y;
            var orderedRows=rows.OrderBy(row=>row.Average(s=>-s.X*uy+s.Y*ux)).ToArray();
            var levels=orderedRows.Select(row=>row.Average(s=>-s.X*uy+s.Y*ux)).ToArray();
            var vertical=rows.All(row=>Straight(row,ux,uy))&&levels.Zip(levels.Skip(1),(a,b)=>b-a).Any(d=>Math.Abs(d-22)<.01)
                &&!levels.Zip(levels.Skip(1),(a,b)=>b-a).Any(d=>d<21.99);
            var rowShift=0d;
            for(var r=0;r<orderedRows.Length;r++)
            {
                var row=orderedRows[r];if(vertical&&r>0)rowShift+=4;
                var a=Axis(row);var dx=a.X;var dy=a.Y;
                var sorted=row.OrderBy(s=>s.X*dx+s.Y*dy).ToArray();
                var positions=sorted.Select(s=>s.X*dx+s.Y*dy).ToArray();
                var distances=positions.Zip(positions.Skip(1),(x,y)=>y-x).ToArray();
                var horizontal=Straight(row,dx,dy)&&distances.Any(d=>Math.Abs(d-20)<.01)&&distances.All(d=>d>=19.99);
                if(!horizontal&&rowShift==0)continue;
                double Shift(double p)
                {
                    if(!horizontal)return 0;
                    for(var i=1;i<positions.Length;i++)if(p<=positions[i])return 2*(i-1+Math.Clamp((p-positions[i-1])/(positions[i]-positions[i-1]),0,1));
                    return 2*(positions.Length-1);
                }
                var anchors=row.Select(s=>s.RowAisleAnchors).FirstOrDefault(v=>v is not null);
                var transformed=anchors?.Select(p=>{var shift=Shift((p.X-8)*dx+(p.Y-8)*dy);return p with{X=p.X-dx*shift+uy*rowShift,Y=p.Y-dy*shift-ux*rowShift};}).ToArray();
                for(var i=0;i<sorted.Length;i++)
                {
                    sorted[i].X-=dx*(horizontal?2*i:0)-uy*rowShift;
                    sorted[i].Y-=dy*(horizontal?2*i:0)+ux*rowShift;
                    sorted[i].RowAisleAnchors=transformed;
                }
            }
        }
    }
    private static (double X,double Y) Axis(IReadOnlyList<DraftSeat> row)
    {
        if(row.Count<2)return (1,0);
        var cx=row.Average(s=>s.X);var cy=row.Average(s=>s.Y);
        var xx=row.Sum(s=>(s.X-cx)*(s.X-cx));var yy=row.Sum(s=>(s.Y-cy)*(s.Y-cy));var xy=row.Sum(s=>(s.X-cx)*(s.Y-cy));
        var angle=.5*Math.Atan2(2*xy,xx-yy);return (Math.Cos(angle),Math.Sin(angle));
    }
    private static bool Straight(IReadOnlyList<DraftSeat> row,double dx,double dy)
    {var values=row.Select(s=>-s.X*dy+s.Y*dx).ToArray();return values.Max()-values.Min()<.01;}
}
public sealed partial class Store
{
    private void EnsureHallCompaction052()
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var schema=Command(c,"""
            CREATE TABLE IF NOT EXISTS hall_compaction_052(venue_id INTEGER PRIMARY KEY REFERENCES venue(id));
            CREATE TABLE IF NOT EXISTS hall_geometry_backup_052(seat_id INTEGER PRIMARY KEY,x REAL NOT NULL,y REAL NOT NULL,anchors TEXT);
            """)){schema.Transaction=tx;schema.ExecuteNonQuery();}
        var venues=new List<long>();
        using(var q=Command(c,"SELECT id FROM venue WHERE id NOT IN(SELECT venue_id FROM hall_compaction_052)"))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())venues.Add(r.GetInt64(0));}
        foreach(var venue in venues)
        {
            var entries=new List<(long Id,DraftSeat Seat,double X,double Y,string? Anchors)>();
            using(var q=Command(c,"SELECT s.id,s.sector_id,s.row_label,s.number,s.x,s.y,a.anchors FROM seat s LEFT JOIN hall_row_anchors a ON a.seat_id=s.id WHERE s.venue_id=$v ORDER BY s.id","$v",venue))
            {
                q.Transaction=tx;using var r=q.ExecuteReader();
                while(r.Read())
                {
                    var json=r.IsDBNull(6)?null:r.GetString(6);var x=r.GetDouble(4);var y=r.GetDouble(5);
                    entries.Add((r.GetInt64(0),new(){Sector=r.GetInt64(1).ToString(),Row=r.GetString(2),Number=r.GetString(3),X=x,Y=y,RowAisleAnchors=json is null?null:JsonSerializer.Deserialize<HallEditing025.RowAnchor[]>(json)},x,y,json));
                }
            }
            HallCompaction052.Compact(entries.Select(e=>e.Seat).ToArray());
            // Legacy glyph centres were (8,8); the same 18-unit cell now has centre (9,9).
            foreach(var entry in entries)if(entry.Seat.RowAisleAnchors is not null)
                entry.Seat.RowAisleAnchors=entry.Seat.RowAisleAnchors.Select(a=>a with{X=a.X+1,Y=a.Y+1}).ToArray();
            foreach(var e in entries.Where(e=>Math.Abs(e.X-e.Seat.X)>.00001||Math.Abs(e.Y-e.Seat.Y)>.00001||e.Anchors!=(e.Seat.RowAisleAnchors is null?null:JsonSerializer.Serialize(e.Seat.RowAisleAnchors))))
            {
                using var backup=Command(c,"INSERT OR IGNORE INTO hall_geometry_backup_052 VALUES($s,$x,$y,$a)","$s",e.Id,"$x",e.X,"$y",e.Y,"$a",(object?)e.Anchors??DBNull.Value);backup.Transaction=tx;backup.ExecuteNonQuery();
                using var update=Command(c,"UPDATE seat SET x=$x,y=$y WHERE id=$s","$x",e.Seat.X,"$y",e.Seat.Y,"$s",e.Id);update.Transaction=tx;update.ExecuteNonQuery();
                if(e.Seat.RowAisleAnchors is not null){using var anchors=Command(c,"INSERT OR REPLACE INTO hall_row_anchors VALUES($s,$a)","$s",e.Id,"$a",JsonSerializer.Serialize(e.Seat.RowAisleAnchors));anchors.Transaction=tx;anchors.ExecuteNonQuery();}
            }
            using var mark=Command(c,"INSERT INTO hall_compaction_052 VALUES($v)","$v",venue);mark.Transaction=tx;mark.ExecuteNonQuery();
        }
        tx.Commit();
    }
}