using Tickets.Core;

internal static class SenderRequisitesChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("restored sender remains selectable and user deletions survive reopening",s=>{
            const string restored="ИП Баринов А.С.";
            Require(s.Senders().Contains(restored),"exact restored sender seed missing");
            s.RemoveSenderChoice("ИП Баринов А С");
            Require(s.Senders().Contains(restored),"deleting unpunctuated sender hid distinct requested sender");
            s.SaveDocumentSettings(restored,s.DocumentsDirectory);
            Require(new Store(s.Path).Sender==restored,"restored sender suppressed as document default");
            s.RemoveSenderChoice(restored);
            Require(!new Store(s.Path).Senders().Contains(restored),"one-time seed resurrected deleted sender");
            Require(s.SaveSender(restored)==restored,"explicit sender restoration changed name");
            Require(new Store(s.Path).Senders().Contains(restored),"explicit save did not restore sender choice");
            var custom=s.SaveSender("  ИП   Новый Отправитель  ");
            Require(custom=="ИП Новый Отправитель","sender whitespace not normalized");
            Require(s.SaveSender("ип новый отправитель")==custom,"case-insensitive sender duplicate inserted");
            Reject(()=>s.SaveSender(" "));Reject(()=>s.SaveSender("Отправитель\nстрока"));
        });
        test("organization requisites persist separately by role and retain historical snapshots",s=>{
            var name=s.SaveSender("Организация с реквизитами");
            s.SaveOrganizationDetails(name,true,new(Name:name,Inn:"1234567890",Account:"40702810000000000001",Signatory:"Иванов И.И."));
            s.SaveOrganizationDetails(name,false,new(Name:name,Inn:"0987654321",Phone:"+7 900 000-00-00"));
            var reopened=new Store(s.Path);
            Require(reopened.GetOrganizationDetails(name,true).Inn=="1234567890","sender requisites did not persist");
            Require(reopened.GetOrganizationDetails(name,false).Inn=="0987654321","recipient requisites overwrote sender role");
            var e=s.Events().First(x=>s.Seats(x.Id).Any(y=>y.Available));
            var seat=s.Seats(e.Id).First(x=>x.Available).Id;
            var issue=s.Issue(e.Id,new[]{seat},name,"REQ",1);
            s.UpdateInvoiceSender(e.Id,issue,name);
            s.SaveOrganizationDetails(name,true,new(Name:name,Inn:"2222222222"));
            s.SaveOrganizationDetails(name,false,new(Name:name,Inn:"3333333333"));
            s.UpdateInvoiceSender(e.Id,issue,name);
            Require(s.GetIssueOrganizationDetails(issue,true).Inn=="1234567890","unchanged sender save rewrote snapshot");
            Require(s.GetIssueOrganizationDetails(issue,false).Inn=="0987654321","recipient edit rewrote snapshot");
            var next=s.SaveSender("ИП Следующий");s.SaveOrganizationDetails(next,true,new(Name:next,Inn:"444444444444"));s.UpdateInvoiceSender(e.Id,issue,next);
            Require(s.GetIssueOrganizationDetails(issue,true).Inn=="444444444444","changed sender did not capture selected requisites");
            Require(s.Invoice(e.Id,issue).Recipient==name,"requisites edit changed historical recipient name");
            Reject(()=>s.RefreshInvoiceOrganizations(-1,issue));
            s.RefreshInvoiceOrganizations(e.Id,issue);
            Require(new Store(s.Path).GetIssueOrganizationDetails(issue,false).Inn=="3333333333","explicit refresh did not update recipient requisites");
            Require(s.GetIssueOrganizationDetails(issue,true).Inn=="444444444444","refresh captured another sender");
            Reject(()=>s.SaveOrganizationDetails(name,true,new(Inn:"line\nbreak")));
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
