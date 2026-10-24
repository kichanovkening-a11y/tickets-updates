using System.Globalization;
namespace Tickets.Core;

public static class HallEditing025
{
    public static void MakeAisle(IList<DraftSeat> seats,IReadOnlyList<DraftSeat> selected)
    {
        var chosen=selected.ToHashSet();var full=seats.GroupBy(s=>(s.Sector,s.Row)).Where(g=>g.Any(chosen.Contains)&&g.Where(s=>!s.Hidden).All(chosen.Contains)).SelectMany(g=>g).ToHashSet();
        var partial=selected.Where(s=>!full.Contains(s)).ToArray();
        HallEditing023.MakeAisle(seats.ToArray(),partial);
        var sectors=full.Select(s=>s.Sector).ToHashSet();foreach(var seat in full)seats.Remove(seat);
        foreach(var sector in sectors){var n=1;foreach(var row in seats.Where(s=>s.Sector==sector).GroupBy(s=>s.Row))if(IsNumeric(row.Key)){foreach(var seat in row)seat.Row=n.ToString(CultureInfo.InvariantCulture);n++;}}
    }
    private static bool IsNumeric(string s)=>long.TryParse(s,out _);
    public static void AssignLetter(IList<DraftSeat> seats,DraftSeat target,string input,bool row)
    {
        var letter=input.Trim().ToUpperInvariant();if(letter.Length!=1||!char.IsLetter(letter[0]))throw new TicketException("Введите одну букву.");
        var sector=seats.Where(s=>s.Sector==target.Sector).ToArray();
        if(row){var original=target.Row;if(sector.Any(s=>StringComparer.OrdinalIgnoreCase.Equals(s.Row,letter)&&s.Row!=original))throw new TicketException("Такой ряд уже есть.");foreach(var s in sector.Where(s=>s.Row==original))s.Row=letter;var n=1;foreach(var group in sector.GroupBy(s=>s.Row))if(IsNumeric(group.Key)){foreach(var s in group)s.Row=n.ToString(CultureInfo.InvariantCulture);n++;}}
        else{
            var points=sector.Where(s=>s.Row==target.Row).ToArray();if(points.Any(s=>s!=target&&StringComparer.OrdinalIgnoreCase.Equals(s.Number,letter)))throw new TicketException("Такое место уже есть.");
            // Capture direction before replacing the last numeric labels. Draft row
            // enumeration is saved as seat insertion IDs and restored by HallDraft;
            // canonicalizing these entries therefore survives all-letter rows/reopen.
            var logical=LogicalSeats(points.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"",null,"","",null)).ToArray())
                .Select(s=>points[(int)s.Id-1]).ToArray();
            var indices=Enumerable.Range(0,seats.Count).Where(i=>seats[i].Sector==target.Sector&&seats[i].Row==target.Row).ToArray();
            for(var i=0;i<indices.Length;i++)seats[indices[i]]=logical[i];
            // The old numeric labels retain the numbering direction, including RTL rows.
            var ordered=points.Where(s=>IsNumeric(s.Number)).OrderBy(s=>long.Parse(s.Number,CultureInfo.InvariantCulture)).ToArray();target.Number=letter;
            var n=1;foreach(var s in ordered.Where(s=>s!=target))s.Number=(n++).ToString(CultureInfo.InvariantCulture);
        }
    }
    public sealed record RowAnchor(double X,double Y,bool Centre=false);
    public static IReadOnlyList<RowAnchor> RowAnchors(IReadOnlyList<SeatInfo> row)
    {
        if(row.Count==0)return [];
        var points=LogicalSeats(row);var first=points[0];var last=points[^1];
        var vx=last.X-first.X;var vy=last.Y-first.Y;var len=Math.Sqrt(vx*vx+vy*vy);if(len<.001){vx=1;vy=0;len=1;}
        var ux=vx/len;var uy=vy/len;var result=new List<RowAnchor>{new(first.X+8-ux*24,first.Y+8-uy*24)};
        var gaps=points.Zip(points.Skip(1),(a,b)=>new{A=a,B=b,Distance=Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2))}).ToArray();
        var standard=gaps.Length==0?20:Math.Min(20,gaps.Min(g=>g.Distance));
        var stored=row.Select(s=>s.RowAisleAnchors).FirstOrDefault(a=>a is not null);
        if(stored is not null)result.AddRange(stored.Where(a=>a.Centre));
        else foreach(var gap in gaps.Where(g=>g.Distance>=standard+12&&g.Distance>=32))result.Add(new((gap.A.X+gap.B.X)/2+8,(gap.A.Y+gap.B.Y)/2+8,true));
        result.Add(new(last.X+8+ux*24,last.Y+8+uy*24));return result;
    }
    internal static SeatInfo[] LogicalSeats(IReadOnlyList<SeatInfo> row)
    {
        if(row.Count==0)return [];
        // Project along the actual row, choosing its direction from numeric labels.
        var cx=row.Average(s=>s.X);var cy=row.Average(s=>s.Y);
        var xx=row.Sum(s=>Math.Pow(s.X-cx,2));var yy=row.Sum(s=>Math.Pow(s.Y-cy,2));var xy=row.Sum(s=>(s.X-cx)*(s.Y-cy));
        var angle=.5*Math.Atan2(2*xy,xx-yy);var dx=Math.Cos(angle);var dy=Math.Sin(angle);
        var numbered=row.Where(s=>long.TryParse(s.Number,out _)).OrderBy(s=>long.Parse(s.Number,CultureInfo.InvariantCulture)).ToArray();
        var start=numbered.Length>=2?numbered[0]:row.MinBy(s=>s.Id)!;var finish=numbered.Length>=2?numbered[^1]:row.MaxBy(s=>s.Id)!;
        if((finish.X-start.X)*dx+(finish.Y-start.Y)*dy<0){dx=-dx;dy=-dy;}
        return row.OrderBy(s=>s.X*dx+s.Y*dy).ToArray();
    }

}
public static class SenderChange025
{
    public static string Accept(string previous,string next,Func<bool> confirm)=>string.IsNullOrWhiteSpace(previous)||previous==next||confirm()?next:previous;
}
public static class TicketPrintOrder
{
    public static IReadOnlyList<TicketInfo> Order(IEnumerable<TicketInfo> tickets,IReadOnlyList<SeatInfo>? hall=null)
    {
        var source=tickets.ToArray();if(hall is null)return source.OrderBy(t=>t.Sector,Natural.Instance).ThenBy(t=>t.Row,Natural.Instance).ThenBy(t=>t.Seat,Natural.Instance).ToArray();
        var rows=hall.OrderBy(s=>s.Id).GroupBy(s=>(s.Sector,s.Row)).Select((g,i)=>new{g.Key,Rank=i,Places=g.ToArray()}).ToArray();
        var ranks=new Dictionary<long,int>();var rank=0;
        foreach(var sector in rows.GroupBy(r=>r.Key.Sector))
        {
            var orderedRows=new List<int>();var numeric=new List<int>();
            void FlushRows(){orderedRows.AddRange(numeric.OrderBy(i=>rows[i].Key.Row,Natural.Instance));numeric.Clear();}
            foreach(var row in sector){if(long.TryParse(row.Key.Row,out _))numeric.Add(row.Rank);else{FlushRows();orderedRows.Add(row.Rank);}}FlushRows();
            foreach(var index in orderedRows){var row=rows[index];var run=new List<SeatInfo>();void Flush(){foreach(var s in run.OrderBy(s=>s.Number,Natural.Instance))ranks[s.Id]=rank++;run.Clear();}foreach(var seat in HallEditing025.LogicalSeats(row.Places)){if(long.TryParse(seat.Number,out _))run.Add(seat);else{Flush();ranks[seat.Id]=rank++;}}Flush();}}
        return source.OrderBy(t=>ranks.GetValueOrDefault(t.SeatId,int.MaxValue)).ThenBy(t=>t.Sector,Natural.Instance).ThenBy(t=>t.Row,Natural.Instance).ThenBy(t=>t.Seat,Natural.Instance).ToArray();
    }
    private sealed class Natural:IComparer<string>{internal static readonly Natural Instance=new();public int Compare(string? a,string? b)=>long.TryParse(a,out var x)&&long.TryParse(b,out var y)?x.CompareTo(y):StringComparer.CurrentCultureIgnoreCase.Compare(a,b);}
}
public sealed partial class Store
{
    public bool TicketPrintWarningSuppressed(string? blankId)=>DocumentSetting("ticket-print-warning:"+(blankId??"standard"),"")=="1";
    public void SuppressTicketPrintWarning(string? blankId,bool suppress){using var c=Open();using var cmd=Command(c,"INSERT INTO document_setting(name,value) VALUES($name,$value) ON CONFLICT(name) DO UPDATE SET value=excluded.value","$name","ticket-print-warning:"+(blankId??"standard"),"$value",suppress?"1":"0");cmd.ExecuteNonQuery();}
}
