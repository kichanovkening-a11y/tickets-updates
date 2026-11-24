using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.Core;

internal static class ClassicParityChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Tree(child))yield return node;}
    internal static void Run(string seed,string work)
    {
        var assembly=typeof(Tickets.App.MainWindow).Assembly;
        assembly.GetType("Tickets.App.ClassicVisual")!.GetMethod("Install",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
        var db=Path.Combine(work,"classic-parity.db");File.Copy(seed,db);var store=new Store(db);
        var main=new Tickets.App.MainWindow(store);Application.Current.MainWindow=main;main.Show();Pump();
        try
        {
            Check(!main.SeparateWindows,"classic preserves default embedded forms");
            assembly.GetType("Tickets.App.MainWindow")!.GetMethod("VerifyClassicNavigation",Private)!.Invoke(main,null);Pump();
            var search=(TextBox)main.FindName("SearchBox");
            Check(search.IsVisible,"search is reachable from classic catalog");
            search.Text="no-match-unique-7d9b";Pump();
            var performances=(ListBox)main.GetType().GetField("classicPerformances",Private)!.GetValue(main)!;
            Check(performances.Items.Count==0,"classic catalog responds to shared search");search.Clear();Pump();
            Check(performances.Items.Count>0,"clearing search restores catalog");
            Exception? dialogFailure=null;var inspected=false;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
                var layer=(Grid)main.FindName("DialogLayer");
                try
                {
                    Check(layer.IsVisible&&ReferenceEquals(layer.Parent,main.Content),"embedded settings overlay is above catalog and navigation");
                    var labels=Tree(layer).OfType<Button>().Select(b=>b.Content?.ToString()).ToArray();
                    foreach(var label in new[]{"Обновление","Резервная копия…","Ticket PRO","Импорт","Экспорт","Сохранить"})
                        Check(labels.Contains(label),"classic settings command: "+label);
                    inspected=true;
                }
                catch(Exception ex){dialogFailure=ex;}
                finally{foreach(var editor in Tree(layer).OfType<ContentControl>().Select(c=>c.Tag).OfType<Window>().ToArray())editor.Close();}
            }));
            Tree(main).OfType<Button>().First(b=>b.IsVisible&&Equals(b.Content,"Настройки")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(dialogFailure is not null)throw dialogFailure;Check(inspected,"classic settings opens and closes in embedded mode");
            var save=main.GetType().GetMethod("SaveWindowSettings",Private)!;
            foreach(var separate in new[]{true,false})
            {
                save.Invoke(main,[separate]);var reopened=new Tickets.App.MainWindow(store);
                Check(reopened.SeparateWindows==separate,"saved window mode survives reopening: "+separate);reopened.Close();
            }
            main.GetType().GetMethod("ShowClassicIssue",Private)!.Invoke(main,null);Pump();
            var labelsIssue=Tree(main).OfType<Button>().Where(b=>b.IsVisible).Select(b=>b.Content?.ToString()).ToArray();
            foreach(var label in new[]{"Получатели","Расценовки","Настройки"})
                Check(labelsIssue.Contains(label),"classic issuance command: "+label);
            Check(search.IsVisible,"search is reachable from classic issuance");
            Check(((FrameworkElement)main.FindName("HomePanel")).IsVisible,"issuance opens poster home");
            Check(!labelsIssue.Contains("Новый показ")&&!labelsIssue.Contains("Бланки печати"),"duplicate commands removed from issuance");
            main.SeparateWindows=true;
            Agreed052UiChecks.Run(store);
            Console.WriteLine("PASS classic navigation, command parity, search, persisted modes, embedded overlays, hall editing and price-free printing");
        }
        finally{main.Close();}
    }
}
