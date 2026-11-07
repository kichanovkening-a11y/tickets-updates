using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record HomeCard(string Page, long PerformanceId, bool Hidden = false);

public sealed partial class Store
{
    private static void ReleaseDeletedPosters(SqliteConnection db, SqliteTransaction transaction, IReadOnlySet<long> ids)
    {
        if (ids.Count == 0) return;
        using var read = Command(db, "SELECT value FROM document_setting WHERE name='home-repertoire'");
        read.Transaction = transaction;
        if (read.ExecuteScalar() is not string json) return;
        var cards = JsonSerializer.Deserialize<List<HomeCard>>(json)!;
        if (cards.RemoveAll(card => ids.Contains(card.PerformanceId)) == 0) return;
        using var write = Command(db, "UPDATE document_setting SET value=$value WHERE name='home-repertoire'",
            "$value", JsonSerializer.Serialize(cards));
        write.Transaction = transaction;
        write.ExecuteNonQuery();
    }

    public string HomeLayout => DocumentSetting("home-repertoire", "");
    public void SaveHomeLayout(string layout)
    {
        using var db = Open();
        using var cmd = Command(db, "INSERT INTO document_setting(name,value) VALUES('home-repertoire',$layout) ON CONFLICT(name) DO UPDATE SET value=excluded.value", "$layout", layout);
        cmd.ExecuteNonQuery();
    }
}
