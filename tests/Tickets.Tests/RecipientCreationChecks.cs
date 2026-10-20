using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class RecipientCreationChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("recipient can be saved without an issue and restored without renaming",s=>{
            var name=s.SaveRecipient("  Новая   организация Ёлка  ");
            Require(name=="Новая организация Ёлка","recipient whitespace was not normalized");
            Require(s.RecipientHistory(name).Count==0,"creation issued tickets");
            Require(new Store(s.Path).RecipientDirectory().Single(x=>x.Name==name).IssueCount==0,"independent recipient not persisted");
            Require(s.SaveRecipient("новая организация елка")==name,"duplicate changed existing display name");
            Require(s.Recipients().Count(x=>CatalogText.Key(x)==CatalogText.Key(name))==1,"normalized duplicate inserted");
            s.DeleteRecipient(name);
            Require(!s.Recipients().Contains(name),"recipient deletion did not hide choice");
            Require(s.SaveRecipient("НОВАЯ ОРГАНИЗАЦИЯ ЕЛКА")==name,"restoration renamed recipient");
            Require(new Store(s.Path).Recipients().Contains(name),"explicit creation did not persist restoration");
            Reject(()=>s.SaveRecipient("   "));Reject(()=>s.SaveRecipient(new string('я',201)));Reject(()=>s.SaveRecipient("Получатель\nстрока"));
        });
        test("retired sender variants stay excluded without changing invoice snapshots",s=>{
            var e=s.Events().First(x=>s.Seats(x.Id).Any(y=>y.Available));
            var seat=s.Seats(e.Id).First(x=>x.Available).Id;
            var issue=s.Issue(e.Id,new[]{seat},"Получатель проверки отправителя","SENDER",1);
            const string retired="ИП Баринов А. С.";
            s.UpdateInvoiceSender(e.Id,issue,retired);
            s.SaveSpecification("Получатель проверки отправителя","ИП Баринов А.С.","Спецификация","Текст");
            using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=s.Path,Pooling=false}.ToString()))
            {
                c.Open();using var cmd=c.CreateCommand();cmd.CommandText="""
                    INSERT OR IGNORE INTO sender_directory(sender_name) VALUES('ИП Баринов А С'),('ИП Баринов А.С.');
                    INSERT INTO document_setting(name,value) VALUES('sender','ИП Баринов А. С.') ON CONFLICT(name) DO UPDATE SET value=excluded.value;
                    """;cmd.ExecuteNonQuery();
            }
            var reopened=new Store(s.Path);
            Require(reopened.Sender==Store.DefaultSender,"retired default sender persisted");
            Require(!reopened.Senders().Any(x=>x.Contains("Баринов",StringComparison.OrdinalIgnoreCase)),"retired sender imported back into choices");
            Require(reopened.Invoice(e.Id,issue).Sender==retired,"historical invoice sender was rewritten");
            Require(reopened.Specifications().Single(x=>x.Recipient=="Получатель проверки отправителя").Sender=="ИП Баринов А.С.","historical specification sender was rewritten");
            reopened.UpdateInvoiceSender(e.Id,issue,"Другой отправитель");
            Require(!reopened.Senders().Contains(retired),"remembering previous invoice sender restored retired choice");
            reopened.RemoveSenderChoice("Другой отправитель");
            Require(!new Store(s.Path).Senders().Contains("Другой отправитель"),"sender deletion was resurrected by invoice source");
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected TicketException");}
}
