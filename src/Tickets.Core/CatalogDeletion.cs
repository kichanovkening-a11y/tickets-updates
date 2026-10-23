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

    public sealed record PerformanceDeletionInfo(int Shows,int Issues,int Tickets);
    public PerformanceDeletionInfo PerformanceDeletionSummary(long id)
    {
        using var c=Open();
        using var title=Command(c,"SELECT title FROM performance WHERE id=$i","$i",id);
        var name=title.ExecuteScalar() as string??throw new TicketException("Спектакль не найден.");
        var key=CatalogText.Key(name);
        var events=new List<long>();using(var q=Command(c,"SELECT id,title FROM event")){using var r=q.ExecuteReader();while(r.Read())if(CatalogText.Key(r.GetString(1))==key)events.Add(r.GetInt64(0));}
        var issues=0;var tickets=0;
        foreach(var e in events)
        {
            using var q=Command(c,"SELECT (SELECT count(*) FROM issue_batch WHERE event_id=$e),(SELECT count(*) FROM ticket WHERE event_id=$e)","$e",e);
            using var r=q.ExecuteReader();r.Read();issues+=r.GetInt32(0);tickets+=r.GetInt32(1);
        }
        return new(events.Count,issues,tickets);
    }

    // The watermark is persisted separately because issue_batch uses INTEGER PRIMARY KEY
    // without AUTOINCREMENT. Deleting the last issue must never reuse its document number.
    private static long AllocateIssueId(SqliteConnection c,SqliteTransaction tx,bool allocate=true)
    {
        using var q=Command(c,"SELECT max(coalesce((SELECT max(id) FROM issue_batch),0),coalesce((SELECT CAST(value AS INTEGER) FROM document_setting WHERE name='invoice-last-id'),0))");q.Transaction=tx;
        var last=Convert.ToInt64(q.ExecuteScalar());
        if(allocate&&last==long.MaxValue)throw new TicketException("Номера накладных закончились.");
        var value=allocate?last+1:last;
        using var save=Command(c,"INSERT INTO document_setting(name,value) VALUES('invoice-last-id',$v) ON CONFLICT(name) DO UPDATE SET value=excluded.value","$v",value.ToString(CultureInfo.InvariantCulture));save.Transaction=tx;save.ExecuteNonQuery();return value;
    }

    public void DeletePerformance(long id)=>DeletePerformanceOrEvents(id,null);

    /// <summary>Explicit event purge, leaving shared halls, seat geometry and price plans intact.</summary>
    public void DeleteEventsPermanently(IReadOnlyList<long> eventIds)
    {
        var ids=eventIds.Distinct().ToArray();if(ids.Length==0)return;
        var available=Events(true).Select(e=>e.Id).ToHashSet();
        if(ids.Any(id=>!available.Contains(id)))throw new TicketException("Показ не найден. Обновите список.");
        DeletePerformanceOrEvents(0,ids);
        using var c=Open();using var compact=Command(c,"VACUUM");
        try{compact.ExecuteNonQuery();}
        catch(SqliteException ex) when(ex.SqliteErrorCode is 5 or 6){throw new TicketException("Данные удалены, резервная копия сохранена. Уплотнение базы отложено: файл занят другим процессом.");}
    }

    private void DeletePerformanceOrEvents(long id,IReadOnlyList<long>? selectedEvents)
    {
        // A complete recoverable copy is made before deleting any linked records/files.
        if(selectedEvents is null)PerformanceDeletionSummary(id);
        var backupDirectory=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"Backups","delete-performance-"+id+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(backupDirectory);Backup(System.IO.Path.Combine(backupDirectory,"tickets.db"));
        var moved=new List<(string Source,string Backup)>();
        using var c=Open();EnsureInvoiceMergeTable(c);using var tx=c.BeginTransaction(deferred:false);
        EnsureEventPlanSources(c,tx);
        try
        {
            using var title=Command(c,"SELECT title FROM performance WHERE id=$i","$i",id);title.Transaction=tx;
            var name=selectedEvents is null?title.ExecuteScalar() as string??throw new TicketException("Спектакль не найден."):"";var key=CatalogText.Key(name);
            using(var create=Command(c,"CREATE TEMP TABLE deleting_events(id INTEGER PRIMARY KEY); CREATE TEMP TABLE deleting_issues(id INTEGER PRIMARY KEY)")){create.Transaction=tx;create.ExecuteNonQuery();}
            var events=new List<long>();using(var q=Command(c,"SELECT id,title FROM event")){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())if(selectedEvents is null?CatalogText.Key(r.GetString(1))==key:selectedEvents.Contains(r.GetInt64(0)))events.Add(r.GetInt64(0));}
            foreach(var e in events){using var add=Command(c,"INSERT INTO deleting_events VALUES($e)","$e",e);add.Transaction=tx;add.ExecuteNonQuery();}
            using(var add=Command(c,"INSERT INTO deleting_issues SELECT id FROM issue_batch WHERE event_id IN(SELECT id FROM deleting_events)")){add.Transaction=tx;add.ExecuteNonQuery();}
            var journalPath=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"journal-columns-by-event.json");
            if(File.Exists(journalPath))
            {
                System.Text.Json.Nodes.JsonObject? layouts=null;
                try{layouts=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath)) as System.Text.Json.Nodes.JsonObject;}catch(System.Text.Json.JsonException){}
                if(layouts is not null&&events.Any(e=>layouts.ContainsKey(e.ToString(CultureInfo.InvariantCulture))))
                {
                    foreach(var e in events)layouts.Remove(e.ToString(CultureInfo.InvariantCulture));
                    var saved=System.IO.Path.Combine(backupDirectory,"journal-columns-by-event.json");File.Move(journalPath,saved);moved.Add((journalPath,saved));
                    File.WriteAllText(journalPath+".tmp",layouts.ToJsonString());File.Move(journalPath+".tmp",journalPath,true);
                }
            }
            // Shared packages contain snapshots rather than issue rows; purge those tied
            // to the deleted shows too, without touching packages of other shows.
            using(var exists=Command(c,"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='recipient_package'"))
            {
                exists.Transaction=tx;
                if(Convert.ToInt32(exists.ExecuteScalar())>0)
                {
                    var packages=new List<long>();
                    using(var read=Command(c,"SELECT id,snapshot FROM recipient_package"))
                    {
                        read.Transaction=tx;using var r=read.ExecuteReader();
                        while(r.Read())
                        {
                            var package=System.Text.Json.JsonSerializer.Deserialize<RecipientPackageInfo>(r.GetString(1));
                            if(package?.Issues.Any(member=>events.Contains(member.EventId))==true)packages.Add(r.GetInt64(0));
                        }
                    }
                    foreach(var package in packages)
                    {
                        using var remove=Command(c,"DELETE FROM recipient_package_document WHERE package_id=$p; DELETE FROM recipient_package WHERE id=$p;","$p",package);
                        remove.Transaction=tx;remove.ExecuteNonQuery();
                    }
                }
            }
            AllocateIssueId(c,tx,false);
            var issues=new List<long>();using(var q=Command(c,"SELECT id FROM deleting_issues")){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())issues.Add(r.GetInt64(0));}
            foreach(var issue in issues)
            {
                var paths=new[]{InvoiceDocumentPath(issue),IssueSpecificationDocumentPath(issue)}.Concat(IssueDocumentKinds.All.Select(kind=>IssueDocumentPath(issue,kind)));
                foreach(var path in paths.Where(File.Exists))
                {
                    var copy=System.IO.Path.Combine(backupDirectory,"Documents",System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path))!,System.IO.Path.GetFileName(path));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copy)!);File.Move(path,copy);moved.Add((path,copy));
                }
            }
            using(var delete=Command(c,"""
                DELETE FROM invoice_merge WHERE source_issue_id IN(SELECT id FROM deleting_issues) OR target_issue_id IN(SELECT id FROM deleting_issues);
                DELETE FROM ticket_state WHERE ticket_id IN(SELECT id FROM ticket WHERE event_id IN(SELECT id FROM deleting_events));
                DELETE FROM ticket WHERE event_id IN(SELECT id FROM deleting_events);
                DELETE FROM issue_custom_form WHERE issue_id IN(SELECT id FROM deleting_issues);
                DELETE FROM issue_contract_number WHERE issue_id IN(SELECT id FROM deleting_issues);
                DELETE FROM issue_contract_date WHERE issue_id IN(SELECT id FROM deleting_issues);
                DELETE FROM issue_organization_details WHERE issue_id IN(SELECT id FROM deleting_issues);
                DELETE FROM issue_document WHERE event_id IN(SELECT id FROM deleting_events) OR issue_id IN(SELECT id FROM deleting_issues);
                DELETE FROM issue_batch WHERE id IN(SELECT id FROM deleting_issues);
                DELETE FROM event_plan_source WHERE event_id IN(SELECT id FROM deleting_events);
                DELETE FROM event_seat_block WHERE event_id IN(SELECT id FROM deleting_events);
                DELETE FROM event_price WHERE event_id IN(SELECT id FROM deleting_events);
                DELETE FROM event WHERE id IN(SELECT id FROM deleting_events);
                DELETE FROM performance WHERE id=$i AND $i>0;
                ""","$i",id)){delete.Transaction=tx;delete.ExecuteNonQuery();}
            // Keep a tombstone to prevent seed/import routines from silently restoring it.
            if(selectedEvents is null)HideCatalogEntry(c,tx,"performance",key);
            // Remove only hidden performance shells made orphaned by this purge.
            if(selectedEvents is not null)
            {
                var candidates=new List<(long Id,string Title)>();
                using(var q=Command(c,"SELECT id,title FROM performance")){q.Transaction=tx;using var r=q.ExecuteReader();while(r.Read())candidates.Add((r.GetInt64(0),r.GetString(1)));}
                var shells=candidates.Where(p=>IsCatalogHidden(c,tx,"performance",CatalogText.Key(p.Title))&&!EventsTitleExists(c,tx,p.Title)).Select(p=>p.Id).ToArray();
                foreach(var shell in shells){using var q=Command(c,"DELETE FROM performance WHERE id=$i","$i",shell);q.Transaction=tx;q.ExecuteNonQuery();}
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();foreach(var item in moved.AsEnumerable().Reverse()){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(item.Source)!);File.Move(item.Backup,item.Source,true);}
            throw;
        }
    }

    private static bool EventsTitleExists(SqliteConnection c,SqliteTransaction tx,string title)
    {
        using var q=Command(c,"SELECT title FROM event");q.Transaction=tx;using var r=q.ExecuteReader();
        while(r.Read())if(CatalogText.Key(r.GetString(0))==CatalogText.Key(title))return true;
        return false;
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
