using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Tickets.Core;

internal static class Parties018Checks
{
    internal static void Run(Store store)
    {
        var assembly=typeof(Tickets.App.App).Assembly;
        var price=(Window)Activator.CreateInstance(assembly.GetType("Tickets.App.SeatPriceWindow")!,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{store,100L,12},null)!;
        Exception? failure=null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try
            {
                var input=Logical(price).OfType<TextBox>().Single();
                Check(!Logical(price).OfType<ComboBox>().Any(),"price popup has no color selector");
                Check(!Logical(price).OfType<TextBlock>().Any(t=>t.Text.Contains("Выбрано мест")),"price popup has no selected-seat count");
                var buttons=Logical(price).OfType<Button>().ToArray();
                Check(buttons.Length==2&&buttons.Any(b=>Equals(b.Content,"Применить"))&&buttons.Any(b=>Equals(b.Content,"Отмена")),"price popup has only apply/cancel actions");
                Check(price.Width<=400&&price.Height<=250,"price popup is compact");
                input.Text="1250";input.Focus();
                input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            }
            catch(Exception ex){failure=ex;price.Close();}
        }));
        Check(price.ShowDialog()==true,"Enter confirms entered price");
        if(failure is not null)throw failure;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Check((long)price.GetType().GetProperty("Amount",flags)!.GetValue(price)! ==1250,"popup returns entered amount");
        Check(price.GetType().GetProperty("Color",flags)!.GetValue(price) is null,"popup delegates color to automatic price mapping");
        const string selected="UI018 выбранный отправитель";
        store.SaveSender(selected);
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var issue=store.Issue(show.Id,[store.Seats(show.Id).First(s=>s.Available).Id],"UI018 получатель","П018",1);
        var invoice=(Window)Activator.CreateInstance(assembly.GetType("Tickets.App.InvoiceWindow")!,store,show.Id,issue,false)!;
        invoice.Show();Pump();
        try
        {
            var sender=(ComboBox)invoice.GetType().GetField("senderBox",flags)!.GetValue(invoice)!;
            sender.SelectedItem=selected;
            Check(sender.ContextMenu?.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Удалить отправителя из списка"))==true,"sender dropdown offers deletion");
            Check(Logical(invoice).OfType<Button>().Any(b=>Equals(b.Content,"Реквизиты получателя")),"recipient requisites remain explicitly separate");
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var editor=Application.Current.Windows.OfType<Window>().SingleOrDefault(w=>w.Title=="Реквизиты отправителя");
                try
                {
                    Check(editor is not null,"adjacent requisites opens sender editor");
                    Check(Logical(editor!).OfType<TextBox>().First().Text==selected,"sender editor uses selected sender");
                }
                catch(Exception ex){failure=ex;}
                finally{editor?.Close();}
            }));
            Logical(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Реквизиты")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(failure is not null)throw failure;
        }
        finally{invoice.Close();Pump();}
        Console.WriteLine("Parties v0.18: selected sender requisites, separate recipient action, sender delete menu, compact Enter-confirmed price popup.");
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject parent)
    {yield return parent;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
