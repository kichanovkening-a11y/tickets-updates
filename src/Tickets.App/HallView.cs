using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Tickets.Core;

namespace Tickets.App;

/// <summary>One cached drawing for the hall. Mouse motion never writes to the database.</summary>
public sealed class HallView : FrameworkElement
{
    private List<SeatInfo> seats=[];
    private DrawingGroup drawing=new();
    private IReadOnlyList<SectorInfo> sectors=[];
    private decimal? focusPrice;
    /// <summary>Visual price focus, preserved across reloads until the caller clears it.</summary>
    public decimal? FocusPrice
    {
        get=>focusPrice;
        set { if(focusPrice==value)return; focusPrice=value; RebuildDrawing(); InvalidateVisual(); }
    }
    private Rect bounds=new(0,0,100,100);
    private double zoom=1;
    private Vector offset;
    private Point start, last;
    private Point worldAnchor;
    private MouseButton gestureButton;
    private readonly DispatcherTimer scrollTimer;
    private readonly ToolTip seatTip=new() {
        Placement=PlacementMode.Relative, StaysOpen=true, IsHitTestVisible=false,
        Foreground=Brushes.Black,FontWeight=FontWeights.Bold,Background=Brushes.LightYellow,
        Opacity=1,Padding=new Thickness(9,6,9,6),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)
    };
    private bool dragging, panning, marquee, removing, rightGesture, panMoved;
    private HashSet<long> gestureSelection=[];
    private long? hoverId;
    public HashSet<long> Selected { get; }=[];
    public bool AllowUnavailableSelection{get;set;}
    private bool CanSelect(SeatInfo seat)=>AllowUnavailableSelection||SeatSelection.CanSelect(seat);
    public event Action? SelectionChanged;
    public event EventHandler? EmptySpaceClicked;
    public event Action<long,Point>? SeatContextRequested;
    public event Action<SeatInfo>? SeatSelectionRejected;
    public IReadOnlyList<SeatInfo> SelectedSeats => seats.Where(s=>Selected.Contains(s.Id)).ToList();

    public HallView()
    {
        ClipToBounds=true; Focusable=true;
        seatTip.PlacementTarget=this;
        scrollTimer=new DispatcherTimer(TimeSpan.FromMilliseconds(30),DispatcherPriority.Input,(_,_)=>AutoScroll(),Dispatcher);
        scrollTimer.Stop();
        Unloaded+=(_,_)=> { EndGesture(); CloseTip(); };
        SizeChanged+=(_,_)=> { if(seats.Count>0) Fit(); };
    }

    public static Brush SeatBrush(string color)
    {
        try { var b=(Brush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }
        catch { return Brushes.SteelBlue; }
    }
    private static FormattedText Text(string text,double size,Brush color)
        => new(text,CultureInfo.GetCultureInfo("ru-RU"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.Bold,FontStretches.Normal),size,color,1);

    public void Load(IReadOnlyList<SeatInfo> source,IReadOnlyList<SectorInfo> sectors,bool fit)
    {
        EndGesture(); CloseTip(); seats=source.ToList(); this.sectors=sectors.ToList(); Selected.Clear(); hoverId=null;
        RebuildDrawing();
        if(fit) Fit(); else InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    private void RebuildDrawing()
    {
        var group=new DrawingGroup();
        using(var dc=group.Open())
        {
            var sectorSeats=seats.ToLookup(s=>s.Sector);
            foreach(var sector in sectors)
            {
                var label=Text(sector.Name,15.6,Brushes.SlateGray);
                var places=sectorSeats[sector.Name];
                var x=places.Any()?(places.Min(s=>s.X)+places.Max(s=>s.X)+16-label.Width)/2:sector.X;
                dc.DrawText(label,new Point(x,sector.Y));
            }
            foreach(var s in seats)
            {
                var rect=new Rect(s.X,s.Y,16,16);
                var occupied=s.TicketId.HasValue || s.IsReserved;
                var printed=occupied && s.IsPrinted && !s.IsReserved;
                var dimmed=focusPrice.HasValue && s.Amount!=focusPrice.Value;
                var brush=dimmed?Brushes.White:s.IsBlocked?Brushes.LightGray:occupied?(printed?Brushes.Red:Brushes.Black):s.Amount.HasValue?SeatBrush(s.Color):Brushes.Gainsboro;
                dc.DrawRoundedRectangle(brush,s.IsBlocked?new Pen(Brushes.Black,1):null,rect,2,2);
                if(occupied && !s.IsBlocked && !dimmed)
                {
                    // Clip a dense pattern to the seat, including its rounded corners.
                    dc.PushClip(new RectangleGeometry(rect,2,2));
                    var hatchPen=new Pen(printed?Brushes.Black:Brushes.White,.9);
                    for(var x=-16;x<16;x+=4)
                        dc.DrawLine(hatchPen,new Point(s.X+x,s.Y+16),new Point(s.X+x+16,s.Y));
                    dc.Pop();
                }
                if(s.IsBlocked && !dimmed)
                {
                    var cross=new Pen(Brushes.Black,2.6);
                    dc.DrawLine(cross,new Point(s.X+3,s.Y+3),new Point(s.X+13,s.Y+13));
                    dc.DrawLine(cross,new Point(s.X+13,s.Y+3),new Point(s.X+3,s.Y+13));
                    continue;
                }
                var textColor=Brushes.Black;
                if(brush is SolidColorBrush solid && (solid.Color.R*.299+solid.Color.G*.587+solid.Color.B*.114)<145) textColor=Brushes.White;
                var text=Text(s.Number,10.4,textColor);
                // Advance width and line height include bearings and font leading. Center
                // the actual visible glyph outlines instead, in the same world space as seats.
                var ink=text.BuildGeometry(new Point());
                var inkBounds=ink.Bounds;
                if(!inkBounds.IsEmpty && inkBounds.Width>0 && inkBounds.Height>0)
                {
                    var scale=Math.Min(1,Math.Min(14/inkBounds.Width,14/inkBounds.Height));
                    ink.Transform=new MatrixTransform(scale,0,0,scale,
                        s.X+8-(inkBounds.X+inkBounds.Width/2)*scale,
                        s.Y+8-(inkBounds.Y+inkBounds.Height/2)*scale);
                    dc.DrawGeometry(textColor,null,ink);
                }
            }
            foreach(var row in seats.GroupBy(s=>(s.Sector,s.Y,s.Row)))
            {
                var first=row.MinBy(s=>s.X)!;
                var label=Text(first.Row,11.7,Brushes.SlateGray);
                dc.DrawText(label,new Point(first.X-label.Width-6,first.Y+1));
            }
        }
        group.Freeze(); drawing=group;
        bounds=drawing.Bounds;
        if(bounds.IsEmpty) bounds=new Rect(0,0,100,100);
    }

    public void Fit()
    {
        CloseTip();
        zoom=Math.Clamp(Math.Min(Math.Max(50,ActualWidth-40)/Math.Max(1,bounds.Width),
            Math.Max(50,ActualHeight-40)/Math.Max(1,bounds.Height)),.08,4);
        offset=new Vector((ActualWidth-bounds.Width*zoom)/2-bounds.X*zoom,
            (ActualHeight-bounds.Height*zoom)/2-bounds.Y*zoom);
        InvalidateVisual();
    }
    public void Zoom(double factor) => ZoomAt(new Point(ActualWidth/2,ActualHeight/2),factor);
    public void Clear() { Selected.Clear(); SelectionChanged?.Invoke(); InvalidateVisual(); }
    public void SetSelection(IEnumerable<long> ids)
    { var allowed=seats.Where(CanSelect).Select(s=>s.Id).ToHashSet();Selected.Clear();foreach(var id in ids)if(allowed.Contains(id))Selected.Add(id);SelectionChanged?.Invoke();InvalidateVisual(); }
    private Point World(Point p)=>new((p.X-offset.X)/zoom,(p.Y-offset.Y)/zoom);
    private Point Screen(Point p)=>new(p.X*zoom+offset.X,p.Y*zoom+offset.Y);
    private void ZoomAt(Point pivot,double factor)
    {
        CloseTip();
        var world=World(pivot); zoom=Math.Clamp(zoom*factor,.08,6);
        offset=new Vector(pivot.X-world.X*zoom,pivot.Y-world.Y*zoom); InvalidateVisual();
    }
    private SeatInfo? Hit(Point p)
    {
        var w=World(p);
        return SeatSelection.Hit(seats,w.X,w.Y);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));
        dc.PushTransform(new MatrixTransform(zoom,0,0,zoom,offset.X,offset.Y));
        dc.DrawDrawing(drawing);
        var outerPen=new Pen(Brushes.Black,2/zoom);
        var innerPen=new Pen(Brushes.White,2/zoom);
        foreach(var s in seats.Where(s=>Selected.Contains(s.Id)))
        {
            // Two screen-pixel borders remain visible over every price and hatch color.
            // Neither border covers the seat fill or changes its status pattern.
            dc.DrawRoundedRectangle(null,outerPen,new Rect(s.X-3/zoom,s.Y-3/zoom,16+6/zoom,16+6/zoom),2,2);
            dc.DrawRoundedRectangle(null,innerPen,new Rect(s.X-1/zoom,s.Y-1/zoom,16+2/zoom,16+2/zoom),2,2);
        }
        dc.Pop();
        if(marquee && dragging) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30,30,110,220)),
            new Pen(Brushes.DodgerBlue,1),new Rect(Screen(worldAnchor),last));
        if(seats.Count==0) dc.DrawText(Text("В этом зале пока нет мест",16,Brushes.SlateGray),new Point(25,25));
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    { ZoomAt(e.GetPosition(this),e.Delta>0?1.15:1/1.15); e.Handled=true; }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if(e.ChangedButton is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle))return;
        if(dragging)return;
        if(e.ChangedButton==MouseButton.Left && e.ClickCount==2 && Hit(e.GetPosition(this)) is null)
        { Fit(); e.Handled=true; return; }
        CloseTip();
        Focus(); start=last=e.GetPosition(this); dragging=true;
        gestureButton=e.ChangedButton;
        panning=e.ChangedButton is MouseButton.Right or MouseButton.Middle || Keyboard.IsKeyDown(Key.Space);
        rightGesture=e.ChangedButton==MouseButton.Right;panMoved=false;
        marquee=false;gestureSelection=Selected.ToHashSet();
        worldAnchor=World(start);
        removing=SeatSelection.RemoveOnDrag(seats,gestureSelection,worldAnchor.X,worldAnchor.Y);
        CaptureMouse(); if(panning) Cursor=Cursors.Hand; e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p=e.GetPosition(this);
        if(dragging)
        {
            if(panning)
            {
                // A stationary right click opens seat actions without nudging the hall.
                if(!rightGesture || panMoved || (p-start).Length>=3)
                { offset+=p-(rightGesture && !panMoved?start:last);panMoved=true; }
            }
            else if(marquee || (p-start).Length>=6){marquee=true;SelectRange(p);scrollTimer.Start();}
            last=p; InvalidateVisual(); return;
        }
        UpdateTip(p);
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if(!dragging || e.ChangedButton!=gestureButton) return;
        var p=e.GetPosition(this);
        var emptyClick=false;
        var contextSeat=rightGesture && e.ChangedButton==MouseButton.Right && !panMoved && (p-start).Length<3?Hit(p):null;
        if(!panning)
        {
            if(marquee || (p-start).Length>=6)
            {
                SelectRange(p);
            }
            else if((p-start).Length<6)
            {
                var s=Hit(p);
                if(s is not null)
                {
                    if(AllowUnavailableSelection)SeatSelection.Toggle(Selected,s.Id);else SeatSelection.Toggle(Selected,s);
                    if(!CanSelect(s))SeatSelectionRejected?.Invoke(s);
                }
                else if(e.ChangedButton==MouseButton.Left && Hit(start) is null)emptyClick=true;
            }
            SelectionChanged?.Invoke();
        }
        EndGesture(); e.Handled=true;
        if(contextSeat is not null) SeatContextRequested?.Invoke(contextSeat.Id,p);
        if(emptyClick)EmptySpaceClicked?.Invoke(this,EventArgs.Empty);
    }
    private void SelectRange(Point end)
    {
        var to=World(end);
        var next=SeatSelection.ApplyRectangle(seats,gestureSelection,worldAnchor.X,worldAnchor.Y,to.X,to.Y,removing,AllowUnavailableSelection);
        if(!AllowUnavailableSelection)
        {
            var left=Math.Min(worldAnchor.X,to.X);var right=Math.Max(worldAnchor.X,to.X);var top=Math.Min(worldAnchor.Y,to.Y);var bottom=Math.Max(worldAnchor.Y,to.Y);
            foreach(var seat in seats.Where(s=>!CanSelect(s)&&s.X<right&&s.X+SeatSelection.SeatSize>left&&s.Y<bottom&&s.Y+SeatSelection.SeatSize>top))SeatSelectionRejected?.Invoke(seat);
        }
        if(Selected.SetEquals(next))return;
        Selected.Clear();Selected.UnionWith(next);SelectionChanged?.Invoke();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e)
    { EndGesture(); base.OnLostMouseCapture(e); }

    protected override void OnMouseLeave(MouseEventArgs e)
    { CloseTip(); base.OnMouseLeave(e); }

    private void EndGesture()
    {
        scrollTimer.Stop();
        dragging=panning=marquee=rightGesture=panMoved=false;
        Cursor=Cursors.Arrow;
        if(IsMouseCaptured)ReleaseMouseCapture();
        InvalidateVisual();
    }

    private void AutoScroll()
    {
        if(!dragging || !marquee || panning || Mouse.LeftButton!=MouseButtonState.Pressed || !IsMouseCaptured)
        { scrollTimer.Stop(); return; }
        // Mouse capture supplies positions outside the viewport too. Keep the original
        // world anchor fixed while moving the hall beneath the other marquee corner.
        last=Mouse.GetPosition(this);
        var delta=new Vector(EdgeScroll(last.X,ActualWidth),EdgeScroll(last.Y,ActualHeight));
        var next=offset;
        if(delta.X!=0)next.X=ClampScroll(offset.X+delta.X,ActualWidth,bounds.Left,bounds.Right);
        if(delta.Y!=0)next.Y=ClampScroll(offset.Y+delta.Y,ActualHeight,bounds.Top,bounds.Bottom);
        if(next==offset)return;
        offset=next; SelectRange(last); InvalidateVisual();
    }

    private static double EdgeScroll(double position,double extent)
    {
        if(extent<=0)return 0;
        var edge=Math.Min(36,extent/3);
        if(position<edge)return 18*Math.Clamp((edge-position)/edge,0,1);
        if(position>extent-edge)return -18*Math.Clamp((position-extent+edge)/edge,0,1);
        return 0;
    }

    private double ClampScroll(double value,double extent,double first,double final)
    {
        var a=24-first*zoom;var b=extent-24-final*zoom;
        return Math.Clamp(value,Math.Min(a,b),Math.Max(a,b));
    }

    private void CloseTip() { seatTip.IsOpen=false; hoverId=null; }

    private void UpdateTip(Point p)
    {
        var s=Hit(p);
        if(s is null || ActualWidth<40 || ActualHeight<40) { CloseTip(); return; }
        if(s.Id!=hoverId)
        {
            hoverId=s.Id;
            seatTip.Content=$"{s.Label}\n{(s.Amount.HasValue?s.Amount+" ₽":"Цена не назначена")}\n{s.Status}";
        }
        seatTip.MaxWidth=Math.Max(20,ActualWidth-8);
        seatTip.MaxHeight=Math.Max(20,ActualHeight-8);
        seatTip.Measure(new Size(seatTip.MaxWidth,seatTip.MaxHeight));
        var size=seatTip.DesiredSize;
        // Prefer below/right of the cursor, flip near edges, and stay in the hall.
        var x=p.X+14;var y=p.Y+18;
        if(x+size.Width>ActualWidth-4)x=p.X-size.Width-14;
        if(y+size.Height>ActualHeight-4)y=p.Y-size.Height-18;
        seatTip.HorizontalOffset=Math.Clamp(x,4,Math.Max(4,ActualWidth-size.Width-4));
        seatTip.VerticalOffset=Math.Clamp(y,4,Math.Max(4,ActualHeight-size.Height-4));
        seatTip.IsOpen=true;
    }
}
