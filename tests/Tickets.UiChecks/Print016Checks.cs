using System.Printing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using Tickets.Core;

internal static class Print016Checks
{
    internal static void Run(Store store)
    {
        var assembly=typeof(Tickets.App.App).Assembly;
        var type=assembly.GetType("Tickets.App.TicketPrintWindow")!;
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);
        var blank=store.TicketBlanks().First();store.RememberTicketBlank(blank.Id);
        Window Open()=> (Window)Activator.CreateInstance(type,store,show.Id,new[]{seat.Id},"UI016","PRINT016",1L)!;
        var window=Open();
        var choices=(ComboBox)type.GetField("blankChoice",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
        Check((choices.SelectedItem as TicketBlank)?.Id==blank.Id,"saved blank restored in ticket selection");
        Check(choices.FontSize>=18&&choices.FontWeight==FontWeights.Bold&&choices.VerticalContentAlignment==VerticalAlignment.Center,"blank selector has larger bold centred text");
        Check(!Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Бланки печати")),"selection view has no redundant blank management action");
        choices.SelectedIndex=0;window.Close();
        Check(new Store(store.Path).LastTicketBlankId is null,"standard choice is persisted from selector");
        window=Open();choices=(ComboBox)type.GetField("blankChoice",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
        Check(choices.SelectedIndex==0,"next ticket window restores standard selection");window.Close();

        var previewType=assembly.GetType("Tickets.App.UnifiedPrintPreview")!;
        var select=previewType.GetMethod("SelectMedia",BindingFlags.NonPublic|BindingFlags.Static)!;
        var letter=new PageMediaSize(PageMediaSizeName.NorthAmericaLetter,816,1056);
        var a4=new PageMediaSize(PageMediaSizeName.ISOA4,210*96/25.4,297*96/25.4);
        var result=(PageMediaSize)select.Invoke(null,new object?[]{new[]{letter,a4},null})!;
        Check(result.PageMediaSizeName==PageMediaSizeName.ISOA4,"A4 is selected ahead of printer Letter default");
        result=(PageMediaSize)select.Invoke(null,new object?[]{new[]{letter},null})!;
        Check(result.PageMediaSizeName==PageMediaSizeName.ISOA4,"A4 remains default without advertised A4 capability");
        result=(PageMediaSize)select.Invoke(null,new object?[]{new[]{letter,a4},letter})!;
        Check(result.PageMediaSizeName==PageMediaSizeName.NorthAmericaLetter,"explicit alternative paper remains selectable");

        var main=(Tickets.App.MainWindow)Application.Current.MainWindow;
        var previous=main.SeparateWindows;main.SeparateWindows=false;
        var before=Application.Current.Windows.OfType<Window>().Count(w=>w.IsVisible);var inspected=false;var built=false;Exception? previewError=null;var deadline=DateTime.UtcNow.AddSeconds(10);
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(_,_)=>
        {
            var host=Logical(main).OfType<ContentControl>().FirstOrDefault(c=>c.Tag?.GetType().Name=="PrintPreviewWindow");
            if(host is null)
            {
                if(DateTime.UtcNow<deadline)return;
                timer.Stop();previewError=new Exception("Print preview was not embedded within ten seconds");
                foreach(var pending in Application.Current.Windows.Cast<Window>().Where(w=>w.GetType().Name=="PrintPreviewWindow").ToArray())pending.Close();
                return;
            }
            timer.Stop();var editor=(Window)host.Tag;
            try
            {
            Check(Application.Current.Windows.OfType<Window>().Count(w=>w.IsVisible)==before,"embedded print preview creates no visible top-level window");
            Check(!editor.IsVisible&&!editor.ShowInTaskbar&&host.ActualHeight>0&&host.ActualHeight<=main.ActualHeight,"print preview fits the embedded host without taskbar entry");
            inspected=true;
            }
            catch(Exception ex){previewError=ex;}
            finally{editor.Close();}
        };
        timer.Start();
        try
        {
            Func<PrintDialog,bool,FixedDocument> build=(dialog,_)=>
            {
                Check(dialog.PrintTicket.PageMediaSize?.PageMediaSizeName==PageMediaSizeName.ISOA4&&dialog.PrintTicket.PageOrientation==PageOrientation.Portrait,"preview starts on A4 portrait even when legacy landscape is requested");
                built=true;return new FixedDocument();
            };
            previewType.GetMethod("Show",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{main,"UI016",build,(Action<PrintDialog,FixedDocument>)((_,_)=>throw new Exception("cancel submitted print")),true,null,new Size(100,200)});
            if(previewError is not null)throw previewError;
            Check(inspected&&built,"embedded print preview exercised and cancelled");
        }
        finally{timer.Stop();main.SeparateWindows=previous;}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
