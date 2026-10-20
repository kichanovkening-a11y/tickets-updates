using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tickets.Update;

public sealed record UpdateFile(string Path,string Sha256);
public sealed record UpdatePackage(string Name,string Asset,string Sha256,List<UpdateFile> Files);
public sealed record UpdateManifest(int Format,string Repository,string Version,List<UpdatePackage> Packages);
public sealed record InstallPlan(string Target,string Stage,List<UpdateFile> Files);

public static class UpdateFiles
{
    public static UpdateManifest Parse(string json,string repository)
    {
        if(json.Length>2000000)throw new InvalidDataException("Описание обновления слишком велико.");
        UpdateManifest m;
        try{m=JsonSerializer.Deserialize<UpdateManifest>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Пустое описание обновления.");}
        catch(JsonException ex){throw new InvalidDataException("Неверное описание обновления.",ex);}
        if(m.Format!=1||!string.Equals(m.Repository,repository,StringComparison.OrdinalIgnoreCase)||m.Packages is null||m.Packages.Count is 0 or >10)
            throw new InvalidDataException("Источник или формат обновления не совпадает.");
        VersionNumber(m.Version);
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var all=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var p in m.Packages)
        {
            if(p is null||p.Name is null||p.Asset is null||!Regex.IsMatch(p.Name,"^[A-Za-z0-9_-]{1,40}$")||!Regex.IsMatch(p.Asset,"^[A-Za-z0-9_.-]+\\.zip$")||!names.Add(p.Asset)||!Digest(p.Sha256)||p.Files is null||p.Files.Count is 0 or >2000)
                throw new InvalidDataException("Неверный пакет обновления.");
            foreach(var f in p.Files)
            { if(f is null||!Digest(f.Sha256)||!all.Add(f.Path))throw new InvalidDataException("Повтор или неверный хеш файла.");SafePath(System.IO.Path.GetTempPath(),f.Path); }
        }
        return m;
    }
    private static bool Digest(string? text)=>text is not null&&Regex.IsMatch(text,"^[a-fA-F0-9]{64}$");
    public static Version VersionNumber(string text)
    {
        if(!Regex.IsMatch(text??"","^\\d+\\.\\d+\\.\\d+(\\.\\d+)?$")||!Version.TryParse(text,out var v))throw new InvalidDataException("Неверный номер версии.");
        return new(v.Major,v.Minor,v.Build,Math.Max(0,v.Revision));
    }
    public static string SafePath(string root,string path)
    {
        if(string.IsNullOrEmpty(path)||path.Contains('\\')||path.StartsWith('/')||path.Contains(':'))throw new InvalidDataException("Недопустимый путь обновления.");
        var parts=path.Split('/');
        if(parts.Any(p=>!Regex.IsMatch(p,"^[A-Za-z0-9_.-]+$")||p is "." or ".."||p.EndsWith('.')||Regex.IsMatch(p,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\.|$)",RegexOptions.IgnoreCase)))throw new InvalidDataException("Недопустимое имя файла.");
        var managed=parts.Length==1&&(path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".deps.json",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".runtimeconfig.json",StringComparison.OrdinalIgnoreCase));
        var resource=parts.Length==2&&Regex.IsMatch(parts[0],"^[a-z]{2,3}(-[A-Za-z]{2,8})?$")&&parts[1].EndsWith(".resources.dll",StringComparison.OrdinalIgnoreCase);
        if(!managed&&!resource)throw new InvalidDataException("Обновление не может изменять базу, настройки или пользовательские файлы.");
        var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,System.IO.Path.Combine(parts)));
        var prefix=System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь выходит за папку программы.");
        var current=full;
        while(!string.Equals(current.TrimEnd(System.IO.Path.DirectorySeparatorChar),prefix.TrimEnd(System.IO.Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))
        {
            if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ссылки в папке обновления не поддерживаются.");
            current=System.IO.Path.GetDirectoryName(current)??throw new InvalidDataException("Неверный путь.");
        }
        return full;
    }
    public static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    public static List<UpdatePackage> Needed(string root,UpdateManifest manifest)=>manifest.Packages.Where(p=>p.Files.Any(f=>!File.Exists(SafePath(root,f.Path))||!Hash(SafePath(root,f.Path)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))).ToList();
    public static void Extract(string zip,string stage,UpdatePackage package)
    {
        if(!Hash(zip).Equals(package.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Контрольная сумма пакета не совпадает.");
        Directory.CreateDirectory(stage);using var archive=ZipFile.OpenRead(zip);
        var expected=package.Files.ToDictionary(f=>f.Path,StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long bytes=0;
        foreach(var entry in archive.Entries)
        {
            if(entry.FullName.EndsWith('/'))continue;
            if(!expected.TryGetValue(entry.FullName,out var spec)||!seen.Add(entry.FullName))throw new InvalidDataException("В пакете лишние или повторяющиеся файлы.");
            if(entry.Length>250_000_000||(bytes+=entry.Length)>700_000_000)throw new InvalidDataException("Пакет слишком велик.");
            var target=SafePath(stage,spec.Path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew)){input.CopyTo(output);}
            if(!Hash(target).Equals(spec.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Повреждён файл обновления: "+spec.Path);
        }
        if(seen.Count!=expected.Count)throw new InvalidDataException("В пакете не хватает файлов.");
    }
    public static void Apply(InstallPlan plan)
    {
        if(plan.Files.Count==0||plan.Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=plan.Files.Count)throw new InvalidDataException("Пустой или повторный список обновления.");
        foreach(var f in plan.Files)
        {
            SafePath(plan.Target,f.Path);
            if(!Digest(f.Sha256)||!Hash(SafePath(plan.Stage,f.Path)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Повреждён подготовленный файл: "+f.Path);
        }
        var data=System.IO.Path.Combine(plan.Target,"Data");Directory.CreateDirectory(data);
        using var guard=new FileStream(System.IO.Path.Combine(data,"session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var backup=System.IO.Path.Combine(plan.Target,".updates","rollback-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
        var changed=new List<(string Target,string Backup,bool Existed)>();
        try
        {
            foreach(var f in plan.Files)
            {
                var target=SafePath(plan.Target,f.Path);var old=SafePath(backup,f.Path);var exists=File.Exists(target);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(old)!);
                if(exists)File.Copy(target,old);
                var temporary=target+".new-"+Guid.NewGuid().ToString("N");
                try{File.Copy(SafePath(plan.Stage,f.Path),temporary);File.Move(temporary,target,true);changed.Add((target,old,exists));}
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
        }
        catch(Exception failure)
        {
            var errors=new List<Exception>{failure};
            foreach(var f in changed.AsEnumerable().Reverse())
                try{if(f.Existed)File.Copy(f.Backup,f.Target,true);else File.Delete(f.Target);}catch(Exception ex){errors.Add(ex);}
            if(errors.Count>1)throw new IOException("Ошибка обновления и восстановления. Предыдущие файлы сохранены: "+backup,new AggregateException(errors));
            throw;
        }
        try{Directory.Delete(backup,true);}catch(IOException){}
    }
}
