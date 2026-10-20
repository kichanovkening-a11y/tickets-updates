using System.Text.RegularExpressions;

namespace Tickets.Core;

public static class SearchText
{
    private static readonly HashSet<string> Ignored=new(StringComparer.Ordinal){"ООО","ИП","АНО","ТЕАТР"};
    private static string[] Words(string text)=>Regex.Matches(CatalogText.Key(text),@"[\p{L}\p{Nd}]+").Select(m=>m.Value).Where(w=>!Ignored.Contains(w)).ToArray();
    public static bool Matches(string value,string query)
    {
        var wanted=Words(query);if(wanted.Length==0)return true;
        var words=Words(value);return wanted.All(prefix=>words.Any(word=>word.StartsWith(prefix,StringComparison.Ordinal)));
    }
}
