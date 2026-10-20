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
    string Recipient, string Series, long Number, long Amount, string IssuedAt, string? ReturnedAt)
{
    public string Status => ReturnedAt is null ? "Выдан" : "Возвращён";
    public string Code => $"{Series} {Number:D6}";
}
public sealed class TicketException(string message) : Exception(message);
