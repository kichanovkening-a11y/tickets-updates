using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureCatalogDeletion()
    {
        using var c=Open();
        using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS catalog_hidden(kind TEXT NOT NULL,entity_key TEXT NOT NULL,PRIMARY KEY(kind,entity_key))");
        cmd.ExecuteNonQuery();
    }

    private HashSet<string> HiddenCatalogKeys(string kind)
    {
        using var c=Open();using var cmd=Command(c,"SELECT entity_key FROM catalog_hidden WHERE kind=$k","$k",kind);
        using var r=cmd.ExecuteReader();var keys=new HashSet<string>(StringComparer.Ordinal);
        while(r.Read())keys.Add(r.GetString(0));return keys;
    }

    private static bool IsCatalogHidden(SqliteConnection c,SqliteTransaction? tx,string kind,string key)
    {
        using var cmd=Command(c,"SELECT count(*) FROM catalog_hidden WHERE kind=$k AND entity_key=$e","$k",kind,"$e",key);
        cmd.Transaction=tx;return Convert.ToInt64(cmd.ExecuteScalar())!=0;
    }

    private static void RequireActiveVenue(SqliteConnection c,SqliteTransaction tx,long venueId)
    {
        using var cmd=Command(c,"SELECT count(*) FROM venue WHERE id=$v","$v",venueId);cmd.Transaction=tx;
        if(Convert.ToInt64(cmd.ExecuteScalar())!=1||IsCatalogHidden(c,tx,"venue",venueId.ToString(CultureInfo.InvariantCulture)))
            throw new TicketException("Выберите действующий зал.");
    }

    public bool IsEventArchived(long eventId)
    {
        using var c=Open();
        long venueId;string title;
        using(var cmd=Command(c,"SELECT venue_id,title FROM event WHERE id=$e","$e",eventId))
        {
            using var r=cmd.ExecuteReader();
            if(!r.Read())throw new TicketException("Показ не найден.");
            venueId=r.GetInt64(0);title=r.GetString(1);
        }
        return IsCatalogHidden(c,null,"venue",venueId.ToString(CultureInfo.InvariantCulture))
            ||IsCatalogHidden(c,null,"performance",CatalogText.Key(title));
    }

    private static void RequireActiveEvent(SqliteConnection c,SqliteTransaction tx,long eventId)
    {
        long venueId;string title;
        using(var cmd=Command(c,"SELECT venue_id,title FROM event WHERE id=$e","$e",eventId))
        {
            cmd.Transaction=tx;using var r=cmd.ExecuteReader();
            if(!r.Read())throw new TicketException("Показ не найден.");
            venueId=r.GetInt64(0);title=r.GetString(1);
        }
        RequireActiveVenue(c,tx,venueId);
        if(IsCatalogHidden(c,tx,"performance",CatalogText.Key(title)))throw new TicketException("Спектакль удалён из справочника.");
    }

    private static void HideCatalogEntry(SqliteConnection c,SqliteTransaction tx,string kind,string key)
    {
        using var cmd=Command(c,"INSERT OR IGNORE INTO catalog_hidden(kind,entity_key) VALUES($k,$e)","$k",kind,"$e",key);
        cmd.Transaction=tx;cmd.ExecuteNonQuery();
    }

    public void DeletePerformance(long id)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var cmd=Command(c,"SELECT title FROM performance WHERE id=$i","$i",id);cmd.Transaction=tx;
        var title=cmd.ExecuteScalar() as string??throw new TicketException("Спектакль не найден.");
        HideCatalogEntry(c,tx,"performance",CatalogText.Key(title));tx.Commit();
    }

    public void DeleteVenue(long id)=>DeleteCatalogId("venue",id,"Зал не найден.");
    public void DeletePlan(long id)=>DeleteCatalogId("plan",id,"Расценовка не найдена.");

    private void DeleteCatalogId(string kind,long id,string error)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var table=kind=="venue"?"venue":"pricing_plan";
        using var cmd=Command(c,$"SELECT count(*) FROM {table} WHERE id=$i","$i",id);cmd.Transaction=tx;
        if(Convert.ToInt64(cmd.ExecuteScalar())!=1)throw new TicketException(error);
        HideCatalogEntry(c,tx,kind,id.ToString(CultureInfo.InvariantCulture));tx.Commit();
    }

    public void DeleteRecipient(string name)
    {
        var key=CatalogText.Key(Required(name,"Получатель"));
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var cmd=Command(c,"SELECT name FROM recipient UNION SELECT recipient FROM issue_batch");cmd.Transaction=tx;
        var found=false;using(var r=cmd.ExecuteReader())while(r.Read())if(CatalogText.Key(r.GetString(0))==key){found=true;break;}
        if(!found)throw new TicketException("Получатель не найден.");
        HideCatalogEntry(c,tx,"recipient",key);tx.Commit();
    }
}
