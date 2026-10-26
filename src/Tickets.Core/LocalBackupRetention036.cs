using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace Tickets.Core;

public static class LocalBackupRetention036
{
    private static readonly Regex Automatic=new(@"^tickets-(?:\d{4}-\d{2}-\d{2}|before-update-\d{8}-\d{6}|cleanup-\d{8}-\d{6})\.db$",RegexOptions.CultureInvariant);
    private static readonly Regex Migration=new(@"^tickets\.db\.before-v\d+(?:-[a-z-]+)?\.db$",RegexOptions.CultureInvariant);
    public static int Prune(Store store,string backupDirectory)
    {
        // Only application-owned files. Deleted-show bundles and manual exports are separate archives.
        var candidates=new List<FileInfo>();
        if(Directory.Exists(backupDirectory))candidates.AddRange(new DirectoryInfo(backupDirectory).EnumerateFiles().Where(f=>Automatic.IsMatch(f.Name)));
        var data=System.IO.Path.GetDirectoryName(store.Path)!;
        candidates.AddRange(new DirectoryInfo(data).EnumerateFiles().Where(f=>Migration.IsMatch(f.Name)));
        var ordered=candidates.DistinctBy(f=>f.FullName,StringComparer.OrdinalIgnoreCase).OrderByDescending(f=>f.LastWriteTimeUtc).ThenByDescending(f=>f.Name,StringComparer.Ordinal).ToArray();
        var keep=ordered.Where(f=>Valid(f.FullName)).Take(2).Select(f=>f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(keep.Count<2)return 0; // Never remove anything without two independently readable backups.
        var removed=0;
        foreach(var file in ordered.Where(f=>!keep.Contains(f.FullName)))
        {try{file.Delete();removed++;}catch(IOException){}catch(UnauthorizedAccessException){}}
        return removed;
    }
    private static bool Valid(string path)
    {
        try
        {
            using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();
            using var command=connection.CreateCommand();command.CommandText="PRAGMA integrity_check";
            if((string?)command.ExecuteScalar()!="ok")return false;
            command.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('event','ticket')";
            return Convert.ToInt32(command.ExecuteScalar())==2;
        }
        catch(SqliteException){return false;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
    }
}
