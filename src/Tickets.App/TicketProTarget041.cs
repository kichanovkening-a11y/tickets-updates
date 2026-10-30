using Tickets.Core;

namespace Tickets.App;

internal static class TicketProTarget041
{
    internal static async Task<string> ReadAsync(long eventId,string? boundUrl,Func<CancellationToken,Task<TicketProSession037.Discovery038>> discover,Func<string,CancellationToken,Task<string>> read,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var url=boundUrl;
        if(url is null)
        {
            var discovery=await discover(token);token.ThrowIfCancellationRequested();
            if(discovery.Errors.Count>0)throw new TicketException(string.Join(" · ",discovery.Errors));
            var urls=discovery.Shows.Where(s=>s.EventId==eventId).Select(s=>s.Url).Distinct().ToArray();
            if(urls.Length!=1)throw new TicketException(urls.Length>1?"Ticket PRO: несколько сеансов совпадают с выбранным показом. Последние данные сохранены.":"Ticket PRO: выбранный показ не найден. Последние данные сохранены.");
            url=urls[0];
        }
        return await read(url,token);
    }
}
