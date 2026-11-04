using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Refresh049UiChecks
{
    internal static void CheckRefreshButton(MainWindow main,TicketlandReport028 report)
    {
        var fetch=main.TicketlandFetch028!;
        var button=(Button)main.FindName("RefreshTicketlandButton029");
        var toggle=(Button)main.FindName("ToggleHallButton038");
        var normal=button.Foreground;
        try
        {
            foreach(var expanded in new[]{false,true})
            {
                if(expanded){toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
                foreach(var fail in new[]{false,true})
                {
                    var completion=new TaskCompletionSource<TicketlandReport028>();
                    main.TicketlandFetch028=(_,_)=>completion.Task;
                    try
                    {
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                        Check(main.TicketlandIsUpdating028&&!button.IsEnabled,"refresh is pending and duplicate input is disabled");
                        var red=false;var other=false;
                        var samples=new List<string>();
                        var until=DateTime.UtcNow.AddSeconds(5);
                        while(!(red&&other)&&DateTime.UtcNow<until)
                        {
                            Pump();var color=(button.Foreground as SolidColorBrush)?.Color;
                            samples.Add($"{DateTime.UtcNow:HH:mm:ss.fff}:{color}");
                            red|=color==Colors.Red;other|=color!=Colors.Red;
                        }
                        if(!(red&&other))Console.Error.WriteLine($"REFRESH049 expanded={expanded} fail={fail} visible={button.IsVisible} animated={button.HasAnimatedProperties} base={button.GetAnimationBaseValue(Control.ForegroundProperty)} samples={string.Join(",",samples)}");
                        Check(red&&other,"refresh flashes red during update in "+(expanded?"expanded":"ordinary")+" hall");
                        if(fail)completion.SetException(new TicketException("UI049 test failure"));
                        else completion.SetResult(report);
                        until=DateTime.UtcNow.AddSeconds(15);
                        while(!button.IsEnabled&&DateTime.UtcNow<until)Pump();
                        Check(button.IsEnabled&&!main.TicketlandIsUpdating028&&Equals(button.Foreground,normal),"refresh restores normal appearance after "+(fail?"error":"success"));
                        Pump();Check(Equals(button.Foreground,normal),"completed refresh does not leave flashing active");
                    }
                    finally
                    {
                        completion.TrySetCanceled();
                        var until=DateTime.UtcNow.AddSeconds(15);
                        while(main.TicketlandIsUpdating028&&DateTime.UtcNow<until)Pump();
                    }
                }
            }
        }
        finally
        {
            main.TicketlandFetch028=fetch;
            if(Equals(toggle.ToolTip,"Свернуть зал"))toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(60)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    private static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS 049 "+label);}
}
