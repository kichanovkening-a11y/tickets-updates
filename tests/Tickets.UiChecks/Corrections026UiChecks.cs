using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Corrections026UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
        const string recipient="UI026 Получатель";store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000026",Position:"Директор",Signatory:"Петров П.П."));
        var issue=store.Issue(show.Id,new[]{seat.Id},recipient,"UI026",1);var assembly=typeof(MainWindow).Assembly;
        foreach(var kind in new[]{"contract","act","ttn","bill","specification"})
        {
            Exception? error=null;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var editor=Application.Current.Windows.OfType<Window>().Single(w=>w!=main&&w.IsVisible&&(w.GetType().Name is "CustomIssueDocumentWindow" or "IssueDocumentWindow" or "SpecificationWindow"));
                try{
                    var elements=Walk(editor).ToArray();var label=elements.OfType<TextBlock>().Where(t=>t.LayoutTransform is RotateTransform{Angle:90}).ToArray();
                    Check(editor.Title==recipient&&label.Length==1,"026 "+kind+" has recipient header and one external tab");
                    Check(!elements.OfType<TextBlock>().Any(t=>t.Text=="Получатель: "+recipient),"026 no duplicated recipient label");
                    var commands=elements.OfType<Button>().Where(b=>b.Content is string text&&(text=="Открыть в Word"||text=="Печать"||text=="Накладная"||text=="Спецификация")).ToArray();
                    var ys=commands.Select(b=>b.TranslatePoint(new Point(),editor).Y).ToArray();Check(ys.Max()-ys.Min()<1,"026 document commands share one line including bill");
                    Check(commands.All(b=>b.Padding.Left==2&&b.Padding.Right==2),"026 document commands have 2px side padding");
                }catch(Exception ex){error=ex;}finally{editor.Close();}
            }));
            assembly.GetType("Tickets.App.IssueDocumentNavigation")!.GetMethod("Open",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[store,show.Id,issue,kind]);
            if(error is not null)throw error;
        }
        var type=assembly.GetType("Tickets.App.InvoiceWindow")!;var invoice=(Window)Activator.CreateInstance(type,[store,show.Id,issue,false])!;invoice.Show();Pump();
        try{
            var box=(ComboBox)type.GetField("senderBox",Private)!.GetValue(invoice)!;var original=box.Text;var next=store.Senders().First(s=>s!=original);box.Focus();box.Text=next;
            var prompts=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};Program.ExpectedConfirmationTitle="Изменение отправителя";
            timer.Tick+=(_,_)=>{var handle=FindWindow(null,"Изменение отправителя");if(handle!=IntPtr.Zero){prompts++;PostMessage(GetDlgItem(handle,7),0xF5,IntPtr.Zero,IntPtr.Zero);}};timer.Start();
            try{type.GetMethod("PersistSender",Private)!.Invoke(invoice,null);Pump();}finally{timer.Stop();Program.ExpectedConfirmationTitle=null;}
            Check(prompts==1&&box.Text==original&&store.Invoice(show.Id,issue).Sender==original,"026 No restores sender and produces exactly one confirmation through focus loss");
        }finally{invoice.Close();Pump();}
        Console.WriteLine("Corrections026 native checks passed");
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindow(string? cls,string? title);
    [DllImport("user32.dll")]private static extern IntPtr GetDlgItem(IntPtr window,int id);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint msg,IntPtr w,IntPtr l);
}
