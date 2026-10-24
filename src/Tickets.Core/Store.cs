using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    public string Path { get; }
    public Store(string path) { Path=System.IO.Path.GetFullPath(path); EnsureCatalog(); EnsureCatalogDeletion(); EnsureHallHiddenSeats024(); EnsureHallRowAnchors027(); EnsureIssues(); EnsureScheduling(); EnsureDocuments(); EnsurePricing(); EnsurePackageDocuments(); EnsureSaleStates(); EnsureTicketLifecycle(); EnsurePlanAvailability(); EnsureOrganizationDirectory(); EnsureSberDocumentForms(); using var formConnection=Open(); EnsureRecipientForms(formConnection); EnsureVenueLayouts019(); EnsureCompactVenueLayouts020(); EnsureRetiredHallSectors021(); EnsureContiguousEstrada022(); EnsureTicketland028(); }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource=Path, Mode=SqliteOpenMode.ReadWrite, ForeignKeys=true,
            DefaultTimeout=5, Pooling=false }.ToString());
        c.Open();
        return c;
    }

    private static SqliteCommand Command(SqliteConnection c, string sql, params object[] pairs)
    {
        var cmd=c.CreateCommand(); cmd.CommandText=sql;
        for (var i=0;i<pairs.Length;i+=2) cmd.Parameters.AddWithValue((string)pairs[i],pairs[i+1]);
        return cmd;
    }

    public List<EventInfo> Events(bool includeDeleted=false)
    {
        using var c=Open();
        using var cmd=Command(c,"""
            SELECT e.id,e.venue_id,e.title,v.name,e.legacy_date,e.starts_at,
              (SELECT count(*) FROM seat s WHERE s.venue_id=e.venue_id AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat hs WHERE hs.seat_id=s.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))),
              (SELECT count(*) FROM ticket t WHERE t.event_id=e.id AND t.returned_at IS NULL)
            FROM event e JOIN venue v ON v.id=e.venue_id
            WHERE $all=1 OR NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='venue' AND h.entity_key=CAST(e.venue_id AS TEXT))
            ORDER BY e.starts_at DESC,e.id DESC
            ""","$all",includeDeleted?1:0);
        using var r=cmd.ExecuteReader(); var result=new List<EventInfo>();
        var hidden=HiddenCatalogKeys("performance");
        while(r.Read()) if(includeDeleted||!hidden.Contains(CatalogText.Key(r.GetString(2)))) result.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),
            r.GetString(4),r.GetString(5),r.GetInt32(6),r.GetInt32(7)));
        return result;
    }

    public List<SeatInfo> Seats(long eventId)
    {
        using var c=Open();
        using var cmd=Command(c,"""
            SELECT s.id,z.name,s.row_label,s.number,s.x,s.y,coalesce(t.amount,p.amount),coalesce(pc.color,p.color,'#CBD5E1'),
              t.id,coalesce(t.recipient,''),coalesce(t.series,''),t.number,b.seat_id IS NOT NULL,t.issue_id,ts.printed_at,coalesce(ts.state,'issued'),(SELECT anchors FROM hall_row_anchors a WHERE a.seat_id=s.id),EXISTS(SELECT 1 FROM ticketland_sale_028 tl WHERE tl.event_id=e.id AND tl.seat_id=s.id)
            FROM event e JOIN seat s ON s.venue_id=e.venue_id
            JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id
            LEFT JOIN event_price p ON p.event_id=e.id AND p.seat_id=s.id
            LEFT JOIN ticket t ON t.event_id=e.id AND t.seat_id=s.id AND t.returned_at IS NULL
            LEFT JOIN ticket_state ts ON ts.ticket_id=t.id
            LEFT JOIN price_color pc ON pc.amount=coalesce(t.amount,p.amount)
            LEFT JOIN event_seat_block b ON b.event_id=e.id AND b.seat_id=s.id
            WHERE e.id=$e AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat hs WHERE hs.seat_id=s.id) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT)) ORDER BY z.y,z.x,s.y,s.x,s.id
            ""","$e",eventId);
        using var r=cmd.ExecuteReader(); var result=new List<SeatInfo>();
        while(r.Read()) result.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),
            r.GetDouble(4),r.GetDouble(5),r.IsDBNull(6)?null:r.GetInt64(6),r.GetString(7),
            r.IsDBNull(8)?null:r.GetInt64(8),r.GetString(9),r.GetString(10),r.IsDBNull(11)?null:r.GetInt64(11),r.GetBoolean(12),r.IsDBNull(13)?null:r.GetInt64(13),!r.IsDBNull(14),r.GetString(15)=="reserved"){RowAisleAnchors=ReadRowAnchors027(r,16),IsTicketlandSold=r.GetBoolean(17)});
        return result;
    }

    public List<SectorInfo> Sectors(long venueId)
    {
        using var c=Open();
        using var cmd=Command(c,"SELECT name,x,y FROM sector WHERE venue_id=$v AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(sector.venue_id AS TEXT)||':'||CAST(sector.id AS TEXT)) ORDER BY y,x","$v",venueId);
        using var r=cmd.ExecuteReader(); var result=new List<SectorInfo>();
        while(r.Read()) result.Add(new(r.GetString(0),r.GetDouble(1),r.GetDouble(2)));
        return result;
    }

    public List<TicketInfo> History(long eventId)
    {
        using var c=Open();
        using var cmd=Command(c,"""
            SELECT t.id,s.id,z.name,s.row_label,s.number,t.recipient,t.series,t.number,t.amount,
              t.issued_at,t.returned_at,t.issue_id,ts.printed_at,coalesce(ts.state,'issued') FROM ticket t JOIN seat s ON s.id=t.seat_id
            LEFT JOIN ticket_state ts ON ts.ticket_id=t.id
            JOIN sector z ON z.venue_id=s.venue_id AND z.id=s.sector_id
            WHERE t.event_id=$e ORDER BY t.id DESC
            ""","$e",eventId);
        using var r=cmd.ExecuteReader(); var result=new List<TicketInfo>();
        while(r.Read()) result.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),
            r.GetString(4),r.GetString(5),r.GetString(6),r.GetInt64(7),r.GetInt64(8),r.GetString(9),
            r.IsDBNull(10)?null:r.GetString(10),r.GetInt64(11),r.IsDBNull(12)?null:r.GetString(12),r.GetString(13)=="reserved"));
        return result;
    }

    private static string Series(string series) => series.Trim().Normalize().ToUpperInvariant();

    public long NextNumber(string series)
    {
        using var c=Open();
        using var cmd=Command(c,"SELECT coalesce(max(number),0) FROM ticket WHERE series=$s","$s",Series(series));
        var last=Convert.ToInt64(cmd.ExecuteScalar());
        if(last==long.MaxValue) throw new TicketException("Номера этой серии закончились. Используйте другую серию.");
        return last+1;
    }

    public long Issue(long eventId, IReadOnlyCollection<long> seats, string recipient, string series, long firstNumber, IReadOnlyDictionary<long,long>? prices=null)
        => IssueCore(eventId,seats,recipient,series,firstNumber,prices,false);

    private long IssueCore(long eventId, IReadOnlyCollection<long> seats, string recipient, string series, long firstNumber, IReadOnlyDictionary<long,long>? prices, bool reserved)
    {
        recipient=Required(recipient,"Получатель"); series=Series(series);
        if(recipient.Length is 0 or >200) throw new TicketException("Укажите получателя (до 200 символов).");
        if(series.Length is 0 or >32 || series.Any(char.IsControl)) throw new TicketException("Укажите серию (до 32 символов).");
        if(seats.Count==0) throw new TicketException("Выберите места.");
        if(seats.Distinct().Count()!=seats.Count) throw new TicketException("В списке повторяются места.");
        if(prices is not null && prices.Any(p=>p.Value<0||!seats.Contains(p.Key)))
            throw new TicketException("Изменённые цены должны относиться к выбранным местам и быть неотрицательными.");
        if(firstNumber<1 || firstNumber>long.MaxValue-seats.Count+1)
            throw new TicketException("Недопустимый диапазон номеров билетов.");
        var selectedSender=Sender;
        if(string.IsNullOrWhiteSpace(selectedSender))throw new TicketException("Выберите или создайте отправителя.");
        try
        {
            using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
            RequireActiveEvent(c,tx,eventId);
            var n=firstNumber; var timestamp=DateTimeOffset.Now.ToString("O");
            using var batch=Command(c,"""
                INSERT INTO issue_batch(id,event_id,recipient,issued_at,series,title,venue,event_date,sender)
                SELECT $issueId,e.id,$r,$time,$series,e.title,v.name,e.legacy_date,$sender FROM event e JOIN venue v ON v.id=e.venue_id WHERE e.id=$e RETURNING id
                ""","$e",eventId,"$r",recipient,"$time",timestamp,"$series",series,"$sender",selectedSender,"$issueId",AllocateIssueId(c,tx));batch.Transaction=tx;
            var result=batch.ExecuteScalar();if(result is null)throw new TicketException("Показ не найден.");
            var issueId=Convert.ToInt64(result);
            using(var senderQuery=Command(c,"SELECT sender FROM issue_batch WHERE id=$i","$i",issueId))
            {
                senderQuery.Transaction=tx;
                CaptureIssueOrganizations(c,tx,issueId,Convert.ToString(senderQuery.ExecuteScalar())!,recipient);
                CaptureRecipientForms(c,tx,issueId,recipient);
            }
            foreach(var sid in seats)
            {
                using var check=Command(c,"""
                    SELECT p.amount FROM event_price p JOIN event e ON e.id=p.event_id
                    JOIN seat s ON s.id=p.seat_id AND s.venue_id=e.venue_id
                    WHERE p.event_id=$e AND p.seat_id=$s
                    AND NOT EXISTS(SELECT 1 FROM hall_hidden_seat hs WHERE hs.seat_id=s.id)
                    AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT))
                    AND NOT EXISTS(SELECT 1 FROM ticket t WHERE t.event_id=$e AND t.seat_id=$s AND t.returned_at IS NULL)
                    AND NOT EXISTS(SELECT 1 FROM ticketland_sale_028 tl WHERE tl.event_id=$e AND tl.seat_id=$s)
                    AND NOT EXISTS(SELECT 1 FROM event_seat_block b WHERE b.event_id=$e AND b.seat_id=$s)
                    ""","$e",eventId,"$s",sid);
                check.Transaction=tx;
                var amount=check.ExecuteScalar();
                if(amount is null) throw new TicketException("Одно из мест уже выдано, заблокировано, не имеет цены или не относится к этому показу. Выдача отменена целиком.");
                var actualAmount=prices is not null && prices.TryGetValue(sid,out var custom)?custom:Convert.ToInt64(amount);
                EnsurePriceColor(c,tx,actualAmount,null);
                using var insert=Command(c,"""
                    INSERT INTO ticket(event_id,seat_id,recipient,series,number,amount,issued_at,issue_id)
                    VALUES($e,$s,$r,$series,$n,$a,$time,$issue)
                    ""","$e",eventId,"$s",sid,"$r",recipient,"$series",series,"$n",n,
                    "$a",actualAmount,"$time",timestamp,"$issue",issueId);
                insert.Transaction=tx; insert.ExecuteNonQuery();
                using var state=Command(c,"INSERT INTO ticket_state(ticket_id,state) VALUES(last_insert_rowid(),$state)","$state",reserved?"reserved":"issued");
                state.Transaction=tx;state.ExecuteNonQuery();
                if(n<long.MaxValue) n++;
            }
            AddRecipient(c,tx,recipient);tx.Commit();return issueId;
        }
        catch(SqliteException ex) when(ex.SqliteErrorCode==19)
        { throw new TicketException("Серия и номер уже использованы. Выдача отменена целиком; выберите следующий номер или другую серию."); }
        catch(SqliteException ex) when(ex.SqliteErrorCode is 5 or 6)
        { throw new TicketException("База занята другим окном программы. Повторите действие."); }
    }

    public void Return(IReadOnlyCollection<long> tickets)
    {
        if(tickets.Count==0 || tickets.Distinct().Count()!=tickets.Count)
            throw new TicketException("Выберите выданные билеты без повторов.");
        using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        var time=DateTimeOffset.Now.ToString("O");
        foreach(var id in tickets)
        {
            using var cmd=Command(c,"UPDATE ticket SET returned_at=$time WHERE id=$id AND returned_at IS NULL",
                "$time",time,"$id",id);
            cmd.Transaction=tx;
            if(cmd.ExecuteNonQuery()!=1) throw new TicketException("Билет уже возвращён или не найден. Возврат отменён целиком.");
        }
        tx.Commit();
    }

    public void Backup(string destination)
    {
        if(System.IO.Path.GetFullPath(destination)==Path) throw new TicketException("Копия должна сохраняться в другой файл.");
        using var source=Open();
        using var target=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=destination,Pooling=false}.ToString());
        target.Open(); source.BackupDatabase(target);
    }
}
