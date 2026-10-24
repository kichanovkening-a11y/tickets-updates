namespace Tickets.Core;

public sealed partial class Store
{
    public IReadOnlyList<TicketInfo> ReturnedTicketsForIssue(long eventId,long issueId)=>
        InvoiceTickets(eventId,issueId).Where(t=>t.ReturnedAt is not null).ToArray();
}
