using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class FutureSessionDeletion030UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        foreach(var directory in new[]{false,true})
        {
            var suffix=Guid.NewGuid().ToString("N");
            var venue=store.CreateHall("UI030 delete "+suffix,"",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);
            var seat=store.VenueSeats(venue).Single().Id;var plan=store.SavePlan(venue,"UI030 delete",[new(seat,600,"#FF0000")]);
            var performance=store.SavePerformance("UI030 delete "+suffix);
            var target=store.CreateEvent(performance,venue,plan,DateTime.Today.AddYears(2));
            var sibling=store.CreateEvent(performance,venue,plan,DateTime.Today.AddYears(2).AddDays(1));
            var issue=store.Issue(target,[seat],"UI030 deletion recipient",directory?"UID030D":"UID030M",1);
            store.Return([store.History(target).Single().Id]);var document=store.SaveInvoiceDocument(target,issue);var documentBytes=File.ReadAllBytes(document);
            var before=Backups(store).ToHashSet();Window owner=main;PerformanceWindow? catalog=null;
            RefreshMain(main);
            if(directory){catalog=new PerformanceWindow(store);owner=catalog;catalog.Show();Pump();}
            try
            {
                var tree=directory?Logical(owner).OfType<TreeView>().Single():(TreeView)main.FindName("EventsList");
                TreeViewItem Find(long id)=>Items(tree).Single(n=>n.Tag is Tickets.Core.EventInfo evt&&evt.Id==id);
                void OpenMenu()
                {
                    var leaf=Find(target);foreach(var node in Items(tree))if(Items(node).Contains(leaf))node.IsExpanded=true;
                    leaf.BringIntoView();Pump();NativeClick((FrameworkElement)leaf.Header,true);Pump();
                    Check(owner.IsVisible&&leaf.ContextMenu is {IsOpen:true},"native right-click opens date menu without opening the show: "+directory);
                    Check(leaf.ContextMenu!.Items.OfType<MenuItem>().Count()==1,"date menu exposes one show deletion command");
                }
                OpenMenu();Confirm(Find(target).ContextMenu!.Items.OfType<MenuItem>().Single(),false);
                Check(store.Events().Any(e=>e.Id==target)&&store.History(target).Count==1&&File.Exists(document)&&Backups(store).ToHashSet().SetEquals(before),"No preserves show returned history document and backup count");
                OpenMenu();Confirm(Find(target).ContextMenu!.Items.OfType<MenuItem>().Single(),true);Pump();
                Check(!store.Events(true).Any(e=>e.Id==target)&&store.Events().Any(e=>e.Id==sibling)&&store.Performances().Any(p=>p.Id==performance),"Yes deletes only selected future date and retains sibling and performance");
                Check(!Items(tree).Any(n=>n.Tag is Tickets.Core.EventInfo evt&&evt.Id==target)&&Items(tree).Any(n=>n.Tag is Tickets.Core.EventInfo evt&&evt.Id==sibling),"date tree refreshes after deletion");
                Check(store.History(target).Count==0&&store.Issues(target).Count==0&&!File.Exists(document),"confirmed deletion removes linked history and document");
                Check(store.VenueSeats(venue).Single().Id==seat&&store.Plans(venue).Any(p=>p.Id==plan)&&store.Seats(sibling).Single().Amount==600,"shared hall and pricing remain intact");
                var backup=Backups(store).Except(before).Single();var restored=new Store(backup);
                Check(restored.Events().Any(e=>e.Id==target)&&restored.History(target).Single().ReturnedAt is not null,"new backup recovers future date and returned history");
                var saved=Directory.EnumerateFiles(Path.GetDirectoryName(backup)!,Path.GetFileName(document),SearchOption.AllDirectories).Single();
                Check(File.ReadAllBytes(saved).SequenceEqual(documentBytes),"new backup preserves deleted invoice bytes");
            }
            finally{catalog?.Close();RefreshMain(main);}
        }
    }
    private static IEnumerable<string> Backups(Store store)
    {
        var root=Path.Combine(Path.GetDirectoryName(store.Path)!,"Backups");return Directory.Exists(root)?Directory.EnumerateFiles(root,"tickets.db",SearchOption.AllDirectories):[];
    }
    private static void RefreshMain(MainWindow main){((TextBox)main.FindName("SearchBox")).Text="";typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);Pump();}
    private static void Confirm(MenuItem menu,bool yes)
    {
        Exception? error=null;var answered=false;
        var timer=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(60)};
        timer.Tick+=(_,_)=>EnumWindows((dialog,_)=>{
            GetWindowThreadProcessId(dialog,out var process);var title=new StringBuilder(256);GetWindowText(dialog,title,title.Capacity);
            if(process!=(uint)Environment.ProcessId||title.ToString()!="Очистка базы")return true;
            try
            {
                var texts=new List<string>();EnumChildWindows(dialog,(child,_)=>{var text=new StringBuilder(2048);GetWindowText(child,text,text.Capacity);texts.Add(text.ToString());return true;},IntPtr.Zero);
                var warning=string.Join(" ",texts);
                Check(warning.Contains("показы: 1")&&warning.Contains("выдачи, возвраты и документы")&&warning.Contains("расценовки сохранятся")&&warning.Contains("резервная копия"),"confirmation explains single date scope related data and backup");
                Check((SendMessage(dialog,0x400,IntPtr.Zero,IntPtr.Zero).ToInt64()&0xFFFF)==7,"confirmation defaults to No");
                answered=true;timer.Stop();PostMessage(GetDlgItem(dialog,yes?6:7),0xF5,IntPtr.Zero,IntPtr.Zero);
            }
            catch(Exception ex){error=ex;timer.Stop();PostMessage(GetDlgItem(dialog,7),0xF5,IntPtr.Zero,IntPtr.Zero);}
            return true;
        },IntPtr.Zero);
        Program.ExpectedConfirmationTitle="Очистка базы";timer.Start();
        try{NativeClick(menu,false);Pump();Check(answered,"native menu command opened confirmation");if(error is not null)throw error;}
        finally{timer.Stop();Program.ExpectedConfirmationTitle=null;}
    }
    private static void NativeClick(FrameworkElement item,bool right)
    {
        item.UpdateLayout();var p=item.PointToScreen(new Point(item.ActualWidth/2,item.ActualHeight/2));
        Check(SetCursorPos((int)p.X,(int)p.Y),"native cursor reaches show/menu");Wait();mouse_event(right?8u:2u,0,0,0,UIntPtr.Zero);Wait();mouse_event(right?16u:4u,0,0,0,UIntPtr.Zero);Wait();
    }
    private static void Wait(){var f=new DispatcherFrame();var t=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(80)};t.Tick+=(_,_)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
    private static IEnumerable<TreeViewItem> Items(ItemsControl root){foreach(var item in root.Items.OfType<TreeViewItem>()){yield return item;foreach(var child in Items(item))yield return child;}}
    private static IEnumerable<DependencyObject> Logical(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS 030 "+text);}
    private delegate bool EnumCallback(IntPtr window,IntPtr state);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumCallback callback,IntPtr state);
    [DllImport("user32.dll")]private static extern bool EnumChildWindows(IntPtr parent,EnumCallback callback,IntPtr state);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll")]private static extern IntPtr GetDlgItem(IntPtr window,int id);
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
}
