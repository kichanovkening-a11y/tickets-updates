using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    public void EditShow050(long eventId,string title,TimeSpan time)
    {
        title=Required(title,"Название спектакля");
        if(time<TimeSpan.Zero||time>=TimeSpan.FromDays(1)||time.Ticks%TimeSpan.TicksPerSecond!=0)
            throw new TicketException("Укажите время от 00:00:00 до 23:59:59.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        RequireWritableEvent(c,tx,eventId);
        using var query=Command(c,"SELECT starts_at FROM event WHERE id=$id","$id",eventId);query.Transaction=tx;
        var date=DateTime.Parse((string)query.ExecuteScalar()!,CultureInfo.InvariantCulture).Date+time;
        if(date<DateTime.Now)throw new TicketException("Новое время показа не должно быть в прошлом.");
        if(IsCatalogHidden(c,tx,"performance",CatalogText.Key(title)))throw new TicketException("Спектакль с таким названием находится в архиве.");
        try
        {
            using var update=Command(c,"UPDATE event SET title=$title,starts_at=$date,legacy_date=$display WHERE id=$id",
                "$title",title,"$date",date.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture),
                "$display",date.ToString("dd.MM.yyyy HH:mm:ss",CultureInfo.InvariantCulture),"$id",eventId);
            update.Transaction=tx;update.ExecuteNonQuery();
            using var performance=Command(c,"INSERT OR IGNORE INTO performance(title,name_key) VALUES($title,$key)",
                "$title",title,"$key",title.Normalize().ToUpperInvariant());performance.Transaction=tx;performance.ExecuteNonQuery();
            tx.Commit();
        }
        catch(SqliteException ex) when(ex.SqliteErrorCode==19&&ex.Message.Contains("event_slot_conflict"))
        {throw new TicketException("На этой сцене уже есть показ в выбранную дату и время.");}
    }
}
