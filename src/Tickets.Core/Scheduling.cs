using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public static class CatalogText
{
    public static string Key(string text)=>Regex.Replace(text.Trim().Normalize().ToUpperInvariant().Replace('Ё','Е'),@"\s+"," ");
    public static TimeSpan Time(string hours,string minutes,string seconds)
    {
        if(!int.TryParse(hours,out var h)||h<0||h>23||!int.TryParse(minutes,out var m)||m<0||m>59||!int.TryParse(seconds,out var s)||s<0||s>59)
            throw new TicketException("Введите время: часы 00–23, минуты и секунды 00–59.");
        return new(h,m,s);
    }
}
public sealed record PerformanceShows(string Title,IReadOnlyList<EventInfo> Shows);
public static class EventSchedule
{
    private static DateTime Start(EventInfo e)=>DateTime.Parse(e.SortDate,CultureInfo.InvariantCulture);
    public static List<PerformanceShows> Groups(IEnumerable<EventInfo> events,bool past,DateTime now,string search="")
    {
        var query=CatalogText.Key(search);
        return events.Where(e=>(Start(e)<now)==past&&SearchText.Matches(e.Title+" "+e.Venue+" "+e.Date,query))
            .GroupBy(e=>CatalogText.Key(e.Title)).Select(g=>new PerformanceShows(g.First().Title,
                (past?g.OrderByDescending(Start):g.OrderBy(Start)).ToList()))
            .OrderBy(g=>past?DateTime.MaxValue-Start(g.Shows[0]):Start(g.Shows[0])-DateTime.MinValue).ToList();
    }
}
public sealed partial class Store
{
    private void EnsureScheduling()
    {
        using var c=Open();using var version=Command(c,"PRAGMA user_version");if(Convert.ToInt32(version.ExecuteScalar())>=4)return;
        var copy=Path+".before-v4.db";if(!File.Exists(copy))
        {using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy,Pooling=false}.ToString());dest.Open();c.BackupDatabase(dest);}
        using var tx=c.BeginTransaction(deferred:false);
        using var cmd=Command(c,"""
            CREATE TABLE IF NOT EXISTS recipient(id INTEGER PRIMARY KEY,name TEXT NOT NULL,name_key TEXT NOT NULL UNIQUE);
            CREATE TRIGGER IF NOT EXISTS event_slot_insert BEFORE INSERT ON event
            WHEN EXISTS(SELECT 1 FROM event WHERE venue_id=NEW.venue_id AND datetime(starts_at)=datetime(NEW.starts_at))
            BEGIN SELECT RAISE(ABORT,'event_slot_conflict'); END;
            CREATE TRIGGER IF NOT EXISTS event_slot_update BEFORE UPDATE OF venue_id,starts_at ON event
            WHEN (NEW.venue_id!=OLD.venue_id OR NEW.starts_at!=OLD.starts_at)
              AND EXISTS(SELECT 1 FROM event WHERE id!=NEW.id AND venue_id=NEW.venue_id AND datetime(starts_at)=datetime(NEW.starts_at))
            BEGIN SELECT RAISE(ABORT,'event_slot_conflict'); END;
            PRAGMA user_version=4;
            """);cmd.Transaction=tx;cmd.ExecuteNonQuery();
        var names=LegacyRecipients.Names.ToList();using(var existing=Command(c,"SELECT DISTINCT recipient FROM ticket"))
        {existing.Transaction=tx;using var read=existing.ExecuteReader();while(read.Read())names.Add(read.GetString(0));}
        foreach(var name in names.Where(n=>!string.IsNullOrWhiteSpace(n)))AddRecipient(c,tx,name.Trim());tx.Commit();
    }
    private static void AddRecipient(SqliteConnection c,SqliteTransaction tx,string name)
    {
        using var cmd=Command(c,"INSERT OR IGNORE INTO recipient(name,name_key) VALUES($n,$k)","$n",name,"$k",CatalogText.Key(name));cmd.Transaction=tx;cmd.ExecuteNonQuery();
    }
    public List<string> Recipients(string search="")
    {
        using var c=Open();using var cmd=Command(c,"SELECT name FROM recipient ORDER BY name COLLATE NOCASE");using var read=cmd.ExecuteReader();var names=new List<string>();
        var query=CatalogText.Key(search);while(read.Read()){var name=read.GetString(0);if(SearchText.Matches(name,query))names.Add(name);}return names;
    }
}
