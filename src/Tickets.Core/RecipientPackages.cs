using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace Tickets.Core;
public sealed record RecipientPackageIssue(long EventId,long IssueId);
public sealed record RecipientPackageInfo(long Id,InvoiceInfo Invoice,IReadOnlyList<RecipientPackageIssue> Issues);
public sealed partial class Store
{
    private static void EnsureRecipientPackages(SqliteConnection c)
    {using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS recipient_package(id INTEGER PRIMARY KEY,snapshot TEXT NOT NULL); CREATE TABLE IF NOT EXISTS recipient_package_document(package_id INTEGER NOT NULL REFERENCES recipient_package(id),kind TEXT NOT NULL,content BLOB NOT NULL,PRIMARY KEY(package_id,kind));");cmd.ExecuteNonQuery();}
    public RecipientPackageInfo CreateRecipientPackage(string recipient,IEnumerable<(long EventId,long IssueId)> selected)
    {
        var members=selected.Distinct().ToArray();if(members.Length==0)throw new TicketException("Отметьте выдачи для общего комплекта документов.");
        var invoices=members.Select(x=>Invoice(x.EventId,x.IssueId)).ToArray();
        if(invoices.Any(x=>CatalogText.Key(x.Recipient)!=CatalogText.Key(recipient)))throw new TicketException("В общий комплект можно включить выдачи только одного получателя.");
        if(invoices.Select(x=>CatalogText.Key(x.Sender)).Distinct().Count()!=1)throw new TicketException("Общий комплект требует одного отправителя. У выбранных выдач разные отправители; выберите выдачи одного отправителя.");
        if(invoices.All(x=>x.ActiveCount==0))throw new TicketException("В выбранных выдачах все билеты возвращены. Отметьте выдачи с билетами на руках.");
        invoices=invoices.Where(x=>x.ActiveCount>0).Select(x=>x with{Tickets=x.Tickets.Where(t=>t.ReturnedAt is null).ToArray()}).ToArray();
        var history=RecipientHistory(recipient);var latest=invoices.OrderByDescending(x=>history.First(h=>h.Issue.Id==x.Id&&h.Issue.EventId==x.EventId).StartsAt).First();
        var shows=invoices.GroupBy(x=>x.EventId).Select(group=>group.First() with{Tickets=group.SelectMany(x=>x.Tickets).ToArray()}).ToArray();
        var aggregate=latest with{Title=string.Join("; ",invoices.Select(x=>$"{x.Title} · {x.EventDate} · {x.VenueWithAddress}").Distinct()),Tickets=invoices.SelectMany(x=>x.Tickets).ToArray(),PackageShows=shows};
        using var c=Open();EnsureRecipientPackages(c);using var tx=c.BeginTransaction();using var insert=Command(c,"INSERT INTO recipient_package(snapshot) VALUES('') RETURNING id");insert.Transaction=tx;var id=Convert.ToInt64(insert.ExecuteScalar());
        var package=new RecipientPackageInfo(id,aggregate with{Id=id},members.Select(x=>new RecipientPackageIssue(x.EventId,x.IssueId)).ToArray());
        using var update=Command(c,"UPDATE recipient_package SET snapshot=$s WHERE id=$i","$s",JsonSerializer.Serialize(package),"$i",id);update.Transaction=tx;update.ExecuteNonQuery();tx.Commit();return package;
    }
    public RecipientPackageInfo RecipientPackage(long id)
    {using var c=Open();EnsureRecipientPackages(c);using var cmd=Command(c,"SELECT snapshot FROM recipient_package WHERE id=$i","$i",id);return cmd.ExecuteScalar() is string snapshot?JsonSerializer.Deserialize<RecipientPackageInfo>(snapshot)??throw new TicketException("Комплект не найден."):throw new TicketException("Комплект не найден.");}
    public IReadOnlyList<RecipientPackageInfo> RecipientPackages(string recipient)
    {using var c=Open();EnsureRecipientPackages(c);using var cmd=Command(c,"SELECT snapshot FROM recipient_package ORDER BY id DESC");using var r=cmd.ExecuteReader();var result=new List<RecipientPackageInfo>();while(r.Read()){var p=JsonSerializer.Deserialize<RecipientPackageInfo>(r.GetString(0));if(p is not null&&CatalogText.Key(p.Invoice.Recipient)==CatalogText.Key(recipient))result.Add(p);}return result;}
    public IssueDocumentInfo GetRecipientPackageDocument(long id,string kind)
    {
        RecipientDocumentName(kind);var invoice=RecipientPackage(id).Invoice;
        if(kind!="invoice"&&invoice.RecipientDetails?.HasRequisites!=true)throw new TicketException("Для этого документа заполните реквизиты получателя перед созданием комплекта. Накладная доступна без реквизитов.");
        using var c=Open();EnsureRecipientPackages(c);using var cmd=Command(c,"SELECT content FROM recipient_package_document WHERE package_id=$i AND kind=$k","$i",id,"$k",kind);var bytes=cmd.ExecuteScalar() as byte[];
        if(bytes is null){var form=RecipientForm(invoice.Recipient,kind)?.Content??StandardDocumentForms.Resolve(kind,null)??throw new TicketException("Бланк документа не найден.");bytes=CustomDocumentForms.Fill(form,invoice,kind,invoice.ContractDate??DateTimeOffset.Parse(invoice.IssuedAt).Date);SaveRecipientPackageDocument(id,kind,bytes);}
        return new(kind,invoice,"",CustomDocumentForms.Text(bytes)){SourceDocument=bytes};
    }
    public void SaveRecipientPackageDocument(long id,string kind,byte[] content)
    {RecipientDocumentName(kind);RecipientPackage(id);CustomDocumentForms.Validate(content);using var c=Open();EnsureRecipientPackages(c);using var cmd=Command(c,"INSERT INTO recipient_package_document(package_id,kind,content) VALUES($i,$k,$b) ON CONFLICT(package_id,kind) DO UPDATE SET content=excluded.content","$i",id,"$k",kind,"$b",content);cmd.ExecuteNonQuery();}
}
