using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tickets.Core;

/// <summary>Reads static SSRS HTML4.0. Nested layout tables are not ticket rows.</summary>
internal static class TicketlandHtml028
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly Regex Heading = new(@"(?<title>[^\r\n]+?)\s+(?<date>\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}(?::\d{2})?)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static string Clean(string s) => Regex.Replace(WebUtility.HtmlDecode(s).Replace('\u00a0', ' ').Replace('\u202f', ' '), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Trim();

    internal static bool IsLogin(string html) => Tokens(html).Any(t => !t.End && t.Name == "input" &&
        (t.Attr.GetValueOrDefault("name") == "TxtPwd" || t.Attr.GetValueOrDefault("id") == "TxtPwd"));

    internal static (string Action, Dictionary<string, string> Fields) LoginForm(string html)
    {
        var inForm = false;
        var action = "";
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasUser = false;
        foreach (var token in Tokens(html))
        {
            if (token.Name == "form" && !token.End) { inForm = true; action = token.Attr.GetValueOrDefault("action", ""); fields.Clear(); hasUser = false; }
            else if (token.Name == "form" && token.End)
            {
                if (hasUser && fields.ContainsKey("TxtPwd")) return (action, fields);
                inForm = false;
            }
            else if (inForm && token.Name == "input" && !token.End)
            {
                var name = token.Attr.GetValueOrDefault("name", "");
                var type = token.Attr.GetValueOrDefault("type", "text").ToLowerInvariant();
                if (name.Length == 0 || token.Attr.ContainsKey("disabled") || type is "button" or "reset" or "file") continue;
                if (type is "checkbox" or "radio" && !token.Attr.ContainsKey("checked")) continue;
                fields[name] = token.Attr.GetValueOrDefault("value", "");
                if (name == "TxtUser") hasUser = true;
            }
        }
        throw new TicketException("Не удалось распознать форму входа Ticketland.");
    }

    internal static IReadOnlyList<TicketlandSale028> ParseReport(string html)
    {
        if (IsLogin(html)) throw new TicketException("Ticketland запросил повторный вход. Отчёт не загружен.");
        var sales = new List<TicketlandSale028>();
        var rows = new Stack<HtmlRow>();
        var outside = new StringBuilder();
        var visible = new StringBuilder();
        var title = "";
        var startsAt = default(DateTime);
        var header = false;
        var closedHtml = false;
        var inScript = 0;
        foreach (var token in Tokens(html))
        {
            if (token.Name is "script" or "style") { inScript += token.End ? -1 : 1; continue; }
            if (inScript > 0) continue;
            if (token.Name == "html" && token.End) closedHtml = true;
            if (token.Name == "#text")
            {
                visible.Append(token.Text).Append(' ');
                if (rows.Count == 0) outside.Append(token.Text);
                else if (rows.Peek().Cell is not null) rows.Peek().Cell!.Append(token.Text);
                continue;
            }
            if (token.Name is "br" or "p" or "div" && token.End || token.Name == "br")
            {
                if (rows.Count == 0) outside.Append('\n');
                else rows.Peek().Cell?.Append(' ');
            }
            if (token.Name == "tr" && !token.End)
            {
                ReadHeading(outside.ToString()); outside.Clear();
                if (rows.Count > 0)
                {
                    if (!rows.Peek().HasChildren) ReadHeading(rows.Peek().Cell?.ToString() ?? "");
                    rows.Peek().HasChildren = true;
                }
                rows.Push(new HtmlRow());
            }
            else if (token.Name is "td" or "th" && rows.Count > 0)
            {
                var row = rows.Peek();
                if (!token.End)
                {
                    if (row.Cell is not null) throw Invalid();
                    row.Cell = new StringBuilder();
                }
                else if (row.Cell is not null)
                {
                    row.Cells.Add(Clean(row.Cell.ToString())); row.Cell = null;
                }
            }
            else if (token.Name == "tr" && token.End)
            {
                if (rows.Count == 0) throw Invalid();
                var row = rows.Pop();
                if (row.Cell is not null) throw Invalid();
                if (!row.HasChildren) ReadRow(row.Cells);
            }
        }
        var text = Clean(visible.ToString());
        if (!closedHtml || rows.Count != 0 || !header || text.Contains("rsProcessingAborted", StringComparison.OrdinalIgnoreCase)
            || text.Contains("rsError", StringComparison.OrdinalIgnoreCase) || text.Contains("Report Server Error", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        if (sales.Count == 0 && !text.Contains("Нет данных", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("Данные отсутствуют", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        return sales;

        void ReadHeading(string text)
        {
            foreach (var line in WebUtility.HtmlDecode(text).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Heading.Match(Clean(line));
                if (match.Success && DateTime.TryParseExact(match.Groups["date"].Value, new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm" }, Russian, DateTimeStyles.None, out var dt))
                {
                    title = Clean(match.Groups["title"].Value); startsAt = dt;
                }
            }
        }
        void ReadRow(List<string> cells)
        {
            var combined = string.Join(" ", cells);
            ReadHeading(combined);
            if (cells.Any(c => c.Contains("Штрих-код", StringComparison.OrdinalIgnoreCase)))
            {
                // SSRS continuation tables can omit repeated headers. Keep the established
                // contract, but validate every header that is present, including later shows.
                if (cells.Count is not (7 or 8) || !cells[1].Contains("Штрих-код", StringComparison.OrdinalIgnoreCase)
                    || cells[2] != "Сектор" || cells[3] != "Ряд" || cells[4] != "Место"
                    || !cells[5].Contains("Стоимость", StringComparison.OrdinalIgnoreCase)) throw Invalid();
                header = true; return;
            }
            if (cells.Count == 0) return;
            if (!int.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal))
            {
                // A damaged ordinal must not turn an otherwise recognizable sale into an ignored row.
                if (cells.Count >= 6 && cells[1].Length >= 6 && cells[1].All(char.IsDigit)) throw Invalid();
                return;
            }
            if (ordinal < 1 || !header || cells.Count != 8 || title.Length == 0 || startsAt == default || cells[1].Length == 0 || cells[2].Length == 0) throw Invalid();
            if (!decimal.TryParse(cells[5], NumberStyles.Number, Russian, out var nominal) || nominal < 0) throw Invalid();
            // The portal uses --/-- for gifts/general admission. Preserve those values for matching;
            // never manufacture a numbered seat from the row ordinal or barcode.
            if (cells[3].Length == 0 || cells[4].Length == 0) throw Invalid();
            sales.Add(new TicketlandSale028(title, startsAt, cells[2], cells[3], cells[4], cells[1], nominal));
        }
    }

    private static TicketException Invalid() => new("Ticketland вернул неполный или нераспознанный отчёт. Продажи не изменены.");
    private sealed class HtmlRow
    {
        internal readonly List<string> Cells = new();
        internal StringBuilder? Cell;
        internal bool HasChildren;
    }
    private sealed record Token(string Name, bool End, Dictionary<string, string> Attr, string Text = "");

    // Small quote-aware tokenizer; only structure/text needed by forms and SSRS tables is retained.
    // No scripts, external entities, URLs or remote resources are evaluated.
    private static IEnumerable<Token> Tokens(string html)
    {
        var empty = new Dictionary<string, string>();
        for (var i = 0; i < html.Length;)
        {
            if (html[i] != '<')
            {
                var end = html.IndexOf('<', i); if (end < 0) end = html.Length;
                yield return new Token("#text", false, empty, html[i..end]); i = end; continue;
            }
            if (html.AsSpan(i).StartsWith("<!--", StringComparison.Ordinal))
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal); if (end < 0) yield break;
                i = end + 3; continue;
            }
            var p = i + 1; var quote = '\0';
            for (; p < html.Length; p++)
            {
                var c = html[p];
                if (quote != '\0') { if (c == quote) quote = '\0'; }
                else if (c is '\'' or '"') quote = c;
                else if (c == '>') break;
            }
            if (p == html.Length) yield break;
            var raw = html[(i + 1)..p]; i = p + 1;
            var at = 0; while (at < raw.Length && char.IsWhiteSpace(raw[at])) at++;
            var closing = at < raw.Length && raw[at] == '/'; if (closing) at++;
            var start = at; while (at < raw.Length && (char.IsLetterOrDigit(raw[at]) || raw[at] is ':' or '_')) at++;
            if (at == start) continue;
            var name = raw[start..at].ToLowerInvariant();
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (at < raw.Length)
            {
                while (at < raw.Length && (char.IsWhiteSpace(raw[at]) || raw[at] == '/')) at++;
                start = at;
                while (at < raw.Length && !char.IsWhiteSpace(raw[at]) && raw[at] is not '=' and not '/') at++;
                if (start == at) break;
                var key = raw[start..at];
                while (at < raw.Length && char.IsWhiteSpace(raw[at])) at++;
                var value = "";
                if (at < raw.Length && raw[at] == '=')
                {
                    at++; while (at < raw.Length && char.IsWhiteSpace(raw[at])) at++;
                    if (at < raw.Length && raw[at] is '\'' or '"')
                    {
                        var q = raw[at++]; start = at; while (at < raw.Length && raw[at] != q) at++;
                        value = raw[start..at]; if (at < raw.Length) at++;
                    }
                    else { start = at; while (at < raw.Length && !char.IsWhiteSpace(raw[at])) at++; value = raw[start..at]; }
                }
                attrs[key] = WebUtility.HtmlDecode(value);
            }
            yield return new Token(name, closing, attrs);
        }
    }
}
