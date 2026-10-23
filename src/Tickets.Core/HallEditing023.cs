namespace Tickets.Core;

/// <summary>Draft-only geometry. Seat identities and numbering never change.</summary>
public static class HallEditing023
{
    public static void Move(IReadOnlyList<DraftSeat> seats,double dx,double dy)
    {
        if(!double.IsFinite(dx)||!double.IsFinite(dy))throw new TicketException("Некорректное перемещение.");
        foreach(var s in seats){s.X+=dx;s.Y+=dy;}
    }
    public static void Rotate(IReadOnlyList<DraftSeat> seats,double degrees)
    {
        if(!double.IsFinite(degrees))throw new TicketException("Некорректный угол.");
        if(seats.Count==0)return;
        var cx=seats.Average(s=>s.X);var cy=seats.Average(s=>s.Y);var a=degrees*Math.PI/180;
        foreach(var s in seats){var x=s.X-cx;var y=s.Y-cy;s.X=cx+x*Math.Cos(a)-y*Math.Sin(a);s.Y=cy+x*Math.Sin(a)+y*Math.Cos(a);}
    }
    // Insert one chair-width gap before each selected run, following the stored row order,
    // including rotated rows. No place is removed or renumbered by this operation.
    public static void MakeAisle(IReadOnlyList<DraftSeat> seats,IReadOnlyList<DraftSeat> selected)
    {
        var chosen=selected.ToHashSet();
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)))
        {
            var ordered=row.ToArray();if(ordered.Length<2||!ordered.Any(chosen.Contains))continue;
            var dx=ordered[^1].X-ordered[0].X;var dy=ordered[^1].Y-ordered[0].Y;var length=Math.Sqrt(dx*dx+dy*dy);
            if(length<.001)continue;
            var gaps=0;var previous=false;
            foreach(var s in ordered)
            {
                var active=chosen.Contains(s);if(active&&!previous)gaps++;
                s.X+=dx/length*20*gaps;s.Y+=dy/length*20*gaps;previous=active;
            }
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
