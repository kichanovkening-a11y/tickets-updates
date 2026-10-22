using Microsoft.Data.Sqlite;
namespace Tickets.Core;

public sealed partial class Store
{
    // Retire the requested sector from working schemes without deleting ticket history.
    // This reads the stored hall only; no scan or replacement numbering is used.
    private void EnsureRetiredHallSectors021()
    {
        using var c=Open();
        var keys=new List<string>();
        using(var q=Command(c,"SELECT CAST(z.venue_id AS TEXT)||':'||CAST(z.id AS TEXT) FROM sector z JOIN venue v ON v.id=z.venue_id WHERE v.name=$n AND z.name='Экстрим партер' AND NOT EXISTS(SELECT 1 FROM catalog_hidden vh WHERE vh.kind='venue' AND vh.entity_key=CAST(v.id AS TEXT)) AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(z.venue_id AS TEXT)||':'||CAST(z.id AS TEXT))","$n",VenueLayouts019.RussianSong))
        {using var r=q.ExecuteReader();while(r.Read())keys.Add(r.GetString(0));}
        if(keys.Count==0)return;
        var backup=Path+".before-v021-sectors.db";if(!File.Exists(backup))Backup(backup);
        using var tx=c.BeginTransaction(deferred:false);
        foreach(var key in keys)HideCatalogEntry(c,tx,"sector",key);
        tx.Commit();
    }
}
