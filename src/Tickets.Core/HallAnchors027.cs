using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace Tickets.Core;

/// <summary>Only aisle labels are fixed; outer row labels continue to follow the surviving seats.</summary>
public static class HallAnchors027
{
    public static void Capture(IReadOnlyList<DraftSeat> seats,bool replace=false)
    {
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)))
        {
            var stored=replace?null:row.Select(s=>s.RowAisleAnchors).FirstOrDefault(a=>a is not null);
            var anchors=stored??HallEditing025.RowAnchors(row.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"",null,"","",null)).ToArray()).Where(a=>a.Centre).ToArray();
            foreach(var seat in row)seat.RowAisleAnchors=anchors.ToArray();
        }
    }
    public static IReadOnlyList<HallEditing025.RowAnchor>? Move(IReadOnlyList<HallEditing025.RowAnchor>? anchors,double dx,double dy)
        =>anchors?.Select(a=>a with{X=a.X+dx,Y=a.Y+dy}).ToArray();
    public static IReadOnlyList<HallEditing025.RowAnchor>? Rotate(IReadOnlyList<HallEditing025.RowAnchor>? anchors,double cx,double cy,double radians)
        =>anchors?.Select(a=>a with{X=cx+(a.X-cx)*Math.Cos(radians)-(a.Y-cy)*Math.Sin(radians),Y=cy+(a.X-cx)*Math.Sin(radians)+(a.Y-cy)*Math.Cos(radians)}).ToArray();
    public static bool Equal(IReadOnlyList<HallEditing025.RowAnchor>? a,IReadOnlyList<HallEditing025.RowAnchor>? b)
        =>a is null?b is null:b is not null&&a.SequenceEqual(b);
}
public sealed partial class Store
{
    private void EnsureHallRowAnchors027()
    {
        using var c=Open();using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS hall_row_anchors(seat_id INTEGER PRIMARY KEY REFERENCES seat(id) ON DELETE CASCADE,anchors TEXT NOT NULL)");cmd.ExecuteNonQuery();
    }
    private static IReadOnlyList<HallEditing025.RowAnchor>? ReadRowAnchors027(SqliteDataReader reader,int ordinal)
        =>reader.IsDBNull(ordinal)?null:JsonSerializer.Deserialize<HallEditing025.RowAnchor[]>(reader.GetString(ordinal));
}
