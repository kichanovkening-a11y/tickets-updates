using Tickets.Core;
using Microsoft.Data.Sqlite;

internal static class Senders020Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("020 deleting default sender never offers it again through fallback settings",store=>{
            store.SaveDocumentSettings(Store.DefaultSender,store.DocumentsDirectory);
            store.RemoveSenderChoice(Store.DefaultSender);
            var reopened=new Store(store.Path);
            Require(reopened.Sender!=Store.DefaultSender,"deleted default sender returned through default setting fallback");
            Reject(()=>reopened.SaveDocumentSettings(Store.DefaultSender,reopened.DocumentsDirectory));
            Require(!new Store(store.Path).Senders().Contains(Store.DefaultSender),"settings recreated removed sender");
            var show=reopened.Events().First(e=>reopened.Seats(e.Id).Any(s=>s.Available));
            var issue=reopened.Issue(show.Id,[reopened.Seats(show.Id).First(s=>s.Available).Id],"Получатель020 запасной","З020",1);
            Require(reopened.Invoice(show.Id,issue).Sender==reopened.Sender&&reopened.Senders().Contains(reopened.Invoice(show.Id,issue).Sender),"new issue used a removed raw settings sender");
        });
        test("020 stale sender settings and selection cannot restore a deleted sender",store=>{
            const string sender="Отправитель020 удалённый";
            store.SaveSender(sender);store.SaveOrganizationDetails(sender,true,new(Name:sender,Inn:"1234567890"));
            store.SaveDocumentSettings(sender,store.DocumentsDirectory);
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
            var first=store.Issue(show.Id,[store.Seats(show.Id).First(s=>s.Available).Id],"Получатель020","У020",1);
            store.SaveSpecification("Получатель020",sender,"Спецификация","Историческая спецификация");
            store.RemoveSenderChoice(sender);
            var reopened=new Store(store.Path);
            Reject(()=>reopened.SaveDocumentSettings("  отправитель020   удаленный ",reopened.DocumentsDirectory));
            reopened.UpdateInvoiceSender(show.Id,first,sender); // Unchanged historical data remains valid.
            reopened.SaveDocumentSettings(Store.DefaultSender,reopened.DocumentsDirectory);
            var second=reopened.Issue(show.Id,[reopened.Seats(show.Id).First(s=>s.Available).Id],"Другой020","У020",2);
            Reject(()=>reopened.UpdateInvoiceSender(show.Id,second,sender));
            reopened.SaveSpecification("Получатель020",sender,"Спецификация","Изменённый текст");
            reopened=new Store(store.Path);
            Require(!reopened.Senders().Contains(sender),"sender returned from historical invoice or specification");
            Require(reopened.Invoice(show.Id,first).Sender==sender&&reopened.Invoice(show.Id,first).SenderDetails?.Inn=="1234567890","removal changed historical sender snapshot");
            Require(reopened.History(show.Id).Any(t=>t.IssueId==first),"removal erased issuance history");
            Require(reopened.Specifications().Single(s=>s.Recipient=="Получатель020").Sender==sender,"removal rewrote historical specification");
            reopened.SaveSender(sender);
            Require(new Store(store.Path).Senders().Contains(sender),"explicit sender creation cannot restore sender");
        });
        test("020 directory initialization respects an existing sender deletion",store=>{
            const string sender="ИП Баринов А.С.";
            store.RemoveSenderChoice(sender);
            using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=store.Path,Pooling=false}.ToString()))
            {
                c.Open();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM document_setting WHERE name='organization-directory-seed-v1'";cmd.ExecuteNonQuery();
            }
            Require(!new Store(store.Path).Senders().Contains(sender),"directory migration seed removed the user's deletion");
        });
        test("020 removing every sender leaves no hidden fallback choice",store=>{
            foreach(var name in store.Senders())store.RemoveSenderChoice(name);
            var reopened=new Store(store.Path);
            Require(reopened.Senders().Count==0&&reopened.Sender=="","all-deleted directory silently selected a hidden sender");
            var show=reopened.Events().First(e=>reopened.Seats(e.Id).Any(s=>s.Available));
            var before=reopened.History(show.Id).Count;
            Reject(()=>reopened.Issue(show.Id,[reopened.Seats(show.Id).First(s=>s.Available).Id],"Получатель020 без отправителя","Б020",1));
            Require(reopened.History(show.Id).Count==before,"rejected issuance changed ticket history");
            var created=reopened.SaveSender("ИП Новый021");reopened.SaveDocumentSettings(created,reopened.DocumentsDirectory);
            var id=reopened.Issue(show.Id,[reopened.Seats(show.Id).First(s=>s.Available).Id],"Получатель021 новый","Б021",1);
            Require(new Store(store.Path).Invoice(show.Id,id).Sender==created,"creating a sender after removing all choices cannot issue tickets");
        });
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
