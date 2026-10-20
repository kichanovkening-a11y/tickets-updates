using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record OrganizationDetails(string Name="",string Inn="",string Kpp="",string Ogrn="",string Address="",string Bank="",string Account="",string CorrespondentAccount="",string Bik="",string Signatory="",string Position="",string Phone="")
{
    public string DisplayText=>string.Join("\n",new[]{Name,Line("ИНН",Inn),Line("КПП",Kpp),Line("ОГРН / ОГРНИП",Ogrn),Line("Адрес",Address),Line("Банк",Bank),Line("Расчётный счёт",Account),Line("Корр. счёт",CorrespondentAccount),Line("БИК",Bik),Line("Подписант",Signatory),Line("Должность",Position),Line("Телефон",Phone)}.Where(x=>x.Length>0));
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
        if(marker.ExecuteScalar() is not null){tx.Commit();return;}
        const string restored="ИП Баринов А.С.";
        using(var unhide=Command(c,"DELETE FROM catalog_hidden WHERE kind='sender' AND (entity_key=$k OR entity_key='ИПБАРИНОВАС')","$k",SenderChoiceKey(restored))){unhide.Transaction=tx;unhide.ExecuteNonQuery();}
        HideCatalogEntry(c,tx,"sender",SenderChoiceKey("ИП Баринов А С"));
        RememberSender(c,tx,restored);
        var alliance=new OrganizationDetails(DefaultSender,"7729768795","772301001","1147746365062","109548, г. Москва, Вн. Тер. Г. Муниципальный округ Печатники, ул. Шоссейная, д. 20, кв. 75","ПАО «ПРОМСВЯЗЬБАНК» г. Москва","40702810500000272771","30101810400000000555","044525555","Заборский Л.Н.","Генеральный директор");
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
        if(cmd.ExecuteScalar() is string json)return JsonSerializer.Deserialize<OrganizationDetails>(json)??new();
        using var original=Command(c,sender?"SELECT sender FROM issue_batch WHERE id=$i":"SELECT recipient FROM issue_batch WHERE id=$i","$i",issueId);
        return new(Name:original.ExecuteScalar() as string??"");
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
        string Clean(string value){value=value.Trim();if(value.Length>2000||value.Any(char.IsControl))throw new TicketException("Реквизиты должны занимать одну строку (до 2000 символов).");return value;}
        details=new(Name:name,Inn:Clean(details.Inn),Kpp:Clean(details.Kpp),Ogrn:Clean(details.Ogrn),Address:Clean(details.Address),Bank:Clean(details.Bank),Account:Clean(details.Account),CorrespondentAccount:Clean(details.CorrespondentAccount),Bik:Clean(details.Bik),Signatory:Clean(details.Signatory),Position:Clean(details.Position),Phone:Clean(details.Phone));
        name=sender?SaveSender(name):SaveRecipient(name);details=details with{Name=name};
        using var c=Open();EnsureOrganizationTable(c);
        using var cmd=Command(c,"INSERT INTO organization_details(role,name_key,details) VALUES($r,$k,$d) ON CONFLICT(role,name_key) DO UPDATE SET details=excluded.details","$r",sender?"sender":"recipient","$k",CatalogText.Key(name),"$d",JsonSerializer.Serialize(details));cmd.ExecuteNonQuery();
    }
}
