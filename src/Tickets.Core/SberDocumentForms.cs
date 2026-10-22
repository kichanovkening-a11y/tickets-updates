namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsureSberDocumentForms()
    {
        using(var c=Open())
        {using var marker=Command(c,"SELECT value FROM document_setting WHERE name='sber-forms-v018'");if(marker.ExecuteScalar() is not null)return;}
        const string alias="Сбер";
        foreach(var kind in new[]{"contract","act","specification","bill"})
        {
            if(RecipientForm(alias,kind) is not null)continue;
            using var resource=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.SberForms.{kind}.docx");
            if(resource is null)return;
            using var bytes=new MemoryStream();resource.CopyTo(bytes);SaveRecipientForm(alias,kind,bytes.ToArray(),RecipientDocumentName(kind)+"-Сбер.docx");
        }
        if(!GetOrganizationDetails(alias,false).HasRequisites)
            SaveOrganizationDetails(alias,false,new(Name:alias,LegalName:"Общероссийский Профсоюз работников системы ПАО Сбербанк, Первичная организация Профсоюза работников системы ПАО Сбербанк Центрального аппарата",Inn:"7736222661",Kpp:"773601001",Ogrn:"7736222661",Address:"117997, г. Москва, ул. Вавилова, 19",Bank:"ПАО Сбербанк",Account:"40703810500020114579",CorrespondentAccount:"30101810400000000225",Bik:"044525225",Signatory:"Микуленко Алексей Борисович",Position:"Председатель комитета первичной организации Профсоюза Центрального аппарата ПАО Сбербанк"));
        using var connection=Open();using var done=Command(connection,"INSERT OR IGNORE INTO document_setting(name,value) VALUES('sber-forms-v018','1')");done.ExecuteNonQuery();
    }
}
