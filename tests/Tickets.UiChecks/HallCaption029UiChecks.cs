using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
using Microsoft.Data.Sqlite;

internal static class HallCaption029UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var name="Балкон длинное название";var suffix=Guid.NewGuid().ToString("N");
        var id=store.CreateHall("029 caption UI "+suffix,"",[new(){Sector=name,Row="7",Number="1",X=20,Y=40},new(){Sector=name,Row="7",Number="2",X=60,Y=40},new(){Sector=name,Row="8",Number="1",X=-80,Y=-20,Hidden=true}]);
        using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=store.Path,Pooling=false}.ToString())){c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE sector SET x=0,y=-200 WHERE venue_id=$v";q.Parameters.AddWithValue("$v",id);q.ExecuteNonQuery();}
        var before=store.VenueSeats(id);var plan=store.SavePlan(id,"029 caption UI",before.Select(s=>new PriceValue(s.Id,500,"#ABCDEF")).ToArray());
        var evt=store.CreateEvent(store.SavePerformance("029 caption UI "+suffix),id,plan,DateTime.Today.AddDays(30));store.Issue(evt,[before[0].Id],"029 caption UI","CU029",1);
        var editor=new HallEditorWindow(store,store.Venues().Single(v=>v.Id==id),true);Exception? failure=null;SectorCaption029? expected=null;Rect expectedWorldBounds=Rect.Empty;
        var body=(DependencyObject)editor.Content;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try
            {
                var hall=(HallView)typeof(HallEditorWindow).GetField("preview",Private)!.GetValue(editor)!;
                var draft=(IEnumerable<DraftSeat>)typeof(HallEditorWindow).GetField("seats",Private)!.GetValue(editor)!;
                var coordinates=draft.Select(s=>(s.X,s.Y,s.Row,s.Number)).ToArray();
                DragCaption(hall,name);Check(draft.All(s=>s.SectorCaption is not null),"native left drag updates only caption metadata");
                Check(draft.Select(s=>(s.X,s.Y,s.Row,s.Number)).SequenceEqual(coordinates),"native caption drag preserves all seat coordinates and numbers");
                typeof(HallEditorWindow).GetMethod("Undo",Private)!.Invoke(editor,null);Pump();Check(draft.All(s=>s.SectorCaption is null),"caption move supports undo");
                DragCaption(hall,name);
                Exception? dialogFailure=null;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    var dialog=Application.Current.Windows.OfType<HallSectorNameWindow027>().Single();
                    try
                    {
                        var box=Logical(dialog).OfType<TextBox>().Single();Check(box.AcceptsReturn,"sector name input supports two lines");box.Text="Балкон\nдлинное название";
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                            var angle=Application.Current.Windows.OfType<SectorCaptionAngleWindow029>().Single();
                            try{Logical(angle).OfType<TextBox>().Single().Text="37";Logical(angle).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                            catch(Exception ex){dialogFailure=ex;angle.Close();}
                        }));
                        Logical(dialog).OfType<Button>().Single(b=>Equals(b.Content,"Повернуть надпись")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Logical(dialog).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch(Exception ex){dialogFailure=ex;dialog.Close();}
                }));
                ((Action<string,Point>)typeof(HallView).GetField("SectorContextRequested",Private)!.GetValue(hall)!)(name,new Point());Pump();if(dialogFailure is not null)throw dialogFailure;
                expected=draft.First().SectorCaption;
                Check(expected is {Angle:37,Text:"Балкон\nдлинное название"}&&draft.All(s=>s.Sector==name),"multiline rotated caption keeps single-line logical sector key");
                Check(draft.Select(s=>(s.X,s.Y,s.Row,s.Number)).SequenceEqual(coordinates),"caption rotation and text preserve seat geometry");
                var bounds=(Rect)typeof(HallView).GetField("bounds",Private)!.GetValue(hall)!;var hit=CaptionBounds(hall,name);
                Check(bounds.Contains(hit),"fit bounds include rotated caption hit region");expectedWorldBounds=hit;
                Corrections025UiChecks.Capture(editor,"029-movable-two-line-sector-caption");
                Logical(body).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить изменения")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch(Exception ex){failure=ex;editor.Close();}
        }));
        try
        {
            var result=editor.ShowDialog();if(failure is not null)throw failure;Check(result==true,"caption edits save successfully");
            Check(editor.CreatedId==id&&new Store(store.Path).Sectors(id).Single().Caption==expected,"used hall saves and reloads caption under original identity");
            var rendered=new HallView{ShowInactivePlaceholders=false};rendered.Load(store.Seats(evt),store.Sectors(id),false);
            Check(CaptionBounds(rendered,name)==expectedWorldBounds,"legacy sector anchor and hidden leftmost seat preserve caption world position after save");
            Check(store.VenueSeats(id).SequenceEqual(before)&&!store.Seats(evt).Single(s=>s.Id==before[0].Id).Available,"saving caption preserves seats and issuance");
            Check(!((HallView)main.FindName("Hall")).SectorEditingEnabled,"sale hall keeps caption dragging disabled");
        }
        finally{editor.Close();}
    }
    internal static void CheckCatalogEntryPoint(Store store,MainWindow main)
    {
        var previous=main.SeparateWindows;
        try
        {
            foreach(var separate in new[]{true,false})
            {
                main.SeparateWindows=separate;var suffix=Guid.NewGuid().ToString("N");var title="029 catalog edit "+suffix;
                var id=store.CreateHall(title,"",[new(){Sector="Партер",Row="1",Number="1",X=12,Y=24}]);var before=store.VenueSeats(id);
                var plan=store.SavePlan(id,title,before.Select(s=>new PriceValue(s.Id,500,"#ABCDEF")).ToArray());var evt=store.CreateEvent(store.SavePerformance(title),id,plan,DateTime.Today.AddDays(31));store.Issue(evt,[before[0].Id],title,separate?"CE029S":"CE029E",1);
                var count=store.Venues().Count;Exception? failure=null;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    EditorWindow? catalog=null;
                    try
                    {
                        catalog=OpenEditors(main).Single(w=>w.Title=="Залы");var list=Logical(EditorBody(main,catalog)).OfType<ListBox>().Single();
                        var venue=list.Items.Cast<VenueInfo>().Single(v=>v.Id==id);list.SelectedItem=venue;list.ScrollIntoView(venue);list.UpdateLayout();Pump();
                        var item=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(venue);
                        var screen=item.PointToScreen(new Point(item.ActualWidth/2,item.ActualHeight/2));
                        SetCursorPos((int)screen.X,(int)screen.Y);PumpInput();mouse_event(2,0,0,0,UIntPtr.Zero);PumpInput();
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                            HallEditorWindow? editor=null;
                            try
                            {
                                editor=OpenEditors(main).OfType<HallEditorWindow>().Single();var body=EditorBody(main,editor);
                                Check(editor.Title=="Редактирование зала"&&Logical(body).OfType<TextBox>().First().Text==title,"ordinary hall list click opens existing scheme in "+(separate?"separate":"embedded")+" mode");
                                var preview=(HallView)typeof(HallEditorWindow).GetField("preview",Private)!.GetValue(editor)!;
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                                    var dialog=OpenEditors(main).OfType<HallSectorNameWindow027>().Single();
                                    try{Logical(EditorBody(main,dialog)).OfType<TextBox>().Single().Text="Амфитеатр";Logical(EditorBody(main,dialog)).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                                    catch(Exception ex){failure=ex;dialog.Close();}
                                }));
                                ((Action<string,Point>)typeof(HallView).GetField("SectorContextRequested",Private)!.GetValue(preview)!)("Партер",new Point());
                                if(failure is not null)throw failure;
                                Logical(body).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить изменения")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Check(editor.CreatedId==id,"actual catalog entrypoint saves same hall identity");
                            }
                            catch(Exception ex){failure=ex;editor?.Close();}
                        }));
                        mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();
                    }
                    catch(Exception ex){failure=ex;}
                    finally{catalog?.Close();}
                }));
                typeof(MainWindow).GetMethod("HallsClick",Private)!.Invoke(main,[main,new RoutedEventArgs()]);if(failure is not null)throw failure;
                Check(store.Venues().Count==count&&store.Venues().Single(v=>v.Id==id).Name==title,"ordinary edit creates no copied or duplicate hall");
                Check(store.VenueSeats(id).Select(s=>(s.Id,s.X,s.Y,s.Row,s.Number)).SequenceEqual(before.Select(s=>(s.Id,s.X,s.Y,s.Row,s.Number)))&&store.Sectors(id).Single().Name=="Амфитеатр"&&!store.Seats(evt).Single().Available,"catalog sector rename keeps issued seats and geometry");
            }
        }
        finally{main.SeparateWindows=previous;}
    }
    private static IEnumerable<EditorWindow> OpenEditors(MainWindow main)=>Application.Current.Windows.OfType<EditorWindow>().Concat(((Dictionary<Grid,EditorWindow>)typeof(MainWindow).GetField("hostedEditors",Private)!.GetValue(main)!).Values).Distinct();
    private static DependencyObject EditorBody(MainWindow main,EditorWindow editor)=>editor.Content as DependencyObject??(DependencyObject)Logical(main).OfType<ContentControl>().Single(c=>ReferenceEquals(c.Tag,editor)).Content;
    private static Rect CaptionBounds(HallView hall,string name)=>((List<(Rect Bounds,string Sector)>)typeof(HallView).GetField("sectorHits",Private)!.GetValue(hall)!).Single(h=>h.Sector==name).Bounds;
    private static void DragCaption(HallView hall,string name)
    {
        var rect=CaptionBounds(hall,name);var world=new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2);
        var point=(Point)typeof(HallView).GetMethod("Screen",Private)!.Invoke(hall,[world])!;var screen=hall.PointToScreen(point);
        SetCursorPos((int)screen.X,(int)screen.Y);PumpInput();mouse_event(2,0,0,0,UIntPtr.Zero);PumpInput();SetCursorPos((int)screen.X+38,(int)screen.Y-27);PumpInput();mouse_event(4,0,0,0,UIntPtr.Zero);PumpInput();
    }
    private static void PumpInput(){var until=DateTime.UtcNow.AddMilliseconds(65);while(DateTime.UtcNow<until){Pump();Thread.Sleep(5);}}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 029 "+message);}
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
}
