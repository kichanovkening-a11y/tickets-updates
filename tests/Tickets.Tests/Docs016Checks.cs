using Tickets.Core;

static class Docs016Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("recipient refresh preserves sender and other issues and persists the full recipient for future issues",s=>{
            const string recipient="Получатель 016";
            var sender=s.SaveSender("Отправитель 016");
            var oldSender=new OrganizationDetails(Name:sender,Inn:"sender-original",Signatory:"Прежний отправитель");
            var oldRecipient=new OrganizationDetails(Name:recipient,Inn:"recipient-original",Signatory:"Прежний получатель");
            s.SaveOrganizationDetails(sender,true,oldSender);s.SaveOrganizationDetails(recipient,false,oldRecipient);
            var first=Issue(s,recipient,sender);var current=Issue(s,recipient,sender);
            var updated=new OrganizationDetails(Name:recipient,Inn:"7701234567",Kpp:"770101001",Ogrn:"1234567890123",Address:"Москва, дом 16",Bank:"Банк 016",Account:"40702810123456789012",CorrespondentAccount:"30101810123456789012",Bik:"044525016",Signatory:"Получатель Обновлённый",Position:"Директор",Phone:"+7 900 000 0016",Okpo:"12345678",Okved:"90.01",Okato:"45286580000",ExtraFields:[new("Лицевой счёт","016-extra")]);
            s.SaveOrganizationDetails(recipient,false,updated);
            s.SaveOrganizationDetails(sender,true,oldSender with{Inn="sender-directory-new"});
            s.RefreshInvoiceRecipient(current.EventId,current.Id);
            var reopened=new Store(s.Path);
            var historical=reopened.Invoice(first.EventId,first.Id);var refreshed=reopened.Invoice(current.EventId,current.Id);
            Require(historical.SenderDetails?.Inn==oldSender.Inn&&historical.RecipientDetails?.Inn==oldRecipient.Inn,"another issue's party snapshots changed");
            Require(refreshed.SenderDetails?.Inn==oldSender.Inn,"recipient refresh changed sender snapshot");
            Equal(updated,refreshed.RecipientDetails!);
            Equal(updated,reopened.GetOrganizationDetails(recipient,false));
            var future=Issue(reopened,recipient,sender);Equal(updated,future.RecipientDetails!);
            Require(reopened.GetOrganizationDetails(sender,true).Inn=="sender-directory-new","recipient refresh changed sender directory");
            var paths=reopened.SaveAllDocuments(current.EventId,current.Id);
            Require(paths.Count==6,"not all six issue documents regenerated");
            foreach(var path in paths)
            {
                using var archive=System.IO.Compression.ZipFile.OpenRead(path);using var stream=archive.GetEntry("word/document.xml")!.Open();
                var text=string.Concat(System.Xml.Linq.XDocument.Load(stream).Descendants(System.Xml.Linq.XName.Get("t","http://schemas.openxmlformats.org/wordprocessingml/2006/main")).Select(t=>t.Value));
                Require(text.Replace('\u00a0',' ').Contains(updated.Signatory)&&text.Replace('\u00a0',' ').Contains(oldSender.Signatory),"regenerated document has stale recipient or changed sender: "+path);
            }
        });
        test("recipient refresh rejects an issue belonging to another event without changing snapshots",s=>{
            var invoice=Issue(s,"Получатель 016 проверка",Store.DefaultSender);
            s.SaveOrganizationDetails(invoice.Recipient,false,new(Name:invoice.Recipient,Inn:"016-new"));
            var before=s.Invoice(invoice.EventId,invoice.Id).RecipientDetails;
            try{s.RefreshInvoiceRecipient(-1,invoice.Id);throw new Exception("foreign event refresh accepted");}catch(TicketException){}
            Equal(before!,s.Invoice(invoice.EventId,invoice.Id).RecipientDetails!);
        });
    }
    private static InvoiceInfo Issue(Store s,string recipient,string sender)
    {
        var evt=s.Events().First(e=>s.Seats(e.Id).Any(x=>x.Available));var seat=s.Seats(evt.Id).First(x=>x.Available).Id;
        var id=s.Issue(evt.Id,[seat],recipient,Guid.NewGuid().ToString("N")[..8],1);s.UpdateInvoiceSender(evt.Id,id,sender);
        return s.Invoice(evt.Id,id);
    }
    private static void Equal(OrganizationDetails expected,OrganizationDetails actual)
        =>Require(expected with{ExtraFields=null}==actual with{ExtraFields=null}&&(expected.ExtraFields??[]).SequenceEqual(actual.ExtraFields??[]),"recipient requisites were not persisted completely");
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
