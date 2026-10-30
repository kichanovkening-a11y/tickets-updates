using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using Tickets.Core;

internal static class TicketPro037UiChecks
{
    static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static System.Collections.Generic.IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Logical(child))yield return node;}
    internal static void Run(Store store,Tickets.App.MainWindow main)
    {
        main.SelectEvent(store.Events().First().Id);Pump();
        Check(((FrameworkElement)main.FindName("WorkspaceNavigation")).Visibility==Visibility.Collapsed,"037 showing opens with hall expanded");
        Check(((Button)main.FindName("ReserveButton")).Visibility==Visibility.Collapsed,"037 no reservation button clutter");
        var workspace=(Grid)main.FindName("Workspace");Check(workspace.ColumnDefinitions.Count==3&&workspace.ColumnDefinitions[2].ActualWidth>=200&&workspace.ColumnDefinitions[2].ActualWidth<=300,"037 narrow tickets sidebar retained");
        var priceType=typeof(Tickets.App.MainWindow).Assembly.GetType("Tickets.App.PriceEditorWindow")!;
        var price=(Window)Activator.CreateInstance(priceType,store,(long?)null)!;
        price.Show();Pump();Check(price.WindowState==WindowState.Maximized,"037 pricing opens maximized");price.Close();
        var type=typeof(Tickets.App.MainWindow).Assembly.GetType("Tickets.App.EventEditorWindow")!;
        var editor=(Window)Activator.CreateInstance(type,store)!;editor.Show();Pump();
        var date=Logical(editor).OfType<DatePicker>().Single();date.ApplyTemplate();
        var input=(TextBox)date.Template.FindName("PART_TextBox",date);
        Check(input.HorizontalAlignment==HorizontalAlignment.Stretch&&input.VerticalAlignment==VerticalAlignment.Stretch&&!input.IsReadOnly&&input.Focusable,"037 whole date field is editable with caret");
        date.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent,Source=date});Pump();
        Check(input.IsKeyboardFocused,"037 outside-text click focuses the date input");editor.Close();
        Console.WriteLine("PASS 037 hall pricing sidebar reservation and date UI");
    }
}
