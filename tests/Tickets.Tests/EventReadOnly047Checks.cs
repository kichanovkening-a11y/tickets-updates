using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class EventReadOnly047Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("047 archived packages retain saved bytes allow lazy reads and reject edits",s=>{
            var venue=s.CreateHall("Комплект архив 047","Адрес",[new DraftSeat()]);
            var seat=s.VenueSeats(venue).Single().Id;
            var plan=s.SavePlan(venue,"Комплект архив 047",[new PriceValue(seat,1000,"#123456")]);
            var performance=s.SavePerformance("Комплект архив 047");
            var first=s.CreateEvent(performance,venue,plan,DateTime.Now.AddDays(20));
            var second=s.CreateEvent(performance,venue,plan,DateTime.Now.AddDays(21));
            const string recipient="Комплект получатель 047";
            s.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7701234567"));
            var a=s.Issue(first,[seat],recipient,"PKARCH047",1);
            var b=s.Issue(second,[seat],recipient,"PKARCH047",2);
            var package=s.CreateRecipientPackage(recipient,[(first,a),(second,b)]);
            var saved=s.GetRecipientPackageDocument(package.Id,"invoice").SourceDocument!;
            void MovePast(long id,int days)
            {
                using var c=new SqliteConnection("Data Source="+s.Path);c.Open();using var q=c.CreateCommand();
                q.CommandText="UPDATE event SET starts_at=$date WHERE id=$id";
                q.Parameters.AddWithValue("$date",DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd HH:mm:ss"));q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();
            }
            MovePast(first,2);
            Require(!s.DocumentArchive().Contains(package),"mixed-date package archived early");
            s.SaveRecipientPackageDocument(package.Id,"invoice",saved);
            var form=Store.DefaultRecipientForm("invoice")!.Content;
            s.SaveRecipientPackageTemplate(package.Id,"invoice",form);
            saved=s.GetRecipientPackageDocument(package.Id,"invoice").SourceDocument!;
            MovePast(second,1);
            Require(s.DocumentArchive().Contains(package),"all-past package remains active");
            Reject(()=>s.SaveRecipientPackageDocument(package.Id,"invoice",saved));
            Reject(()=>s.SaveRecipientPackageTemplate(package.Id,"invoice",form));
            Require(s.GetRecipientPackageDocument(package.Id,"invoice").SourceDocument!.SequenceEqual(saved),"archive package saved bytes changed");
            var lazy=s.GetRecipientPackageDocument(package.Id,"contract");
            Require(lazy.SourceDocument is {Length:>0},"archive lazy document unavailable");
            var reopened=new Store(s.Path).GetRecipientPackageDocument(package.Id,"contract");
            Require(reopened.SourceDocument!.SequenceEqual(lazy.SourceDocument!),"lazy archive rendering did not persist exact bytes");
            var path=Path.Combine(Path.GetDirectoryName(s.Path)!,"archive-package.docx");
            InvoiceDocuments.SaveIssueDocumentWord(reopened,path);
            Require(CustomDocumentForms.Text(File.ReadAllBytes(path))==CustomDocumentForms.Text(lazy.SourceDocument!),"archive package export changed document text");
            Require(s.GetRecipientPackageDocument(package.Id,"contract").SourceDocument!.SequenceEqual(lazy.SourceDocument!),"archive export changed stored bytes");
            Require(s.GetRecipientPackageTemplate(package.Id,"contract").Length>0,"archive template read unavailable");
            Require(s.RecipientPackage(package.Id).Issues.SequenceEqual(package.Issues),"archive membership changed");
        });
        test("047 archived events reject operations atomically and retain documents",s=>{
            var venue=s.CreateHall("Архив 047","Адрес",Enumerable.Range(1,8).Select(n=>new DraftSeat{Number=n.ToString(),X=n*20}).ToArray());
            var seats=s.VenueSeats(venue).Select(x=>x.Id).ToArray();
            var plan=s.SavePlan(venue,"Архив 047",seats.Select(id=>new PriceValue(id,1000,"#123456")).ToArray());
            var performance=s.SavePerformance("Архив 047");
            var past=s.CreateEvent(performance,venue,plan,DateTime.Now.AddDays(10));
            var future=s.CreateEvent(performance,venue,plan,DateTime.Now.AddDays(11));
            var first=s.Issue(past,[seats[0]],"Получатель 047","ARCH047",1);
            var second=s.Issue(past,[seats[1]],"Получатель 047","ARCH047",2);
            var reservation=s.Reserve(past,[seats[2]],"Получатель 047","ARCH047",3);
            var live=s.Issue(future,[seats[0]],"Получатель 047","LIVE047",1);
            var change=s.SetEventPrices(past,[seats[3]],1200);
            s.SetSeatsBlocked(past,[seats[4]],true);
            var document=s.SaveInvoiceDocument(past,first);var bytes=File.ReadAllBytes(document);
            using(var c=new SqliteConnection("Data Source="+s.Path))
            {
                c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE event SET starts_at=$date WHERE id=$id";
                q.Parameters.AddWithValue("$date",DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"));q.Parameters.AddWithValue("$id",past);q.ExecuteNonQuery();
            }
            Require(s.IsEventReadOnly(past)&&!s.IsEventArchived(past),"past policy changed catalog archive semantics");
            Require(!s.IsEventReadOnly(future),"future show is read-only");
            var before=s.History(past).ToArray();var prices=s.EventPrices(past,seats).ToArray();
            var contractNumber=s.ContractNumber(past,first);var contractDate=s.ContractDate(past,first);
            Reject(()=>s.DeleteEventsPermanently([past]));
            Reject(()=>s.SetContractNumber(past,first,"Изменён"));
            Reject(()=>s.SetContractDate(past,first,DateTime.Today));
            Reject(()=>s.SetContractRepresentative(past,first,"Другой представитель"));
            Reject(()=>s.UpdateInvoiceSender(past,first,s.Sender));
            Reject(()=>s.RefreshInvoiceSender(past,first));
            Reject(()=>s.RefreshInvoiceRecipient(past,first));
            Reject(()=>s.RefreshInvoiceOrganizations(past,first));
            Reject(()=>s.SaveIssueDocument(past,first,"contract","Изменённый договор"));
            Reject(()=>s.SaveCustomIssueForm(past,first,"invoice",bytes));
            Require(s.ContractNumber(past,first)==contractNumber&&s.ContractDate(past,first)==contractDate,"archive contract metadata changed");
            Reject(()=>s.Issue(past,[seats[5]],"Получатель 047","ARCH047",4));
            Reject(()=>s.Reserve(past,[seats[5]],"Получатель 047","ARCH047",4));
            Reject(()=>s.ConfirmReservation(past,reservation));
            Reject(()=>s.ExtendInvoice(past,first,[seats[5]]));
            Reject(()=>s.MergeInvoices(past,[first,second]));
            Reject(()=>s.SetEventPrices(past,[seats[5]],1300));
            Reject(()=>s.UndoPriceChange(change));
            Reject(()=>s.SetSeatsBlocked(past,[seats[5]],true));
            Reject(()=>s.SetSeatsBlocked(past,[seats[4]],false));
            var liveTicket=s.History(future).Single(t=>t.IssueId==live).Id;
            Reject(()=>s.Return([liveTicket,before[0].Id]));
            Require(s.History(future).Single().ReturnedAt is null,"mixed-show return partially committed");
            s.MarkTicketsPrinted(past,before.Select(t=>t.Id).ToArray());
            Require(s.History(past).SequenceEqual(before),"archive ticket history was changed");
            Require(s.EventPrices(past,seats).SequenceEqual(prices),"archive prices changed");
            Require(s.Seats(past).Single(x=>x.Id==seats[4]).IsBlocked,"archive block removed");
            Require(s.Issues(past).Count==3&&s.Invoice(past,first).Tickets.Count==1,"archive journal cannot be read");
            Require(File.ReadAllBytes(document).SequenceEqual(bytes),"saved document changed");
            s.Return([liveTicket]);Require(s.History(future).Single().ReturnedAt is not null,"future return rejected");
            s.DeleteVenue(venue);Require(s.IsEventReadOnly(future)&&s.IsEventArchived(future),"hidden future show is writable");
            Reject(()=>s.SetEventPrices(future,[seats[5]],1300));
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
