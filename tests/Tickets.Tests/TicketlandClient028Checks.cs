using System.Net;
using System.Text;
using Tickets.Core;

internal static class TicketlandClient028Checks
{
    private const string Login = "<html><body><form action='/ReportServer/logon.aspx?ReturnUrl=%2freports%2fbrowse%2f' method='post'><input type='hidden' name='__VIEWSTATE' value='a&amp;b'><input type='hidden' name='__EVENTVALIDATION' value='hidden-validation'><input type='text' name='TxtUser'><input type='password' name='TxtPwd'><input type='submit' name='BtnLogon' value='Вход'></form></body></html>";
    private const string Metadata = "{\"value\":[{\"Name\":\"BeginningServerName\",\"ParameterType\":\"DateTime\",\"Prompt\":\"С\"},{\"Name\":\"EndingServerName\",\"ParameterType\":\"DateTime\",\"Prompt\":\"По\"}]}";
    private const string Header = "<tr><td>№ пп</td><td><div>Штрих-код</div></td><td>Сектор</td><td>Ряд</td><td>Место</td><td>Стоимость, руб.</td><td colspan='2'>Комиссия</td></tr>";
    private const string Sale = "<tr><td>1</td><td><div><span>166733602118</span></div></td><td>Амфитеатр левая сторона</td><td>2</td><td>36</td><td>3&nbsp;000,00</td><td>15,00</td><td>450,00</td></tr>";
    private static string Report(string rows = Sale) => "<!DOCTYPE html><html><body><table><tr><td><div>Дубровский 27.10.2026 19:00:00</div><table>" + Header + rows + "<tr><td>Итого</td><td>3 000,00</td></tr></table></td></tr></table></body></html>";

    internal static void Register(Action<string, Action<Store>> run)
    {
        run("028 Ticketland logs in automatically preserves hidden fields and renders whole report", _ => RunHappy().GetAwaiter().GetResult());
        run("028 Ticketland rejects external redirects without sending credentials", _ =>
        {
            var calls = 0;
            using var client = new TicketlandClient028(new Handler((request, ct) => {
                calls++; return Task.FromResult(Redirect("https://outside.invalid/steal"));
            }));
            Reject(client, "secret-user", "secret-password");
            Check(calls == 1, "followed cross-origin redirect");
        });
        run("028 Ticketland rejects unsafe form action before password submission", _ =>
        {
            var calls = 0;
            using var client = new TicketlandClient028(new Handler((request, ct) => {
                calls++; return Task.FromResult(Html(Login.Replace("action='/ReportServer/", "action='http://reports.ticketland.ru/ReportServer/")));
            }));
            Reject(client); Check(calls == 1, "posted credentials to non-HTTPS form");
        });
        run("028 Ticketland rejects failed authentication and does not expose secrets", _ =>
        {
            var calls = 0;
            using var client = new TicketlandClient028(new Handler((request, ct) => { calls++; return Task.FromResult(Html(Login)); }));
            Reject(client); Check(calls == 2, "continued after failed login");
        });
        run("028 Ticketland does not forward submitted passwords through redirects", _ =>
        {
            foreach (var external in new[] { true, false })
            {
                var calls = 0;
                using var client = new TicketlandClient028(new Handler((request, ct) => {
                    calls++;
                    if (calls == 1) return Task.FromResult(Html(Login));
                    var result = Redirect(external ? "https://outside.invalid/steal" : "/ReportServer/other");
                    if (!external) result.StatusCode = HttpStatusCode.TemporaryRedirect;
                    return Task.FromResult(result);
                }));
                Reject(client); Check(calls == 2, "forwarded login request through unsafe redirect");
            }
        });
        run("028 Ticketland rejects ambiguous report parameter identities", _ =>
        {
            using var client = Session(Metadata.Replace("\"Prompt\":\"По\"", "\"Prompt\":\"С\""), Report()); Reject(client);
            using var wrongType = Session(Metadata.Replace("DateTime", "String"), Report()); Reject(wrongType);
        });
        run("028 Ticketland preserves general admission and every show and all pages", _ =>
        {
            var html = Report(Sale + "<tr><td>Подарочный билет 28.10.2026 20:30:00</td></tr>" + Header
                + Sale.Replace("166733602118", "166733602119").Replace("<td>2</td><td>36</td>", "<td>--</td><td>--</td>"));
            using var client = Session(Metadata, html);
            var report = Fetch(client);
            Check(report.Sales.Count == 2, "nested tables duplicated or lost sales");
            Check(report.Sales[1].Title == "Подарочный билет" && report.Sales[1].Row == "--" && report.Sales[1].Seat == "--", "invented general-admission seats or lost show heading");
        });
        run("028 Ticketland refuses truncated login error malformed and unrecognized empty report", _ =>
        {
            foreach (var html in new[] { Report().Replace("</html>", ""), Login, "<html><body>Error</body></html>",
                Report(Sale.Replace("3&nbsp;000,00", "неизвестно")), Report().Replace("<td>36</td>", ""), Report(""),
                Report() .Replace(Header, ""), Report(Sale.Replace("</tr>", "")), Report() .Replace("Итого", "rsProcessingAborted"),
                Report(Sale.Replace("<td>1</td>", "<td>?</td>")), Report().Replace("<td>Ряд</td><td>Место</td>", "<td>Место</td><td>Ряд</td>") })
            {
                using var client = Session(Metadata, html); Reject(client);
            }
        });
        run("028 Ticketland accepts only explicitly complete empty report", _ =>
        {
            using var client = Session(Metadata, Report("<tr><td>Нет данных</td></tr>"));
            Check(Fetch(client).Sales.Count == 0, "explicit empty report rejected");
            using var unrecognized = Session(Metadata, "<html><body>Нет данных</body></html>"); Reject(unrecognized);
        });
        run("028 Ticketland validates headers again for every report section", _ =>
        {
            var secondHeading = "<tr><td>Другой спектакль 28.10.2026 20:30:00</td></tr>";
            var secondSale = Sale.Replace("166733602118", "166733602119");
            var swappedHeader = Header.Replace("<td>Ряд</td><td>Место</td>", "<td>Место</td><td>Ряд</td>");
            foreach (var secondHeadingValue in new[] { secondHeading, "" })
            {
                using var client = Session(Metadata, Report(Sale + secondHeadingValue + swappedHeader + secondSale));
                Reject(client);
            }
            using var continuation = Session(Metadata, Report(Sale + "</table><table>" + secondSale));
            Check(Fetch(continuation).Sales.Count == 2, "SSRS continuation table without repeated header rejected");
        });
        run("028 Ticketland limits response sizes and sanitizes transport errors", _ =>
        {
            using var large = new TicketlandClient028(new Handler((request, ct) => {
                var result = Html("x"); result.Content.Headers.ContentLength = 20 * 1024 * 1024 + 1; return Task.FromResult(result);
            }));
            Reject(large);
            using var broken = new TicketlandClient028(new Handler((request, ct) => throw new IOException("secret-user secret-password")));
            Reject(broken);
        });
        run("028 Ticketland cancellation remains cancellation", _ =>
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            using var client = new TicketlandClient028(new Handler((request, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(Html("")); }));
            try { client.FetchAsync("user", "pass", DateTime.Today, DateTime.Today, cancel.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { return; }
            throw new Exception("cancellation was swallowed");
        });
    }

    private static async Task RunHappy()
    {
        var step = 0;
        using var client = new TicketlandClient028(new Handler(async (request, ct) => {
            step++;
            Check(request.RequestUri!.Scheme == "https" && request.RequestUri.Host == "reports.ticketland.ru", "request escaped origin");
            Check(!request.RequestUri.ToString().Contains("secret-"), "credential in URL");
            switch (step)
            {
                case 1: return Redirect("/ReportServer/logon.aspx");
                case 2: return Html(Login);
                case 3:
                    Check(request.Method == HttpMethod.Post, "login was not posted");
                    var form = await request.Content!.ReadAsStringAsync(ct);
                    Check(form.Contains("__VIEWSTATE=a%26b") && form.Contains("__EVENTVALIDATION=hidden-validation") && form.Contains("TxtUser=secret-user") && form.Contains("TxtPwd=secret-password") && form.Contains("BtnLogon="), "lost login form values");
                    return Redirect("/reports/browse/");
                case 4: Check(request.Method == HttpMethod.Get, "reposted login on redirect"); return Html("<html><body>Reports</body></html>");
                case 5:
                    Check(request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/ParameterDefinitions"), "did not discover REST metadata");
                    Check(Uri.UnescapeDataString(request.RequestUri.AbsolutePath).Contains(TicketlandClient028.ReportPath), "wrong report path");
                    return Html(Metadata);
                case 6:
                    var query = Uri.UnescapeDataString(request.RequestUri.Query);
                    Check(query.Contains(TicketlandClient028.ReportPath) && query.Contains("BeginningServerName=2020-01-01T00:00:00") && query.Contains("EndingServerName=2026-10-28T00:00:00"), "wrong date parameters");
                    Check(query.Contains("rc:Section=0") && query.Contains("rc:Toolbar=false") && query.Contains("rs:Format=HTML4.0"), "report pagination not disabled");
                    return Html(Report());
                default: throw new Exception("unexpected request");
            }
        }));
        var result = await client.FetchAsync("secret-user", "secret-password", new DateTime(2020, 1, 1), new DateTime(2026, 10, 28));
        var sale = result.Sales.Single();
        Check(step == 6 && sale.Title == "Дубровский" && sale.StartsAt == new DateTime(2026, 10, 27, 19, 0, 0) && sale.Nominal == 3000 && sale.Barcode == "166733602118" && sale.Sector == "Амфитеатр левая сторона", "wrong parsed sale");
        Check(result.FetchedAt > DateTimeOffset.UtcNow.AddMinutes(-1), "missing fetch timestamp");
    }
    private static TicketlandClient028 Session(string metadata, string report)
    {
        var calls = 0;
        return new TicketlandClient028(new Handler((request, ct) => Task.FromResult(Html(++calls switch { 1 => "<html><body>reports</body></html>", 2 => metadata, _ => report }))));
    }
    private static TicketlandReport028 Fetch(TicketlandClient028 client) => client.FetchAsync("secret-user", "secret-password", new DateTime(2020, 1, 1), new DateTime(2026, 10, 28)).GetAwaiter().GetResult();
    private static void Reject(TicketlandClient028 client, string user = "secret-user", string password = "secret-password")
    {
        try { client.FetchAsync(user, password, DateTime.Today, DateTime.Today).GetAwaiter().GetResult(); }
        catch (TicketException e) { Check(!e.ToString().Contains(user) && !e.ToString().Contains(password), "exception disclosed credentials"); return; }
        throw new Exception("expected safe TicketException");
    }
    private static HttpResponseMessage Html(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/html") };
    private static HttpResponseMessage Redirect(string url) { var result = new HttpResponseMessage(HttpStatusCode.Found); result.Headers.Location = new Uri(url, UriKind.RelativeOrAbsolute); return result; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
