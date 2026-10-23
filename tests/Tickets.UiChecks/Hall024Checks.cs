using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Hall024Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var draft=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=5},new HallRowLayout{Label="2",SeatCount=5}},"1",false,0).ToList();
        var venueId=store.CreateHall("024 UI hide", "",draft);
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.HallEditorWindow")!;
        var editor=(Window)Activator.CreateInstance(type,[store,store.Venues().Single(v=>v.Id==venueId)])!;
        try
        {
            editor.Show();Pump();
            Check(!Logical(editor).OfType<Button>().Any(b=>Equals(b.Content,"Удалить выбранные (Del)")),"hall no longer has redundant toolbar delete");
            var table=(DataGrid)type.GetField("table",Private)!.GetValue(editor)!;
            Check(table.Columns.Where(c=>Equals(c.Header,"X")||Equals(c.Header,"Y")).All(c=>c.IsReadOnly),"individual seat coordinates are locked");
            var hall=(HallView)type.GetField("preview",Private)!.GetValue(editor)!;
            var notify=(Action<long,Point>)typeof(HallView).GetField("SeatContextRequested",Private)!.GetValue(hall)!;
            hall.SetSelection(new long[]{2,3});notify(2,new Point());hall.ContextMenu!.IsOpen=false;
            hall.ContextMenu.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Скрыть места")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();
            var seats=(IEnumerable<DraftSeat>)type.GetField("seats",Private)!.GetValue(editor)!;
            Check(seats.Count(s=>s.Hidden)==2&&seats.Select(s=>s.Number).SequenceEqual(draft.Select(s=>s.Number)),"hide preserves every original label");
            hall.SetSelection(new long[]{2,3});Check(hall.Selected.Count==0,"hidden places cannot be selected");
            ((CheckBox)type.GetField("showHidden",Private)!.GetValue(editor)!).IsChecked=true;Pump();hall.SetSelection(new long[]{2,3});notify(2,new Point());hall.ContextMenu!.IsOpen=false;
            hall.ContextMenu.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Показать места")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();
            Check(seats.All(s=>!s.Hidden),"hidden places can be restored without renumbering");
        }
        finally{editor.Close();Pump();}
        var view=new HallView{SectorEditingEnabled=true,ShowInactivePlaceholders=false};
        view.Load(new[]{new SeatInfo(1,"Партер","1","1",0,0,0,"#AAA",null,"","",null),new SeatInfo(2,"Партер","1","2",20,0,0,"#AAA",null,"","",null,IsHidden:true),new SeatInfo(3,"Балкон","1","1",90,90,0,"#AAA",null,"","",null)},new[]{new SectorInfo("Партер",0,-26),new SectorInfo("Балкон",90,64)},false);
        view.EditSector("Партер",false);var field=typeof(HallView).GetField("seats",Private)!;var original=(List<SeatInfo>)field.GetValue(view)!;
        typeof(HallView).GetField("transformStart",Private)!.SetValue(view,original.Where(s=>s.Sector=="Партер").ToArray());
        typeof(HallView).GetField("transformDragging",Private)!.SetValue(view,true);
        typeof(HallView).GetMethod("CacheSectorDrawing",Private)!.Invoke(view,null);
        typeof(HallView).GetField("sectorMatrix",Private)!.SetValue(view,new Matrix(1,0,0,1,30,40));
        IReadOnlyList<SeatInfo>? changed=null;view.SectorTransformCompleted+=items=>changed=items;
        typeof(HallView).GetMethod("EndGesture",Private)!.Invoke(view,null);
        Check(changed is {Count:2}&&changed.Single(s=>s.Id==2).X==50&&changed.Single(s=>s.Id==2).Y==40,"sector transform includes hidden places and preserves their spacing");
        Check(((List<SeatInfo>)field.GetValue(view)!).Single(s=>s.Id==3).X==90,"sector transform leaves other sectors fixed");
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
