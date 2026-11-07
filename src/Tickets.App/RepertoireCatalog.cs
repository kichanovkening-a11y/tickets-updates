using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Tickets.Core;

namespace Tickets.App;

internal sealed record Poster(string Page, string Title, string Image);
internal sealed record HomeCard(string Page, long PerformanceId, bool Hidden = false);

internal sealed class RepertoireCatalog
{
    private static readonly Uri Site = new("https://vip-teatre.ru/");
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 8 * 1024 * 1024 };
    private readonly string directory;
    private readonly Dictionary<string, BitmapImage> thumbnails = new();
    internal List<Poster> Posters { get; private set; }
    private string Index => Path.Combine(directory, "catalog.json");

    internal RepertoireCatalog(Store store)
    {
        directory = Path.Combine(Path.GetDirectoryName(store.Path)!, "Repertoire");
        using var seed = Resource("catalog.json");
        Posters = JsonSerializer.Deserialize<List<Poster>>(seed!)!;
        try
        {
            if (File.Exists(Index) && JsonSerializer.Deserialize<List<Poster>>(File.ReadAllText(Index)) is { Count: > 0 } cached)
                Posters = cached;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }

    private static Stream? Resource(string name) =>
        typeof(RepertoireCatalog).Assembly.GetManifestResourceStream("Repertoire." + name);

    private string PhotoPath(string page) => Path.Combine(directory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(page))) + ".img");

    private static BitmapImage Decode(Stream stream)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 400;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    internal BitmapImage? Photo(string page)
    {
        if (page.Length == 0) return null;
        if (thumbnails.TryGetValue(page, out var cached)) return cached;
        var file = PhotoPath(page);
        try
        {
            if (File.Exists(file))
            {
                using var input = File.OpenRead(file);
                return thumbnails[page] = Decode(input);
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or UnauthorizedAccessException) { }
        using var bundled = Resource(Path.GetFileNameWithoutExtension(page) + ".jpg");
        return bundled == null ? null : thumbnails[page] = Decode(bundled);
    }

    internal static List<Poster> Parse(string html)
    {
        var result = new List<Poster>();
        var begin = html.IndexOf("vip-repertuar-tiles", StringComparison.Ordinal);
        if (begin < 0) return result;
        var section = html[begin..].Split("<style", 2)[0].Split("Архив спектаклей", 2)[0];
        foreach (Match match in Regex.Matches(section, """<a\b([^>]*)>(.*?)</a>""", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            static string Attribute(string tag, string name) =>
                WebUtility.HtmlDecode(Regex.Match(tag, @"\b" + name + """\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase).Groups[1].Value);
            var imageTag = Regex.Match(match.Groups[2].Value, "<img\\b[^>]*>", RegexOptions.IgnoreCase).Value;
            if (imageTag.Length == 0) continue;
            var title = Attribute(imageTag, "alt").Trim();
            if (!Uri.TryCreate(Site, Attribute(match.Groups[1].Value, "href"), out var page) ||
                !Uri.TryCreate(Site, Attribute(imageTag, "src"), out var image) ||
                page.GetLeftPart(UriPartial.Authority) != Site.GetLeftPart(UriPartial.Authority) ||
                image.GetLeftPart(UriPartial.Authority) != Site.GetLeftPart(UriPartial.Authority) ||
                !page.AbsolutePath.StartsWith("/repertuar/") || !image.AbsolutePath.StartsWith("/assets/") || title.Length == 0) continue;
            result.Add(new(page.AbsolutePath, title, image.PathAndQuery));
        }
        return result.DistinctBy(p => p.Page).ToList();
    }

    private static async Task WriteAsync(string file, byte[] bytes)
    {
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false); File.Move(temporary, file, true); }
        finally { File.Delete(temporary); }
    }

    internal async Task RefreshAsync()
    {
        try
        {
            var updated = Parse(await Client.GetStringAsync(new Uri(Site, "repertuar/")).ConfigureAwait(false));
            if (updated.Count == 0) return;
            Directory.CreateDirectory(directory);
            await Parallel.ForEachAsync(updated, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async (poster, token) =>
            {
                try
                {
                    var file = PhotoPath(poster.Page);
                    if (File.Exists(file) && File.GetLastWriteTimeUtc(file) > DateTime.UtcNow.AddDays(-1)
                        && Posters.Any(c => c.Page == poster.Page && c.Image == poster.Image)) return;
                    var bytes = await Client.GetByteArrayAsync(new Uri(Site, poster.Image), token);
                    using var stream = new MemoryStream(bytes);
                    Decode(stream);
                    await WriteAsync(PhotoPath(poster.Page), bytes);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or NotSupportedException or FileFormatException) { }
            }).ConfigureAwait(false);
            await WriteAsync(Index, JsonSerializer.SerializeToUtf8Bytes(updated)).ConfigureAwait(false);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { Posters = updated; thumbnails.Clear(); });
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException) { }
    }
}
