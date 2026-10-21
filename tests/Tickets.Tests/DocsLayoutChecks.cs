using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

internal static class DocsLayoutChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("all recipients recover empty legacy requisites and preserve filled history",s=>{
            var evt=s.Events().First(e=>s.Seats(e.Id).Count(x=>x.Available)>=2);
            var seats=s.Seats(evt.Id).Where(x=>x.Available).Take(2).ToArray();
            for(var index=0;index<2;index++)
            {
                var name="Организация для проверки "+index;
                var id=s.Issue(evt.Id,new[]{seats[index].Id},name,"LAYOUT",index+1);
                Require(!s.GetIssueOrganizationDetails(id,false).HasRequisites,"unexpected initial recipient details");
                if(index==1)
                {
                    using var c=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+s.Path);c.Open();using var cmd=c.CreateCommand();
                    cmd.CommandText="DELETE FROM issue_organization_details WHERE issue_id=$i AND role='recipient'";cmd.Parameters.AddWithValue("$i",id);cmd.ExecuteNonQuery();
                }
                s.SaveOrganizationDetails(name,false,new(Inn:"100000000"+index,Address:"Адрес организации "+index,Okpo:"12345678",Okved:"90.01",Okato:"12345678901"));
                var restored=s.Invoice(evt.Id,id);
                Require(restored.RecipientDetails?.Inn=="100000000"+index,"empty legacy snapshot not recovered");
                Require(restored.RecipientDetails?.Okpo=="12345678"&&restored.RecipientDetails?.Okved=="90.01"&&restored.RecipientDetails?.Okato=="12345678901","source registration codes did not persist");
                Require(s.RecipientHistory(name).Any(x=>x.Issue.Id==id),"recipient history cannot find invoice");
                foreach(var document in s.PrepareIssueDocuments(evt.Id,id))
                {
                    Require(BusinessDocumentTemplates.Layout(document).Count(x=>x.Requisites)==1,"requisites not paired exactly once");
                    CheckPair(s.SaveIssueDocumentWord(evt.Id,id,document.Kind),name);
                }
                CheckPair(s.SaveInvoiceDocument(evt.Id,id),name);
                s.SaveOrganizationDetails(name,false,new(Inn:"200000000"+index));
                Require(new Store(s.Path).GetIssueOrganizationDetails(id,false).Inn=="100000000"+index,"filled historical snapshot silently changed");
                s.RefreshInvoiceOrganizations(evt.Id,id);
                Require(s.GetIssueOrganizationDetails(id,false).Inn=="200000000"+index,"explicit refresh did not apply");
            }
        });
        test("retired unpunctuated senders do not hide punctuated sender",s=>{
            // Simulate variants reintroduced into an older database after the first seed.
            using(var c=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+s.Path))
            {
                c.Open();using var cmd=c.CreateCommand();
                cmd.CommandText="DELETE FROM document_setting WHERE name='sender-punctuation-repair-v2'; INSERT OR IGNORE INTO sender_directory(sender_name) VALUES('ИП Баринов АС'),('ИП Баринов А С')";cmd.ExecuteNonQuery();
            }
            var choices=new Store(s.Path).Senders();
            Require(!choices.Contains("ИП Баринов АС")&&!choices.Contains("ИП Баринов А С"),"unpunctuated duplicate remains selectable");
            Require(choices.Contains("ИП Баринов А.С."),"punctuated sender hidden");
        });
        test("uploaded source forms retain distinct source table schemas",s=>{
            Require(BusinessDocumentTemplates.Default(IssueDocumentKinds.Waybill).Contains("ТОРГ-12"),"source waybill form identity missing");
            Require(BusinessDocumentTemplates.ItemWidths(IssueDocumentKinds.Waybill).Length==15,"TORG-12 columns missing");
            Require(BusinessDocumentTemplates.ItemWidths(IssueDocumentKinds.Bill).Length==9,"bill tax and currency columns missing");
        });
    }
    private static void CheckPair(string file,string recipient)
    {
        using var zip=ZipFile.OpenRead(file);using var stream=zip.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var pair=xml.Descendants(w+"tr").FirstOrDefault(row=>row.Elements(w+"tc").Count()==2&&row.Elements(w+"tc").Last().Value.Contains(recipient));
        Require(pair is not null,"Word requisites are not side by side in a two-cell row");
        Require(pair!.Elements(w+"tc").All(cell=>cell.Value.Contains("ИНН:")),"both requisite columns must be populated");
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
