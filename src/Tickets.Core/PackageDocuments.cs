using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public static class IssueDocumentKinds
{
    public const string Contract="contract",Act="act",Waybill="ttn",Bill="bill";
    public static IReadOnlyList<string> All { get; }=Array.AsReadOnly(new[]{Contract,Act,Waybill,Bill});
    public static string Name(string kind)=>kind switch {Contract=>"Договор",Act=>"Акт",Waybill=>"ТТН",Bill=>"Счёт",_=>throw new TicketException("Неизвестный вид документа.")};
    public static string DefaultTemplate(string kind)=>Name(kind)+" № {issueId}\nДата: {date}\nОтправитель: {sender}\nПолучатель: {recipient}\nМероприятие: {event}\nДата мероприятия: {eventDate}\nПлощадка: {venue}\n\nПередача билетов по указанной выдаче.\n\n{summary}\n{total}\n\nПередал: ____________________    Получил: ____________________";
}

public sealed record IssueDocumentInfo(string Kind,InvoiceInfo Invoice,string Template,string Body);

public sealed partial class Store
{
    private void EnsurePackageDocuments()
    {
        using var c=Open();using var version=Command(c,"PRAGMA user_version");if(Convert.ToInt32(version.ExecuteScalar())>=7)return;
        var copy=Path+".before-v7.db";if(!File.Exists(copy))
        {using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy,Pooling=false}.ToString());dest.Open();c.BackupDatabase(dest);}
        using var tx=c.BeginTransaction(deferred:false);
        using(var cmd=Command(c,"""
            CREATE TABLE IF NOT EXISTS document_template(kind TEXT PRIMARY KEY,template TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS issue_document(event_id INTEGER NOT NULL,issue_id INTEGER NOT NULL,kind TEXT NOT NULL,template TEXT NOT NULL,PRIMARY KEY(event_id,issue_id,kind));
            CREATE TABLE IF NOT EXISTS sender_directory(sender_name TEXT PRIMARY KEY);
            INSERT OR IGNORE INTO sender_directory(sender_name) SELECT sender FROM issue_batch WHERE trim(sender)<>'';
            INSERT OR IGNORE INTO sender_directory(sender_name) SELECT sender FROM specification WHERE trim(sender)<>'';
            INSERT OR IGNORE INTO sender_directory(sender_name) SELECT value FROM document_setting WHERE name='sender' AND trim(value)<>'';
            PRAGMA user_version=7;
            """)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        RememberSender(c,tx,DefaultSender);
        foreach(var kind in IssueDocumentKinds.All)
        {using var cmd=Command(c,"INSERT OR IGNORE INTO document_template(kind,template) VALUES($k,$t)","$k",kind,"$t",IssueDocumentKinds.DefaultTemplate(kind));cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        tx.Commit();
    }
    private static void RememberSender(SqliteConnection c,SqliteTransaction tx,string sender)
    {using var cmd=Command(c,"INSERT OR IGNORE INTO sender_directory(sender_name) VALUES($s)","$s",sender);cmd.Transaction=tx;cmd.ExecuteNonQuery();}
    public List<string> Senders()
    {
        using var c=Open();using var cmd=Command(c,"""
            SELECT sender_name FROM sender_directory UNION SELECT sender FROM issue_batch UNION SELECT sender FROM specification
            UNION SELECT value FROM document_setting WHERE name='sender' ORDER BY 1 COLLATE NOCASE
            """);using var read=cmd.ExecuteReader();var result=new List<string>();while(read.Read())if(!string.IsNullOrWhiteSpace(read.GetString(0)))result.Add(read.GetString(0));return result;
    }
    private static string CheckedTemplate(string kind,string template)
    {IssueDocumentKinds.Name(kind);if(string.IsNullOrWhiteSpace(template)||template.Length>100000)throw new TicketException("Заполните текст шаблона (до 100 000 символов).");return template;}
    public string DocumentTemplate(string kind)
    {IssueDocumentKinds.Name(kind);using var c=Open();using var cmd=Command(c,"SELECT template FROM document_template WHERE kind=$k","$k",kind);return cmd.ExecuteScalar() as string??IssueDocumentKinds.DefaultTemplate(kind);}
    public void SaveDocumentTemplate(string kind,string template)
    {
        template=CheckedTemplate(kind,template);using var c=Open();using var cmd=Command(c,"INSERT INTO document_template(kind,template) VALUES($k,$t) ON CONFLICT(kind) DO UPDATE SET template=excluded.template","$k",kind,"$t",template);cmd.ExecuteNonQuery();
    }
    private static IssueDocumentInfo RenderIssueDocument(string kind,InvoiceInfo invoice,string template)
    {
        // A single replacement pass prevents recipient or sender text from becoming template instructions.
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["issueId"]=invoice.Id.ToString(),["recipient"]=invoice.Recipient,["sender"]=invoice.Sender,["event"]=invoice.Title,["title"]=invoice.Title,["date"]=invoice.Date,["eventDate"]=invoice.EventDate,["venue"]=invoice.Venue,["count"]=invoice.Tickets.Count.ToString(),["amount"]=DocumentTotals.Amount(invoice.Total),["summary"]=DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total),["total"]=DocumentTotals.Total(invoice.Tickets.Count,invoice.Total)};
        var body=System.Text.RegularExpressions.Regex.Replace(template,@"\{\{?(\w+)\}\}?",match=>values.TryGetValue(match.Groups[1].Value,out var value)?value:match.Value);
        if(!template.Contains("{summary}",StringComparison.OrdinalIgnoreCase)&&!template.Contains("{{summary}}",StringComparison.OrdinalIgnoreCase))body+="\n\n"+DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total);
        if(!template.Contains("{total}",StringComparison.OrdinalIgnoreCase)&&!template.Contains("{{total}}",StringComparison.OrdinalIgnoreCase))body+="\n"+DocumentTotals.Total(invoice.Tickets.Count,invoice.Total);
        return new(kind,invoice,template,body);
    }
    public IReadOnlyList<IssueDocumentInfo> PrepareIssueDocuments(long eventId,long issueId)
    {
        var invoice=Invoice(eventId,issueId);using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        foreach(var kind in IssueDocumentKinds.All)
        {using var insert=Command(c,"INSERT OR IGNORE INTO issue_document(event_id,issue_id,kind,template) SELECT $e,$i,kind,template FROM document_template WHERE kind=$k","$e",eventId,"$i",issueId,"$k",kind);insert.Transaction=tx;insert.ExecuteNonQuery();}
        var result=new List<IssueDocumentInfo>();
        foreach(var kind in IssueDocumentKinds.All)
        {using var cmd=Command(c,"SELECT template FROM issue_document WHERE event_id=$e AND issue_id=$i AND kind=$k","$e",eventId,"$i",issueId,"$k",kind);cmd.Transaction=tx;var template=cmd.ExecuteScalar() as string??throw new TicketException("Шаблон документа не найден.");result.Add(RenderIssueDocument(kind,invoice,template));}
        tx.Commit();return result;
    }
    public IssueDocumentInfo GetIssueDocument(long eventId,long issueId,string kind)
    {IssueDocumentKinds.Name(kind);return PrepareIssueDocuments(eventId,issueId).Single(document=>document.Kind==kind);}
    public IssueDocumentInfo SaveIssueDocument(long eventId,long issueId,string kind,string template)
    {
        template=CheckedTemplate(kind,template);var invoice=Invoice(eventId,issueId);PrepareIssueDocuments(eventId,issueId);
        using var c=Open();using var cmd=Command(c,"UPDATE issue_document SET template=$t WHERE event_id=$e AND issue_id=$i AND kind=$k","$t",template,"$e",eventId,"$i",issueId,"$k",kind);if(cmd.ExecuteNonQuery()!=1)throw new TicketException("Документ не найден.");return RenderIssueDocument(kind,invoice,template);
    }
    public string SaveIssueDocumentWord(long eventId,long issueId,string kind)
    {
        var info=GetIssueDocument(eventId,issueId,kind);var directory=System.IO.Path.Combine(DocumentsDirectory,IssueDocumentKinds.Name(kind));Directory.CreateDirectory(directory);
        var file=System.IO.Path.Combine(directory,$"{IssueDocumentKinds.Name(kind)}-{issueId}.docx");InvoiceDocuments.SaveIssueDocumentWord(info,file);return file;
    }
    public IReadOnlyList<string> SaveAllDocuments(long eventId,long issueId)
    {
        PrepareIssueDocuments(eventId,issueId);var paths=new List<string>();var failures=new List<Exception>();
        try{paths.Add(SaveInvoiceDocument(eventId,issueId));}catch(Exception ex){failures.Add(ex);}
        foreach(var kind in IssueDocumentKinds.All)try{paths.Add(SaveIssueDocumentWord(eventId,issueId,kind));}catch(Exception ex){failures.Add(ex);}
        if(failures.Count>0)throw new TicketException("Выдача сохранена. Не удалось сохранить часть файлов Word: "+string.Join("; ",failures.Select(ex=>ex.Message)));return paths;
    }
}
