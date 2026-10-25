using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallCaption031UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var caption=new SectorCaption029("Балкон\nсправа",-10,-36,17);
        var id=store.CreateHall("031 angular caption "+Guid.NewGuid().ToString("N"),"",[
            new(){Sector="Балкон справа",Row="1",Number="1",X=20,Y=40,SectorCaption=caption},
            new(){Sector="Балкон справа",Row="1",Number="2",X=60,Y=40,SectorCaption=caption}]);
        var before=store.VenueSeats(id);var plan=store.SavePlan(id,"031 caption",before.Select(s=>new PriceValue(s.Id,500,"#ABCDEF")).ToArray());
        var evt=store.CreateEvent(store.SavePerformance("031 caption "+Guid.NewGuid().ToString("N")),id,plan,DateTime.Today.AddDays(33));
        store.Issue(evt,[before[0].Id],"031 caption","CU031",1);
        var editor=new HallEditorWindow(store,store.Venues().Single(v=>v.Id==id),true);Exception? failure=null;SectorCaption029? expected=null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try
            {
                var hall=(HallView)typeof(HallEditorWindow).GetField("preview",Private)!.GetValue(editor)!;
                var draft=(IEnumerable<DraftSeat>)typeof(HallEditorWindow).GetField("seats",Private)!.GetValue(editor)!;
                var geometry=draft.Select(s=>(s.X,s.Y,s.Row,s.Number)).ToArray();var commits=0;hall.SectorCaptionChanged029+=(_,_)=>commits++;
                BeginRotation(hall);Drag(hall,false);
                Check(commits==0&&draft.All(s=>s.SectorCaption==caption),"angular drag previews without committing metadata");
                Check(Math.Abs(hall.SectorCaption029ForEdit("Балкон справа").Angle-caption.Angle)>30,"caption follows angular mouse movement");
                var preview=hall.SectorCaption029ForEdit("Балкон справа");
                Check(preview.Text==caption.Text&&preview.OffsetX==caption.OffsetX&&preview.OffsetY==caption.OffsetY,"two-line caption rotates about its own fixed centre");
                mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();
                expected=draft.First().SectorCaption;Check(commits==1&&expected==preview,"mouse release creates exactly one caption undo step");
                Check(draft.Select(s=>(s.X,s.Y,s.Row,s.Number)).SequenceEqual(geometry),"rotation leaves seat geometry and identifiers unchanged");
                typeof(HallEditorWindow).GetMethod("Undo",Private)!.Invoke(editor,null);Pump();
                Check(draft.All(s=>s.SectorCaption==caption),"undo restores previous caption angle");
                BeginRotation(hall);Drag(hall,false);
                keybd_event(0x1B,0,0,UIntPtr.Zero);PumpInput();keybd_event(0x1B,0,2,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();
                Check(commits==1&&draft.All(s=>s.SectorCaption==caption)&&hall.SectorCaption029ForEdit("Балкон справа")==caption,"Escape cancels angular preview without a commit");
                BeginRotation(hall);Drag(hall,true);expected=draft.First().SectorCaption;
                Logical(editor).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить изменения")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch(Exception ex){failure=ex;mouse_event(4,0,0,0,UIntPtr.Zero);editor.Close();}
        }));
        try
        {
            var result=editor.ShowDialog();if(failure is not null)throw failure;
            Check(result==true&&editor.CreatedId==id&&new Store(store.Path).Sectors(id).Single().Caption==expected,"caption rotation saves and reloads within a used hall");
            Check(store.VenueSeats(id).SequenceEqual(before)&&!store.Seats(evt).Single(s=>s.Id==before[0].Id).Available,"caption save preserves seats and issued ticket");
        }
        finally{editor.Close();}
    }
    private static void BeginRotation(HallView hall)
    {
        var hit=((List<(Rect Bounds,string Sector)>)typeof(HallView).GetField("sectorHits",Private)!.GetValue(hall)!).Single();
        Move(hall,new Point(hit.Bounds.X+hit.Bounds.Width/2,hit.Bounds.Y+hit.Bounds.Height/2));
        mouse_event(8,0,0,0,UIntPtr.Zero);PumpInput();mouse_event(16,0,0,0,UIntPtr.Zero);PumpInput();
        Check(hall.ContextMenu is {IsOpen:true},"native right click opens caption actions");
        var menu=hall.ContextMenu!;
        Check(menu.Items.OfType<MenuItem>().Select(i=>i.Header?.ToString()).SequenceEqual(new[]{"Изменить название","Повернуть название"}),"caption menu has one rename and one rotation action");
        menu.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Повернуть название")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;PumpInput();
        Check(hall.Cursor==Cursors.Cross,"caption rotation uses sector angular drag cursor");
    }
    private static void Drag(HallView hall,bool release)
    {
        var centres=(Dictionary<string,Point>)typeof(HallView).GetField("sectorCaptionCentres031",Private)!.GetValue(hall)!;var centre=centres.Single().Value;
        Move(hall,new Point(centre.X+35,centre.Y));mouse_event(2,0,0,0,UIntPtr.Zero);PumpInput();
        Move(hall,new Point(centre.X,centre.Y+35));
        if(release){mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();}
    }
    private static void Move(HallView hall,Point world)
    {
        var point=(Point)typeof(HallView).GetMethod("Screen",Private)!.Invoke(hall,[world])!;var screen=hall.PointToScreen(point);
        SetCursorPos((int)screen.X,(int)screen.Y);PumpInput();
    }
    private static void PumpInput(){var until=DateTime.UtcNow.AddMilliseconds(65);while(DateTime.UtcNow<until){Pump();Thread.Sleep(5);}}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 031 "+message);}
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    [DllImport("user32.dll")]private static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
}
