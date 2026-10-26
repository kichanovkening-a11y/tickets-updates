namespace Tickets.Core;

public sealed partial class Store
{
    public TicketlandSyncSummary028 TicketlandShowSummary034(long eventId)
    {
        using var c=Open();using var tx=c.BeginTransaction();
        var shows=Local029(c,tx,loadSeats:false);
        var sales=ReadTicketlandSales029(c,tx);
        var mapped=new Dictionary<string,long>();
        using(var q=Command(c,"SELECT barcode,event_id FROM ticketland_sale_028 WHERE event_id IS NOT NULL"))
        {q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())mapped[r.GetString(0)]=r.GetInt64(1);}
        var matched=0;var unmatched=0;var general=0;
        foreach(var sale in sales)
        {
            if(mapped.TryGetValue(sale.Barcode,out var target))
            {if(target==eventId)matched++;continue;}
            var exact=Exact030(sale,shows);
            if(exact.Length!=1||exact[0].Id!=eventId)continue;
            if(General030(sale))general++;else unmatched++;
        }
        using var conflicts=Command(c,"""
            SELECT count(*) FROM ticketland_sale_028 tl WHERE tl.event_id=$e AND EXISTS(
                SELECT 1 FROM ticket t WHERE t.event_id=tl.event_id AND t.seat_id=tl.seat_id AND t.returned_at IS NULL)
            ""","$e",eventId);
        conflicts.Transaction=tx;
        return new(matched,unmatched,Convert.ToInt32(conflicts.ExecuteScalar()),general);
    }
}
