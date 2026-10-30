using Tickets.Core;

namespace Tickets.App;

internal static class TicketProBatch038
{
    public static async Task<IReadOnlyList<string>> RunAsync(IEnumerable<Func<Task>> reads,CancellationToken token)
    {
        var errors=new List<string>();
        foreach(var read in reads)
        {
            token.ThrowIfCancellationRequested();
            try{await read();}
            catch(TicketException ex){errors.Add(ex.Message);}
            catch(OperationCanceledException) when(!token.IsCancellationRequested){errors.Add("Ticket PRO: время загрузки сеанса истекло. Последние данные сохранены.");}
        }
        return errors;
    }
}
