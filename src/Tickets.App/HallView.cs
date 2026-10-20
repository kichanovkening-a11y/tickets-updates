using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

/// <summary>One cached drawing for the hall. Mouse motion never writes to the database.</summary>
public sealed class HallView : FrameworkElement
{
    private List<SeatInfo> seats=[];
    private DrawingGroup drawing=new();
    private Rect bounds=new(0,0,100,100);
    private double zoom=1;
    private Vector offset;
    private Point start, last;
    private bool dragging, panning, marquee, removing, rightGesture, panMoved;
    private HashSet<long> gestureSelection=[];
    private long? hoverId;
    public HashSet<long> Selected { get; }=[];
    public event Action? SelectionChanged;
    public event Action<long,Point>? SeatContextRequested;
    public IReadOnlyList<SeatInfo> SelectedSeats => seats.Where(s=>Selected.Contains(s.Id)).ToList();

    public HallView()
    {
        ClipToBounds=true; Focusable=true;
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
        seats=source.ToList(); Selected.Clear(); hoverId=null;
        var group=new DrawingGroup();
        using(var dc=group.Open())
        {
            foreach(var sector in sectors) dc.DrawText(Text(sector.Name,15.6,Brushes.SlateGray),new Point(sector.X,sector.Y));
            foreach(var s in seats)
            {
                var rect=new Rect(s.X,s.Y,16,16);
                var occupied=s.TicketId.HasValue || s.IsReserved;
                var printed=occupied && s.IsPrinted && !s.IsReserved;
                var brush=occupied?(printed?Brushes.Red:Brushes.Black):s.IsBlocked?Brushes.DarkOrange:s.Amount.HasValue?SeatBrush(s.Color):Brushes.Gainsboro;
                dc.DrawRoundedRectangle(brush,null,rect,2,2);
                if(occupied)
                {
                    // Clip a dense pattern to the seat, including its rounded corners.
                    dc.PushClip(new RectangleGeometry(rect,2,2));
                    var hatchPen=new Pen(printed?Brushes.Black:Brushes.White,.9);
                    for(var x=-16;x<16;x+=4)
                        dc.DrawLine(hatchPen,new Point(s.X+x,s.Y+16),new Point(s.X+x+16,s.Y));
                    dc.Pop();
                }
                var textColor=Brushes.Black;
                if(brush is SolidColorBrush solid && (solid.Color.R*.299+solid.Color.G*.587+solid.Color.B*.114)<145) textColor=Brushes.White;
                var text=Text(s.Number,10.4,textColor);
                // Keep the complete label visible even when it contains several letters or digits.
                if(text.Width>14)text.SetFontSize(10.4*14/text.Width);
                dc.DrawText(text,new Point(s.X+(16-text.Width)/2,s.Y+(16-text.Height)/2));
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
        if(fit) Fit(); else InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    public void Fit()
    {
        zoom=Math.Clamp(Math.Min(Math.Max(50,ActualWidth-40)/Math.Max(1,bounds.Width),
            Math.Max(50,ActualHeight-40)/Math.Max(1,bounds.Height)),.08,4);
        offset=new Vector((ActualWidth-bounds.Width*zoom)/2-bounds.X*zoom,
            (ActualHeight-bounds.Height*zoom)/2-bounds.Y*zoom);
        InvalidateVisual();
    }
    public void Zoom(double factor) => ZoomAt(new Point(ActualWidth/2,ActualHeight/2),factor);
    public void Clear() { Selected.Clear(); SelectionChanged?.Invoke(); InvalidateVisual(); }
    public void SetSelection(IEnumerable<long> ids)
    { var allowed=seats.Select(s=>s.Id).ToHashSet();Selected.Clear();foreach(var id in ids)if(allowed.Contains(id))Selected.Add(id);SelectionChanged?.Invoke();InvalidateVisual(); }
    private Point World(Point p)=>new((p.X-offset.X)/zoom,(p.Y-offset.Y)/zoom);
    private void ZoomAt(Point pivot,double factor)
    {
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
        var pen=new Pen(Brushes.DodgerBlue,2/zoom);
        var occupiedPen=new Pen(Brushes.Red,3/zoom);
        foreach(var s in seats.Where(s=>Selected.Contains(s.Id)))
            dc.DrawRoundedRectangle(s.TicketId.HasValue || s.IsReserved || s.IsBlocked?null:new SolidColorBrush(Color.FromArgb(70,35,125,255)),
                s.TicketId.HasValue || s.IsReserved || s.IsBlocked?occupiedPen:pen,new Rect(s.X-1,s.Y-1,18,18),2,2);
        dc.Pop();
        if(marquee && dragging) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30,30,110,220)),
            new Pen(Brushes.DodgerBlue,1),new Rect(start,last));
        if(seats.Count==0) dc.DrawText(Text("В этом зале пока нет мест",16,Brushes.SlateGray),new Point(25,25));
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    { ZoomAt(e.GetPosition(this),e.Delta>0?1.15:1/1.15); e.Handled=true; }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus(); start=last=e.GetPosition(this); dragging=true;
        panning=e.ChangedButton is MouseButton.Right or MouseButton.Middle || Keyboard.IsKeyDown(Key.Space);
        rightGesture=e.ChangedButton==MouseButton.Right;panMoved=false;
        marquee=false;gestureSelection=Selected.ToHashSet();
        var worldStart=World(start);
        removing=SeatSelection.RemoveOnDrag(seats,gestureSelection,worldStart.X,worldStart.Y);
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
            else if(marquee || (p-start).Length>=6){marquee=true;SelectRange(p);}
            last=p; InvalidateVisual(); return;
        }
        var s=Hit(p);
        if(s?.Id!=hoverId)
        {
            hoverId=s?.Id;
            ToolTip=s is null?null:$"{s.Label}\n{(s.Amount.HasValue?s.Amount+" ₽":"Цена не назначена")}\n{s.Status}";
        }
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if(!dragging) return;
        var p=e.GetPosition(this);
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
                if(s is not null) SeatSelection.Toggle(Selected,s.Id);
            }
            SelectionChanged?.Invoke();
        }
        dragging=panning=marquee=rightGesture=panMoved=false; Cursor=Cursors.Arrow; ReleaseMouseCapture(); InvalidateVisual(); e.Handled=true;
        if(contextSeat is not null) SeatContextRequested?.Invoke(contextSeat.Id,p);
    }
    private void SelectRange(Point end)
    {
        var from=World(start);var to=World(end);
        var next=SeatSelection.ApplyRectangle(seats,gestureSelection,from.X,from.Y,to.X,to.Y,removing);
        if(Selected.SetEquals(next))return;
        Selected.Clear();Selected.UnionWith(next);SelectionChanged?.Invoke();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e)
    { dragging=panning=marquee=rightGesture=panMoved=false; Cursor=Cursors.Arrow; InvalidateVisual(); }
}
