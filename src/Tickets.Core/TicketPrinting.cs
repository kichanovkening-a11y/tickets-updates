namespace Tickets.Core;

public sealed partial class Store
{
    // A draft is deliberately made from reads only: previewing or cancelling
    // printing must not reserve tickets, numbers, or recipient records.
    public InvoiceInfo TicketPrintPreview(long eventId,IReadOnlyCollection<long> seatIds,string recipient,string series,long firstNumber)
    {
        ValidatePrintSelection(seatIds);
        recipient=Required(recipient,"Получатель");series=Series(series);
        if(recipient.Length>200)throw new TicketException("Укажите получателя (до 200 символов).");
        if(series.Length is 0 or >32||series.Any(char.IsControl))throw new TicketException("Укажите серию (до 32 символов).");
        if(firstNumber<1||firstNumber>long.MaxValue-seatIds.Count+1)throw new TicketException("Недопустимый диапазон номеров билетов.");
        var show=Events().FirstOrDefault(e=>e.Id==eventId)??throw new TicketException("Показ не найден или удалён из справочника.");
        var available=Seats(eventId).ToDictionary(s=>s.Id);
        if(seatIds.Any(id=>!available.TryGetValue(id,out var seat)||!seat.Available))
            throw new TicketException("Выбранные места уже выданы, сняты с продажи, не имеют цены или не относятся к этому показу.");
        using(var c=Open())
        using(var cmd=Command(c,"SELECT count(*) FROM ticket WHERE series=$series AND number BETWEEN $first AND $last","$series",series,"$first",firstNumber,"$last",firstNumber+(seatIds.Count-1L)))
            if(Convert.ToInt64(cmd.ExecuteScalar())>0)throw new TicketException("Серия и номер уже использованы. Выберите следующий номер или другую серию.");
        var timestamp=DateTimeOffset.Now.ToString("O");var number=firstNumber;
        var tickets=new List<TicketInfo>();
        foreach(var id in seatIds)
        {
            var seat=available[id];tickets.Add(new(0,id,seat.Sector,seat.Row,seat.Number,recipient,series,number,seat.Amount!.Value,timestamp,null));
            if(number<long.MaxValue)number++;
        }
        return new(0,eventId,show.Title,show.Venue,show.Date,recipient,timestamp,tickets,Sender);
    }

    public InvoiceInfo TicketReprint(long eventId,IReadOnlyCollection<long> seatIds)
    {
        ValidatePrintSelection(seatIds);
        var active=History(eventId).Where(t=>t.ReturnedAt is null).ToDictionary(t=>t.SeatId);
        if(seatIds.Any(id=>!active.ContainsKey(id)))throw new TicketException("Для повторной печати выберите только выданные и не возвращённые билеты этого показа.");
        var tickets=seatIds.Select(id=>active[id]).ToList();
        var invoice=Invoice(eventId,tickets[0].IssueId);
        return invoice with{Id=tickets.All(t=>t.IssueId==invoice.Id)?invoice.Id:0,
            Recipient=string.Join("; ",tickets.Select(t=>t.Recipient).Distinct()),Tickets=tickets};
    }

    private static void ValidatePrintSelection(IReadOnlyCollection<long> seatIds)
    {
        if(seatIds.Count==0)throw new TicketException("Выберите места для печати.");
        if(seatIds.Distinct().Count()!=seatIds.Count)throw new TicketException("В списке повторяются места.");
    }
}
