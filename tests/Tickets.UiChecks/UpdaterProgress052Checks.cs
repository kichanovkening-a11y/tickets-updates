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
        window.Close();Console.WriteLine("PASS 052 updater progress safely reaches WPF dispatcher");
    }
}