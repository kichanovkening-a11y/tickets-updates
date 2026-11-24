using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed partial class HallEditorWindow
{
    private List<(int Index,DraftSeat Seat)>? geometryOriginal053;
    private bool geometryRefreshing053;
    private Slider? bend053;
    private TextBox? bendNumber053;
    private bool bendUpdating053;
    private List<DraftSeat> GeometryRows053()
    {
        var keys=Selection().Select(s=>(s.Sector,s.Row)).ToHashSet();
        return seats.Where(s=>keys.Contains((s.Sector,s.Row))).ToList();
    }
    private void SelectGeometryRows(string sector,string row)
    {
        var keys=Selection().Select(s=>(s.Sector,s.Row)).ToHashSet();keys.Add((sector,row));
        preview.SetSelection(seats.Select((s,i)=>(s,i)).Where(p=>keys.Contains((p.s.Sector,p.s.Row))).Select(p=>(long)p.i+1));
    }
    private void SelectGeometryZone(string sector)=>preview.SetSelection(seats.Select((s,i)=>(s,i)).Where(p=>p.s.Sector==sector).Select(p=>(long)p.i+1));
    private void BeginGeometry053()
    {
        var selected=GeometryRows053().ToHashSet();if(selected.Count<1)return;
        Remember();if(geometryOriginal053 is not null)return;
        geometryOriginal053=seats.Select((s,i)=>(Index:i,Seat:s)).Where(p=>selected.Contains(p.Seat)).Select(p=>(p.Index,Clone(p.Seat))).ToList();
    }
    private void ChangeGeometry053(string operation,double value,double second=0)
    {
        if(geometryOriginal053 is null)return;
        var copies=geometryOriginal053.Select(p=>Clone(p.Seat)).ToArray();
        if(operation=="bend")
        {
            try{HallCurving053.Bend(copies,value);}
            catch(TicketException ex){summary.Text=ex.Message;return;}
        }
        else if(operation=="rotate")HallEditing023.Rotate(copies,value);
        else HallEditing023.Move(copies,value,second);
        for(var i=0;i<copies.Length;i++)
        {
            var target=seats[geometryOriginal053[i].Index];target.X=copies[i].X;target.Y=copies[i].Y;target.RowAisleAnchors=copies[i].RowAisleAnchors;
        }
        geometryRefreshing053=true;
        try{Refresh(false);}finally{geometryRefreshing053=false;}
    }
    private void InstallGeometryControls(Panel toolbar)
    {
        preview.GeometryEditing053=true;
        var help=new TextBlock{Text="Ряд — по номеру; зона — по названию; Ctrl — несколько рядов.",Margin=new Thickness(6),TextWrapping=TextWrapping.Wrap};toolbar.Children.Add(help);
        toolbar.Children.Add(new TextBlock{Text="Изгиб",Margin=new Thickness(6)});
        bend053=new Slider{Minimum=-120,Maximum=120,Width=140,TickFrequency=1,IsSnapToTickEnabled=true,ToolTip="Изгиб выбранных рядов / зоны"};toolbar.Children.Add(bend053);
        bendNumber053=new TextBox{Width=52,Text="0",Margin=new Thickness(4)};toolbar.Children.Add(bendNumber053);
        bend053.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((_,_)=>BeginGeometry053()));

        bend053.ValueChanged+=(_,_)=>{
            if(bendUpdating053)return;
            Try(()=>{
                var single=geometryOriginal053 is null;if(single)BeginGeometry053();
                ChangeGeometry053("bend",bend053.Value);bendNumber053.Text=bend053.Value.ToString("0",CultureInfo.InvariantCulture);

            });
        };
        bendNumber053.KeyDown+=(_,e)=>{
            if(e.Key!=Key.Enter)return;
            Try(()=>{if(!double.TryParse(bendNumber053.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||Math.Abs(value)>120)throw new TicketException("Изгиб: от −120 до 120 градусов.");BeginGeometry053();ChangeGeometry053("bend",value);});e.Handled=true;
        };
        Button(toolbar,"Выпрямить",()=>Try(()=>{var rows=GeometryRows053();if(rows.Count==0)return;Remember();HallCurving053.Straighten(rows);Refresh(false);}));
        Button(toolbar,"Повернуть",()=>Try(()=>{var rows=GeometryRows053();if(rows.Count==0)return;var dialog=new GeometryAngleWindow053(Store);if(dialog.ShowDialog()!=true)return;Remember();HallEditing023.Rotate(rows,dialog.Angle);Refresh(false);}));
        preview.SelectionChanged+=()=>{
            if(geometryRefreshing053)return;
            geometryOriginal053=null;
            bendUpdating053=true;try{bend053.Value=0;bendNumber053.Text="0";}finally{bendUpdating053=false;}
        };
        preview.GeometryStart053+=()=>{geometryOriginal053=null;BeginGeometry053();};
        preview.GeometryChange053+=(mode,a,b)=>Try(()=>{ChangeGeometry053(mode,a,b);if(mode=="bend"){bendUpdating053=true;try{bend053.Value=a;bendNumber053.Text=a.ToString("0",CultureInfo.InvariantCulture);}finally{bendUpdating053=false;}}});
        preview.GeometryEnd053+=()=>{
            geometryOriginal053=null;bendUpdating053=true;
            try{bend053.Value=0;bendNumber053.Text="0";}finally{bendUpdating053=false;}
        };
    }
}
internal sealed class GeometryAngleWindow053:EditorWindow
{
    internal double Angle{get;private set;}
    internal GeometryAngleWindow053(Store store):base(store,"Поворот выбранных рядов",400,220)
    {
        var form=new StackPanel{Margin=new Thickness(16)};Content=form;var input=Field(form,"Угол, градусов","0");
        Button(form,"Повернуть",()=>Try(()=>{if(!double.TryParse(input.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var a)||!double.IsFinite(a)||Math.Abs(a)>360)throw new TicketException("Угол: от −360 до 360.");Angle=a;Finish(true);}));
    }
}

public sealed partial class HallView
{
    internal bool GeometryEditing053{get;set;}
    internal event Action? GeometryStart053,GeometryEnd053;
    internal event Action<string,double,double>? GeometryChange053;
    private string? geometryDrag053;
    private Point geometryStart053,geometryCentre053;
    private Vector geometryAxis053;
    private double geometrySpan053;
    private (Point Left,Point Middle,Point Right)? GeometryGuide053()
    {
        if(!GeometryEditing053||Selected.Count==0)return null;
        var keys=seats.Where(s=>Selected.Contains(s.Id)).Select(s=>(s.Sector,s.Row)).ToHashSet();
        var selected=seats.Where(s=>keys.Contains((s.Sector,s.Row))).Select(s=>new DraftSeat{Sector=s.Sector,Row=s.Row,X=s.X,Y=s.Y}).ToArray();if(selected.Length<2)return null;
        var (cx,cy,dx,dy)=HallCurving053.Frame(selected);
        var half=SeatSelection.SeatSize/2;
        var values=selected.Select(s=>(s.X-cx)*dx+(s.Y-cy)*dy).ToArray();var lo=values.Min()-24;var hi=values.Max()+24;
        var lift=selected.Min(s=>-(s.X-cx)*dy+(s.Y-cy)*dx)-30;
        Point At(double u)=>Screen(new Point(cx+u*dx-lift*dy+half,cy+u*dy+lift*dx+half));
        return(At(lo),At((lo+hi)/2),At(hi));
    }
    private void DrawGeometry053(DrawingContext dc)
    {
        if(GeometryGuide053() is not {} guide)return;
        var pen=new Pen(Brushes.DodgerBlue,2);dc.DrawLine(pen,guide.Left,guide.Right);
        dc.DrawText(Text("Сдвиг",11,Brushes.DodgerBlue),guide.Left+new Vector(-15,-22));
        dc.DrawText(Text("Изгиб",11,Brushes.DodgerBlue),guide.Middle+new Vector(-15,-22));
        dc.DrawText(Text("Поворот",11,Brushes.DodgerBlue),guide.Right+new Vector(-15,-22));
        foreach(var p in new[]{guide.Left,guide.Middle,guide.Right})dc.DrawEllipse(Brushes.White,pen,p,6,6);
    }
    private bool GeometryDown053(MouseButtonEventArgs e)
    {
        if(!GeometryEditing053||e.ChangedButton!=MouseButton.Left||Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))return false;
        var p=e.GetPosition(this);
        if(GeometryGuide053() is {} guide)
        {
            geometryDrag053=(p-guide.Middle).Length<11?"bend":(p-guide.Left).Length<11?"move":(p-guide.Right).Length<11?"rotate":null;
            if(geometryDrag053 is not null)
            {
                geometryStart053=World(p);geometryCentre053=World(guide.Middle);geometryAxis053=World(guide.Right)-World(guide.Left);geometrySpan053=geometryAxis053.Length;geometryAxis053.Normalize();
                GeometryStart053?.Invoke();CaptureMouse();e.Handled=true;return true;
            }
        }
        var world=World(p);var row=rowHits.FirstOrDefault(h=>h.Bounds.Contains(world)).Id;
        var sector=sectorHits.FirstOrDefault(h=>h.Bounds.Contains(world)).Sector;
        if(row==0&&sector is null)return false;
        var target=seats.FirstOrDefault(s=>s.Id==row);
        var ids=seats.Where(s=>sector is not null?s.Sector==sector:target is not null&&s.Sector==target.Sector&&s.Row==target.Row).Select(s=>s.Id);
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))ids=Selected.Concat(ids).Distinct().ToArray();
        SetSelection(ids);e.Handled=true;return true;
    }
    private bool GeometryMove053(MouseEventArgs e)
    {
        if(geometryDrag053 is null)return false;
        var p=World(e.GetPosition(this));var delta=p-geometryStart053;
        if(geometryDrag053=="move")GeometryChange053?.Invoke("move",delta.X,delta.Y);
        else if(geometryDrag053=="rotate")GeometryChange053?.Invoke("rotate",DragAngle031(geometryStart053,p,geometryCentre053),0);
        else {var normal=new Vector(-geometryAxis053.Y,geometryAxis053.X);var sag=Vector.Multiply(delta,normal);var angle=Math.Clamp(8*Math.Atan2(sag,Math.Max(1,geometrySpan053))*180/Math.PI,-120,120);GeometryChange053?.Invoke("bend",angle,0);}
        e.Handled=true;return true;
    }
    private bool GeometryUp053(MouseButtonEventArgs e)
    {
        if(geometryDrag053 is null||e.ChangedButton!=MouseButton.Left)return false;
        geometryDrag053=null;ReleaseMouseCapture();GeometryEnd053?.Invoke();e.Handled=true;return true;
    }
    internal void EditRowRotation053(string sector,string row)
    {
        transformSector=sector;transformRow=row;rotateSector=true;Cursor=Cursors.Cross;
    }
}
