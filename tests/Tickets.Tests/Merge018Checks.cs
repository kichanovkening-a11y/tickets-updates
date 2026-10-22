using Microsoft.Data.Sqlite;
using Tickets.Core;
internal static class Merge018Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("merge creates the next global invoice and preserves nested source history",s=>{
            var show=s.Events().First(e=>s.Seats(e.Id).Count(x=>x.Available)>=7);
            var seats=s.Seats(show.Id).Where(x=>x.Available).Take(7).Select(x=>x.Id).ToArray();
            const string recipient="Получатель 018";
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"original-018"));
            var first=s.Issue(show.Id,new[]{seats[0],seats[1]},recipient,"MERGE018-A",1);
            var second=s.Issue(show.Id,new[]{seats[2]},recipient,"MERGE018-B",1);
            var third=s.Issue(show.Id,new[]{seats[3]},recipient,"MERGE018-C",1);
            var other=s.Events().First(e=>e.Id!=show.Id&&s.Seats(e.Id).Any(x=>x.Available));
            var otherSeat=s.Seats(other.Id).First(x=>x.Available).Id;
            s.Issue(other.Id,new[]{otherSeat},"Другой показ","MERGE018-GLOBAL",1);
            var globalLast=Last(s);
            var original=s.Invoice(show.Id,first);
            s.MarkTicketsPrinted(show.Id,new[]{original.Tickets[1].Id});s.Return(new[]{original.Tickets[0].Id});
            var history=s.History(show.Id).ToArray();
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"directory-changed-018"));
            var merged=s.MergeInvoices(show.Id,new[]{first,second});
            Check(merged==globalLast+1,"Merge must allocate the next invoice globally, including invoices from other events");
            Check(s.ResolveInvoiceId(first)==merged&&s.ResolveInvoiceId(second)==merged,"All selected sources must resolve to new invoice");
            Check(history.SequenceEqual(s.History(show.Id)),"Merge rewrote ticket history");
            var invoice=s.Invoice(show.Id,merged);
            Check(invoice.Tickets.Count==3&&invoice.RecipientDetails!.Inn=="original-018","Merged membership or copied requisites snapshot wrong");
            Check(s.Invoice(show.Id,first).Tickets.Count==2&&s.Invoice(show.Id,second).Tickets.Count==1,"Original source membership changed");
            var summary=s.Issues(show.Id).Single(x=>x.Id==merged);
            Check(summary.Count==3&&summary.ReturnedCount==1&&summary.PrintedCount==1&&summary.IssuedAt==invoice.IssuedAt,"Merged summary depends on direct owned tickets");
            Check(s.ExtendInvoice(show.Id,first,new[]{seats[4]})==merged,"Old reference extension did not resolve new ID");
            Check(s.Invoice(show.Id,first).Tickets.Count==2,"Extension altered original invoice membership");
            var prior=s.Invoice(show.Id,merged);
            var final=s.MergeInvoices(show.Id,new[]{merged,third});
            Check(final==merged+1&&s.ResolveInvoiceId(second)==final,"Chained merge must allocate and resolve new identity");
            var reopened=new Store(s.Path);
            Check(reopened.Invoice(show.Id,merged).Tickets.SequenceEqual(prior.Tickets),"Historical merged source lost descendant tickets");
            Check(reopened.GetInvoiceMergeSources(show.Id,final).Count==4,"Chained merge audit lost source links");
            Check(reopened.Invoice(show.Id,final).Tickets.Count==5&&reopened.Issues(show.Id).Single(x=>x.Id==final).Count==5,"Final aggregate omitted nested sources");
            var returned=prior.Tickets.Single(t=>t.IssueId==second).Id;s.Return(new[]{returned});
            Check(s.Invoice(show.Id,final).Tickets.Single(t=>t.Id==returned).ReturnedAt is not null,"Subsequent return missing from aggregate");
            Check(s.ExtendInvoice(show.Id,second,new[]{seats[5]})==final,"Chained old reference extension failed");
            Check(s.Invoice(show.Id,merged).Tickets.Count==4&&s.Invoice(show.Id,final).Tickets.Count==6,"New extension leaked into historical merged source");
            Check(s.Issues(show.Id).Single(x=>x.Id==final).ReturnedCount==2,"Active summary did not update returns");
            var next=s.Issue(show.Id,new[]{seats[6]},recipient,"MERGE018-NEXT",1);
            Check(next==final+1,"Normal issuance did not continue numbering after merge");
        });
    }
    private static long Last(Store s)
    {using var c=new SqliteConnection("Data Source="+s.Path);c.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT max(id) FROM issue_batch";return Convert.ToInt64(cmd.ExecuteScalar());}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
