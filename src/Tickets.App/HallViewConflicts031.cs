using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.Core;

namespace Tickets.App;

public sealed partial class HallView
{
    private DispatcherTimer? conflictTimer031;
    private bool conflictFlash031;
    private static bool Conflict031(SeatInfo seat)=>seat.IsTicketlandSold&&seat.TicketId.HasValue&&!seat.IsHidden&&seat.Id>0;

    private void InitializeConflicts031()
    {
        conflictTimer031=new DispatcherTimer(DispatcherPriority.Background,Dispatcher){Interval=TimeSpan.FromMilliseconds(600)};
        conflictTimer031.Tick+=(_,_)=>{conflictFlash031=!conflictFlash031;InvalidateVisual();};
        Loaded+=(_,_)=>UpdateConflicts031();
        IsVisibleChanged+=(_,_)=>UpdateConflicts031();
        Unloaded+=(_,_)=>{conflictTimer031.Stop();conflictFlash031=false;};
    }

    private void UpdateConflicts031()
    {
        if(conflictTimer031 is null)return;
        var animate=IsLoaded&&IsVisible&&seats.Any(Conflict031);
        if(animate){if(!conflictTimer031.IsEnabled){conflictFlash031=false;conflictTimer031.Start();}}
        else{conflictTimer031.Stop();conflictFlash031=false;}
        InvalidateVisual();
    }

    private void DrawConflicts031(DrawingContext dc)
    {
        if(!conflictFlash031)return;
        foreach(var seat in seats.Where(Conflict031))
        {
            if(focusPrice.HasValue&&seat.Amount!=focusPrice.Value||focusStatus030.HasValue&&!HallFocus030.Matches(seat,focusStatus030.Value))continue;
            // Draw only the flashing overlay; keep the cached hall and stored state intact.
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(205,255,255,255)),new Pen(Brushes.Red,2/zoom),new Rect(seat.X,seat.Y,16,16),2,2);
        }
    }
}
