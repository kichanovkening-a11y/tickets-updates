using Tickets.Core;
internal static class Recipient016Checks
{
    internal static void Register(Action<string,Action<Store>> test)=>test("recipient requisites refresh preserves historical sender and persists directory",store=>{
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=2);var seats=store.Seats(show.Id).Where(s=>s.Available).Take(2).Select(s=>s.Id).ToArray();
        var recipient="Получатель016";var sender="ООО Отправитель016";store.SaveOrganizationDetails(sender,true,new(Name:sender,Account:"SENDEROLD"));store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Account:"RECIPIENTOLD"));store.SaveDocumentSettings(sender,store.DocumentsDirectory);
        var issue=store.Issue(show.Id,[seats[0]],recipient,"R016",1);
        store.SaveOrganizationDetails(sender,true,new(Name:sender,Account:"SENDERNEW"));store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Account:"RECIPIENTNEW"));
        var refresh=typeof(Store).GetMethod("RefreshInvoiceRecipient");if(refresh is null)throw new Exception("Recipient-only refresh is missing");refresh.Invoke(store,[show.Id,issue]);
        if(store.GetIssueOrganizationDetails(issue,true).Account!="SENDEROLD"||store.GetIssueOrganizationDetails(issue,false).Account!="RECIPIENTNEW")throw new Exception("Recipient refresh changed wrong party");
        var reopened=new Store(store.Path);if(reopened.GetOrganizationDetails(recipient,false).Account!="RECIPIENTNEW")throw new Exception("Recipient edit did not persist");
        var next=reopened.Issue(show.Id,[seats[1]],recipient,"R016",2);if(reopened.GetIssueOrganizationDetails(next,false).Account!="RECIPIENTNEW")throw new Exception("Future invoice missed updated recipient");
    });
}
