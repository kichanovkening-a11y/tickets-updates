using System.Windows;
using System.Windows.Threading;
using Tickets.Update;
internal static class UpdaterProgress052Checks
{
    internal static void Run()
    {
        var type=System.Reflection.Assembly.Load("Tickets.Updater").GetType("Tickets.Updater.DispatcherProgress");
        if(type is null)throw new Exception("updater progress must explicitly marshal to the dispatcher");
        var window=new Window();window.Show();var called=false;var uiThread=Environment.CurrentManagedThreadId;
        Action<UpdateProgress> callback=p=>{if(Environment.CurrentManagedThreadId!=uiThread)throw new Exception("progress callback left UI thread");called=p.Completed==3;};
        var progress=(IProgress<UpdateProgress>)Activator.CreateInstance(type,window,callback)!;
        Task.Run(()=>progress.Report(new("phase",3,10))).GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        if(!called)throw new Exception("background progress was not delivered");
        var active=true;called=false;
        var guarded=type.GetConstructor([typeof(Window),typeof(Action<UpdateProgress>),typeof(Func<bool>)]);
        if(guarded is null)throw new Exception("queued progress must not overwrite completion or errors");
        var cancellable=(IProgress<UpdateProgress>)guarded.Invoke([window,callback,(Func<bool>)(()=>active)]);
        Task.Run(()=>cancellable.Report(new("late",3,10))).GetAwaiter().GetResult();active=false;
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        if(called)throw new Exception("stale progress overwrote completion");
        window.Close();Console.WriteLine("PASS 052 updater progress safely reaches WPF dispatcher");
    }
}