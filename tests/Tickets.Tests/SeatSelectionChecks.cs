using Tickets.Core;

internal static class SeatSelectionChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("marquee selects every partially intersected seat",_=>{
            var seats=Grid();
            var selection=SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),15,7,61,39,false);
            Require(selection.SetEquals(seats.Select(s=>s.Id)),"partial overlap missed visible seats");
            Require(SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),61,39,15,7,false).SetEquals(selection),"reverse diagonal differs");
        });
        test("marquee requires positive overlap in both dimensions",_=>{
            var seats=Grid();
            Require(SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),16,0,30,16,false).Count==0,"edge-only touches selected seats");
            Require(SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),8,0,8,46,false).Count==0,"zero-width marquee selected seats");
            Require(SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),0,8,76,8,false).Count==0,"zero-height marquee selected seats");
            Require(SeatSelection.ApplyRectangle(seats,Array.Empty<long>(),15.5,15.5,16.5,16.5,false).SetEquals(new long[]{1}),"small corner overlap missed");
        });
        test("drag removal preserves selection outside the rectangle",_=>{
            var seats=Grid();var initial=new long[]{1,2,3,4,5,6};
            Require(SeatSelection.RemoveOnDrag(seats,initial,8,8),"selected starting seat must remove");
            Require(!SeatSelection.RemoveOnDrag(seats,new long[]{6},8,8),"unselected starting seat must add");
            Require(!SeatSelection.RemoveOnDrag(seats,initial,22,8),"blank starting area must add");
            var removed=SeatSelection.ApplyRectangle(seats,initial,8,8,45,45,true);
            Require(removed.SetEquals(new long[]{3,6}),"removal touched outside selection or left intersecting seats");
            var shrunken=SeatSelection.ApplyRectangle(seats,initial,8,8,15,15,true);
            Require(shrunken.SetEquals(new long[]{2,3,4,5,6}),"drag did not recompute from gesture snapshot");
        });
        test("click toggles only the hit seat and occupied seats stay selectable",_=>{
            var seats=Grid();seats[0]=seats[0] with {TicketId=77};
            var selection=new HashSet<long>{6};var hit=SeatSelection.Hit(seats,8,8);
            Require(hit?.Id==1,"occupied hit missing");SeatSelection.Toggle(selection,hit!.Id);
            Require(selection.SetEquals(new long[]{1,6}),"click did not preserve other selections");
            SeatSelection.Toggle(selection,hit.Id);Require(selection.SetEquals(new long[]{6}),"second click did not remove only hit seat");
            Require(SeatSelection.Hit(seats,16,8) is null,"point at seat outer edge must not hit");
            Require(SeatSelection.ApplyRectangle(seats,selection,0,0,15,15,false).SetEquals(new long[]{1,6}),"occupied seat filtered out of marquee");
        });
    }
    private static SeatInfo[] Grid()=>Enumerable.Range(0,6).Select(i=>new SeatInfo(i+1,"S","R",(i+1).ToString(),i%3*30,i/3*30,100,"#123456",null,"","",null)).ToArray();
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
