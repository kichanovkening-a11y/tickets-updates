using Tickets.App;

internal static class TicketlandCredentials028UiChecks
{
    internal static void Run()
    {
        // A random target isolates the check from every saved user connection.
        var connection=Guid.NewGuid().ToString("D");
        var original=new TicketlandLogin028("Проверка UI028 "+Guid.NewGuid().ToString("N"),"пароль-🔑-\0-конец");
        try
        {
            Check(TicketlandCredentials028.Read(connection) is null,"fresh isolated credential target is absent");
            TicketlandCredentials028.Save(connection,original);
            Check(TicketlandCredentials028.Read(connection)==original,"credential roundtrip preserves username and exact UTF-16 password including embedded null");
            var replacement=original with{Password="новый пароль"};
            TicketlandCredentials028.Save(connection,replacement);
            Check(TicketlandCredentials028.Read(connection)==replacement,"saving connection replaces its credentials");
            Reject<ArgumentException>(()=>TicketlandCredentials028.Save(connection,original with{Password=new string('x',1281)}));
            Check(TicketlandCredentials028.Read(connection)==replacement,"oversized password leaves existing credential unchanged");
            TicketlandCredentials028.Save(connection,original with{Password=""});
            Check(TicketlandCredentials028.Read(connection)==original with{Password=""},"empty password roundtrips");
            TicketlandCredentials028.Delete(connection);
            Check(TicketlandCredentials028.Read(connection) is null,"deletion removes isolated credential");
            TicketlandCredentials028.Delete(connection);
            foreach(var invalid in new[]{"", "../other", "Tickets.Ticketland."+connection})
            {
                Reject<ArgumentException>(()=>TicketlandCredentials028.Read(invalid));
                Reject<ArgumentException>(()=>TicketlandCredentials028.Save(invalid,original));
                Reject<ArgumentException>(()=>TicketlandCredentials028.Delete(invalid));
            }
        }
        finally{TicketlandCredentials028.Delete(connection);}
    }
    private static void Reject<T>(Action action) where T:Exception
    {
        try{action();}catch(T){return;}throw new Exception("Credential validation did not reject invalid input.");
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 028 "+message);}
}
