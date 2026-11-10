using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Tickets.Core;

internal static class Hall013Checks
{
    // Inspect the rendered vector ink, so font advance/line-height centering cannot pass.
    internal static void Run()
    {
        var hall=new Tickets.App.HallView();
        var labels=new[]{"1","11","123","А","Б12"};
        var seats=labels.Select((label,i)=>new SeatInfo(i+1,"","",label,i*24,0,100,"#CCDDFF",null,"","",null)).ToArray();
        hall.Load(seats,Array.Empty<SectorInfo>(),false);
        var field=typeof(Tickets.App.HallView).GetField("drawing",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var drawing=(DrawingGroup)field.GetValue(hall)!;
        foreach(var seat in seats)
        {
            var ink=drawing.Children.OfType<GeometryDrawing>().Where(d=>d.Geometry.Bounds.Width>0&&d.Geometry.Bounds.Height>0&&d.Brush==Brushes.Black&&d.Pen is null).Select(d=>d.Bounds).SingleOrDefault(r=>Math.Abs(r.X+r.Width/2-seat.X-SeatSelection.SeatSize/2)<.01);
            Require(!ink.IsEmpty&&ink.Width>0,"Seat label is drawn as measurable ink");
            Require(Math.Abs(ink.Y+ink.Height/2-seat.Y-SeatSelection.SeatSize/2)<.01,"Seat label ink is vertically centered: "+seat.Number);
            Require(ink.Width<=14.001&&ink.Height<=14.001,"Complete seat label fits within seat: "+seat.Number);
        }
        hall.SetSelection(new[]{1L});var changes=0;hall.SelectionChanged+=()=>changes++;
        hall.FocusPrice=200;
        Require(hall.Selected.SetEquals(new[]{1L})&&changes==0,"Price focus leaves selection unchanged");
        drawing=(DrawingGroup)field.GetValue(hall)!;
        Require(drawing.Children.OfType<GeometryDrawing>().Count(d=>d.Brush==Brushes.White&&d.Geometry is RectangleGeometry)==seats.Length,"Other prices render white");
        hall.Load(seats,Array.Empty<SectorInfo>(),false);
        Require(hall.FocusPrice==200,"Reload preserves caller controlled price focus");
        var multiple=seats.Select((seat,i)=>seat with{Amount=i==0?100:i==1?200:i==2?null:300}).ToArray();
        hall.Load(multiple,Array.Empty<SectorInfo>(),false);hall.SetSelection([1L]);changes=0;
        hall.TogglePriceFocus(100);
        Require(hall.FocusPrices.ToHashSet().SetEquals([100m,200m])&&changes==0,"Second price adds to visual focus without modifiers or selection events");
        drawing=(DrawingGroup)field.GetValue(hall)!;
        Require(drawing.Children.OfType<GeometryDrawing>().Count(d=>d.Brush==Brushes.White&&d.Geometry is RectangleGeometry)==3,"Union highlights both prices and dims other or unpriced seats");
        hall.Load(multiple,Array.Empty<SectorInfo>(),false);hall.SetSelection([1L]);changes=0;
        Require(hall.FocusPrices.ToHashSet().SetEquals([100m,200m]),"Reload preserves all focused prices");
        hall.TogglePriceFocus(100);
        Require(hall.FocusPrices.SequenceEqual([200m]),"Repeated price click removes only that price");
        hall.TogglePriceFocus(200);
        Require(hall.FocusPrices.Count==0,"Removing the last price restores all prices");
        hall.TogglePriceFocus(100);hall.TogglePriceFocus(200);
        hall.FocusStatus030=HallStatus030.Reserved;
        Require(hall.FocusPrices.Count==0,"Status focus clears every price");
        hall.TogglePriceFocus(100);hall.TogglePriceFocus(200);
        Require(hall.FocusStatus030 is null,"Price focus clears status focus");
        Require(hall.Selected.SetEquals([1L])&&changes==0,"Multiple price toggles never mutate seat selection");
        hall.FocusPrice=null;
        Require(hall.FocusPrices.Count==0,"Clearing focus removes all prices");
        drawing=(DrawingGroup)field.GetValue(hall)!;
        Require(!drawing.Children.OfType<GeometryDrawing>().Any(d=>d.Brush==Brushes.White&&d.Geometry is RectangleGeometry),"Clearing focus restores category fill");
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}