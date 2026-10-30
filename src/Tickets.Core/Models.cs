namespace Tickets.Core;

public sealed record EventInfo(long Id, long VenueId, string Title, string Venue, string Date,
    string SortDate, int SeatCount, int IssuedCount)
{
    public string Caption => $"{Title} · {Date}";
}
public sealed record SeatInfo(long Id, string Sector, string Row, string Number, double X, double Y,
    long? Amount, string Color, long? TicketId, string Recipient, string Series, long? TicketNumber, bool IsBlocked=false, long? IssueId=null, bool IsPrinted=false, bool IsReserved=false, bool IsHidden=false)
{
    private readonly RowAisleAnchorList027? rowAisleAnchors;
    public IReadOnlyList<HallEditing025.RowAnchor>? RowAisleAnchors
    {
        get=>rowAisleAnchors;
        init=>rowAisleAnchors=value is null?null:new RowAisleAnchorList027(value);
    }
    public bool IsTicketlandSold {get;init;}
    public bool IsTicketlandOccupied {get;init;}
    public string TicketlandSourceStatus {get;init;}="";
    public bool Available => !IsHidden && !IsTicketlandSold && !IsTicketlandOccupied && Amount.HasValue && !TicketId.HasValue && !IsBlocked;
    public string Label => $"{Sector} · ряд {Row}, место {Number}";
    public string Status => IsTicketlandSold ? (TicketId.HasValue ? $"Конфликт: продано Ticketland и выдано локально: {Recipient}" : "Продано Ticketland") : TicketId.HasValue ? $"{(IsReserved ? "Забронирован" : IsPrinted ? "Накладная выдана, напечатан" : "Накладная выдана, не напечатан")}: {Recipient}" : IsTicketlandOccupied ? "Занято Ticket PRO · "+TicketlandSourceStatus : IsBlocked ? "Заблокировано" : Amount.HasValue ? "Свободно" : "Нет цены";
}
public sealed record SectorInfo(string Name, double X, double Y)
{public SectorCaption029? Caption {get;init;}}
public sealed record TicketInfo(long Id, long SeatId, string Sector, string Row, string Seat,
    string Recipient, string Series, long Number, long Amount, string IssuedAt, string? ReturnedAt, long IssueId=0, string? PrintedAt=null, bool IsReserved=false)
{
    public bool IsPrinted => PrintedAt is not null;
    public string Status => ReturnedAt is not null ? "Возвращён" : IsReserved ? "Забронирован" : IsPrinted ? "Накладная выдана, напечатан" : "Накладная выдана, не напечатан";
    public string Code => $"{Series} {Number:D6}";
}
public sealed class TicketException(string message) : Exception(message);

public sealed record IssueInfo(long Id,long EventId,string Recipient,string IssuedAt,int Count,int ReturnedCount,decimal Total,int ReservedCount=0,int PrintedCount=0)
{
    public string Sender { get; init; } = "";
    public string Status=>ReturnedCount==Count?"Возвращено":ReturnedCount>0?"Частичный возврат":
        ReservedCount==Count?"Забронировано":ReservedCount>0?"Частично выдана накладная":
        PrintedCount==Count?"Накладная выдана, билеты напечатаны":PrintedCount>0?"Накладная выдана, частично напечатано":"Накладная выдана, не напечатано";
    public string Date=>DateTimeOffset.Parse(IssuedAt).ToString("dd.MM.yyyy HH:mm");
}
public sealed record InvoiceInfo(long Id,long EventId,string Title,string Venue,string EventDate,string Recipient,string IssuedAt,IReadOnlyList<TicketInfo> Tickets,string Sender="ООО \"Альянс ТА\"")
{
    public IReadOnlyList<InvoiceInfo> PackageShows { get; init; }=Array.Empty<InvoiceInfo>();
    public string VenueAddress { get; init; }="";
    public string VenueWithAddress=>string.IsNullOrWhiteSpace(VenueAddress)?Venue:Venue+" ("+VenueAddress+")";
    public string ContractNumber { get; init; }="";
    public DateTime? ContractDate { get; init; }
    public OrganizationDetails? SenderDetails { get; init; }
    public OrganizationDetails? RecipientDetails { get; init; }
    public string DocumentRecipient=>string.IsNullOrWhiteSpace(RecipientDetails?.LegalName)?Recipient:RecipientDetails.LegalName;
    public string DocumentSender=>string.IsNullOrWhiteSpace(SenderDetails?.LegalName)?Sender:SenderDetails.LegalName;
    public decimal Total=>Tickets.Sum(t=>(decimal)t.Amount);
    public int ActiveCount=>Tickets.Count(t=>t.ReturnedAt is null);
    public decimal ActiveTotal=>Tickets.Where(t=>t.ReturnedAt is null).Sum(t=>(decimal)t.Amount);
    public string Date=>DateTimeOffset.Parse(IssuedAt).ToString("dd.MM.yyyy HH:mm");
}
