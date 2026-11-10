using System.Windows;
using Tickets.Update;
namespace Tickets.Updater;

/// <summary>Explicit dispatch also works before Application.Run installs an ambient synchronization context.</summary>
public sealed class DispatcherProgress(Window owner,Action<UpdateProgress> callback,Func<bool> isActive):IProgress<UpdateProgress>
{
    public DispatcherProgress(Window owner,Action<UpdateProgress> callback):this(owner,callback,()=>true){}
    public void Report(UpdateProgress value)
    {
        if(!owner.Dispatcher.HasShutdownStarted)
            owner.Dispatcher.BeginInvoke(new Action(()=>{if(owner.IsVisible&&isActive())callback(value);}));
    }
}