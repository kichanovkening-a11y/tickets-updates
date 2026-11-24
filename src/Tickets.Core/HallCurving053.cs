namespace Tickets.Core;

/// <summary>Coordinate-only deformation: identities, labels and explicit aisle gaps survive.</summary>
public static class HallCurving053
{
    public static void Bend(IReadOnlyList<DraftSeat> seats,double degrees)
    {
        if(!double.IsFinite(degrees)||Math.Abs(degrees)>120)throw new TicketException("Изгиб: от −120 до 120 градусов.");
        if(seats.Count<2||Math.Abs(degrees)<.000001)return;
        var frame=Frame(seats);var (cx,cy,dx,dy)=frame;
        double U(double x,double y)=>(x-cx)*dx+(y-cy)*dy;
        double V(double x,double y)=>-(x-cx)*dy+(y-cy)*dx;
        var span=seats.Max(s=>U(s.X,s.Y))-seats.Min(s=>U(s.X,s.Y));if(span<.001)return;
        var radius=span/(degrees*Math.PI/180);
        if(seats.Any(s=>Math.Abs(V(s.X,s.Y)/radius)>.8))throw new TicketException("Для этой глубины зоны изгиб слишком сильный. Уменьшите его или выберите отдельные ряды.");
        (double X,double Y) Map(double x,double y)
        {
            var u=U(x,y);var v=V(x,y);var r=radius-v;var a=u/r;
            var nx=r*Math.Sin(a);var ny=v+r*(1-Math.Cos(a));
            return(cx+nx*dx-ny*dy,cy+nx*dy+ny*dx);
        }
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)))
        {
            var anchors=row.First().RowAisleAnchors?.Select(a=>{var p=Map(a.X-SeatSelection.SeatSize/2,a.Y-SeatSelection.SeatSize/2);return a with{X=p.X+SeatSelection.SeatSize/2,Y=p.Y+SeatSelection.SeatSize/2};}).ToArray();
            foreach(var s in row){var p=Map(s.X,s.Y);s.X=p.X;s.Y=p.Y;s.RowAisleAnchors=anchors;}
        }
    }
    public static void Straighten(IReadOnlyList<DraftSeat> seats)
    {
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)))
        {
            var points=row.ToArray();if(points.Length<2)continue;
            var (cx,cy,dx,dy)=Frame(points);var sorted=points.OrderBy(s=>(s.X-cx)*dx+(s.Y-cy)*dy).ToArray();
            var distances=new double[sorted.Length];for(var i=1;i<sorted.Length;i++)distances[i]=distances[i-1]+Math.Sqrt(Math.Pow(sorted[i].X-sorted[i-1].X,2)+Math.Pow(sorted[i].Y-sorted[i-1].Y,2));
            var start=-distances[^1]/2;
            var anchors=points[0].RowAisleAnchors?.Select(a=>{var u=(a.X-SeatSelection.SeatSize/2-cx)*dx+(a.Y-SeatSelection.SeatSize/2-cy)*dy;return a with{X=cx+u*dx+SeatSelection.SeatSize/2,Y=cy+u*dy+SeatSelection.SeatSize/2};}).ToArray();
            for(var i=0;i<sorted.Length;i++){sorted[i].X=cx+(start+distances[i])*dx;sorted[i].Y=cy+(start+distances[i])*dy;sorted[i].RowAisleAnchors=anchors;}
        }
    }
    public static (double X,double Y,double Dx,double Dy) Frame(IReadOnlyList<DraftSeat> seats)
    {
        var cx=seats.Average(s=>s.X);var cy=seats.Average(s=>s.Y);
        // Rows, rather than the tall zone's bounding box, define the horizontal direction.
        double xx=0,yy=0,xy=0;
        foreach(var row in seats.GroupBy(s=>(s.Sector,s.Row)))
        {
            var rx=row.Average(s=>s.X);var ry=row.Average(s=>s.Y);
            foreach(var s in row){xx+=(s.X-rx)*(s.X-rx);yy+=(s.Y-ry)*(s.Y-ry);xy+=(s.X-rx)*(s.Y-ry);}
        }
        var angle=.5*Math.Atan2(2*xy,xx-yy);return(cx,cy,Math.Cos(angle),Math.Sin(angle));
    }
}
