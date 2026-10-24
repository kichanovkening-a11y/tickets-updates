namespace Tickets.Core;

/// <summary>Draft-only geometry. Seat identities and numbering never change.</summary>
public static class HallEditing023
{
    public static void Move(IReadOnlyList<DraftSeat> seats,double dx,double dy)
    {
        if(!double.IsFinite(dx)||!double.IsFinite(dy))throw new TicketException("Некорректное перемещение.");
        foreach(var s in seats){s.X+=dx;s.Y+=dy;s.RowAisleAnchors=HallAnchors027.Move(s.RowAisleAnchors,dx,dy);}
    }
    public static void Rotate(IReadOnlyList<DraftSeat> seats,double degrees)
    {
        if(!double.IsFinite(degrees))throw new TicketException("Некорректный угол.");
        if(seats.Count==0)return;
        var cx=seats.Average(s=>s.X);var cy=seats.Average(s=>s.Y);var a=degrees*Math.PI/180;
        foreach(var s in seats){var x=s.X-cx;var y=s.Y-cy;s.X=cx+x*Math.Cos(a)-y*Math.Sin(a);s.Y=cy+x*Math.Sin(a)+y*Math.Cos(a);s.RowAisleAnchors=HallAnchors027.Rotate(s.RowAisleAnchors,cx,cy,a);}
    }
    // Insert one chair-width gap before each selected run, following the stored row order,
    // including rotated rows. No place is removed or renumbered by this operation.
    public static void MakeAisle(IReadOnlyList<DraftSeat> seats,IReadOnlyList<DraftSeat> selected)
    {
        var chosen=selected.ToHashSet();
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)))
        {
            var points=row.ToArray();if(points.Length<2||!points.Any(chosen.Contains))continue;
            var cx=points.Average(s=>s.X);var cy=points.Average(s=>s.Y);
            var xx=points.Sum(s=>(s.X-cx)*(s.X-cx));var yy=points.Sum(s=>(s.Y-cy)*(s.Y-cy));var xy=points.Sum(s=>(s.X-cx)*(s.Y-cy));
            if(xx+yy<.001)continue;
            var angle=.5*Math.Atan2(2*xy,xx-yy);var dx=Math.Cos(angle);var dy=Math.Sin(angle);
            if(Math.Abs(dy)>Math.Abs(dx)&&dy<0){dx=-dx;dy=-dy;}
            // Curved rows are stored by Y/X, often interleaving their left and right
            // ends. Sort on their geometric major axis instead of database order.
            var ordered=points.OrderBy(s=>s.X*dx+s.Y*dy).ToArray();
            HallAnchors027.Capture(points);
            var anchors=points[0].RowAisleAnchors!;
            var original=ordered.Select(s=>(s.X+8)*dx+(s.Y+8)*dy).ToArray();
            var shifts=new double[ordered.Length];var starts=new List<int>();var gaps=0;var previous=false;
            for(var i=0;i<ordered.Length;i++)
            {
                var seat=ordered[i];var active=chosen.Contains(seat);
                if(active&&!previous){gaps++;starts.Add(i);}
                shifts[i]=20*gaps;seat.X+=dx*shifts[i];seat.Y+=dy*shifts[i];previous=active;
            }
            // Carry saved centres by this explicit geometry edit only. Never derive
            // previous aisle positions from the surviving seats after deletions.
            double Shift(double position)
            {
                if(position<=original[0])return shifts[0];
                for(var i=1;i<original.Length;i++)if(position<=original[i])
                {
                    var span=original[i]-original[i-1];var t=span<.001?0:(position-original[i-1])/span;
                    return shifts[i-1]+(shifts[i]-shifts[i-1])*t;
                }
                return shifts[^1];
            }
            var updated=anchors.Select(a=>{var shift=Shift(a.X*dx+a.Y*dy);return a with{X=a.X+dx*shift,Y=a.Y+dy*shift};}).ToList();
            foreach(var i in starts.Where(i=>i>0))
            {
                // Widening an existing aisle keeps its saved caption; only a new
                // selected-run boundary introduces an additional aisle number.
                if(anchors.Any(a=>{var p=a.X*dx+a.Y*dy;return p>=original[i-1]&&p<=original[i];}))continue;
                updated.Add(new((ordered[i-1].X+ordered[i].X)/2+8,(ordered[i-1].Y+ordered[i].Y)/2+8,true));
            }
            foreach(var seat in points)seat.RowAisleAnchors=updated.ToArray();
        }
    }
    public static IReadOnlyList<SeatInfo> WithEstradaPlaceholders(IReadOnlyList<SeatInfo> source)
    {
        // Exact seat-key signature avoids interpreting genuine aisles in other halls.
        var expected=VenueLayouts019.EstradaSeats();
        if(source.Count!=expected.Count)return source;
        var keys=source.Select(s=>(s.Sector,s.Row,s.Number)).ToHashSet();
        if(keys.Count!=expected.Count||expected.Any(s=>!keys.Contains((s.Sector,s.Row,s.Number))))return source;
        var result=source.ToList();long id=-1;
        foreach(var (row,first,last) in new[]{("2",12,13),("3",17,19),("4",16,19)})
        {
            var left=source.Single(s=>s.Sector=="Партер"&&s.Row==row&&s.Number==(first-1).ToString());
            var right=source.Single(s=>s.Sector=="Партер"&&s.Row==row&&s.Number==(last+1).ToString());
            var dx=right.X-left.X;var dy=right.Y-left.Y;var len=Math.Sqrt(dx*dx+dy*dy);if(len<.001)continue;
            // Preserve a centre aisle and create sufficient room for the missing labels.
            var ux=dx/len;var uy=dy/len;var extra=(last-first+1)*18;
            foreach(var s in source.Where(s=>s.Sector=="Партер"&&s.Row==row&&int.Parse(s.Number)>last))
            {var index=result.FindIndex(p=>p.Id==s.Id);result[index]=s with{X=s.X+ux*extra,Y=s.Y+uy*extra};}
            for(var n=first;n<=last;n++)result.Add(new(id--,"Партер",row,n.ToString(),left.X+ux*18*(n-first+1),left.Y+uy*18*(n-first+1),null,"#B8B8B8",null,"","",null));
        }
        return result;
    }
}
