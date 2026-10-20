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
    private bool dragging, panning, marquee;
    private HashSet<long> gestureSelection=[];
    private long? hoverId;
    public HashSet<long> Selected { get; }=[];
    public event Action? SelectionChanged;
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
        => new(text,CultureInfo.GetCultureInfo("ru-RU"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,color,1);

    public void Load(IReadOnlyList<SeatInfo> source,IReadOnlyList<SectorInfo> sectors,bool fit)
    {
        seats=source.ToList(); Selected.Clear(); hoverId=null;
        var group=new DrawingGroup();
        using(var dc=group.Open())
        {
            foreach(var sector in sectors) dc.DrawText(Text(sector.Name,12,Brushes.SlateGray),new Point(sector.X,sector.Y));
            foreach(var s in seats)
            {
                var rect=new Rect(s.X,s.Y,16,16);
                var brush=s.TicketId.HasValue?Brushes.SlateGray:s.Amount.HasValue?SeatBrush(s.Color):Brushes.Gainsboro;
                dc.DrawRoundedRectangle(brush,null,rect,2,2);
                var textColor=Brushes.Black;
                if(brush is SolidColorBrush solid && (solid.Color.R*.299+solid.Color.G*.587+solid.Color.B*.114)<145) textColor=Brushes.White;
                var text=Text(s.Number.Length>3?s.Number[..3]:s.Number,8,textColor);
                dc.DrawText(text,new Point(s.X+(16-text.Width)/2,s.Y+1.5));
                if(s.TicketId.HasValue)
                {
                    var pen=new Pen(Brushes.White,.8);
                    dc.DrawLine(pen,new Point(s.X+4,s.Y+11),new Point(s.X+12,s.Y+4));
                }
            }
            foreach(var row in seats.GroupBy(s=>(s.Sector,s.Y,s.Row)))
            {
                var first=row.MinBy(s=>s.X)!;
                var label=Text(first.Row,9,Brushes.SlateGray);
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
        return seats.LastOrDefault(s=>w.X>=s.X && w.X<=s.X+16 && w.Y>=s.Y && w.Y<=s.Y+16);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));
        dc.PushTransform(new MatrixTransform(zoom,0,0,zoom,offset.X,offset.Y));
        dc.DrawDrawing(drawing);
        var pen=new Pen(Brushes.DodgerBlue,2/zoom);
        foreach(var s in seats.Where(s=>Selected.Contains(s.Id)))
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70,35,125,255)),pen,new Rect(s.X-1,s.Y-1,18,18),2,2);
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
        marquee=false;gestureSelection=Selected.ToHashSet();
        CaptureMouse(); if(panning) Cursor=Cursors.Hand; e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p=e.GetPosition(this);
        if(dragging)
        {
            if(panning) { offset+=p-last; }
            else if((p-start).Length>=6){marquee=true;SelectRange(p);}
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
        if(!panning)
        {
            if(marquee)
            {
                SelectRange(p);
            }
            else if((p-start).Length<6)
            {
                var s=Hit(p);
                if(s is not null && !Selected.Remove(s.Id)) Selected.Add(s.Id);
            }
            SelectionChanged?.Invoke();
        }
        dragging=panning=marquee=false; Cursor=Cursors.Arrow; ReleaseMouseCapture(); InvalidateVisual(); e.Handled=true;
    }
    private void SelectRange(Point end)
    {
        var rectangle=new Rect(World(start),World(end));
        var next=gestureSelection.Concat(seats.Where(s=>rectangle.Contains(new Point(s.X+8,s.Y+8))).Select(s=>s.Id)).ToHashSet();
        if(Selected.SetEquals(next))return;
        Selected.Clear();Selected.UnionWith(next);SelectionChanged?.Invoke();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e)
    { dragging=panning=marquee=false; Cursor=Cursors.Arrow; InvalidateVisual(); }
}
