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
        var sessionType=typeof(Tickets.App.MainWindow).Assembly.GetType("Tickets.App.TicketProSession037")!;
        var applies=0;
        var session=(Window)Activator.CreateInstance(sessionType,BindingFlags.Instance|BindingFlags.NonPublic,null,[main,store,Guid.NewGuid().ToString(),(Func<Task>)(()=>{applies++;return Task.CompletedTask;}),(Action)(()=>{})],null)!;
        var off=Logical(session).OfType<Button>().Single(b=>Equals(b.Content,"Отключить обновление"));
        off.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        var observed=sessionType.GetMethod("OnSignInObserved038",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(observed is not null,"038 auto connection has explicit sign-in observation");
        ((Task)observed!.Invoke(session,[true])!).GetAwaiter().GetResult();
        Check(applies==0,"038 disabling updates is not undone by saved sign-in");
        sessionType.GetMethod("DisposeSession",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null);
        var batch=typeof(Tickets.App.MainWindow).Assembly.GetType("Tickets.App.TicketProBatch038");
        Check(batch is not null,"038 batch reader keeps successful shows across individual failures");
        var successes=0;
        Func<Task>[] reads=[()=>{successes++;return Task.CompletedTask;},()=>Task.FromException(new TicketException("failed show")),()=>Task.FromException(new OperationCanceledException()),()=>{successes++;return Task.CompletedTask;}];
        var errors=((Task<IReadOnlyList<string>>)batch!.GetMethod("RunAsync")!.Invoke(null,[reads,CancellationToken.None])!).GetAwaiter().GetResult();
        Check(successes==2&&errors.Count==2,"038 a failed or timed-out show preserves successes and continues");
        var toggle=main.FindName("ToggleHallButton038") as Button;
        Check(toggle is not null&&toggle.IsVisible,"038 hall has an accessible expand/restore button");
        var nav=(FrameworkElement)main.FindName("WorkspaceNavigation");
        Check(nav.Visibility==Visibility.Visible,"038 showing starts with navigation available");
        Corrections025UiChecks.Capture(main,"038-hall-normal");
        toggle!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
        Check(nav.Visibility==Visibility.Collapsed&&toggle.IsVisible&&Equals(toggle.Content,"Свернуть зал"),"038 expanded hall hides navigation and keeps restore accessible");
        Corrections025UiChecks.Capture(main,"038-hall-expanded");
        toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
        Check(nav.Visibility==Visibility.Visible&&Equals(toggle.Content,"Раскрыть зал"),"038 restoring hall brings back navigation");
        Check(((Button)main.FindName("ReserveButton")).Visibility==Visibility.Collapsed,"037 no reservation button clutter");
        var workspace=(Grid)main.FindName("Workspace");Check(workspace.ColumnDefinitions.Count==3&&workspace.ColumnDefinitions[2].ActualWidth>=200&&workspace.ColumnDefinitions[2].ActualWidth<=300,"037 narrow tickets sidebar retained");
        var priceType=typeof(Tickets.App.MainWindow).Assembly.GetType("Tickets.App.PriceEditorWindow")!;
        var price=(Window)Activator.CreateInstance(priceType,store,(long?)null)!;
        price.Show();Pump();
        var priceToggle=Logical(price).OfType<Button>().SingleOrDefault(b=>Equals(b.Content,"Раскрыть зал"));
        Check(priceToggle is not null&&price.WindowState==WindowState.Normal,"038 pricing starts with reversible expansion");
        priceToggle!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
        Check(price.WindowState==WindowState.Maximized&&Equals(priceToggle.Content,"Свернуть зал"),"038 pricing expands");
        priceToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
        Check(price.WindowState==WindowState.Normal,"038 pricing restores");price.Close();
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
