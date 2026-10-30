namespace Tickets.Core;

public sealed partial class Store
{
    public TicketlandSyncSummary028 TicketlandShowSummary034(long eventId)
    {
        using var c=Open();using var tx=c.BeginTransaction();
        using(var pro=Command(c,"SELECT count(*) FROM ticketpro_show_037 WHERE event_id=$e","$e",eventId))
        {
            pro.Transaction=tx;
            if(Convert.ToInt64(pro.ExecuteScalar())>0)
            {
                using var counts=Command(c,"SELECT count(*),coalesce(sum(EXISTS(SELECT 1 FROM ticket t WHERE t.event_id=p.event_id AND t.seat_id=p.seat_id AND t.returned_at IS NULL)),0) FROM ticketpro_place_037 p WHERE p.event_id=$e AND p.state='sold'","$e",eventId);counts.Transaction=tx;using var r=counts.ExecuteReader();r.Read();return new(r.GetInt32(0),0,r.GetInt32(1),0);
            }
        }
        var shows=Local029(c,tx,loadSeats:false);
        var sales=ReadTicketlandSales029(c,tx);
        var mapped=new Dictionary<string,long>();
        using(var q=Command(c,"SELECT barcode,event_id FROM ticketland_effective_sold_037 WHERE event_id IS NOT NULL"))
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
            SELECT count(*) FROM ticketland_effective_sold_037 tl WHERE tl.event_id=$e AND EXISTS(
                SELECT 1 FROM ticket t WHERE t.event_id=tl.event_id AND t.seat_id=tl.seat_id AND t.returned_at IS NULL)
            ""","$e",eventId);
        conflicts.Transaction=tx;
        return new(matched,unmatched,Convert.ToInt32(conflicts.ExecuteScalar()),general);
    }
}
