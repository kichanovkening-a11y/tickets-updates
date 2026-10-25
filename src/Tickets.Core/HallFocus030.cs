namespace Tickets.Core;

public enum HallStatus030 { Reserved, Printed, Blocked, NoPrice, TicketlandSold }

/// <summary>Visual legend focus only; never changes selection or seat status.</summary>
public static class HallFocus030
{
    public static bool Matches(SeatInfo seat,HallStatus030 status)
    {
        var occupied=seat.TicketId.HasValue||seat.IsReserved;
        return status switch
        {
            HallStatus030.Reserved=>!seat.IsBlocked&&!seat.IsTicketlandSold&&occupied&&(!seat.IsPrinted||seat.IsReserved),
            HallStatus030.Printed=>!seat.IsBlocked&&!seat.IsTicketlandSold&&occupied&&seat.IsPrinted&&!seat.IsReserved,
            HallStatus030.Blocked=>seat.IsBlocked,
            HallStatus030.NoPrice=>!seat.IsBlocked&&!seat.IsTicketlandSold&&!occupied&&!seat.Amount.HasValue,
            HallStatus030.TicketlandSold=>seat.IsTicketlandSold,
            _=>false
        };
    }
}
