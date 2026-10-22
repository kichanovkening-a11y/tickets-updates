using Microsoft.Data.Sqlite;
namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureSaleStates()
    {
        using var c=Open();using var version=Command(c,"PRAGMA user_version");
        if(Convert.ToInt32(version.ExecuteScalar())>=8)return;
        var backup=Path+".before-v8.db";
        if(!File.Exists(backup)){using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=backup,Pooling=false}.ToString());dest.Open();c.BackupDatabase(dest);}
        using var tx=c.BeginTransaction(deferred:false);
        using var create=Command(c,"""
            CREATE TABLE IF NOT EXISTS event_seat_block(event_id INTEGER NOT NULL REFERENCES event(id),seat_id INTEGER NOT NULL REFERENCES seat(id),changed_at TEXT NOT NULL,PRIMARY KEY(event_id,seat_id));
            PRAGMA user_version=8;
            """);create.Transaction=tx;create.ExecuteNonQuery();tx.Commit();
    }
    public void SetSeatsBlocked(long eventId,IReadOnlyCollection<long> ids,bool blocked)
    {
        if(ids.Count==0||ids.Distinct().Count()!=ids.Count)throw new TicketException("Выберите места без повторов.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);RequireActiveEvent(c,tx,eventId);
        foreach(var id in ids)
        {
            using var check=Command(c,"""
                SELECT count(*) FROM seat s JOIN event e ON e.venue_id=s.venue_id
                WHERE e.id=$e AND s.id=$s AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT)) AND NOT EXISTS(SELECT 1 FROM ticket t WHERE t.event_id=e.id AND t.seat_id=s.id AND t.returned_at IS NULL)
                ""","$e",eventId,"$s",id);check.Transaction=tx;
            if(Convert.ToInt64(check.ExecuteScalar())!=1)throw new TicketException("Заблокировать или вернуть в продажу можно только невыданные места этого показа. Изменения отменены целиком.");
            using var save=blocked?Command(c,"INSERT OR IGNORE INTO event_seat_block(event_id,seat_id,changed_at) VALUES($e,$s,$time)","$e",eventId,"$s",id,"$time",DateTimeOffset.Now.ToString("O")):
                Command(c,"DELETE FROM event_seat_block WHERE event_id=$e AND seat_id=$s","$e",eventId,"$s",id);
            save.Transaction=tx;save.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
