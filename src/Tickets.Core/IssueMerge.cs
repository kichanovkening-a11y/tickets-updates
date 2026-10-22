using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record InvoiceMergeSource(long SourceIssueId,long TargetIssueId,string MergedAt);

public sealed partial class Store
{
    private static void EnsureInvoiceMergeTable(SqliteConnection c)
    {
        using var command=Command(c,"""
            CREATE TABLE IF NOT EXISTS invoice_merge(
                source_issue_id INTEGER PRIMARY KEY REFERENCES issue_batch(id),
                target_issue_id INTEGER NOT NULL REFERENCES issue_batch(id),
                merged_at TEXT NOT NULL,CHECK(source_issue_id<>target_issue_id));
            CREATE INDEX IF NOT EXISTS invoice_merge_target ON invoice_merge(target_issue_id);
            """);command.ExecuteNonQuery();
    }

    private static Dictionary<long,long> InvoiceMergeLinks(SqliteConnection c,SqliteTransaction? tx=null)
    {
        using var read=Command(c,"SELECT source_issue_id,target_issue_id FROM invoice_merge");read.Transaction=tx;
        using var rows=read.ExecuteReader();var links=new Dictionary<long,long>();
        while(rows.Read())links.Add(rows.GetInt64(0),rows.GetInt64(1));return links;
    }
    private static long ResolveInvoiceId(long id,IReadOnlyDictionary<long,long> links)
    {
        var visited=new HashSet<long>();
        while(links.TryGetValue(id,out var target))
        {if(!visited.Add(id))throw new TicketException("Повреждены ссылки объединённых накладных.");id=target;}
        return id;
    }
    public long ResolveInvoiceId(long issueId)
    {using var c=Open();EnsureInvoiceMergeTable(c);return ResolveInvoiceId(issueId,InvoiceMergeLinks(c));}

    public IReadOnlyList<InvoiceMergeSource> GetInvoiceMergeSources(long eventId,long issueId)
    {
        using var c=Open();EnsureInvoiceMergeTable(c);
        using var command=Command(c,"""
            WITH RECURSIVE members(id) AS (
                SELECT id FROM issue_batch WHERE id=$i AND event_id=$e
                UNION SELECT m.source_issue_id FROM invoice_merge m JOIN members b ON m.target_issue_id=b.id)
            SELECT m.source_issue_id,m.target_issue_id,m.merged_at FROM invoice_merge m
            JOIN members b ON b.id=m.source_issue_id ORDER BY m.merged_at,m.source_issue_id
            ""","$i",issueId,"$e",eventId);
        using var rows=command.ExecuteReader();var result=new List<InvoiceMergeSource>();
        while(rows.Read())result.Add(new(rows.GetInt64(0),rows.GetInt64(1),rows.GetString(2)));return result;
    }

    private IReadOnlyList<TicketInfo> InvoiceTickets(long eventId,long issueId)
    {
        using var c=Open();EnsureInvoiceMergeTable(c);
        // Follow only this invoice's sources: an earlier merged invoice remains
        // readable after it becomes a source of a later merge.
        using var read=Command(c,"""
            WITH RECURSIVE members(id) AS (
                SELECT id FROM issue_batch WHERE id=$i AND event_id=$e
                UNION SELECT m.source_issue_id FROM invoice_merge m JOIN members b ON m.target_issue_id=b.id)
            SELECT id FROM members
            ""","$i",issueId,"$e",eventId);
        using var rows=read.ExecuteReader();var members=new HashSet<long>();
        while(rows.Read())members.Add(rows.GetInt64(0));
        return History(eventId).Where(t=>members.Contains(t.IssueId))
            .OrderBy(t=>t.Series,StringComparer.Ordinal).ThenBy(t=>t.Number).ThenBy(t=>t.Id).ToList();
    }
    private List<IssueInfo> MergeIssueSummaries(long eventId)
    {
        using var c=Open();EnsureInvoiceMergeTable(c);var links=InvoiceMergeLinks(c);
        var headers=new Dictionary<long,(string Recipient,string IssuedAt,string Sender)>();
        using(var command=Command(c,"SELECT id,recipient,issued_at,sender FROM issue_batch WHERE event_id=$e","$e",eventId))
        using(var rows=command.ExecuteReader())
            while(rows.Read())headers.Add(rows.GetInt64(0),(rows.GetString(1),rows.GetString(2),rows.GetString(3)));
        var tickets=History(eventId);
        return tickets.GroupBy(t=>ResolveInvoiceId(t.IssueId,links)).Select(g=>{
            var original=headers[g.Key];
            return new IssueInfo(g.Key,eventId,original.Recipient,original.IssuedAt,g.Count(),g.Count(t=>t.ReturnedAt is not null),
                g.Sum(t=>(decimal)t.Amount),g.Count(t=>t.ReturnedAt is null&&t.IsReserved),g.Count(t=>t.ReturnedAt is null&&t.IsPrinted)){Sender=original.Sender};
        }).OrderByDescending(i=>i.Id).ToList();
    }

    // Allocate a new global number, retaining the first source header and organization snapshots.
    // Tickets and historical invoice records never change ownership during a merge.
    public long MergeInvoices(long eventId,IReadOnlyList<long> issueIds)
    {
        ArgumentNullException.ThrowIfNull(issueIds);
        if(issueIds.Count<2||issueIds.Any(id=>id<=0)||issueIds.Distinct().Count()!=issueIds.Count)
            throw new TicketException("Выберите не менее двух разных накладных.");
        try
        {
            using var c=Open();EnsureInvoiceMergeTable(c);using var tx=c.BeginTransaction(deferred:false);
            RequireActiveEvent(c,tx,eventId);var links=InvoiceMergeLinks(c,tx);
            string? recipient=null,sender=null;
            foreach(var id in issueIds)
            {
                if(ResolveInvoiceId(id,links)!=id)throw new TicketException("Одна из накладных уже объединена. Обновите журнал и выберите действующую накладную.");
                using(var read=Command(c,"""
                    WITH RECURSIVE members(id) AS (SELECT $i UNION SELECT m.source_issue_id FROM invoice_merge m JOIN members b ON m.target_issue_id=b.id)
                    SELECT b.recipient,b.sender,b.event_id FROM issue_batch b JOIN members m ON m.id=b.id
                    ""","$i",id))
                {
                    read.Transaction=tx;using var row=read.ExecuteReader();var found=false;
                    while(row.Read())
                    {
                        found=true;
                        if(row.GetInt64(2)!=eventId)throw new TicketException("Все накладные должны относиться к выбранному показу.");
                        var nextRecipient=CatalogText.Key(row.GetString(0));var nextSender=CatalogText.Key(row.GetString(1));
                        if(recipient is not null&&(recipient!=nextRecipient||sender!=nextSender))
                            throw new TicketException("Объединение возможно только для одного получателя и одного отправителя.");
                        recipient=nextRecipient;sender=nextSender;
                    }
                    if(!found)throw new TicketException("Все накладные должны относиться к выбранному показу.");
                }
                using var check=Command(c,"""
                    WITH RECURSIVE members(id) AS (SELECT $i UNION SELECT m.source_issue_id FROM invoice_merge m JOIN members b ON m.target_issue_id=b.id)
                    SELECT count(*),sum(CASE WHEN coalesce(ts.state,'issued')='reserved' THEN 1 ELSE 0 END)
                    FROM ticket t JOIN members b ON b.id=t.issue_id LEFT JOIN ticket_state ts ON ts.ticket_id=t.id
                    WHERE t.event_id=$e AND t.returned_at IS NULL
                    ""","$i",id,"$e",eventId);check.Transaction=tx;using var state=check.ExecuteReader();state.Read();
                if(state.GetInt64(0)==0)throw new TicketException("Выберите действующие накладные с билетами на руках.");
                if(state.GetInt64(1)>0)throw new TicketException("Сначала подтвердите все брони: объединять можно только выданные накладные.");
            }
            long target;
            using(var create=Command(c,"""
                INSERT INTO issue_batch(event_id,recipient,issued_at,series,title,venue,event_date,sender)
                SELECT event_id,recipient,issued_at,series,title,venue,event_date,sender FROM issue_batch WHERE id=$source;
                SELECT last_insert_rowid();
                ""","$source",issueIds[0]))
            {create.Transaction=tx;target=Convert.ToInt64(create.ExecuteScalar());}
            using(var snapshot=Command(c,"""
                INSERT INTO issue_organization_details(issue_id,role,details)
                SELECT $target,role,details FROM issue_organization_details WHERE issue_id=$source
                ""","$target",target,"$source",issueIds[0]))
            {snapshot.Transaction=tx;snapshot.ExecuteNonQuery();}
            CopyCustomIssueForms(c,tx,issueIds[0],target);
            var timestamp=DateTimeOffset.Now.ToString("O");
            foreach(var source in issueIds)
            {
                using var write=Command(c,"INSERT INTO invoice_merge(source_issue_id,target_issue_id,merged_at) VALUES($s,$t,$at)","$s",source,"$t",target,"$at",timestamp);
                write.Transaction=tx;write.ExecuteNonQuery();
            }
            tx.Commit();return target;
        }
        catch(SqliteException ex) when(ex.SqliteErrorCode is 5 or 6)
        {throw new TicketException("База занята другим окном программы. Повторите действие.");}
    }
}
