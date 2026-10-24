using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Hall027UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var assembly=typeof(MainWindow).Assembly;var type=assembly.GetType("Tickets.App.HallEditorWindow")!;
        var id=store.CreateHall("027 UI saved","",new[]{new DraftSeat{Sector="Партер",Row="1",Number="1",X=0,Y=0},new DraftSeat{Sector="Партер",Row="1",Number="2",X=20,Y=0}});
        var venue=store.Venues().Single(v=>v.Id==id);
        foreach(var edit in new[]{false,true})
        {
            var editor=(Window)Activator.CreateInstance(type,[store,venue,edit])!;
            try
            {
                editor.Show();Pump();var nodes=Logical(editor).ToArray();
                Check(!nodes.OfType<Expander>().Any(e=>Equals(e.Header,"Настройки рядов")||Equals(e.Header,"Нумерация и общие настройки")),"obsolete manual hall settings removed");
                Check(!nodes.OfType<TextBox>().Any(t=>t.Text.Contains("А, 1")),"obsolete manual instructions removed");
                Check(nodes.OfType<Button>().Any(b=>Equals(b.Content,"Загрузить скан — проба"))==!edit,"scan exists only during creation");
                Check(nodes.OfType<Button>().Any(b=>Equals(b.Content,"Сетка как в старой программе"))==!edit,"legacy grid exists only during creation");
                Check(!nodes.OfType<CheckBox>().Any(c=>Equals(c.Content,"Нумерация справа налево")),"numbering direction absent from saved scheme editor");
                var hall=(HallView)type.GetField("preview",Private)!.GetValue(editor)!;
                var notify=(Action<long,Point>)typeof(HallView).GetField("RowContextRequested",Private)!.GetValue(hall)!;
                notify(1,new Point());hall.ContextMenu!.IsOpen=false;
                Check(hall.ContextMenu.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Сдвинуть ряд")),"row context includes horizontal move");
                var changed=(Action<IReadOnlyList<SeatInfo>>)typeof(HallView).GetField("RowTransformCompleted",Private)!.GetValue(hall)!;
                changed(new[]{new SeatInfo(1,"Партер","1","1",35,0,0,"#AAA",null,"","",null),new SeatInfo(2,"Партер","1","2",55,0,0,"#AAA",null,"","",null)});
                var seats=(IEnumerable<DraftSeat>)type.GetField("seats",Private)!.GetValue(editor)!;
                Check(seats.Select(s=>s.X).SequenceEqual(new[]{35d,55d})&&seats.All(s=>s.Y==0),"row move completion applies draft coordinates");
                type.GetMethod("Undo",Private)!.Invoke(editor,null);Check(seats.Select(s=>s.X).SequenceEqual(new[]{0d,20d}),"row move supports undo");
                Exception? renameError=null;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    var dialog=Application.Current.Windows.Cast<Window>().Single(w=>w.GetType().Name=="HallSectorNameWindow027");
                    try{Logical(dialog).OfType<TextBox>().Single().Text="Амфитеатр027";Logical(dialog).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                    catch(Exception ex){renameError=ex;dialog.Close();}
                }));
                ((Action<string,Point>)typeof(HallView).GetField("SectorContextRequested",Private)!.GetValue(hall)!)("Партер",new Point());Pump();
                if(renameError is not null)throw renameError;
                Check(seats.All(s=>s.Sector=="Амфитеатр027"),"right-click caption renames the entire sector");
                type.GetMethod("Undo",Private)!.Invoke(editor,null);Check(seats.All(s=>s.Sector=="Партер"),"sector rename supports undo");
            }
            finally{editor.Close();Pump();}
        }
        var sectorType=assembly.GetType("Tickets.App.HallSectorWindow023")!;
        foreach(var creating in new[]{false,true})
        {
            var dialog=(Window)Activator.CreateInstance(sectorType,[store,creating])!;
            try{Check(Logical(dialog).OfType<CheckBox>().Any(c=>Equals(c.Content,"Нумерация справа налево"))==creating,"add sector direction available only in creation");}finally{dialog.Close();}
        }
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
