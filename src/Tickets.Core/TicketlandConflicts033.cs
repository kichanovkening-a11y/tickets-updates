using System.Globalization;
namespace Tickets.Core;
public sealed record TicketlandConflict033(long EventId,string Title,DateTime StartsAt,string Venue,string Sector,string Row,string Seat,string Recipient);
public sealed partial class Store
{
    public IReadOnlyList<TicketlandConflict033> TicketlandConflicts033()
    {
        using var c=Open();using var q=Command(c,"""
            SELECT DISTINCT e.id,e.title,e.starts_at,v.name,z.name,s.row_label,s.number,t.recipient
            FROM ticketland_sale_028 tl JOIN event e ON e.id=tl.event_id JOIN venue v ON v.id=e.venue_id
            JOIN seat s ON s.id=tl.seat_id JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id
            JOIN ticket t ON t.event_id=e.id AND t.seat_id=s.id AND t.returned_at IS NULL
            ORDER BY e.starts_at,e.title,z.name,s.row_label,s.number
            """);using var r=q.ExecuteReader();var result=new List<TicketlandConflict033>();
        while(r.Read())result.Add(new(r.GetInt64(0),r.GetString(1),DateTime.Parse(r.GetString(2),CultureInfo.InvariantCulture),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7)));return result;
    }
}
