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
    private readonly Popup rejectionPopup=new(){Placement=PlacementMode.RelativePoint,StaysOpen=true,AllowsTransparency=true};
    private bool dragging, panning, marquee, removing, rightGesture, panMoved;
    private HashSet<long> gestureSelection=[];
    private long? hoverId;
    public HashSet<long> Selected { get; }=[];
    public bool AllowUnavailableSelection{get;set;}
    public bool AllowOccupiedSelection{get;set;}
    public bool ShowInactivePlaceholders{get;set;}=true;
    private bool CanSelect(SeatInfo seat)=>!seat.IsHidden&&seat.Id>0&&(AllowUnavailableSelection||SeatSelection.CanSelect(seat)||(AllowOccupiedSelection&&(seat.TicketId.HasValue||seat.IsBlocked)));
    private void ExcludeOccupiedFromMixedSelection()
    {if(!AllowUnavailableSelection&&SelectedSeats.Any(SeatSelection.CanSelect))Selected.ExceptWith(seats.Where(s=>!SeatSelection.CanSelect(s)).Select(s=>s.Id));}
    public event Action? SelectionChanged;
    public event Action<IReadOnlyList<SeatInfo>>? SectorTransformCompleted;
    public event Action<IReadOnlyList<SeatInfo>>? RowTransformCompleted;
    private string? transformRow;
    private string? transformSector;
    private bool rotateSector,transformDragging;
    private SeatInfo[] transformStart=[];
    private DrawingGroup? movingSectorDrawing;
    private Matrix sectorMatrix=Matrix.Identity;
    private Point sectorCentre;
    public bool SectorEditingEnabled{get;set;}
    public void EditSector(string sector,bool rotate){if(!SectorEditingEnabled)return;CancelRowMove();transformSector=sector;rotateSector=rotate;Cursor=rotate?Cursors.Cross:Cursors.SizeAll;}
    public void EditRow(string sector,string row)
    {
        if(!SectorEditingEnabled)return;
        CancelRowMove();transformSector=sector;transformRow=row;rotateSector=false;
        transformStart=seats.Where(s=>s.Sector==sector&&s.Row==row&&s.Id>0).ToArray();
        if(transformStart.Length==0){transformRow=null;transformSector=null;return;}
        worldAnchor=World(Mouse.GetPosition(this));transformDragging=true;CacheSectorDrawing();
        Focus();Cursor=Cursors.SizeWE;InvalidateVisual();
    }
    public bool CancelPendingRowMove(){if(transformRow is null)return false;CancelRowMove();return true;}
    private void CancelRowMove()
    {
        if(transformRow is null)return;
        transformDragging=false;transformRow=null;transformSector=null;transformStart=[];movingSectorDrawing=null;sectorMatrix=Matrix.Identity;
        RebuildDrawing();Cursor=Cursors.Arrow;InvalidateVisual();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&transformRow is not null){CancelRowMove();e.Handled=true;return;}
        base.OnKeyDown(e);
    }

    public event EventHandler? EmptySpaceClicked;
    public event Action<long,Point>? SeatContextRequested;
    public event Action<long,Point>? RowContextRequested;
    public event Action<string,Point>? SectorContextRequested;
    private readonly List<(Rect Bounds,long Id)> rowHits=[];
    private readonly List<(Rect Bounds,string Sector)> sectorHits=[];
    public event Action<SeatInfo>? SeatSelectionRejected;
    public IReadOnlyList<SeatInfo> SelectedSeats => seats.Where(s=>Selected.Contains(s.Id)).ToList();

    public HallView()
    {
        ClipToBounds=true; Focusable=true;
        seatTip.PlacementTarget=this;rejectionPopup.PlacementTarget=this;
        scrollTimer=new DispatcherTimer(TimeSpan.FromMilliseconds(30),DispatcherPriority.Input,(_,_)=>AutoScroll(),Dispatcher);
        scrollTimer.Stop();
        Unloaded+=(_,_)=> { EndGesture(); CloseTip(); rejectionPopup.IsOpen=false; };
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
        CancelRowMove();EndGesture(); transformSector=null; CloseTip(); rejectionPopup.IsOpen=false; seats=(ShowInactivePlaceholders?HallEditing023.WithEstradaPlaceholders(source):source).ToList(); this.sectors=sectors.ToList(); Selected.Clear(); hoverId=null;
        RebuildDrawing();
        if(fit) Fit(); else InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    private void RebuildDrawing()
    {
        var visible=seats.Where(s=>!s.IsHidden).ToArray();rowHits.Clear();sectorHits.Clear();
        var group=new DrawingGroup();
        using(var dc=group.Open())
        {
            var sectorSeats=visible.ToLookup(s=>s.Sector);
            var seatBounds=visible.Select(seat=>new Rect(seat.X,seat.Y,16,16)).ToArray();
            var rowLabels=new List<RowCaption>();
            // Keep the logical anchors fixed; resolve only small rendered-ink
            // collisions locally, before headings reserve the final row labels.
            foreach(var row in visible.GroupBy(s=>(s.Sector,s.Row)))
            {
                var label=Text(row.Key.Row,14.3,Brushes.Black);
                var native=new DrawingGroup();using(var context=native.Open())context.DrawText(label,new Point());
                if(native.Bounds.IsEmpty)continue;
                foreach(var anchor in HallEditing025.RowAnchors(row.ToArray()))
                {
                    var ink=new Rect(anchor.X-native.Bounds.Width/2,anchor.Y-native.Bounds.Height/2,native.Bounds.Width,native.Bounds.Height);
                    rowLabels.Add(PlaceRowCaption(new(label,native.Bounds,ink,ink,row.Min(s=>s.Id),anchor.Centre),seatBounds,rowLabels));
                }
            }
            var captionBounds=rowLabels.Select(label=>label.Ink).ToList();
            foreach(var sector in sectors)
            {
                var wrapped=sector.Name.StartsWith("Ложа партера (",StringComparison.Ordinal)||sector.Name.StartsWith("Балкон №",StringComparison.Ordinal);
                var label=Text(wrapped?sector.Name.Replace(" (","\n("):sector.Name,15.6,Brushes.SlateGray);
                label.TextAlignment=TextAlignment.Center;
                var places=sectorSeats[sector.Name];
                var captionDrawing=new DrawingGroup();using(var textContext=captionDrawing.Open())textContext.DrawText(label,new Point());
                var ink=captionDrawing.Bounds;
                var origin=SectorCaptionOrigin(ink,places.ToArray(),visible,captionBounds,new Point(sector.X,sector.Y));
                var x=origin.X;var y=origin.Y;
                dc.DrawText(label,new Point(x,y));ink.Offset(x,y);captionBounds.Add(ink);sectorHits.Add((ink,sector.Name));
            }
            foreach(var s in visible)
            {
                var rect=new Rect(s.X,s.Y,16,16);
                var occupied=s.TicketId.HasValue || s.IsReserved;
                var printed=occupied && s.IsPrinted && !s.IsReserved;
                var dimmed=focusPrice.HasValue && s.Amount!=focusPrice.Value;
                var brush=s.Id<0?Brushes.Silver:dimmed?Brushes.White:s.IsBlocked?Brushes.LightGray:occupied?(printed?Brushes.Red:Brushes.Black):s.Amount.HasValue?SeatBrush(s.Color):Brushes.Gainsboro;
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
            foreach(var label in rowLabels)
            {
                dc.DrawText(label.Label,new Point(label.Ink.X-label.Glyph.X,label.Ink.Y-label.Glyph.Y));
                rowHits.Add((label.Ink,label.Id));
            }
        }
        group.Freeze(); drawing=group;
        bounds=drawing.Bounds;
        if(bounds.IsEmpty) bounds=new Rect(0,0,100,100);
    }

    private sealed record RowCaption(FormattedText Label,Rect Glyph,Rect Anchor,Rect Ink,long Id,bool Fixed);
    private static RowCaption PlaceRowCaption(RowCaption label,IReadOnlyList<Rect> seats,List<RowCaption> prior)
    {
        if(label.Fixed)return label;
        const double limit=12,gap=.25;
        static Rect Moved(Rect ink,Vector offset){ink.Offset(offset);return ink;}
        static bool Clear(Rect ink,IEnumerable<Rect> obstacles)=>!obstacles.Any(ink.IntersectsWith);
        static bool Local(Rect ink,Rect anchor)=>Math.Pow(ink.X-anchor.X,2)+Math.Pow(ink.Y-anchor.Y,2)<=limit*limit;
        var obstacles=seats.Concat(prior.Select(p=>p.Ink)).ToArray();
        if(Clear(label.Ink,obstacles))return label;
        var area=label.Ink;area.Inflate(limit,limit);var nearby=obstacles.Where(area.IntersectsWith).ToArray();
        var xs=nearby.SelectMany(r=>new[]{r.Left-gap-label.Ink.Right,r.Right+gap-label.Ink.Left}).Append(0).Where(x=>Math.Abs(x)<=limit).Distinct();
        var ys=nearby.SelectMany(r=>new[]{r.Top-gap-label.Ink.Bottom,r.Bottom+gap-label.Ink.Top}).Append(0).Where(y=>Math.Abs(y)<=limit).Distinct().ToArray();
        foreach(var offset in xs.SelectMany(x=>ys.Select(y=>new Vector(x,y))).Where(v=>v.LengthSquared<=limit*limit).OrderBy(v=>v.LengthSquared))
        {var ink=Moved(label.Ink,offset);if(Clear(ink,nearby))return label with{Ink=ink};}
        // Adjacent blocks can put two two-digit labels only 4px apart. Share
        // their separation instead of pushing one label beyond its own row.
        for(var i=0;i<prior.Count;i++)
        {
            var previous=prior[i];if(previous.Fixed||!label.Ink.IntersectsWith(previous.Ink))continue;
            var others=seats.Concat(prior.Where((_,index)=>index!=i).Select(p=>p.Ink)).ToArray();
            var offsets=new[]{new Vector((previous.Ink.Left-gap-label.Ink.Right)/2,0),new Vector((previous.Ink.Right+gap-label.Ink.Left)/2,0),new Vector(0,(previous.Ink.Top-gap-label.Ink.Bottom)/2),new Vector(0,(previous.Ink.Bottom+gap-label.Ink.Top)/2)};
            foreach(var offset in offsets.OrderBy(v=>v.LengthSquared))
            {
                var ink=Moved(label.Ink,offset);var other=Moved(previous.Ink,-offset);
                if(!Local(ink,label.Anchor)||!Local(other,previous.Anchor)||ink.IntersectsWith(other)||!Clear(ink,others)||!Clear(other,others))continue;
                prior[i]=previous with{Ink=other};return label with{Ink=ink};
            }
        }
        // A custom layout may have no free space within the local bound. Keep
        // its logical anchor rather than drifting to an unrelated row.
        return label;
    }

    private static Point SectorCaptionOrigin(Rect glyph,IReadOnlyList<SeatInfo> own,IReadOnlyList<SeatInfo> all,IReadOnlyList<Rect> captions,Point fallback)
    {
        if(own.Count==0)return fallback;
        var left=own.Min(s=>s.X);var right=own.Max(s=>s.X)+16;
        var centre=(left+right)/2-(glyph.Left+glyph.Width/2);var y=own.Min(s=>s.Y)-5-glyph.Bottom;
        var obstacles=all.Select(s=>new Rect(s.X,s.Y,16,16)).Concat(captions).ToArray();
        bool Clear(double x){var ink=glyph;ink.Offset(x,y);return !obstacles.Any(ink.IntersectsWith);}
        // Keep the requested vertical gap even in dense existing halls. If another
        // sector occupies the caption band, resolve the collision horizontally.
        if(Clear(centre))return new Point(centre,y);
        var candidates=obstacles.SelectMany(r=>new[]{r.Left-1-glyph.Right,r.Right+1-glyph.Left})
            .OrderBy(x=>Math.Abs(x-centre));
        return new Point(candidates.First(Clear),y);
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
    { var allowed=seats.Where(CanSelect).Select(s=>s.Id).ToHashSet();Selected.Clear();foreach(var id in ids)if(allowed.Contains(id))Selected.Add(id);ExcludeOccupiedFromMixedSelection();SelectionChanged?.Invoke();InvalidateVisual(); }
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
        return SeatSelection.Hit(seats.Where(s=>!s.IsHidden),w.X,w.Y);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));
        dc.PushTransform(new MatrixTransform(zoom,0,0,zoom,offset.X,offset.Y));
        dc.DrawDrawing(drawing);
        if(transformDragging&&movingSectorDrawing is not null)
        {
            if(!rotateSector)dc.PushTransform(new MatrixTransform(sectorMatrix));
            dc.DrawDrawing(movingSectorDrawing);
            if(!rotateSector)dc.Pop();
        }
        var outerPen=new Pen(Brushes.Black,2/zoom);
        var innerPen=new Pen(Brushes.White,2/zoom);
        foreach(var s in seats.Where(s=>!transformDragging&&Selected.Contains(s.Id)))
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
        if(transformRow is not null&&e.ChangedButton==MouseButton.Left){EndGesture();e.Handled=true;return;}
        if(dragging)return;
        if(e.ChangedButton==MouseButton.Left && e.ClickCount==2 && Hit(e.GetPosition(this)) is null)
        { Clear(); Fit(); e.Handled=true; return; }
        CloseTip();rejectionPopup.IsOpen=false;
        Focus(); start=last=e.GetPosition(this); dragging=true;
        gestureButton=e.ChangedButton;
        panning=e.ChangedButton is MouseButton.Right or MouseButton.Middle || Keyboard.IsKeyDown(Key.Space);
        rightGesture=e.ChangedButton==MouseButton.Right;panMoved=false;
        marquee=false;gestureSelection=Selected.ToHashSet();
        worldAnchor=World(start);
        if(e.ChangedButton==MouseButton.Left&&transformSector is not null)
        {
            transformStart=seats.Where(s=>s.Sector==transformSector&&s.Id>0).ToArray();
            transformDragging=transformStart.Length>0;panning=false;
            if(transformDragging)CacheSectorDrawing();
        }
        removing=SeatSelection.RemoveOnDrag(seats,gestureSelection,worldAnchor.X,worldAnchor.Y);
        CaptureMouse(); if(panning) Cursor=Cursors.Hand; e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p=e.GetPosition(this);
        if(transformRow is not null&&transformDragging)
        {
            sectorMatrix=Matrix.Identity;sectorMatrix.Translate(World(p).X-worldAnchor.X,0);
            InvalidateVisual();return;
        }
        if(dragging)
        {
            if(transformDragging)
            {
                var world=World(p);var cx=sectorCentre.X;var cy=sectorCentre.Y;
                sectorMatrix=Matrix.Identity;
                if(rotateSector)
                {
                    var angle=(Math.Atan2(world.Y-cy,world.X-cx)-Math.Atan2(worldAnchor.Y-cy,worldAnchor.X-cx))*180/Math.PI;
                    sectorMatrix.RotateAt(angle,cx,cy);
                }
                else sectorMatrix.Translate(world.X-worldAnchor.X,world.Y-worldAnchor.Y);
                if(rotateSector)
                {
                    var original=seats;var originalSectors=sectors;var still=drawing;var originalBounds=bounds;
                    try
                    {
                        seats=transformStart.Select(s=>{var point=sectorMatrix.Transform(new Point(s.X,s.Y));return s with{X=point.X,Y=point.Y,RowAisleAnchors=TransformAnchors(s.RowAisleAnchors)};}).ToList();
                        sectors=originalSectors.Where(s=>s.Name==transformSector).ToArray();
                        RebuildDrawing();movingSectorDrawing=drawing;
                    }
                    finally{seats=original;sectors=originalSectors;drawing=still;bounds=originalBounds;}
                }
                last=p;InvalidateVisual();return;
            }
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
        SeatInfo? rejected=null;
        var contextSector=rightGesture&&!panMoved&&(p-start).Length<3?sectorHits.FirstOrDefault(h=>h.Bounds.Contains(World(p))).Sector:null;
        var contextRow=rightGesture&&!panMoved?rowHits.FirstOrDefault(h=>h.Bounds.Contains(World(p))).Id:0;
        var contextSeat=rightGesture && e.ChangedButton==MouseButton.Right && !panMoved && (p-start).Length<3?Hit(p):null;
        if(!panning&&!transformDragging)
        {
            if(marquee || (p-start).Length>=6)
            {
                SelectRange(p);
                rejected=RejectedSeats(p).FirstOrDefault();
            }
            else if((p-start).Length<6)
            {
                var s=Hit(p);
                if(s is not null)
                {
                    var click=ClickSeat(s);rejected=click.Rejected;contextSeat=click.Context;
                }
                else if(e.ChangedButton==MouseButton.Left && Hit(start) is null){Selected.Clear();emptyClick=true;}
            }
            SelectionChanged?.Invoke();
        }
        EndGesture(); e.Handled=true;
        if(rejected is not null){SeatSelectionRejected?.Invoke(rejected);ShowSeatRejection(rejected,p);}
        if(contextSector is not null&&contextSeat is null)SectorContextRequested?.Invoke(contextSector,p);
        else if(contextRow>0&&contextSeat is null)RowContextRequested?.Invoke(contextRow,p);
        else if(contextSeat is {Id:>0}) SeatContextRequested?.Invoke(contextSeat.Id,p);
        if(emptyClick)EmptySpaceClicked?.Invoke(this,EventArgs.Empty);
    }
    private (SeatInfo? Context,SeatInfo? Rejected) ClickSeat(SeatInfo seat)
    {
        // Left click always toggles a place; actions belong exclusively to right click.
        if(CanSelect(seat))SeatSelection.Toggle(Selected,seat.Id);else Selected.Remove(seat.Id);
        var mixed=SelectedSeats.Any(SeatSelection.CanSelect)&&SelectedSeats.Any(s=>!SeatSelection.CanSelect(s));
        var rejected=seat.Id<0?null:mixed?SelectedSeats.First(s=>!SeatSelection.CanSelect(s)):!CanSelect(seat)?seat:null;
        ExcludeOccupiedFromMixedSelection();
        return (null,rejected);
    }

    private void SelectRange(Point end)
    {
        var to=World(end);
        var candidates=seats.Where(CanSelect).ToArray();
        var next=SeatSelection.ApplyRectangle(candidates,gestureSelection,worldAnchor.X,worldAnchor.Y,to.X,to.Y,removing,true);
        if(!AllowUnavailableSelection&&candidates.Any(s=>next.Contains(s.Id)&&SeatSelection.CanSelect(s)))next.ExceptWith(candidates.Where(s=>!SeatSelection.CanSelect(s)).Select(s=>s.Id));
        if(Selected.SetEquals(next))return;
        Selected.Clear();Selected.UnionWith(next);SelectionChanged?.Invoke();
    }
    private IReadOnlyList<SeatInfo> RejectedSeats(Point end)
    {
        if(AllowUnavailableSelection)return [];
        var to=World(end);
        var left=Math.Min(worldAnchor.X,to.X);var right=Math.Max(worldAnchor.X,to.X);
        var top=Math.Min(worldAnchor.Y,to.Y);var bottom=Math.Max(worldAnchor.Y,to.Y);
        if(left>=right||top>=bottom)return [];
        var touched=seats.Where(s=>s.X<right&&s.X+SeatSelection.SeatSize>left&&s.Y<bottom&&s.Y+SeatSelection.SeatSize>top).ToArray();
        if(AllowOccupiedSelection&&!touched.Any(SeatSelection.CanSelect)&&!SelectedSeats.Any(SeatSelection.CanSelect))return [];
        return touched.Where(s=>s.Id>0&&!s.IsHidden&&!SeatSelection.CanSelect(s)).ToArray();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    { EndGesture(); base.OnLostMouseCapture(e); }

    protected override void OnMouseLeave(MouseEventArgs e)
    { CloseTip(); base.OnMouseLeave(e); }

    private void EndGesture()
    {
        scrollTimer.Stop();
        SeatInfo[]? transformed=null;var rowMove=transformRow is not null;
        if(transformDragging)
        {
            transformed=transformStart.Select(s=>{var p=sectorMatrix.Transform(new Point(s.X,s.Y));return s with{X=p.X,Y=p.Y,RowAisleAnchors=TransformAnchors(s.RowAisleAnchors)};}).ToArray();
            var updates=transformed.ToDictionary(s=>s.Id);
            for(var i=0;i<seats.Count;i++)if(updates.TryGetValue(seats[i].Id,out var seat))seats[i]=seat;
        }
        transformDragging=false;movingSectorDrawing=null;sectorMatrix=Matrix.Identity;
        if(transformed is not null){transformSector=null;transformRow=null;transformStart=[];RebuildDrawing();}
        dragging=panning=marquee=rightGesture=panMoved=false;
        Cursor=Cursors.Arrow;
        if(IsMouseCaptured)ReleaseMouseCapture();
        InvalidateVisual();
        if(transformed is not null){if(rowMove)RowTransformCompleted?.Invoke(transformed);else SectorTransformCompleted?.Invoke(transformed);}
    }

    private IReadOnlyList<HallEditing025.RowAnchor>? TransformAnchors(IReadOnlyList<HallEditing025.RowAnchor>? anchors)
        =>anchors?.Select(a=>{var point=sectorMatrix.Transform(new Point(a.X,a.Y));return a with{X=point.X,Y=point.Y};}).ToArray();
    private void CacheSectorDrawing()
    {
        var originalSeats=seats;var originalSectors=sectors;var originalBounds=bounds;
        var originalRowHits=rowHits.ToArray();var originalSectorHits=sectorHits.ToArray();
        try
        {
            var moved=transformStart.Select(s=>s.Id).ToHashSet();
            seats=originalSeats.Where(s=>moved.Contains(s.Id)).ToList();sectors=transformRow is null?originalSectors.Where(s=>s.Name==transformSector).ToArray():[];RebuildDrawing();movingSectorDrawing=drawing;
            seats=originalSeats.Where(s=>!moved.Contains(s.Id)).ToList();sectors=transformRow is null?originalSectors.Where(s=>s.Name!=transformSector).ToArray():originalSectors;RebuildDrawing();
        }
        finally{seats=originalSeats;sectors=originalSectors;bounds=originalBounds;rowHits.Clear();rowHits.AddRange(originalRowHits);sectorHits.Clear();sectorHits.AddRange(originalSectorHits);}
        sectorCentre=new Point(transformStart.Average(s=>s.X),transformStart.Average(s=>s.Y));sectorMatrix=Matrix.Identity;
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

    private void ShowSeatRejection(SeatInfo seat,Point position)
    {
        CloseTip();
        var body=new StackPanel();
        body.Children.Add(new TextBlock{
            Text=$"{seat.Label}: {seat.Status}.\nВ смешанном выборе оставлены только свободные места. Повторная печать занятых — из исходной накладной.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black,FontSize=14,MaxWidth=270
        });
        var ok=new Button{Content="ОК",Width=70,Height=30,FontSize=14,Margin=new Thickness(0,6,0,0),ContentTemplate=null,HorizontalAlignment=HorizontalAlignment.Right};
        ok.Click+=(_,_)=>rejectionPopup.IsOpen=false;body.Children.Add(ok);
        var border=new Border{Child=body,Background=Brushes.White,BorderBrush=Brushes.Red,BorderThickness=new Thickness(2),Padding=new Thickness(8),MaxWidth=290};
        border.Measure(new Size(290,double.PositiveInfinity));
        rejectionPopup.Child=border;
        // This separate visual overlays the hall without changing its measured size,
        // fit, or current selection. StaysOpen avoids taking mouse capture from drags.
        rejectionPopup.HorizontalOffset=position.X-border.DesiredSize.Width-12;
        rejectionPopup.VerticalOffset=position.Y;
        rejectionPopup.IsOpen=true;
    }

    private void CloseTip() { seatTip.IsOpen=false; hoverId=null; }

    private void UpdateTip(Point p)
    {
        if(rejectionPopup.IsOpen){CloseTip();return;}
        var s=Hit(p);
        if(s is null || ActualWidth<40 || ActualHeight<40) { CloseTip(); return; }
        if(s.Id!=hoverId)
        {
            hoverId=s.Id;
            seatTip.Content=s.Id<0?$"{s.Label}\nНеактивное место":$"{s.Label}\n{(s.Amount.HasValue?s.Amount+" ₽":"Цена не назначена")}\n{s.Status}";
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
