using System.Globalization;
namespace Tickets.Core;

/// <summary>Draft edits never repack remaining seats into empty geometric cells.</summary>
public static class HallEditing024
{
    public static void Delete(IList<DraftSeat> seats,IReadOnlyList<DraftSeat> selected)
    {
        HallAnchors027.Capture(seats.ToArray());
        var chosen=selected.ToHashSet();
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)).Where(g=>g.Any(chosen.Contains)).ToArray())
        {
            // Keep custom numeric prefixes (VIP-1, А1) as well as ordinary and letter sequences.
            var replacements=new Dictionary<DraftSeat,string>();
            var fallback=false;
            foreach(var family in row.GroupBy(s=>System.Text.RegularExpressions.Regex.Match(s.Number,@"^(.*?)([0-9]+)$") is {Success:true} match?"number:"+match.Groups[1].Value:"letters"))
            {
                var ordered=family.OrderBy(s=>s.Number,SeatLabelComparer.Instance).ToArray();
                var first=ordered[0].Number;var suffix=System.Text.RegularExpressions.Regex.Match(first,@"^(.*?)([0-9]+)$");var offset=0;
                foreach(var seat in ordered.Where(s=>!chosen.Contains(s)))
                {
                    try{replacements[seat]=suffix.Success?suffix.Groups[1].Value+HallLayout.Label(suffix.Groups[2].Value,offset++):HallLayout.Label(first,offset++);}
                    catch(TicketException){fallback=true;break;}
                }
            }
            if(fallback)
            {
                var number=1;foreach(var seat in row.Where(s=>!chosen.Contains(s)).OrderBy(s=>s.Number,SeatLabelComparer.Instance))replacements[seat]=(number++).ToString(CultureInfo.InvariantCulture);
            }
            foreach(var pair in replacements)pair.Key.Number=pair.Value;
        }
        foreach(var seat in chosen)seats.Remove(seat);
    }
    public static void MakeRowAisle(IReadOnlyList<DraftSeat> seats,IReadOnlyList<DraftSeat> selected,double gap=22)
    {
        if(!double.IsFinite(gap)||gap<=0||gap>1000)throw new TicketException("Ширина прохода: больше 0 и до 1000.");
        foreach(var sector in selected.GroupBy(s=>s.Sector))
        {
            var rows=seats.Where(s=>s.Sector==sector.Key).GroupBy(s=>s.Row).Select(g=>new{Label=g.Key,Seats=g.ToArray(),X=g.Average(s=>s.X),Y=g.Average(s=>s.Y)}).ToArray();
            if(rows.Length<2)continue;
            // Stored row insertion order anchors progression through rotations and arbitrary labels.
            var dx=rows[^1].X-rows[0].X;var dy=rows[^1].Y-rows[0].Y;var length=Math.Sqrt(dx*dx+dy*dy);
            var widest=rows.MaxBy(r=>r.Seats.Length)!;
            if(widest.Seats.Length>=2)
            {
                var points=widest.Seats.OrderBy(s=>s.Number,SeatLabelComparer.Instance).ToArray();var vx=points[^1].X-points[0].X;var vy=points[^1].Y-points[0].Y;var rowLength=Math.Sqrt(vx*vx+vy*vy);
                if(rowLength>.001){var nx=-vy/rowLength;var ny=vx/rowLength;if(nx*dx+ny*dy<0){nx=-nx;ny=-ny;}dx=nx;dy=ny;length=1;}
            }
            if(length<.001)throw new TicketException("Не удалось определить направление рядов.");
            dx/=length;dy/=length;
            var chosenRows=sector.Select(s=>s.Row).ToHashSet();var ordered=rows.OrderBy(r=>r.X*dx+r.Y*dy).ToArray();var shift=0d;
            foreach(var row in ordered)
            {
                foreach(var seat in row.Seats){seat.X+=dx*shift;seat.Y+=dy*shift;seat.RowAisleAnchors=HallAnchors027.Move(seat.RowAisleAnchors,dx*shift,dy*shift);}
                if(chosenRows.Contains(row.Label))shift+=gap;
            }
        }
    }
    private sealed class SeatLabelComparer:IComparer<string>
    {
        internal static readonly SeatLabelComparer Instance=new();
        public int Compare(string? a,string? b)
        {
            if(long.TryParse(a,out var x)&&long.TryParse(b,out var y))return x.CompareTo(y);
            var length=(a?.Length??0).CompareTo(b?.Length??0);return length!=0?length:StringComparer.CurrentCultureIgnoreCase.Compare(a,b);
        }
    }
}
