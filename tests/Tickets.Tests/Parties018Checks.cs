using Tickets.Core;

internal static class Parties018Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("018 sender removal hides choice while preserving issued history and requisites",store=>{
            const string sender="Отправитель018 Ёлка",recipient="Получатель018 партии";
            store.SaveSender(sender);
            store.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"1234567890"));
            store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"0987654321"));
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
            var seat=store.Seats(show.Id).First(s=>s.Available);
            var issue=store.Issue(show.Id,[seat.Id],recipient,"П018",1);
            store.UpdateInvoiceSender(show.Id,issue,sender);
            store.RemoveSenderChoice("  отправитель018 елка ");
            var reopened=new Store(store.Path);
            Require(!reopened.Senders().Any(s=>CatalogText.Key(s)==CatalogText.Key(sender)),"deleted sender reappeared from issuance history");
            var invoice=reopened.Invoice(show.Id,issue);
            Require(invoice.Sender==sender&&invoice.SenderDetails?.Inn=="1234567890"&&invoice.Tickets.Single().Number==1,"sender removal changed issued document");
            Require(reopened.GetOrganizationDetails(sender,true).Inn=="1234567890","sender removal erased requisites");
            Require(reopened.History(show.Id).Any(t=>t.IssueId==issue),"sender removal erased issued history");
        });
        test("018 sender refresh updates selected sender snapshot without refreshing recipient",store=>{
            const string sender="Отправитель018 реквизиты",recipient="Получатель018 реквизиты";
            store.SaveSender(sender);store.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"1111111111"));
            store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"2222222222"));
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
            var issue=store.Issue(show.Id,[store.Seats(show.Id).First(s=>s.Available).Id],recipient,"Р018",1);
            store.UpdateInvoiceSender(show.Id,issue,sender);
            store.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"3333333333"));
            store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"4444444444"));
            store.RefreshInvoiceSender(show.Id,issue);
            var reopened=new Store(store.Path);
            Require(reopened.GetIssueOrganizationDetails(issue,true).Inn=="3333333333","selected sender requisites did not update");
            Require(reopened.GetIssueOrganizationDetails(issue,false).Inn=="2222222222","sender edit unexpectedly refreshed recipient");
            try{store.RefreshInvoiceSender(-1,issue);throw new Exception("wrong event accepted");}catch(TicketException){}
        });
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
