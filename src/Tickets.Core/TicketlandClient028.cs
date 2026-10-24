using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Tickets.Core;

public sealed record TicketlandSale028(string Title, DateTime StartsAt, string Sector, string Row, string Seat, string Barcode, decimal Nominal);
public sealed record TicketlandReport028(IReadOnlyList<TicketlandSale028> Sales, DateTimeOffset FetchedAt);

/// <summary>Session-scoped SSRS forms authentication. Credentials are never persisted.</summary>
public sealed class TicketlandClient028 : IDisposable
{
    public const string ReportPath = "/Театры/(TC4OS) Продажи с фрейма площадки и продажи МДТЗК";
    private static readonly Uri Origin = new("https://reports.ticketland.ru/");
    private const int MaxBytes = 20 * 1024 * 1024;
    private readonly HttpClient client;
    public Action<string>? Progress0281 { get; set; }
    private (string From,string To,string Period,string Value)? currentParameters0281;
    public const string CurrentReportPath0281="/Театры/(C4at) По вознаграждению со списком билетов";

    public async Task AuthenticateCurrentAsync(string username,string password,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(username)||string.IsNullOrEmpty(password))throw new TicketException("Введите логин и пароль Ticketland.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(90));
        Progress0281?.Invoke("Ticketland: проверяю вход…");
        try
        {
            var login=await SendAsync(new HttpRequestMessage(HttpMethod.Get,new Uri(Origin,"reports/browse/")),timeout.Token);
            if(TicketlandHtml028.IsLogin(login.Text))
            {
                var form=TicketlandHtml028.LoginForm(login.Text);var target=new Uri(login.Uri,form.Action);EnsureOrigin(target);
                var fields=new Dictionary<string,string>(form.Fields,StringComparer.Ordinal){["TxtUser"]=username,["TxtPwd"]=password};
                using var body=new FormUrlEncodedContent(fields);
                var result=await SendAsync(new HttpRequestMessage(HttpMethod.Post,target){Content=body},timeout.Token);
                if(TicketlandHtml028.IsLogin(result.Text))throw new TicketException("Не удалось войти в Ticketland. Проверьте логин и пароль.");
            }
            var path="reports/api/v2.0/Reports(path='"+Uri.EscapeDataString(CurrentReportPath0281.Replace("'","''"))+"')/ParameterDefinitions";
            var metadata=await SendAsync(new HttpRequestMessage(HttpMethod.Get,new Uri(Origin,path)),timeout.Token);
            var dates=DateParameters(metadata.Text);
            using var document=JsonDocument.Parse(metadata.Text);
            var periods=document.RootElement.GetProperty("value").EnumerateArray().Where(p=>p.TryGetProperty("Prompt",out var prompt)&&prompt.GetString()?.Trim()=="Период"&&p.GetProperty("ParameterType").GetString()=="Integer").ToArray();
            if(periods.Length!=1)throw new TicketException("Не найден выбор периода мероприятий Ticketland.");
            var options=periods[0].GetProperty("ValidValues").EnumerateArray().Where(p=>p.GetProperty("Label").GetString()=="Период мероприятий").ToArray();
            if(options.Length!=1||options[0].GetProperty("Value").GetString()!="1")throw new TicketException("Не удалось подтвердить период мероприятий Ticketland.");
            currentParameters0281=(dates.From,dates.To,periods[0].GetProperty("Name").GetString()!,"1");
            Progress0281?.Invoke("Ticketland: вход выполнен.");
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(TicketException ex){throw new TicketException("Вход в Ticketland не подтверждён. "+ex.Message);}
        catch(OperationCanceledException){throw new TicketException("Превышено время ожидания входа в Ticketland.");}
        catch{throw new TicketException("Не удалось подтвердить вход и доступ к отчёту Ticketland. Проверьте соединение и права доступа.");}
    }

    public async Task<TicketlandReport028> FetchCurrentAsync(string username,string password,DateTime from,DateTime to,CancellationToken ct=default)
    {
        if(from.Date>to.Date)throw new TicketException("Начало периода Ticketland позже окончания.");
        await AuthenticateCurrentAsync(username,password,ct);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(90));
        Progress0281?.Invoke("Ticketland: вход выполнен. Получаю проданные места на текущие сеансы…");
        try
        {
            var parameters=currentParameters0281!.Value;
            var query=new List<string>{Uri.EscapeDataString(CurrentReportPath0281),"rs:Command=Render","rs:Format=XML"};
            query.Add(Uri.EscapeDataString(parameters.From)+"="+Uri.EscapeDataString(from.Date.ToString("yyyy-MM-dd'T'00:00:00",CultureInfo.InvariantCulture)));
            query.Add(Uri.EscapeDataString(parameters.To)+"="+Uri.EscapeDataString(to.Date.ToString("yyyy-MM-dd'T'23:59:59",CultureInfo.InvariantCulture)));
            query.Add(Uri.EscapeDataString(parameters.Period)+"="+parameters.Value);
            var report=await SendAsync(new HttpRequestMessage(HttpMethod.Get,new Uri(Origin,"ReportServer?"+string.Join("&",query))),timeout.Token);
            return new(TicketlandXml0281.Parse(report.Text,from.Date,to.Date),DateTimeOffset.UtcNow);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(TicketException ex){throw new TicketException("Вход в Ticketland выполнен. "+ex.Message);}
        catch(OperationCanceledException){throw new TicketException("Вход в Ticketland выполнен. Превышено время ожидания отчёта текущих сеансов.");}
        catch{throw new TicketException("Вход в Ticketland выполнен. Не удалось получить полный отчёт текущих сеансов. Предыдущие данные сохранены.");}
    }

    public TicketlandClient028(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }, true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<TicketlandReport028> FetchAsync(string username, string password, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new TicketException("Введите логин и пароль Ticketland.");
        if (from.Date > to.Date) throw new TicketException("Начало периода Ticketland позже окончания.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            var login = await SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(Origin, "reports/browse/")), timeout.Token);
            if (TicketlandHtml028.IsLogin(login.Text))
            {
                var form = TicketlandHtml028.LoginForm(login.Text);
                var target = new Uri(login.Uri, form.Action);
                EnsureOrigin(target);
                var fields = new Dictionary<string, string>(form.Fields, StringComparer.Ordinal) {
                    ["TxtUser"] = username, ["TxtPwd"] = password
                };
                using var body = new FormUrlEncodedContent(fields);
                var loggedIn = await SendAsync(new HttpRequestMessage(HttpMethod.Post, target) { Content = body }, timeout.Token);
                if (TicketlandHtml028.IsLogin(loggedIn.Text)) throw new TicketException("Не удалось войти в Ticketland. Проверьте логин и пароль.");
            }

            // Use the portal's observed REST metadata. Parameter identities and types come from
            // the server, never from guessed P1/P2 names or localized input order.
            var metadataPath = "reports/api/v2.0/Reports(path='" + Uri.EscapeDataString(ReportPath.Replace("'", "''")) + "')/ParameterDefinitions";
            using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(Origin, metadataPath));
            var metadata = await SendAsync(metadataRequest, timeout.Token);
            var parameters = DateParameters(metadata.Text);
            var query = new List<string> { Uri.EscapeDataString(ReportPath), "rs:Command=Render", "rs:Format=HTML4.0", "rc:Section=0", "rc:Toolbar=false" };
            query.Add(Uri.EscapeDataString(parameters.From) + "=" + Uri.EscapeDataString(from.Date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
            query.Add(Uri.EscapeDataString(parameters.To) + "=" + Uri.EscapeDataString(to.Date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
            var report = await SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(Origin, "ReportServer?" + string.Join("&", query))), timeout.Token);
            return new TicketlandReport028(TicketlandHtml028.ParseReport(report.Text), DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (TicketException) { throw; }
        catch (OperationCanceledException) { throw new TicketException("Превышено время ожидания отчёта Ticketland."); }
        catch { throw new TicketException("Не удалось получить полный отчёт Ticketland. Проверьте соединение и повторите загрузку."); }
    }

    private static (string From, string To) DateParameters(string json)
    {
        if (TicketlandHtml028.IsLogin(json)) throw new TicketException("Сеанс Ticketland не подтверждён. Проверьте логин и пароль.");
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { throw new TicketException("Ticketland не вернул описание параметров отчёта."); }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                throw new TicketException("Ticketland не вернул описание параметров отчёта.");
            static string Text(JsonElement p, string key) => p.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
            var parameters = value.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object).Select(p => new {
                Name = Text(p, "Name"), Prompt = Text(p, "Prompt"), Type = Text(p, "ParameterType")
            }).ToArray();
            static string Label(string s) => s.Trim().Trim(':', '.', ' ').ToLowerInvariant();
            var from = parameters.Where(p => new[] { "с", "c", "от", "дата с", "дата начала", "начало", "from", "start date" }.Contains(Label(p.Prompt))).ToArray();
            var to = parameters.Where(p => new[] { "по", "до", "дата по", "дата окончания", "окончание", "to", "end date" }.Contains(Label(p.Prompt))).ToArray();
            if (from.Length != 1 || to.Length != 1 || string.IsNullOrWhiteSpace(from[0].Name) || string.IsNullOrWhiteSpace(to[0].Name) || from[0].Name == to[0].Name
                || from[0].Type != "DateTime" || to[0].Type != "DateTime")
                throw new TicketException("Не удалось однозначно определить параметры дат «С» и «По» отчёта Ticketland.");
            return (from[0].Name, to[0].Name);
        }
    }

    private async Task<(string Text, Uri Uri)> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var redirects = 0; redirects < 8; redirects++)
        {
            using (var current = request)
            {
                EnsureOrigin(current.RequestUri!);
                using var response = await client.SendAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (response.Headers.Location is null || ((int)response.StatusCode is 307 or 308 && current.Method != HttpMethod.Get))
                        throw new TicketException("Ticketland вернул неподдерживаемое перенаправление.");
                    var next = new Uri(current.RequestUri!, response.Headers.Location);
                    EnsureOrigin(next);
                    request = new HttpRequestMessage(HttpMethod.Get, next);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new TicketException("Сервер Ticketland не вернул отчёт. Повторите загрузку позже.");
                if (response.Content.Headers.ContentLength > MaxBytes) throw new TicketException("Отчёт Ticketland превышает допустимый размер.");
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var buffer = new MemoryStream();
                var chunk = new byte[16384];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct)) != 0)
                {
                    if (buffer.Length + read > MaxBytes) throw new TicketException("Отчёт Ticketland превышает допустимый размер.");
                    buffer.Write(chunk, 0, read);
                }
                var text = Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF');
                return (text, current.RequestUri!);
            }
        }
        request.Dispose();
        throw new TicketException("Слишком много перенаправлений Ticketland.");
    }

    private static void EnsureOrigin(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, Origin.Host, StringComparison.OrdinalIgnoreCase) || uri.Port != 443 || uri.UserInfo.Length != 0)
            throw new TicketException("Ticketland перенаправил запрос на неподтверждённый адрес.");
    }
    public void Dispose() => client.Dispose();
}
