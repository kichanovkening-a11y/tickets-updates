using Tickets.Core;

namespace Tickets.App;

internal static class TicketProReadOnly040
{
    internal const string EmptyBasketJson="{\"status\":true,\"data\":[]}";
    internal static bool ShouldEmulateEmptyBasket(string method,string page,string requestUrl)
        =>method=="POST"&&TicketPro037.IsSchemeUrl(page)&&
          Uri.TryCreate(requestUrl,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&
          uri.Host=="pro.ticketland.ru"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&
          uri.AbsolutePath=="/api/basket/clear"&&uri.Query.Length==0&&uri.Fragment.Length==0;
}
