namespace Tickets.Core;

public sealed partial class Store
{
    // Sender requisites are an explicit edit of this invoice's sender snapshot.
    // Do not pick up unrelated recipient-directory edits in the same action.
    public void RefreshInvoiceSender(long eventId,long issueId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var read=Command(c,"SELECT sender FROM issue_batch WHERE id=$i AND event_id=$e","$i",issueId,"$e",eventId);read.Transaction=tx;
        var sender=read.ExecuteScalar() as string??throw new TicketException("Накладная не найдена в этом показе.");
        CaptureIssueOrganization(c,tx,issueId,sender,true);tx.Commit();
    }
}
