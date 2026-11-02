using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    // Operational policy is separate from catalog visibility used by external sales sync.
    public bool IsEventReadOnly(long eventId)
    {
        using var c=Open();
        return IsEventReadOnly(c,null,eventId);
    }

    private static bool IsEventReadOnly(SqliteConnection c,SqliteTransaction? tx,long eventId)
    {
        long venue;string title,startsAt;
        using(var query=Command(c,"SELECT venue_id,title,starts_at FROM event WHERE id=$e","$e",eventId))
        {
            query.Transaction=tx;using var row=query.ExecuteReader();
            if(!row.Read())throw new TicketException("Показ не найден.");
            venue=row.GetInt64(0);title=row.GetString(1);startsAt=row.GetString(2);
        }
        return DateTime.Parse(startsAt,CultureInfo.InvariantCulture)<DateTime.Now
            ||IsCatalogHidden(c,tx,"venue",venue.ToString(CultureInfo.InvariantCulture))
            ||IsCatalogHidden(c,tx,"performance",CatalogText.Key(title));
    }

    private void RequireWritableEvent(long eventId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        RequireWritableEvent(c,tx,eventId);
    }

    private static void RequireWritableEvent(SqliteConnection c,SqliteTransaction tx,long eventId)
    {
        if(IsEventReadOnly(c,tx,eventId))
            throw new TicketException("Архивный показ доступен только для просмотра и печати сохранённых документов.");
    }
}
