using Tickets.Core;
internal static class OrgMerge012Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("additional organization fields persist globally and preserve historical snapshots",s=>{
            const string name="Организация с реквизитами";
            var details=new OrganizationDetails(Name:name,ExtraFields:new[]{new OrganizationExtraField(" Лицензия "," Л-123 "),new OrganizationExtraField("Контакт","")});
            s.SaveOrganizationDetails(name,false,details);s.SaveOrganizationDetails(name,true,details);
            var saved=new Store(s.Path).GetOrganizationDetails(name,false);
            Check(saved.ExtraFields is { Count:2 }&&saved.ExtraFields[0]==new OrganizationExtraField("Лицензия","Л-123"),"custom fields did not persist trimmed");
            Check(saved.HasRequisites&&saved.DisplayText.Contains("Лицензия: Л-123")&&!saved.DisplayText.Contains("Контакт"),"custom field display wrong");
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=2);var seats=s.Seats(e.Id).Where(x=>x.Available).Take(2).Select(x=>x.Id).ToArray();
            var issue=s.Issue(e.Id,new[]{seats[0]},name,"EXTRAS012",1);
            s.SaveOrganizationDetails(name,false,details with{ExtraFields=new[]{new OrganizationExtraField("Лицензия","Новая")}});
            Check(s.Invoice(e.Id,issue).RecipientDetails!.ExtraFields![0].Value=="Л-123","historical requisites changed silently");
            s.RefreshInvoiceOrganizations(e.Id,issue);Check(s.Invoice(e.Id,issue).RecipientDetails!.ExtraFields![0].Value=="Новая","explicit snapshot refresh lost extras");
            var next=s.Issue(e.Id,new[]{seats[1]},name,"EXTRAS012",2);Check(s.Invoice(e.Id,next).RecipientDetails!.ExtraFields![0].Value=="Новая","new invoice did not share global directory");
            Reject(()=>s.SaveOrganizationDetails("Не создавать",false,new(ExtraFields:new[]{new OrganizationExtraField("","значение")})));
            Check(!s.RecipientDirectory().Any(x=>x.Name=="Не создавать"),"invalid edit created directory entry");
            Reject(()=>s.SaveOrganizationDetails(name,false,new(ExtraFields:new[]{new OrganizationExtraField("Код","1"),new OrganizationExtraField("код","2")})));
            Reject(()=>s.SaveOrganizationDetails(name,true,new(ExtraFields:new[]{new OrganizationExtraField("Код","строка\nдва")})));
            Check(s.GetOrganizationDetails(name,false).ExtraFields![0].Value=="Новая","invalid edit altered saved fields");
        });
        test("invoice merge preserves original ticket history, states and references across chained merges",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=6);var seats=s.Seats(e.Id).Where(x=>x.Available).Take(6).Select(x=>x.Id).ToArray();
            var first=s.Issue(e.Id,new[]{seats[0],seats[1]},"Общий получатель","MERGE-A",10);
            var second=s.Issue(e.Id,new[]{seats[2],seats[3]},"Общий получатель","MERGE-B",30);
            var third=s.Issue(e.Id,new[]{seats[4]},"Общий получатель","MERGE-C",50);
            var firstInvoice=s.Invoice(e.Id,first);var secondInvoice=s.Invoice(e.Id,second);
            s.MarkTicketsPrinted(e.Id,new[]{secondInvoice.Tickets[0].Id});s.Return(new[]{firstInvoice.Tickets[0].Id});
            var before=s.History(e.Id).Where(t=>new[]{first,second,third}.Contains(t.IssueId)).OrderBy(t=>t.Id).ToArray();
            Check(s.MergeInvoices(e.Id,new[]{first,second})==first,"wrong primary identity");
            Check(s.Invoice(e.Id,first).Tickets.Count==4&&s.Invoice(e.Id,second).Tickets.Count==2,"combined/source invoice membership wrong");
            Check(s.Issues(e.Id).Any(x=>x.Id==first&&x.Count==4&&x.ReturnedCount==1&&x.PrintedCount==1)&&!s.Issues(e.Id).Any(x=>x.Id==second),"journal did not aggregate merge");
            Check(s.MergeInvoices(e.Id,new[]{third,first})==third&&s.ResolveInvoiceId(second)==third,"chained merge resolution wrong");
            var reopened=new Store(s.Path);Check(reopened.GetInvoiceMergeSources(e.Id,third).Count==2&&reopened.Invoice(e.Id,third).Tickets.Count==5,"merge audit did not persist");
            Check(before.SequenceEqual(reopened.History(e.Id).Where(t=>new[]{first,second,third}.Contains(t.IssueId)).OrderBy(t=>t.Id)),"merge mutated original ticket numbers, series, prices, state or timestamps");
            var returned=secondInvoice.Tickets[1].Id;s.Return(new[]{returned});Check(s.Invoice(e.Id,third).Tickets.Single(t=>t.Id==returned).ReturnedAt is not null,"combined invoice lost subsequent returns");
            Check(s.Invoice(e.Id,second).Recipient==secondInvoice.Recipient&&s.Invoice(e.Id,second).IssuedAt==secondInvoice.IssuedAt,"source header changed");
            var canonicalBefore=s.Invoice(e.Id,third);
            s.Return(canonicalBefore.Tickets.Where(t=>t.IssueId==third).Select(t=>t.Id).ToArray());
            Check(s.ExtendInvoice(e.Id,second,new[]{seats[5]})==third,"extension through old reference did not resolve canonical invoice");
            var extended=s.Invoice(e.Id,third);var added=extended.Tickets.Single(t=>t.SeatId==seats[5]);
            Check(added.IssueId==third&&added.Series=="MERGE-C"&&added.Number==51&&added.IssuedAt==canonicalBefore.IssuedAt,"merged extension changed canonical ownership, series or date");
            Check(extended.Recipient==canonicalBefore.Recipient&&extended.Sender==canonicalBefore.Sender&&s.Invoice(e.Id,second).Tickets.Count==2,"merged extension altered source history or canonical parties");
        });
        test("invoice merge validates every source atomically and rejects cross party, event and reservations",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Count(y=>y.Available)>=6);var seats=s.Seats(e.Id).Where(x=>x.Available).Take(6).Select(x=>x.Id).ToArray();
            var a=s.Issue(e.Id,new[]{seats[0]},"Один","ATOMIC012",1);var b=s.Issue(e.Id,new[]{seats[1]},"Один","ATOMIC012",2);
            var other=s.Issue(e.Id,new[]{seats[2]},"Другой","ATOMIC012",3);var reserved=s.Reserve(e.Id,new[]{seats[3]},"Один","ATOMIC012",4);
            Reject(()=>s.MergeInvoices(e.Id,new[]{a,b,other}));Reject(()=>s.MergeInvoices(e.Id,new[]{a,b,-1L}));Reject(()=>s.MergeInvoices(e.Id,new[]{a,reserved}));
            Reject(()=>s.MergeInvoices(e.Id+999999,new[]{a,b}));Reject(()=>s.MergeInvoices(e.Id,new[]{a,a}));
            Check(s.ResolveInvoiceId(b)==b&&s.Invoice(e.Id,a).Tickets.Count==1&&s.GetInvoiceMergeSources(e.Id,a).Count==0,"rejected merge partially changed membership");
            s.UpdateInvoiceSender(e.Id,b,"Другой отправитель");Reject(()=>s.MergeInvoices(e.Id,new[]{a,b}));s.UpdateInvoiceSender(e.Id,b,s.Invoice(e.Id,a).Sender);
            s.Return(s.Invoice(e.Id,b).Tickets.Select(t=>t.Id).ToArray());Reject(()=>s.MergeInvoices(e.Id,new[]{a,b}));
        });
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected rejected invalid organization/merge operation");}
}
