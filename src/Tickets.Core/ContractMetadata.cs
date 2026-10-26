using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Tickets.Core;

public sealed partial class Store
{
    private InvoiceInfo ApplyContractMetadata(InvoiceInfo invoice)
    {
        using var c=Open();EnsureRecipientForms(c);
        using var number=Command(c,"SELECT value FROM issue_contract_number WHERE issue_id=$i","$i",invoice.Id);
        using var date=Command(c,"SELECT value FROM issue_contract_date WHERE issue_id=$i","$i",invoice.Id);
        var sender=DocumentParties.Sender(invoice);
        return invoice with {ContractNumber=number.ExecuteScalar() as string??invoice.Id.ToString(),ContractDate=DateTime.TryParse(date.ExecuteScalar() as string,CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed:DateTimeOffset.Parse(invoice.IssuedAt).Date,SenderDetails=sender};
    }
    public string ContractNumber(long eventId,long issueId)=>Invoice(eventId,issueId).ContractNumber;
    public void SetContractNumber(long eventId,long issueId,string number)
    {
        Invoice(eventId,issueId);number=(number??"").Trim();
        if(number.Length==0||number.Length>100||number.Any(char.IsControl))throw new TicketException("Укажите номер договора (до 100 символов).");
        using var c=Open();EnsureRecipientForms(c);using var cmd=Command(c,"INSERT INTO issue_contract_number(issue_id,value) VALUES($i,$v) ON CONFLICT(issue_id) DO UPDATE SET value=excluded.value","$i",issueId,"$v",number);cmd.ExecuteNonQuery();
    }
    public OrganizationDetails ContractRepresentative(long eventId,long issueId)=>Invoice(eventId,issueId).RecipientDetails??new();
    public void SetContractRepresentative(long eventId,long issueId,string signatory,string position="")
    {
        var invoice=Invoice(eventId,issueId);signatory=(signatory??"").Trim();position=(position??"").Trim();
        if(signatory.Length>2000||position.Length>2000||signatory.Any(char.IsControl)||position.Any(char.IsControl))throw new TicketException("Укажите представителя и должность одной строкой.");
        var details=(invoice.RecipientDetails??new(Name:invoice.Recipient)) with {Signatory=signatory,Position=position};
        using var c=Open();using var cmd=Command(c,"INSERT INTO issue_organization_details(issue_id,role,details) VALUES($i,'recipient',$d) ON CONFLICT(issue_id,role) DO UPDATE SET details=excluded.details","$i",issueId,"$d",JsonSerializer.Serialize(details));cmd.ExecuteNonQuery();
    }
    private void SynchronizeContractRepresentative(long eventId,long issueId,string text)
    {
        // Read only the buyer's explicit clause, never the sender's or unrelated files.
        var invoice=Invoice(eventId,issueId);
        var buyer=string.IsNullOrWhiteSpace(invoice.RecipientDetails?.LegalName)?invoice.Recipient:invoice.RecipientDetails.LegalName;
        var seller=string.IsNullOrWhiteSpace(invoice.SenderDetails?.LegalName)?invoice.Sender:invoice.SenderDetails.LegalName;
        foreach(Match match in Regex.Matches(text,@"в\s+лице\s+([^{}\r\n]+?)(?:,\s*действующ|,\s*с одной стороны|\s+действующ)",RegexOptions.IgnoreCase))
        {
            var prefix=text[Math.Max(0,match.Index-1500)..match.Index];
            var buyerIndex=prefix.LastIndexOf(buyer,StringComparison.OrdinalIgnoreCase);
            var sellerIndex=prefix.LastIndexOf(seller,StringComparison.OrdinalIgnoreCase);
            var buyerRole=Regex.Matches(prefix,@"покупател|заказчик",RegexOptions.IgnoreCase).Cast<Match>().LastOrDefault()?.Index??-1;
            var sellerRole=Regex.Matches(prefix,@"исполнител|продавец|поставщик",RegexOptions.IgnoreCase).Cast<Match>().LastOrDefault()?.Index??-1;
            if(!(buyerIndex>=0&&buyerIndex>sellerIndex)&&!(buyerRole>=0&&buyerRole>sellerRole))continue;
            var clause=match.Groups[1].Value.Trim();
            var name=Regex.Match(clause,@"(?<name>[А-ЯЁ][а-яё-]+\s+(?:[А-ЯЁ][а-яё-]+\s+[А-ЯЁ][а-яё-]+|[А-ЯЁ]\.\s*[А-ЯЁ]\.))$");
            if(name.Success){SetContractRepresentative(eventId,issueId,name.Groups["name"].Value,clause[..name.Index].Trim());return;}
        }
    }
}

internal static class DocumentParties
{
    public static OrganizationDetails Sender(InvoiceInfo invoice)
    {
        var details=invoice.SenderDetails??new(Name:invoice.Sender);
        if(!string.IsNullOrWhiteSpace(details.Signatory))return details;
        var name=CatalogText.Key(invoice.Sender);
        if(name.Contains("БАРИНОВ"))return details with {Signatory="Баринов А.С."};
        if(name.Contains("АЛЬЯНС"))return details with {Signatory="Заборский Л.Н."};
        return details;
    }
    public static string ReceivedBy(InvoiceInfo invoice)
    {
        var recipient=invoice.RecipientDetails;
        return string.IsNullOrWhiteSpace(recipient?.Signatory)?invoice.DocumentRecipient:string.Join(" ",new[]{recipient.Position,recipient.Signatory}.Where(x=>!string.IsNullOrWhiteSpace(x)));
    }
}
