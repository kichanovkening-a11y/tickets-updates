using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record OrganizationExtraField(string Label,string Value);

public sealed record OrganizationDetails(string Name="",string Inn="",string Kpp="",string Ogrn="",string Address="",string Bank="",string Account="",string CorrespondentAccount="",string Bik="",string Signatory="",string Position="",string Phone="",string Okpo="",string Okved="",string Okato="",IReadOnlyList<OrganizationExtraField>? ExtraFields=null,string LegalName="",string Authority="")
{
    public bool HasRequisites=>new[]{LegalName,Authority,Inn,Kpp,Ogrn,Address,Bank,Account,CorrespondentAccount,Bik,Signatory,Position,Phone,Okpo,Okved,Okato}.Any(value=>!string.IsNullOrWhiteSpace(value))||(ExtraFields?.Any(field=>!string.IsNullOrWhiteSpace(field.Label)&&!string.IsNullOrWhiteSpace(field.Value))??false);
    public string DisplayText=>string.Join("\n",new[]{Name,Line("ИНН",Inn),Line("КПП",Kpp),Line("ОГРН / ОГРНИП",Ogrn),Line("Адрес",Address),Line("ОКПО",Okpo),Line("ОКВЭД",Okved),Line("ОКАТО",Okato),Line("Банк",Bank),Line("Расчётный счёт",Account),Line("Корр. счёт",CorrespondentAccount),Line("БИК",Bik),Line("Подписант",Signatory),Line("Должность",Position),Line("Основание полномочий",Authority),Line("Телефон",Phone)}.Concat((ExtraFields??[]).Where(field=>!string.IsNullOrWhiteSpace(field.Label)).Select(field=>Line(field.Label,field.Value))).Where(x=>x.Length>0));
    private static string Line(string label,string value)=>string.IsNullOrWhiteSpace(value)?"":label+": "+value;
}

public sealed partial class Store
{
    private static void EnsureOrganizationTable(SqliteConnection c)
    {
        using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS organization_details(role TEXT NOT NULL,name_key TEXT NOT NULL,details TEXT NOT NULL,PRIMARY KEY(role,name_key)); CREATE TABLE IF NOT EXISTS issue_organization_details(issue_id INTEGER NOT NULL,role TEXT NOT NULL,details TEXT NOT NULL,PRIMARY KEY(issue_id,role))");cmd.ExecuteNonQuery();
    }
    private void EnsureOrganizationDirectory()
    {
        using var c=Open();EnsureOrganizationTable(c);using var tx=c.BeginTransaction(deferred:false);
        using var marker=Command(c,"SELECT value FROM document_setting WHERE name='organization-directory-seed-v1'");marker.Transaction=tx;
        var seeded=marker.ExecuteScalar() is not null;
        using(var repair=Command(c,"SELECT value FROM document_setting WHERE name='sender-punctuation-repair-v2'"))
        {
            repair.Transaction=tx;
            if(repair.ExecuteScalar() is null)
            {
                HideCatalogEntry(c,tx,"sender",SenderChoiceKey("ИП Баринов АС"));
                HideCatalogEntry(c,tx,"sender",SenderChoiceKey("ИП Баринов А С"));
                using var done=Command(c,"INSERT INTO document_setting(name,value) VALUES('sender-punctuation-repair-v2','1')");done.Transaction=tx;done.ExecuteNonQuery();
            }
        }
        if(seeded){tx.Commit();return;}
        const string restored="ИП Баринов А.С.";
        // Seeding can add missing defaults, but must never restore a user-deleted sender.
        HideCatalogEntry(c,tx,"sender",SenderChoiceKey("ИП Баринов А С"));
        RememberSender(c,tx,restored);
        var alliance=new OrganizationDetails(DefaultSender,"7729768795","772301001","1147746365062","109548, г. Москва, Вн. Тер. Г. Муниципальный округ Печатники, ул. Шоссейная, д. 20, кв. 75","ПАО «ПРОМСВЯЗЬБАНК» г. Москва","40702810500000272771","30101810400000000555","044525555","Заборский Л.Н.","Генеральный директор",Okved:"90.01",Okato:"45286580000");
        using(var seed=Command(c,"INSERT OR IGNORE INTO organization_details(role,name_key,details) VALUES('sender',$k,$d)","$k",CatalogText.Key(DefaultSender),"$d",JsonSerializer.Serialize(alliance))){seed.Transaction=tx;seed.ExecuteNonQuery();}
        using(var done=Command(c,"INSERT INTO document_setting(name,value) VALUES('organization-directory-seed-v1','1')")){done.Transaction=tx;done.ExecuteNonQuery();}
        tx.Commit();
    }
    public string SaveSender(string name)
    {
        name=Regex.Replace(Required(name,"От кого").Normalize(),@"\s+"," ");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var key=CatalogText.Key(name);string? saved=null;
        using(var read=Command(c,"SELECT sender_name FROM sender_directory ORDER BY rowid")){read.Transaction=tx;using var r=read.ExecuteReader();while(r.Read())if(CatalogText.Key(r.GetString(0))==key){saved=r.GetString(0);break;}}
        name=saved??name;
        using(var restore=Command(c,"DELETE FROM catalog_hidden WHERE kind='sender' AND entity_key=$k","$k",SenderChoiceKey(name))){restore.Transaction=tx;restore.ExecuteNonQuery();}
        RememberSender(c,tx,name);tx.Commit();return name;
    }
    public OrganizationDetails GetOrganizationDetails(string name,bool sender)
    {
        name=Required(name,sender?"От кого":"Получатель");using var c=Open();EnsureOrganizationTable(c);
        using var cmd=Command(c,"SELECT details FROM organization_details WHERE role=$r AND name_key=$k","$r",sender?"sender":"recipient","$k",CatalogText.Key(name));
        return cmd.ExecuteScalar() is string json?(JsonSerializer.Deserialize<OrganizationDetails>(json)??new()) with{Name=name}:new(Name:name);
    }
    private static void CaptureIssueOrganization(SqliteConnection c,SqliteTransaction tx,long issueId,string name,bool sender)
    {
        var role=sender?"sender":"recipient";
        using var read=Command(c,"SELECT details FROM organization_details WHERE role=$r AND name_key=$k","$r",role,"$k",CatalogText.Key(name));read.Transaction=tx;
        var details=read.ExecuteScalar() is string json?(JsonSerializer.Deserialize<OrganizationDetails>(json)??new()) with{Name=name}:new OrganizationDetails(Name:name);
        using var write=Command(c,"INSERT INTO issue_organization_details(issue_id,role,details) VALUES($i,$r,$d) ON CONFLICT(issue_id,role) DO UPDATE SET details=excluded.details","$i",issueId,"$r",role,"$d",JsonSerializer.Serialize(details));write.Transaction=tx;write.ExecuteNonQuery();
    }
    private static void CaptureIssueOrganizations(SqliteConnection c,SqliteTransaction tx,long issueId,string sender,string recipient)
    {CaptureIssueOrganization(c,tx,issueId,sender,true);CaptureIssueOrganization(c,tx,issueId,recipient,false);}
    public OrganizationDetails GetIssueOrganizationDetails(long issueId,bool sender)
    {
        using var c=Open();using var cmd=Command(c,"SELECT details FROM issue_organization_details WHERE issue_id=$i AND role=$r","$i",issueId,"$r",sender?"sender":"recipient");
        var oldJson=cmd.ExecuteScalar() as string;
        var snapshot=oldJson is not null?JsonSerializer.Deserialize<OrganizationDetails>(oldJson):null;
        if(snapshot?.HasRequisites==true)return snapshot;
        using var original=Command(c,sender?"SELECT sender FROM issue_batch WHERE id=$i":"SELECT recipient FROM issue_batch WHERE id=$i","$i",issueId);
        var name=original.ExecuteScalar() as string??snapshot?.Name??"";
        if(string.IsNullOrWhiteSpace(name))return snapshot??new();
        var current=GetOrganizationDetails(name,sender);
        if(!current.HasRequisites)return snapshot??new(Name:name);
        // Legacy invoices captured only a name. Fill those once; a populated historical
        // snapshot can only change through the explicit refresh command.
        using var write=oldJson is null
            ?Command(c,"INSERT INTO issue_organization_details(issue_id,role,details) VALUES($i,$r,$d) ON CONFLICT(issue_id,role) DO NOTHING","$i",issueId,"$r",sender?"sender":"recipient","$d",JsonSerializer.Serialize(current))
            :Command(c,"UPDATE issue_organization_details SET details=$d WHERE issue_id=$i AND role=$r AND details=$old","$i",issueId,"$r",sender?"sender":"recipient","$d",JsonSerializer.Serialize(current),"$old",oldJson);
        write.ExecuteNonQuery();
        // Another editor may have refreshed this invoice after our read. Respect its value.
        return cmd.ExecuteScalar() is string saved?JsonSerializer.Deserialize<OrganizationDetails>(saved)??current:current;
    }
    public void RefreshInvoiceRecipient(long eventId,long issueId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var read=Command(c,"SELECT recipient FROM issue_batch WHERE id=$i AND event_id=$e","$i",issueId,"$e",eventId);read.Transaction=tx;
        var recipient=read.ExecuteScalar() as string??throw new TicketException("Накладная не найдена в этом показе.");
        CaptureIssueOrganization(c,tx,issueId,recipient,false);tx.Commit();
    }
    public void RefreshInvoiceOrganizations(long eventId,long issueId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        string sender,recipient;
        using(var read=Command(c,"SELECT sender,recipient FROM issue_batch WHERE id=$i AND event_id=$e","$i",issueId,"$e",eventId))
        {read.Transaction=tx;using var r=read.ExecuteReader();if(!r.Read())throw new TicketException("Накладная не найдена в этом показе.");sender=r.GetString(0);recipient=r.GetString(1);}
        CaptureIssueOrganizations(c,tx,issueId,sender,recipient);tx.Commit();
    }
    public void SaveOrganizationDetails(string name,bool sender,OrganizationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        // Validate before creating the directory entry so a rejected edit has no side effects.
        string Clean(string value){value=(value??"").Trim();if(value.Length>2000||value.Any(char.IsControl))throw new TicketException("Реквизиты должны занимать одну строку (до 2000 символов).");return value;}
        var extras=new List<OrganizationExtraField>();var labels=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var field in details.ExtraFields??[])
        {
            if(field is null)throw new TicketException("Дополнительный реквизит не заполнен.");
            var label=Clean(field.Label);var value=Clean(field.Value);
            if(label.Length==0&&value.Length==0)continue;
            if(label.Length==0)throw new TicketException("Укажите название дополнительного реквизита.");
            if(label.Length>120)throw new TicketException("Название дополнительного реквизита — до 120 символов.");
            if(!labels.Add(CatalogText.Key(label)))throw new TicketException("Названия дополнительных реквизитов не должны повторяться.");
            extras.Add(new(label,value));
        }
        if(extras.Count>100)throw new TicketException("Добавьте не более 100 дополнительных реквизитов.");
        details=new(Name:name,LegalName:Clean(details.LegalName),Authority:Clean(details.Authority),Inn:Clean(details.Inn),Kpp:Clean(details.Kpp),Ogrn:Clean(details.Ogrn),Address:Clean(details.Address),Bank:Clean(details.Bank),Account:Clean(details.Account),CorrespondentAccount:Clean(details.CorrespondentAccount),Bik:Clean(details.Bik),Signatory:Clean(details.Signatory),Position:Clean(details.Position),Phone:Clean(details.Phone),Okpo:Clean(details.Okpo),Okved:Clean(details.Okved),Okato:Clean(details.Okato),ExtraFields:extras);
        name=sender?SaveSender(name):SaveRecipient(name);details=details with{Name=name};
        using var c=Open();EnsureOrganizationTable(c);
        using var cmd=Command(c,"INSERT INTO organization_details(role,name_key,details) VALUES($r,$k,$d) ON CONFLICT(role,name_key) DO UPDATE SET details=excluded.details","$r",sender?"sender":"recipient","$k",CatalogText.Key(name),"$d",JsonSerializer.Serialize(details));cmd.ExecuteNonQuery();
    }
}
