using Tickets.Core;

internal static class Print016Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("last ticket blank selection survives reopening and standard selection",store=>
        {
            store.RememberTicketBlank(null);
            Check(store.LastTicketBlankId is null,"standard selection is explicit");
            var blank=store.TicketBlanks().First();
            store.RememberTicketBlank(blank.Id);
            Check(new Store(store.Path).LastTicketBlankId==blank.Id,"saved blank survives restart");
            store.RememberTicketBlank(null);
            Check(new Store(store.Path).LastTicketBlankId is null,"explicit standard choice replaces previous selection");
            store.RememberTicketBlank(blank.Id);
            try{store.RememberTicketBlank("missing");throw new Exception("missing blank accepted");}
            catch(TicketException){}
            Check(new Store(store.Path).LastTicketBlankId==blank.Id,"invalid selection leaves previous blank intact");
        });
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
