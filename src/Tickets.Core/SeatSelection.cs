namespace Tickets.Core;

/// <summary>Selection geometry in hall coordinates, shared by clicks and drag gestures.</summary>
public static class SeatSelection
{
    public const double SeatSize=16;

    public static SeatInfo? Hit(IEnumerable<SeatInfo> seats,double x,double y)
        =>seats.LastOrDefault(s=>x>=s.X && x<s.X+SeatSize && y>=s.Y && y<s.Y+SeatSize);

    public static bool RemoveOnDrag(IEnumerable<SeatInfo> seats,IEnumerable<long> initial,double x,double y)
        =>Hit(seats,x,y) is { } seat && initial.Contains(seat.Id);

    public static bool CanSelect(SeatInfo seat)=>!seat.TicketId.HasValue&&!seat.IsReserved&&!seat.IsBlocked;

    public static void Toggle(HashSet<long> selection,SeatInfo seat)
    { if(CanSelect(seat))Toggle(selection,seat.Id);else selection.Remove(seat.Id); }

    public static void Toggle(HashSet<long> selection,long id)
    { if(!selection.Remove(id))selection.Add(id); }

    public static HashSet<long> ApplyRectangle(IEnumerable<SeatInfo> seats,IEnumerable<long> initial,
        double startX,double startY,double endX,double endY,bool remove,bool includeUnavailable=false)
    {
        var allowed=seats.Where(s=>includeUnavailable||CanSelect(s)).Select(s=>s.Id).ToHashSet();
        var next=initial.Where(allowed.Contains).ToHashSet();
        var left=Math.Min(startX,endX);var right=Math.Max(startX,endX);
        var top=Math.Min(startY,endY);var bottom=Math.Max(startY,endY);
        if(left>=right || top>=bottom)return next;
        // Strict inequalities exclude seats that only touch a marquee edge.
        foreach(var seat in seats.Where(s=>(includeUnavailable||CanSelect(s))&&s.X<right && s.X+SeatSize>left && s.Y<bottom && s.Y+SeatSize>top))
        { if(remove)next.Remove(seat.Id);else next.Add(seat.Id); }
        return next;
    }
}
