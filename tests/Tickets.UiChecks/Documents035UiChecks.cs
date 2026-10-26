using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Documents035UiChecks
{
    internal static void Run(Store store,MainWindow main)
    {
        var settings=new SettingsWindow(store,main);settings.Show();Pump();
        try
        {
            var buttons=Walk(settings).OfType<Button>().Where(b=>new[]{"Выбрать папку…","Обновление","Резервная копия…","Ticketland","Сохранить","Импорт","Экспорт"}.Contains(b.Content?.ToString())).ToArray();
            Check(buttons.Select(b=>b.Content?.ToString()).SequenceEqual(new[]{"Выбрать папку…","Обновление","Резервная копия…","Ticketland","Сохранить","Импорт","Экспорт"}),"settings import and export are last");
            var import=buttons[^2];var export=buttons[^1];
            Check(import.Parent is WrapPanel row&&row.HorizontalAlignment==HorizontalAlignment.Left,"transfer buttons are aligned to left");
            Check(import.Parent==export.Parent&&Math.Abs(import.TranslatePoint(new Point(),settings).Y-export.TranslatePoint(new Point(),settings).Y)<1,"import and export share one row");
            Corrections025UiChecks.Capture(settings,"035-settings-buttons-left");
        }
        finally{settings.Close();}
        var owner=new Window{Title="035 save close test",Width=300,Height=150};owner.Show();Pump();
        Program.expectedSaveDialog=true;Program.ExpectedConfirmationTitle=owner.Title;
        try
        {
            foreach(var choice in new[]{7,2,6})
            {
                var writes=0;var fileDialog=false;var asked=false;var timedOut=false;var started=DateTime.UtcNow;
                var timer=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(100)};
                timer.Tick+=(_,_)=>{
                    var question=FindWindow("#32770",owner.Title);
                    if(question!=IntPtr.Zero){asked=true;PostMessage(question,0x111,new IntPtr(choice),IntPtr.Zero);}
                    var save=FindWindow("#32770","Сохранить документ");
                    if(save!=IntPtr.Zero){fileDialog=true;PostMessage(save,0x111,new IntPtr(2),IntPtr.Zero);}
                    if(DateTime.UtcNow-started>TimeSpan.FromSeconds(15))
                    {timedOut=true;if(question!=IntPtr.Zero)PostMessage(question,0x111,new IntPtr(2),IntPtr.Zero);if(save!=IntPtr.Zero)PostMessage(save,0x10,IntPtr.Zero,IntPtr.Zero);}
                };
                timer.Start();bool closed;
                try{closed=DocumentSaveDialog.SaveOnClose(store,owner,"035-test.docx",_=>writes++);}finally{timer.Stop();}
                Check(!timedOut&&asked,"close asks about saving");
                Check(writes==0,"discard and cancellation never write a document");
                Check(closed==(choice==7),"cancelled save keeps editor open while No permits close");
                Check(fileDialog==(choice==6),"Yes opens standard Windows destination picker only");
            }
        }
        finally{Program.expectedSaveDialog=false;Program.ExpectedConfirmationTitle=null;owner.Close();}
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject n){yield return n;foreach(var c in LogicalTreeHelper.GetChildren(n).OfType<DependencyObject>())foreach(var x in Walk(c))yield return x;}
    static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS UI035 "+text);}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
}
