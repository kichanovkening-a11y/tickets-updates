using System.IO;
using System.Reflection;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using Tickets.App;
using Tickets.Core;
internal static class Ui023Checks
{
    internal static void Run(Store store)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS 023 "+message);}
        var hall=new HallView();Check(!hall.SectorEditingEnabled,"issuance hall cannot move or rotate sectors");
        var editor=new HallEditorWindow(store);editor.Show();Pump();
        try
        {
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var preview=(HallView)typeof(HallEditorWindow).GetField("preview",flags)!.GetValue(editor)!;
            Check(preview.SectorEditingEnabled,"sector transforms exist only in hall editor");
            var tabs=Walk(editor.Content as DependencyObject).OfType<TabItem>().ToArray();
            Check(!tabs.Any(t=>Equals(t.Header,"Сдвиг")),"obsolete Shift tab is removed");
            var places=(TextBox)typeof(HallEditorWindow).GetField("placesInput",flags)!.GetValue(editor)!;places.Text="5";Pump();
            preview.SetSelection(new long[]{1,2,3});
            var context=(Action<long,Point>)typeof(HallView).GetField("SeatContextRequested",flags)!.GetValue(preview)!;
            context(2,new Point(10,10));Pump();
            Check(preview.Selected.Count==3,"right-click preserves selected group when materializing new draft");
            var menu=preview.ContextMenu!;
            var remove=menu.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Удалить места"));remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;
            var draft=(System.Collections.ObjectModel.ObservableCollection<DraftSeat>)typeof(HallEditorWindow).GetField("seats",flags)!.GetValue(editor)!;
            Check(draft.Select(s=>s.Number).SequenceEqual(new[]{"4","5"}),"group deletion retains remaining original numbers");
            typeof(HallEditorWindow).GetMethod("Undo",flags)!.Invoke(editor,null);Pump();
            preview.SetSelection(new long[]{2,4});context(2,new Point(10,10));Pump();
            var before=draft.Select(s=>s.X).ToArray();menu=preview.ContextMenu!;
            menu.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Сделать проход")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;
            Check(draft.Count==5&&draft.Select(s=>s.Number).SequenceEqual(new[]{"1","2","3","4","5"})&&draft[1].X-before[1]==20&&draft[3].X-before[3]==40,"aisles apply to both selected runs without renumbering");
            Exception? popupError=null;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var popup=Application.Current.Windows.OfType<HallSectorWindow023>().Single();
                try
                {
                    var inputs=Walk(popup).OfType<TextBox>().ToArray();Check(inputs.Length==3,"sector popup asks name rows and places");
                    inputs[0].Text="Балкон023";inputs[1].Text="2";inputs[2].Text="3";
                    Check(draft.Count==5,"sector is not added before Save");
                    Walk(popup).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch(Exception ex){popupError=ex;popup.Close();}
            }));
            Walk(editor).OfType<Button>().Single(b=>Equals(b.Content,"Добавить сектор")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            if(popupError is not null)throw popupError;
            Check(draft.Count==11&&draft.Count(s=>s.Sector=="Балкон023")==6,"saved sector joins the single editable hall canvas");
        }
        finally{editor.Close();}
        var dir=Path.Combine(Path.GetTempPath(),"Tickets-shortcut023-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir,"Tickets.exe"),new byte[]{1});
            Shortcut023.Ensure(dir);
            var icon=Path.Combine(dir,"Data","Tickets-red023.ico");var expected=File.ReadAllBytes(icon);File.WriteAllBytes(icon,new byte[]{9});Shortcut023.Ensure(dir);
            Check(File.ReadAllBytes(icon).SequenceEqual(expected),"existing icon is refreshed on subsequent update");
            Check(File.Exists(Path.Combine(dir,"! Билеты.lnk")),"compact launch shortcut is created in application folder");
            Check(File.Exists(Path.Combine(dir,"Data","Tickets-red023.ico")),"shortcut owns separate red icon to avoid old EXE cache");
        }
        finally{Directory.Delete(dir,true);}
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Walk(DependencyObject? root)
    {if(root is null)yield break;yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
