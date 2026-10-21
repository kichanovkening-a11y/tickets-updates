using Tickets.Core;
internal static class Journal016Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("journal sender uses each historical invoice and survives global sender changes and restart",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=3);
            var seats=store.Seats(show.Id).Where(s=>s.Available).Take(3).Select(s=>s.Id).ToArray();
            store.SaveDocumentSettings("ООО Исторический отправитель",store.DocumentsDirectory);
            var first=store.Issue(show.Id,new[]{seats[0]},"Журнал 016","J016",1);
            store.SaveDocumentSettings("ИП Исторический отправитель",store.DocumentsDirectory);
            var second=store.Issue(show.Id,new[]{seats[1]},"Журнал 016","J016",2);
            store.SaveDocumentSettings("ООО Новый отправитель",store.DocumentsDirectory);
            var sender=typeof(IssueInfo).GetProperty("Sender");
            Check(sender is not null,"Journal summaries must expose the historical sender");
            var rows=new Store(store.Path).Issues(show.Id);
            Check((string?)sender!.GetValue(rows.Single(i=>i.Id==first))=="ООО Исторический отправитель","First journal sender changed with global settings");
            Check((string?)sender.GetValue(rows.Single(i=>i.Id==second))=="ИП Исторический отправитель","Second journal sender lost its own snapshot");
            store.ExtendInvoice(show.Id,first,new[]{seats[2]});
            Check(store.Invoice(show.Id,first).Tickets.All(t=>t.Recipient=="Журнал 016"),"Extension changed recipient ownership");
        });
        test("merge rejects distinct recipient identities including legal forms and shared initial names",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=5);
            var seats=store.Seats(show.Id).Where(s=>s.Available).Take(5).Select(s=>s.Id).ToArray();
            var names=new[]{"Оля","Оля Пупкин","ИП Оля","ООО Оля","Оля"};
            var ids=names.Select((name,index)=>store.Issue(show.Id,new[]{seats[index]},name,"PARTY016",index+1)).ToArray();
            foreach(var other in ids.Skip(1).Take(3))
            {
                Reject(()=>store.MergeInvoices(show.Id,new[]{ids[0],other}));
                Check(store.ResolveInvoiceId(other)==other,"Rejected merge changed membership");
            }
            Check(store.MergeInvoices(show.Id,new[]{ids[0],ids[4]})==ids[0],"Same-recipient merge should remain available");
            var invoice=store.Invoice(show.Id,ids[0]);
            Check(invoice.Tickets.All(t=>t.Recipient=="Оля"),"Merged recipient ownership changed");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected recipient identity rejection");}
}
