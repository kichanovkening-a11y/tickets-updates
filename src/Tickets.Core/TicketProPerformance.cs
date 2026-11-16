using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public static class TicketProPerformance
{
    public static bool SameVenue(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        Store.LinkedTicketProVenue(left) == Store.LinkedTicketProVenue(right);

    public static string? SitePerformanceId(string html, DateTime startsAt, string venue)
    {
        var data = Regex.Match(html, """<script\b[^>]*\bid=["']vip-visible-events["'][^>]*>(.*?)</script>""",
            RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        if (!data.Success) return null;
        using var document = JsonDocument.Parse(data.Groups[1].Value);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in document.RootElement.GetProperty("@graph").EnumerateArray())
        {
            if (!item.TryGetProperty("startDate", out var start) ||
                !DateTimeOffset.TryParse(start.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                date.DateTime != startsAt ||
                !item.TryGetProperty("location", out var location) ||
                !location.TryGetProperty("name", out var name) ||
                !SameVenue(venue, name.GetString() ?? "") ||
                !item.TryGetProperty("offers", out var offer) ||
                !offer.TryGetProperty("url", out var link) ||
                !Uri.TryCreate(new Uri("https://vip-teatre.ru"), link.GetString(), out var url) ||
                url.GetLeftPart(UriPartial.Authority) != "https://vip-teatre.ru" ||
                url.AbsolutePath != "/ticket.html") continue;
            var id = Regex.Match(url.Query, @"^\?performance_id=([1-9][0-9]*)$");
            if (id.Success) ids.Add(id.Groups[1].Value);
        }
        if (ids.Count > 1)
            throw new TicketException("Ticket PRO: на привязанной странице несколько сеансов с этой датой, временем и площадкой.");
        return ids.SingleOrDefault();
    }

    public static string FrameSessionId(string json)
    {
        using var document = JsonDocument.Parse(json);
        var script = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(document.RootElement.GetProperty("script").GetString() ?? ""));
        var start = Regex.Match(script, """\bstart\s*:\s*["'](/[^"']+)["']""");
        var id = Regex.Match(start.Groups[1].Value, @"/t_[0-9]{8}_[0-9]{4}_p_([1-9][0-9]*)/?$");
        if (!id.Success) throw new TicketException("Ticket PRO: во фрейме сайта не найден ID сеанса.");
        return id.Groups[1].Value;
    }
}

public sealed partial class Store
{
    internal static string LinkedTicketProVenue(string text) => Venue029(text) switch
    {
        "МОСКОВСКИЙ ТЕАТР ЭСТРАДЫ" => "ТЕАТР ЭСТРАДЫ",
        var venue => venue
    };

    private long LinkedTicketProPerformance(SqliteConnection db, SqliteTransaction tx, string title, string page)
    {
        using var read = Command(db, "SELECT value FROM document_setting WHERE name='home-repertoire'");
        read.Transaction = tx;
        var cards = read.ExecuteScalar() is string layout ? JsonSerializer.Deserialize<HomeCard[]>(layout) ?? [] : [];
        var ids = cards.Where(card => !card.Hidden && card.Page == page && card.PerformanceId > 0)
            .Select(card => card.PerformanceId).Distinct().ToArray();
        if (ids.Length != 1) return 0;

        using var performance = Command(db, "SELECT title FROM performance WHERE id=$id", "$id", ids[0]);
        performance.Transaction = tx;
        return performance.ExecuteScalar() is string name && CatalogText.Key(name) == CatalogText.Key(title) ? ids[0] : 0;
    }

    private string? ReadTicketProCard(SqliteConnection db, SqliteTransaction tx, long performance, string page)
    {
        using var read = Command(db, "SELECT card_url FROM ticketpro_performance WHERE performance_id=$id AND page=$page",
            "$id", performance, "$page", page);
        read.Transaction = tx;
        return read.ExecuteScalar() as string;
    }

    public string? TicketProCardForPage(long eventId, string page)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var read = Command(db, "SELECT title FROM event WHERE id=$id", "$id", eventId);
        read.Transaction = tx;
        if (read.ExecuteScalar() is not string title) return null;
        var performance = LinkedTicketProPerformance(db, tx, title, page);
        return performance > 0 ? ReadTicketProCard(db, tx, performance, page) : null;
    }

    public void ApplyLinkedTicketPro(long eventId, TicketProSnapshot037 snapshot, string page, string expectedUrl) =>
        ApplyTicketPro037(eventId, snapshot, page, expectedUrl);
}
