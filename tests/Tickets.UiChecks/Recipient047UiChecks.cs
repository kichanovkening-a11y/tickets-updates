using System.Reflection;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Recipient047UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        main.SelectEvent(show.Id);Pump();
        store.SaveRecipient("Альфа UI047");store.SaveRecipient("Бета UI047");
        var recipient=(ComboBox)main.FindName("RecipientBox");recipient.ApplyTemplate();
        var edit=(TextBox)recipient.Template.FindName("PART_EditableTextBox",recipient);
        Invoke(main,"RefreshRecipients",false);
        edit.Focus();edit.Clear();recipient.IsDropDownOpen=false;Pump();
        ClickField(edit);Pump();
        Check(recipient.IsDropDownOpen&&HasBoth(recipient),"clicking empty recipient text field opens the complete directory");
        Check(edit.IsKeyboardFocused&&edit.Text=="","opening recipient directory leaves the editable field focused and empty");

        edit.Text="Альфа";Pump();
        Check(recipient.IsDropDownOpen&&recipient.Items.Contains("Альфа UI047")&&!recipient.Items.Contains("Бета UI047"),"typing still filters recipient suggestions");
        edit.CaretIndex=2;ClickField(edit);Pump();
        Check(recipient.IsDropDownOpen&&HasBoth(recipient)&&edit.Text=="Альфа"&&edit.CaretIndex==2&&edit.IsKeyboardFocused,"clicking a filtered recipient field restores the full directory without replacing typed text or caret");
        recipient.UpdateLayout();
        var choice=(ComboBoxItem)recipient.ItemContainerGenerator.ContainerFromItem("Бета UI047");
        var itemsBeforeChoice=recipient.ItemsSource;
        ClickField(choice);Pump();
        Check(ReferenceEquals(itemsBeforeChoice,recipient.ItemsSource)&&edit.Text=="Альфа","popup item mouse route does not refresh the directory or overwrite the editor before selection");
        choice.IsSelected=true;Pump();
        Check(!recipient.IsDropDownOpen&&edit.Text=="Бета UI047","existing recipient choice commits and remains closed after callbacks");
        ClickField(edit);Pump();
        Check(recipient.IsDropDownOpen&&HasBoth(recipient)&&edit.Text=="Бета UI047","clicking an existing recipient opens all choices and preserves the name");
        var arrow=VisualChildren(recipient).OfType<ToggleButton>().First();
        var itemsBeforeArrow=recipient.ItemsSource;
        ClickField(arrow);Pump();
        Check(recipient.IsDropDownOpen&&ReferenceEquals(itemsBeforeArrow,recipient.ItemsSource),"arrow mouse preview leaves an open directory unchanged for the native toggle");
        recipient.IsDropDownOpen=false;
        ClickField(arrow);Pump();
        Check(!recipient.IsDropDownOpen,"arrow mouse preview does not pre-open a closed directory before the native toggle");
        var toggle=(IToggleProvider)new ToggleButtonAutomationPeer(arrow);
        toggle.Toggle();Pump();
        Check(recipient.IsDropDownOpen&&HasBoth(recipient),"native arrow toggle still opens the full directory");
        toggle.Toggle();Pump();
        Check(!recipient.IsDropDownOpen,"native arrow toggle still closes the directory");

        edit.Focus();edit.Text="Новая организация UI047";Pump();
        var hall=(HallView)main.FindName("Hall");hall.SetSelection([store.Seats(show.Id).First(s=>s.Available).Id]);Pump();
        Check(edit.Text=="Новая организация UI047"&&((Button)main.FindName("IssueButton")).IsEnabled&&!store.Recipients().Contains(edit.Text),"a manually entered new organization remains available for issuance without choosing a directory item");
        ClickField(edit);Pump();
        Check(HasBoth(recipient)&&edit.Text=="Новая организация UI047","full directory can be browsed without losing a new organization name");
        edit.Text="Альфа";recipient.SelectedItem="Альфа UI047";Pump();
        Check(!recipient.IsDropDownOpen&&edit.Text=="Альфа UI047","selection cancels queued typing refresh without reopening the directory");
        Invoke(main,"ClearIssuanceRecipient");hall.Clear();Pump();
    }
    private static bool HasBoth(ComboBox recipient)=>recipient.Items.Contains("Альфа UI047")&&recipient.Items.Contains("Бета UI047");
    // PreviewMouseDown tunnels and promotes the direct left-button event at each
    // element, matching native input. Raising PreviewMouseLeftButtonDown on the
    // textbox itself cannot exercise the ComboBox's handler.
    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);yield return child;
            foreach(var descendant in VisualChildren(child))yield return descendant;
        }
    }
    private static void ClickField(UIElement element)=>element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseDownEvent});
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private)!.Invoke(target,args);
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
