using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class PrintSchedule028UiChecks
{
    internal static void Run(Store store,MainWindow main)
    {
        var flag=DocumentFlag025.Tab("Выдача №28");
        Check(((SolidColorBrush)flag.Background).Color==Colors.Red&&flag.Child is TextBlock text&&((SolidColorBrush)text.Foreground).Color==Colors.White,"document flag uses red background and white upright text");
        var previousMode=main.SeparateWindows;
        try
        {
            foreach(var separate in new[]{false,true})
            {
                main.SeparateWindows=separate;
                var editor=new EventEditorWindow(store);var body=(DependencyObject)editor.Content;Exception? failure=null;
                main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
                    try
                    {
                        var date=Walk(body).OfType<DatePicker>().Single();var input=(DatePickerTextBox)date.Template.FindName("PART_TextBox",date);
                        Check(input.IsKeyboardFocused&&ReferenceEquals(Keyboard.FocusedElement,input)&&input.CaretIndex==0,"date input receives initial keyboard focus and caret in "+(separate?"native":"embedded")+" form");
                        var plan=Walk(body).OfType<ComboBox>().ElementAt(2);
                        foreach(var pair in new[]{("Расценовка",(FrameworkElement)plan),("Дата",(FrameworkElement)date)})
                        {
                            var label=Walk(body).OfType<TextBlock>().Single(t=>t.Text==pair.Item1);
                            var labelCenter=label.TranslatePoint(new Point(label.ActualWidth/2,0),(UIElement)body).X;
                            var fieldCenter=pair.Item2.TranslatePoint(new Point(pair.Item2.ActualWidth/2,0),(UIElement)body).X;
                            Check(Math.Abs(labelCenter-fieldCenter)<.5,pair.Item1+" is centered over its own field");
                        }
                        Check(Walk(body).OfType<DatePicker>().Count()==1&&Walk(body).OfType<ComboBox>().Count()==6,"date and pricing fixes add no duplicate inputs");
                    }
                    catch(Exception ex){failure=ex;}
                    finally{editor.Close();}
                }));
                main.PresentEditor(editor);if(failure is not null)throw failure;
            }
        }
        finally{main.SeparateWindows=previousMode;}
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).ToArray();
        var issue=store.Issue(show.Id,[seats[0].Id],"Печать UI028","UI028",1);
        var ticket=store.Invoice(show.Id,issue).Tickets.Single();
        var print=new TicketPrintWindow(store,show.Id,[seats[0].Id]);
        Check(print.ConfirmReprint(false),"first print proceeds without confirmation");
        Confirm(()=>Check(!print.ConfirmReprint(true),"No refuses repeat print"),false);
        Confirm(()=>Check(print.ConfirmReprint(true),"Yes permits repeat print"),true);
        store.MarkTicketsPrinted(show.Id,[ticket.Id]);
        Confirm(()=>InvokePrint(print),false);
        Check(!print.Printed&&store.Invoice(show.Id,issue).Tickets.Single().Id==ticket.Id,"No stops actual repeat print before printer access and preserves original ticket");print.Close();
        var mixed=new TicketPrintWindow(store,show.Id,seats.Select(s=>s.Id).ToArray(),"Печать UI028","UI028",2);
        Confirm(()=>InvokePrint(mixed),false);
        Check(mixed.IssueId==0&&!mixed.Printed&&store.Seats(show.Id).Single(s=>s.Id==seats[1].Id).Available,"No stops mixed repeat print before issuing new tickets");mixed.Close();
    }
    private static void InvokePrint(TicketPrintWindow print)=>typeof(TicketPrintWindow).GetMethod("Print",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(print,[null,null]);
    private static void Confirm(Action action,bool accept)
    {
        var seen=false;var timer=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(50)};
        timer.Tick+=(_,_)=>EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out var process);var title=new StringBuilder(256);var kind=new StringBuilder(64);GetClassName(window,kind,kind.Capacity);GetWindowText(window,title,title.Capacity);if(process!=(uint)Environment.ProcessId||kind.ToString()!="#32770"||title.ToString()!="Повторная печать билетов")return true;seen=true;PostMessage(window,0x111,new IntPtr(accept?6:7),IntPtr.Zero);return false;},IntPtr.Zero);
        Program.ExpectedConfirmationTitle="Повторная печать билетов";timer.Start();try{action();}finally{timer.Stop();Program.ExpectedConfirmationTitle=null;}Check(seen,"repeat print asks explicit confirmation");
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 028 "+message);}
    private delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr window,StringBuilder text,int capacity);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr window,StringBuilder text,int capacity);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
}
