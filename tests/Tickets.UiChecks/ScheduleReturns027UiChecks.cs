using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class ScheduleReturns027UiChecks
{
    internal static void Run(Store store)=>Run(store,false);
    internal static void Run(Store store,bool offscreen)
    {
        var performance=store.Performances().First();
        var editor=new EventEditorWindow(store,performance.Id);Show(editor,offscreen);
        try
        {
            var combos=Walk(editor).OfType<ComboBox>().ToArray();
            Check(combos[0].SelectedItem is PerformanceInfo p&&p.Id==performance.Id&&combos[0].Text==performance.Title,"known performance selected");
            var create=Walk(editor).OfType<Button>().Single(b=>Equals(b.Content,"Создать"));
            Check(Math.Abs(combos[2].ActualHeight-create.ActualHeight)<.1,"pricing selector matches Create height");
            var date=Walk(editor).OfType<DatePicker>().Single();date.SelectedDate=DateTime.Today.AddDays(5);
            combos[3].Text="19";combos[4].Text="25";combos[5].Text="12";
            Walk(editor).OfType<Button>().Single(b=>Equals(b.Content,"Добавить дату")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(date.SelectedDate is null&&string.IsNullOrEmpty(date.Text)&&combos.Skip(3).All(c=>c.SelectedIndex==-1&&c.Text==""),"adding date clears date and every time component");
            var dates=Walk(editor).OfType<ListBox>().Single();
            Check(dates.Items.Count==1&&dates.Items[0] is DateTime saved&&saved.Hour==19&&saved.Minute==25&&saved.Second==12,"added date keeps entered time");
            var input=(DatePickerTextBox)date.Template.FindName("PART_TextBox",date);input.ApplyTemplate();
            Check(input.BorderThickness==new Thickness(0)&&!Visuals(input).OfType<ContentControl>().Any(c=>c.Name=="PART_Watermark"),"date input has no inner frame or watermark");
        }
        finally{editor.Close();}

        var show=store.Events().First(e=>DateTime.TryParse(e.SortDate,out var starts)&&starts<DateTime.Now&&store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
        const string recipient="UI Возврат027";store.SaveOrganizationDetails(recipient,false,new(Name:recipient));
        var issue=store.Issue(show.Id,[seat.Id],recipient,"UIRET027",1);store.Return([store.History(show.Id).Single(t=>t.IssueId==issue).Id]);
        var window=new RecipientsWindow(store);Show(window,offscreen);
        try
        {
            var organizations=Walk(window).OfType<ListBox>().Single();organizations.SelectedItem=organizations.Items.OfType<RecipientDirectoryInfo>().Single(r=>r.Name==recipient);Pump();
            Walk(window).OfType<Button>().Single(b=>Equals(b.Content,"Архив")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            var toggle=Walk(window).OfType<TextBlock>().Single(t=>t.Text=="Возврат: 1");
            Check(ReturnedTicketDetails027.IsToggleSource(toggle),"return count identifiable before invoice handler");
            var before=Application.Current.Windows.OfType<Window>().Count();
            void ClickCount()=>toggle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});
            ClickCount();Pump();
            var table=Walk(window).OfType<DataGrid>().Single();
            Check(table.Visibility==Visibility.Visible&&table.Items.Count==1&&table.Columns.Count==6,"return count expands six-column inline details");
            Check(Application.Current.Windows.OfType<Window>().Count()==before,"return count opens no invoice or separate window");
            ClickCount();Pump();Check(table.Visibility==Visibility.Collapsed,"second return count click collapses inline details");
            ClickCount();Pump();Check(table.Visibility==Visibility.Visible,"return details reopen inline");
        }
        finally{window.Close();}
    }
    private static void Show(Window window,bool offscreen)
    {
        if(offscreen){window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-10000;window.Top=-10000;window.ShowActivated=false;window.ShowInTaskbar=false;}
        window.Show();Pump();
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 027 "+message);}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root){yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Visuals(VisualTreeHelper.GetChild(root,i)))yield return item;}
}
