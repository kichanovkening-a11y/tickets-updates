using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallRename0281UiChecks
{
    internal static void Run(Store store,MainWindow main)
    {
        var separate=main.SeparateWindows;
        try
        {
            foreach(var mode in new[]{true,false})
            {
                main.SeparateWindows=mode;
                var suffix=Guid.NewGuid().ToString("N");
                var deleted=store.CreateHall("0281 deleted "+suffix,"",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);
                var active=store.CreateHall("0281 main "+suffix,"",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);
                store.DeleteVenue(deleted);var count=store.Venues().Count;
                var editor=new HallEditorWindow(store,store.Venues().Single(v=>v.Id==active),true);
                var body=(DependencyObject)editor.Content;Exception? failure=null;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    try
                    {
                        var boxes=Logical(body).OfType<TextBox>().ToArray();
                        boxes[0].Text="0281 renamed "+suffix;boxes[1].Text="new address";
                        Logical(body).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить изменения")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch(Exception ex){failure=ex;editor.Close();}
                }));
                try
                {
                    var result=editor.ShowDialog();if(failure is not null)throw failure;
                    Check(result==true&&editor.CreatedId==active,"title-only edit saves original hall in "+(mode?"separate":"embedded")+" mode");
                    Check(store.Venues().Count==count&&!store.Venues().Any(v=>v.Id==deleted)&&store.Venues().Single(v=>v.Id==active).Name=="0281 renamed "+suffix,"delete then rename another hall creates no duplicate or restoration");
                }
                finally{editor.Close();}
            }
        }
        finally{main.SeparateWindows=separate;}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS "+text);}
}
