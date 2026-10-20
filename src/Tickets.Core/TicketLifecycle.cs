using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureTicketLifecycle()
    {
        using var c=Open();using var cmd=Command(c,"""
            CREATE TABLE IF NOT EXISTS ticket_state(
                ticket_id INTEGER PRIMARY KEY REFERENCES ticket(id) ON DELETE CASCADE,
                state TEXT NOT NULL CHECK(state IN ('reserved','issued')),
                printed_at TEXT);
            """);cmd.ExecuteNonQuery();
    }

    public long Reserve(long eventId,IReadOnlyCollection<long> seats,string recipient,string series,long firstNumber,IReadOnlyDictionary<long,long>? prices=null)
        =>IssueCore(eventId,seats,recipient,series,firstNumber,prices,true);

    public void ConfirmReservation(long eventId,long issueId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var check=Command(c,"""
            SELECT count(*) FROM ticket t JOIN issue_batch b ON b.id=t.issue_id
            WHERE b.id=$i AND b.event_id=$e AND t.event_id=$e AND t.returned_at IS NULL
            ""","$i",issueId,"$e",eventId))
        {
            check.Transaction=tx;
            if(Convert.ToInt64(check.ExecuteScalar())==0)throw new TicketException("Активная накладная не найдена в этом показе.");
        }
        using var update=Command(c,"""
            UPDATE ticket_state SET state='issued' WHERE state='reserved' AND ticket_id IN
            (SELECT id FROM ticket WHERE event_id=$e AND issue_id=$i AND returned_at IS NULL)
            ""","$e",eventId,"$i",issueId);update.Transaction=tx;update.ExecuteNonQuery();tx.Commit();
    }

    // Call only after the print dialog has accepted the job. Repeated submissions
    // preserve the first successful print timestamp and the original invoice.
    public void MarkTicketsPrinted(long eventId,IReadOnlyCollection<long> ticketIds)
    {
        if(ticketIds.Count==0||ticketIds.Distinct().Count()!=ticketIds.Count)
            throw new TicketException("Выберите билеты для печати без повторов.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var timestamp=DateTimeOffset.Now.ToString("O");
        foreach(var id in ticketIds)
        {
            using var check=Command(c,"""
                SELECT count(*) FROM ticket t JOIN issue_batch b ON b.id=t.issue_id
                WHERE t.id=$id AND t.event_id=$e AND b.event_id=$e AND t.returned_at IS NULL
                ""","$id",id,"$e",eventId);check.Transaction=tx;
            if(Convert.ToInt64(check.ExecuteScalar())!=1)throw new TicketException("Билет возвращён или не найден в этом показе. Печать не отмечена.");
            using var update=Command(c,"""
                INSERT INTO ticket_state(ticket_id,state,printed_at) VALUES($id,'issued',$time)
                ON CONFLICT(ticket_id) DO UPDATE SET state='issued',printed_at=coalesce(ticket_state.printed_at,excluded.printed_at)
                ""","$id",id,"$time",timestamp);update.Transaction=tx;update.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public long ExtendInvoice(long eventId,long issueId,IReadOnlyCollection<long> seatIds)
    {
        if(seatIds.Count==0||seatIds.Distinct().Count()!=seatIds.Count)throw new TicketException("Выберите места без повторов.");
        try
        {
            using var c=Open();using var tx=c.BeginTransaction(deferred:false);
            RequireActiveEvent(c,tx,eventId);
            string recipient,series,timestamp;
            using(var batch=Command(c,"""
                SELECT b.recipient,b.series,b.issued_at FROM issue_batch b WHERE b.id=$i AND b.event_id=$e
                AND EXISTS(SELECT 1 FROM ticket t WHERE t.issue_id=b.id AND t.returned_at IS NULL)
                AND NOT EXISTS(SELECT 1 FROM ticket t JOIN ticket_state ts ON ts.ticket_id=t.id WHERE t.issue_id=b.id AND t.returned_at IS NULL AND ts.state='reserved')
                ""","$i",issueId,"$e",eventId))
            {
                batch.Transaction=tx;using var r=batch.ExecuteReader();
                if(!r.Read())throw new TicketException("Для дополнения выберите действующую выданную накладную этого показа.");
                recipient=r.GetString(0);series=r.GetString(1);timestamp=r.GetString(2);
            }
            using var next=Command(c,"SELECT coalesce(max(number),0) FROM ticket WHERE series=$s","$s",series);next.Transaction=tx;
            var last=Convert.ToInt64(next.ExecuteScalar());
            if(last>long.MaxValue-seatIds.Count)throw new TicketException("Номера этой серии закончились. Используйте другую серию.");
            foreach(var sid in seatIds)
            {
                using var check=Command(c,"""
                    SELECT p.amount FROM event_price p JOIN event e ON e.id=p.event_id
                    JOIN seat s ON s.id=p.seat_id AND s.venue_id=e.venue_id
                    WHERE p.event_id=$e AND p.seat_id=$s
                    AND NOT EXISTS(SELECT 1 FROM ticket t WHERE t.event_id=$e AND t.seat_id=$s AND t.returned_at IS NULL)
                    AND NOT EXISTS(SELECT 1 FROM event_seat_block b WHERE b.event_id=$e AND b.seat_id=$s)
                    ""","$e",eventId,"$s",sid);check.Transaction=tx;
                var amount=check.ExecuteScalar();
                if(amount is null)throw new TicketException("Место уже занято, снято с продажи, не имеет цены или не относится к этому показу. Дополнение отменено целиком.");
                using var insert=Command(c,"""
                    INSERT INTO ticket(event_id,seat_id,recipient,series,number,amount,issued_at,issue_id)
                    VALUES($e,$s,$r,$series,$n,$a,$time,$issue)
                    ""","$e",eventId,"$s",sid,"$r",recipient,"$series",series,"$n",++last,
                    "$a",Convert.ToInt64(amount),"$time",timestamp,"$issue",issueId);insert.Transaction=tx;insert.ExecuteNonQuery();
                using var state=Command(c,"INSERT INTO ticket_state(ticket_id,state) VALUES(last_insert_rowid(),'issued')");state.Transaction=tx;state.ExecuteNonQuery();
            }
            tx.Commit();return issueId;
        }
        catch(SqliteException ex) when(ex.SqliteErrorCode==19)
        {throw new TicketException("Серия и номер уже использованы или место занято. Дополнение отменено целиком.");}
        catch(SqliteException ex) when(ex.SqliteErrorCode is 5 or 6)
        {throw new TicketException("База занята другим окном программы. Повторите действие.");}
    }
}
