using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallEditor021Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.HallEditorWindow")!;
        var editor=(Window)Activator.CreateInstance(type,[store,null])!;
        try
        {
            editor.Show();Pump();
            var preview=(HallView)type.GetField("preview",Private)!.GetValue(editor)!;
            var drawn=(List<SeatInfo>)typeof(HallView).GetField("seats",Private)!.GetValue(preview)!;
            Check(drawn.Count==0,"new hall starts empty until a creation method is accepted");
            Check(Logical(editor).OfType<Button>().Any(b=>Equals(b.Content,"Загрузить скан — проба"))&&Logical(editor).OfType<Button>().Any(b=>Equals(b.Content,"Сетка как в старой программе")),"new hall offers scan and legacy grid creation");
            Check(!Logical(editor).OfType<Expander>().Any(e=>Equals(e.Header,"Настройки рядов")),"obsolete manual row settings are absent");
            Check(((System.Collections.ICollection)type.GetField("seats",Private)!.GetValue(editor)!).Count==0,"opening an empty hall never silently creates seats");
        }
        finally{editor.Close();Pump();}
        ScanReview(store);
    }
    private static void ScanReview(Store store)
    {
        const int width=600,height=300;var pixels=Enumerable.Repeat((byte)255,width*height).ToArray();
        foreach(var y in new[]{40,100,160})foreach(var x in new[]{80,140,200})
            for(var k=0;k<20;k++)
            {pixels[y*width+x+k]=0;pixels[(y+19)*width+x+k]=0;pixels[(y+k)*width+x]=0;pixels[(y+k)*width+x+19]=0;}
        var image=System.Windows.Media.Imaging.BitmapSource.Create(width,height,96,96,System.Windows.Media.PixelFormats.Gray8,null,pixels,width);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"hall021-synthetic-"+Guid.NewGuid().ToString("N")+".png");
        using(var file=System.IO.File.Create(path))encoder.Save(file);
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.ScanReviewWindow021")!;
        var review=(Window)Activator.CreateInstance(type,[store,path,"Синтетический сектор"])!;
        Exception? error=null;
        try
        {
            review.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                try
                {
                    var grid=(DataGrid)type.GetField("table",Private)!.GetValue(review)!;
                    Check(!grid.IsReadOnly&&grid.Items.Count==9,"scan trial detects actual synthetic chair outlines and overrides readonly app style");
                    Check(grid.Items.Cast<object>().All(mark=>!(bool)mark.GetType().GetProperty("Reviewed")!.GetValue(mark)!),"all detected scan identities start unreviewed");
                    grid.SelectedItems.Add(grid.Items[0]);grid.SelectedItems.Add(grid.Items[1]);
                    var overlay=(Canvas)type.GetField("overlay",Private)!.GetValue(review)!;
                    overlay.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
                    Check(grid.SelectedItems.Count==0&&grid.Items.Count==9,"empty scan click clears all selected marks without changing the scheme");
                    grid.SelectedItem=grid.Items[0];grid.CurrentCell=new DataGridCellInfo(grid.Items[0],grid.Columns[1]);grid.ScrollIntoView(grid.Items[0],grid.Columns[1]);grid.Focus();
                    Check(grid.BeginEdit(),"scan row labels can be manually edited");Pump();
                    var element=grid.Columns[1].GetCellContent(grid.Items[0]);var text=element as TextBox??Visual(element!).OfType<TextBox>().First();text.Text="А";text.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    Check(grid.CommitEdit(DataGridEditingUnit.Cell,true)&&grid.CommitEdit(DataGridEditingUnit.Row,true),"scan manual row correction commits");Pump();
                    foreach(var mark in grid.Items.Cast<object>())
                    {
                        grid.ScrollIntoView(mark,grid.Columns[0]);Pump();
                        var checkbox=(CheckBox)grid.Columns[0].GetCellContent(mark);checkbox.IsChecked=true;checkbox.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.UpdateSource();
                    }
                    Check(grid.Items.Cast<object>().All(mark=>(bool)mark.GetType().GetProperty("Reviewed")!.GetValue(mark)!),"individual review checkboxes can mark every synthetic candidate");
                    var accept=Logical(review).OfType<Button>().Single(b=>Equals(b.Content,"Принять проверенные места"));accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch(Exception ex){error=ex;review.Close();}
            }));
            var accepted=review.ShowDialog();if(error is not null)throw error;
            Check(accepted==true,"scan only returns geometry after explicit acceptance");
            var result=(IReadOnlyList<DraftSeat>)type.GetProperty("Result")!.GetValue(review)!;
            Check(result.Count==9&&result[0].Row=="А"&&result.All(s=>s.Sector=="Синтетический сектор"),"accepted scan preserves manually corrected labels and sector");
        }
        finally{review.Close();System.IO.File.Delete(path);Pump();}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject parent)
    {
        yield return parent;
        foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var nested in Logical(child))yield return nested;
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject parent)
    {
        yield return parent;
        for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
            foreach(var child in Visual(System.Windows.Media.VisualTreeHelper.GetChild(parent,i)))yield return child;
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
