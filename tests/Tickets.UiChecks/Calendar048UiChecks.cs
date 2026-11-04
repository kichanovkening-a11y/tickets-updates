using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Calendar048UiChecks
{
    internal static void Run(Store store,MainWindow main)
    {
        var previous=main.SeparateWindows;
        try
        {
            foreach(var separate in new[]{false,true})
            {
                main.SeparateWindows=separate;
                var editor=new EventEditorWindow(store);var body=(DependencyObject)editor.Content;
                Exception? failure=null;
                main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
                    try
                    {
                        var mode=separate?"separate":"embedded";
                        var date=Logical(body).OfType<DatePicker>().Single();
                        Check(date.SelectedDate is null,"new date is empty: "+mode);
                        var open=Logical(body).OfType<Button>().Single(b=>Equals(b.Content,"Календарь"));
                        Click(open);
                        var popup=(Popup)date.Template.FindName("PART_Popup",date);
                        Check(popup.IsOpen,"calendar opens with mouse: "+mode);
                        var calendar=Visual(popup.Child).OfType<Calendar>().Single();
                        var month=calendar.DisplayDate;
                        var next=Visual(calendar).OfType<Button>().Single(b=>b.Name=="PART_NextButton");
                        Click(next);
                        Check(calendar.DisplayDate.Year==month.AddMonths(1).Year&&calendar.DisplayDate.Month==month.AddMonths(1).Month,"mouse advances calendar month: "+mode);
                        var day=Visual(calendar).OfType<CalendarDayButton>().First(b=>!b.IsInactive&&b.IsEnabled&&b.IsVisible&&b.DataContext is DateTime);
                        var chosen=((DateTime)day.DataContext).Date;
                        Click(day);
                        Check(date.SelectedDate==chosen,"mouse chooses calendar date: "+mode);
                        Check(!popup.IsOpen,"calendar closes after selection: "+mode);
                        var combos=Logical(body).OfType<ComboBox>().ToArray();
                        combos[3].Text="19";combos[4].Text="25";combos[5].Text="12";
                        Logical(body).OfType<Button>().Single(b=>Equals(b.Content,"Добавить дату")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(Logical(body).OfType<ListBox>().Single().Items.Cast<DateTime>().Single()==chosen.AddHours(19).AddMinutes(25).AddSeconds(12),"chosen calendar date reaches showing list: "+mode);
                        Click(open);Check(popup.IsOpen,"calendar reopens after adding date: "+mode);
                        date.IsDropDownOpen=false;
                    }
                    catch(Exception ex){failure=ex;}
                    finally{editor.Close();}
                }));
                main.PresentEditor(editor);
                if(failure is not null)throw failure;
            }
        }
        finally{main.SeparateWindows=previous;}
    }
    private static void Click(FrameworkElement element)
    {
        element.BringIntoView();Pump();
        var point=element.PointToScreen(new Point(element.ActualWidth/2,element.ActualHeight/2));
        Check(SetCursorPos((int)point.X,(int)point.Y),"mouse reaches "+element.GetType().Name);
        Pump();mouse_event(2,0,0,0,UIntPtr.Zero);Pump();mouse_event(4,0,0,0,UIntPtr.Zero);Pump();
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(80)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Logical(child))yield return node;}
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var node in Visual(VisualTreeHelper.GetChild(root,i)))yield return node;}
    private static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS 048 "+label);}
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
}
