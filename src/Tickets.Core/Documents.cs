using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record SpecificationInfo(long Id,string Recipient,string Sender,string Title,string Body,string UpdatedAt)
{
    public string RecipientLegalName{get;init;}="";
    public string SenderLegalName{get;init;}="";
    public string DocumentRecipient=>string.IsNullOrWhiteSpace(RecipientLegalName)?Recipient:RecipientLegalName;
    public string DocumentSender=>string.IsNullOrWhiteSpace(SenderLegalName)?Sender:SenderLegalName;
}

public sealed partial class Store
{
    public const string DefaultSender="ООО \"Альянс ТА\"";
    private void EnsureDocuments()
    {
        using var c=Open();using var version=Command(c,"PRAGMA user_version");if(Convert.ToInt32(version.ExecuteScalar())>=5)return;
        var copy=Path+".before-v5.db";if(!File.Exists(copy))
        {using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy,Pooling=false}.ToString());dest.Open();c.BackupDatabase(dest);}
        using var tx=c.BeginTransaction(deferred:false);
        var hasSender=false;using(var columns=Command(c,"PRAGMA table_info(issue_batch)"))
        {columns.Transaction=tx;using var read=columns.ExecuteReader();while(read.Read())if(read.GetString(1)=="sender")hasSender=true;}
        if(!hasSender){using var alter=Command(c,"ALTER TABLE issue_batch ADD COLUMN sender TEXT NOT NULL DEFAULT 'ООО \"Альянс ТА\"'");alter.Transaction=tx;alter.ExecuteNonQuery();}
        using var cmd=Command(c,"""
            CREATE TABLE IF NOT EXISTS document_setting(name TEXT PRIMARY KEY,value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS specification(id INTEGER PRIMARY KEY,recipient TEXT NOT NULL,recipient_key TEXT NOT NULL UNIQUE,
                sender TEXT NOT NULL,title TEXT NOT NULL,body TEXT NOT NULL,updated_at TEXT NOT NULL);
            PRAGMA user_version=5;
            """);cmd.Transaction=tx;cmd.ExecuteNonQuery();tx.Commit();
    }
    private string DocumentSetting(string name,string fallback)
    {using var c=Open();using var cmd=Command(c,"SELECT value FROM document_setting WHERE name=$n","$n",name);return cmd.ExecuteScalar() as string??fallback;}
    public string Sender { get {var sender=DocumentSetting("sender",DefaultSender);return SenderChoiceAllowed(sender)?sender:Senders().FirstOrDefault()??"";} }
    private string DocumentDataDirectory=>System.IO.Path.GetDirectoryName(Path)!;
    public string DocumentsDirectory=>ResolveDocumentDirectory(DocumentSetting("directory","data-relative:Documents"));
    private string ResolveDocumentDirectory(string directory)
    {
        if(directory.StartsWith("absolute:",StringComparison.Ordinal))return directory[9..];
        if(directory.StartsWith("data-relative:",StringComparison.Ordinal))
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(DocumentDataDirectory,directory[14..].Replace('/',System.IO.Path.DirectorySeparatorChar)));
        // Earlier versions persisted the default as an absolute path on every issue.
        // A copied database must use its copied Documents even if the old app still exists.
        var normalized=directory.Replace('\\','/').TrimEnd('/');
        // Only the known legacy installation layout is safe to relocate automatically.
        // Other absolute custom directories retain their original meaning.
        const string marker="/Tickets/Data/Documents";
        var index=normalized.LastIndexOf(marker,StringComparison.OrdinalIgnoreCase);
        if(index>=0&&(index+marker.Length==normalized.Length||normalized[index+marker.Length]=='/'))
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(DocumentDataDirectory,"Documents"+normalized[(index+marker.Length)..].Replace('/',System.IO.Path.DirectorySeparatorChar)));
        return directory;
    }
    private string PortableDocumentDirectory(string directory)
    {
        var data=DocumentDataDirectory;
        var app=string.Equals(System.IO.Path.GetFileName(data),"Data",StringComparison.OrdinalIgnoreCase)?System.IO.Path.GetDirectoryName(data)!:data;
        var relative=System.IO.Path.GetRelativePath(app,directory);
        var inside=!System.IO.Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+System.IO.Path.DirectorySeparatorChar,StringComparison.Ordinal);
        return inside?"data-relative:"+System.IO.Path.GetRelativePath(data,directory).Replace(System.IO.Path.DirectorySeparatorChar,'/'):"absolute:"+directory;
    }
    // Manual exports share their last destination without changing managed internal copies.
    public string DocumentSaveDirectory=>ResolveDocumentDirectory(DocumentSetting("manual-save-directory","absolute:"+DocumentsDirectory));
    public void RememberDocumentSaveDirectory(string directory)
    {
        directory=System.IO.Path.GetFullPath(Required(directory,"Папка сохранения"));
        using var c=Open();using var cmd=Command(c,"INSERT INTO document_setting(name,value) VALUES('manual-save-directory',$d) ON CONFLICT(name) DO UPDATE SET value=excluded.value","$d",PortableDocumentDirectory(directory));cmd.ExecuteNonQuery();
    }
    public void SaveDocumentSettings(string sender,string directory)
    {
        sender=Required(sender,"От кого");if(!SenderChoiceAllowed(sender))throw new TicketException("Отправитель удалён из списка. Выберите другого или создайте отправителя заново.");
        directory=System.IO.Path.GetFullPath(directory.Trim());Directory.CreateDirectory(directory);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var prior=Command(c,"SELECT value FROM document_setting WHERE name='sender'"))
        {prior.Transaction=tx;if(prior.ExecuteScalar() is string previous)RememberSender(c,tx,previous);}
        foreach(var pair in new[]{("sender",sender),("directory",PortableDocumentDirectory(directory))})
        {using var cmd=Command(c,"INSERT INTO document_setting(name,value) VALUES($n,$v) ON CONFLICT(name) DO UPDATE SET value=excluded.value","$n",pair.Item1,"$v",pair.Item2);cmd.Transaction=tx;cmd.ExecuteNonQuery();}RememberSender(c,tx,sender);tx.Commit();
    }
    public void UpdateInvoiceSender(long eventId,long issueId,string sender)
    {
        sender=Required(sender,"От кого");using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        RequireWritableEvent(c,tx,eventId);
        using(var old=Command(c,"SELECT sender FROM issue_batch WHERE id=$i AND event_id=$e","$i",issueId,"$e",eventId))
        {old.Transaction=tx;var prior=old.ExecuteScalar() as string;if(prior is null)throw new TicketException("Выдача не найдена в этом мероприятии.");if(CatalogText.Key(prior)!=CatalogText.Key(sender)&&!SenderChoiceAllowed(sender))throw new TicketException("Отправитель удалён из списка. Выберите другого или создайте отправителя заново.");RememberSender(c,tx,prior);if(prior!=sender)CaptureIssueOrganization(c,tx,issueId,sender,true);}
        using var cmd=Command(c,"UPDATE issue_batch SET sender=$s WHERE id=$i AND event_id=$e","$s",sender,"$i",issueId,"$e",eventId);cmd.Transaction=tx;
        if(cmd.ExecuteNonQuery()!=1)throw new TicketException("Выдача не найдена в этом мероприятии.");RememberSender(c,tx,sender);tx.Commit();
    }
    public string SaveInvoiceDocument(long eventId,long issueId)
    {
        var directory=System.IO.Path.Combine(DocumentsDirectory,"Накладные");Directory.CreateDirectory(directory);
        var file=System.IO.Path.Combine(directory,$"Накладная-{issueId}.docx");if(CustomIssueDocument(eventId,issueId,"invoice") is {} custom)InvoiceDocuments.SaveIssueDocumentWord(custom,file);else InvoiceDocuments.SaveWord(Invoice(eventId,issueId),file);return file;
    }
    public List<SpecificationInfo> Specifications()
    {
        using var c=Open();using var cmd=Command(c,"SELECT id,recipient,sender,title,body,updated_at FROM specification ORDER BY recipient COLLATE NOCASE");using var read=cmd.ExecuteReader();var result=new List<SpecificationInfo>();
        while(read.Read())result.Add(new(read.GetInt64(0),read.GetString(1),read.GetString(2),read.GetString(3),read.GetString(4),read.GetString(5)){RecipientLegalName=GetOrganizationDetails(read.GetString(1),false).LegalName,SenderLegalName=GetOrganizationDetails(read.GetString(2),true).LegalName});return result;
    }
    public SpecificationInfo SaveSpecification(string recipient,string sender,string title,string body)
    {
        recipient=Required(recipient,"Получатель");sender=Required(sender,"От кого");title=Required(title,"Название");
        body=body.Trim();if(body.Length==0||body.Length>100000)throw new TicketException("Заполните текст спецификации (до 100 000 символов).");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);var timestamp=DateTimeOffset.Now.ToString("O");
        using var cmd=Command(c,"""
            INSERT INTO specification(recipient,recipient_key,sender,title,body,updated_at) VALUES($r,$k,$s,$t,$b,$u)
            ON CONFLICT(recipient_key) DO UPDATE SET recipient=excluded.recipient,sender=excluded.sender,title=excluded.title,body=excluded.body,updated_at=excluded.updated_at RETURNING id
            ""","$r",recipient,"$k",CatalogText.Key(recipient),"$s",sender,"$t",title,"$b",body,"$u",timestamp);cmd.Transaction=tx;var id=Convert.ToInt64(cmd.ExecuteScalar());AddRecipient(c,tx,recipient);RememberSender(c,tx,sender);tx.Commit();
        return new(id,recipient,sender,title,body,timestamp){RecipientLegalName=GetOrganizationDetails(recipient,false).LegalName,SenderLegalName=GetOrganizationDetails(sender,true).LegalName};
    }
    public string SaveSpecificationDocument(SpecificationInfo specification)
    {
        var directory=System.IO.Path.Combine(DocumentsDirectory,"Спецификации");Directory.CreateDirectory(directory);
        var file=System.IO.Path.Combine(directory,$"Спецификация-{specification.Id}.docx");InvoiceDocuments.SaveSpecificationWord(specification,file);return file;
    }
}
