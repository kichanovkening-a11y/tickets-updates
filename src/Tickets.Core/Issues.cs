using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureIssues()
    {
        using var c=Open();using var version=Command(c,"PRAGMA user_version");
        if(Convert.ToInt32(version.ExecuteScalar())>=3)return;
        var copy=Path+".before-v3.db";
        if(!File.Exists(copy))
        {
            using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy,Pooling=false}.ToString());
            dest.Open();c.BackupDatabase(dest);
        }
        using var tx=c.BeginTransaction(deferred:false);
        using var cmd=Command(c,"""
            CREATE TABLE issue_batch(id INTEGER PRIMARY KEY,event_id INTEGER NOT NULL REFERENCES event(id),
                recipient TEXT NOT NULL,issued_at TEXT NOT NULL,series TEXT NOT NULL,title TEXT NOT NULL,venue TEXT NOT NULL,event_date TEXT NOT NULL);
            ALTER TABLE ticket ADD COLUMN issue_id INTEGER REFERENCES issue_batch(id);
            INSERT INTO issue_batch(event_id,recipient,issued_at,series,title,venue,event_date)
                SELECT t.event_id,t.recipient,t.issued_at,t.series,e.title,v.name,e.legacy_date
                FROM ticket t JOIN event e ON e.id=t.event_id JOIN venue v ON v.id=e.venue_id
                GROUP BY t.event_id,t.recipient,t.issued_at,t.series ORDER BY min(t.id);
            UPDATE ticket SET issue_id=(SELECT b.id FROM issue_batch b
                WHERE b.event_id=ticket.event_id AND b.recipient=ticket.recipient AND b.issued_at=ticket.issued_at AND b.series=ticket.series ORDER BY b.id LIMIT 1);
            CREATE INDEX ticket_issue ON ticket(issue_id);
            CREATE INDEX issue_event ON issue_batch(event_id);
            PRAGMA user_version=3;
            """);cmd.Transaction=tx;cmd.ExecuteNonQuery();tx.Commit();
    }
    public List<IssueInfo> Issues(long eventId)=>MergeIssueSummaries(eventId);

    public InvoiceInfo Invoice(long eventId,long issueId)
    {
        using var c=Open();using var cmd=Command(c,"SELECT title,venue,event_date,recipient,issued_at,sender FROM issue_batch WHERE id=$i AND event_id=$e","$i",issueId,"$e",eventId);
        using var r=cmd.ExecuteReader();if(!r.Read())throw new TicketException("Выдача не найдена в этом мероприятии.");
        var values=Enumerable.Range(0,6).Select(r.GetString).ToArray();
        r.Close();
        return ApplyContractMetadata(new(issueId,eventId,values[0],values[1],values[2],values[3],values[4],InvoiceTickets(eventId,issueId),values[5]){SenderDetails=GetIssueOrganizationDetails(issueId,true),RecipientDetails=GetIssueOrganizationDetails(issueId,false)});
    }
}
