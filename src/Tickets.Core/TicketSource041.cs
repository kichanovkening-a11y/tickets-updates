using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    public string TicketSalesSource041(long? eventId)
    {
        if(!eventId.HasValue)return "Ticketland";
        using var connection=Open();return TicketSalesSource041(connection,null,eventId.Value);
    }
    private static string TicketSalesSource041(SqliteConnection connection,SqliteTransaction? transaction,long eventId)
    {
        using var command=Command(connection,"SELECT EXISTS(SELECT 1 FROM ticketpro_show_037 WHERE event_id=$e)","$e",eventId);
        command.Transaction=transaction;
        return Convert.ToInt64(command.ExecuteScalar())!=0?"Ticket PRO":"Ticketland";
    }
}
