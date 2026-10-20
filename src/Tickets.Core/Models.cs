namespace Tickets.Core;

public sealed record EventInfo(long Id, long VenueId, string Title, string Venue, string Date,
    string SortDate, int SeatCount, int IssuedCount)
{
    public string Caption => $"{Title} · {Date}";
}
public sealed record SeatInfo(long Id, string Sector, string Row, string Number, double X, double Y,
    long? Amount, string Color, long? TicketId, string Recipient, string Series, long? TicketNumber)
{
    public bool Available => Amount.HasValue && !TicketId.HasValue;
    public string Label => $"{Sector} · ряд {Row}, место {Number}";
    public string Status => TicketId.HasValue ? $"Выдан: {Recipient}" : Amount.HasValue ? "Свободно" : "Нет цены";
}
public sealed record SectorInfo(string Name, double X, double Y);
public sealed record TicketInfo(long Id, long SeatId, string Sector, string Row, string Seat,
    string Recipient, string Series, long Number, long Amount, string IssuedAt, string? ReturnedAt, long IssueId=0)
{
    public string Status => ReturnedAt is null ? "Выдан" : "Возвращён";
    public string Code => $"{Series} {Number:D6}";
}
public sealed class TicketException(string message) : Exception(message);

public sealed record IssueInfo(long Id,long EventId,string Recipient,string IssuedAt,int Count,int ReturnedCount,decimal Total)
{
    public string Status=>ReturnedCount==0?"Выдано":ReturnedCount==Count?"Возвращено":"Частичный возврат";
    public string Date=>DateTimeOffset.Parse(IssuedAt).ToString("dd.MM.yyyy HH:mm");
}
public sealed record InvoiceInfo(long Id,long EventId,string Title,string Venue,string EventDate,string Recipient,string IssuedAt,IReadOnlyList<TicketInfo> Tickets,string Sender="ООО \"Альянс ТА\"")
{
    public decimal Total=>Tickets.Sum(t=>(decimal)t.Amount);
    public int ActiveCount=>Tickets.Count(t=>t.ReturnedAt is null);
    public decimal ActiveTotal=>Tickets.Where(t=>t.ReturnedAt is null).Sum(t=>(decimal)t.Amount);
    public string Date=>DateTimeOffset.Parse(IssuedAt).ToString("dd.MM.yyyy HH:mm");
}
