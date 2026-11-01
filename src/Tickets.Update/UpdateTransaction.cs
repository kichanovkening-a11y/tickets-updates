using System.Text.Json;

namespace Tickets.Update;

// Journal is durable before any target changes. Backups remain until an explicit healthy start.
public sealed class UpdateTransaction : IDisposable
{
    public sealed record Original(string Path,bool Existed,string Hash);
    public sealed record Journal(int Format,string Target,string Phase,List<Original> Files,List<Original> Data);
    private readonly string target,folder;
    private readonly InstallPlan? plan;
    private Journal journal;
    private FileStream? guard;
    private readonly FileStream updateLock;
    public string BackupDirectory=>folder;
    private UpdateTransaction(string target,string folder,InstallPlan? plan,Journal journal,FileStream updateLock,FileStream guard)
    {this.target=target;this.folder=folder;this.plan=plan;this.journal=journal;this.updateLock=updateLock;this.guard=guard;}
    public static bool HasPending(string target)=>File.Exists(Path.Combine(target,".updates","pending.json"));
    public bool IsHealthy
    {
        get
        {
            if(journal.Phase=="confirmed")return true;
            if(journal.Phase!="installed")return false;
            try
            {
                var receipt=Path.Combine(folder,"health.json");var tokenFile=Path.Combine(folder,"startup-token");NoLinks(receipt);NoLinks(tokenFile);
                var token=File.ReadAllText(tokenFile);
                return System.Text.RegularExpressions.Regex.IsMatch(token,"^[a-f0-9]{32}$")&&File.ReadAllText(receipt)==token;
            }
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
    }
    public static void PublishHealth(string path,string token)
    {
        NoLinks(path);var temporary=path+".tmp";NoLinks(temporary);
        using(var output=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        {var bytes=System.Text.Encoding.UTF8.GetBytes(token);output.Write(bytes);output.Flush(true);}
        File.Move(temporary,path,true);
    }
    private static FileStream Lock(string path)=>new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    private static void NoLinks(string path)
    {
        for(var p=Path.GetFullPath(path);p is not null;p=Path.GetDirectoryName(p))
            if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки в пути обновления не поддерживаются: "+p);
    }
    private static string DataPath(string root,string relative)
    {
        if(string.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)||relative.IndexOfAny(new[]{':','*','?','"','<','>','|'})>=0||relative.Split('/','\\').Any(x=>x is ".." or "." or ""||x.EndsWith('.')||x.EndsWith(' ')||x.Any(char.IsControl))||relative.Equals("session.lock",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Неверный путь резервной копии.");
        var p=Path.GetFullPath(Path.Combine(root,relative));
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!p.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь выходит за папку данных.");
        NoLinks(p);return p;
    }
    private static IEnumerable<string> DataFiles(string root)
    {
        NoLinks(root);
        foreach(var file in Directory.EnumerateFiles(root)){NoLinks(file);if(Path.GetFileName(file)!="session.lock")yield return file;}
        foreach(var dir in Directory.EnumerateDirectories(root)){NoLinks(dir);foreach(var file in DataFiles(dir))yield return file;}
    }
    private static void DurableCopy(string source,string destination)
    {
        NoLinks(destination);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input=File.OpenRead(source);using var output=new FileStream(destination,FileMode.Create,FileAccess.Write,FileShare.None);input.CopyTo(output);output.Flush(true);
    }
    private static void WriteJson(string path,object value)
    {
        var tmp=path+".tmp";NoLinks(path);NoLinks(tmp);
        using(var output=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,value);output.Flush(true);}
        File.Move(tmp,path,true);
    }
    private void Phase(string phase){journal=journal with{Phase=phase};WriteJson(Path.Combine(folder,"journal.json"),journal);}
    public static UpdateTransaction Prepare(InstallPlan plan)
    {
        var target=Path.GetFullPath(plan.Target);NoLinks(target);NoLinks(plan.Stage);
        if(!Directory.Exists(target))throw new IOException("Папка программы не найдена.");
        var updates=Path.Combine(target,".updates");NoLinks(updates);Directory.CreateDirectory(updates);
        var updateLock=Lock(Path.Combine(updates,"update.lock"));FileStream? guard=null;
        try
        {
            if(HasPending(target))throw new IOException("Есть незавершённое обновление. Сначала восстановите предыдущую версию.");
            var data=Path.Combine(target,"Data");NoLinks(data);Directory.CreateDirectory(data);guard=Lock(Path.Combine(data,"session.lock"));
            if(plan.Files.Count==0||plan.Files.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=plan.Files.Count)throw new InvalidDataException("Неверный список файлов.");
            long needed=0;
            foreach(var f in plan.Files)
            {
                var source=UpdateFiles.SafePath(plan.Stage,f.Path);var dest=UpdateFiles.SafePath(target,f.Path);
                if(!UpdateFiles.Hash(source).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Повреждён файл: "+f.Path);
                needed+=new FileInfo(source).Length*2;
                if(Directory.Exists(dest))throw new IOException("Вместо файла обнаружена папка: "+f.Path);
                if(File.Exists(dest)){using var probe=new FileStream(dest,FileMode.Open,FileAccess.ReadWrite,FileShare.None);needed+=probe.Length;if((File.GetAttributes(dest)&FileAttributes.ReadOnly)!=0)throw new IOException("Файл только для чтения: "+f.Path);}
                var parent=Path.GetDirectoryName(dest)!;Directory.CreateDirectory(parent);
                var probePath=Path.Combine(parent,".write-test-"+Guid.NewGuid().ToString("N"));using(var probe=new FileStream(probePath,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose)){probe.WriteByte(0);probe.Flush(true);}
            }
            var dataFiles=DataFiles(data).ToList();needed+=dataFiles.Sum(x=>new FileInfo(x).Length)*3;
            var drive=new DriveInfo(Path.GetPathRoot(target)!);if(drive.AvailableFreeSpace<needed+16*1024*1024)throw new IOException("Недостаточно места для обновления и резервной копии.");
            var id="transaction-"+Guid.NewGuid().ToString("N");var folder=Path.Combine(updates,id);Directory.CreateDirectory(folder);
            var originals=new List<Original>();var snapshot=new List<Original>();
            foreach(var f in plan.Files)
            {
                var dest=UpdateFiles.SafePath(target,f.Path);var exists=File.Exists(dest);var hash=exists?UpdateFiles.Hash(dest):"";
                if(exists)DurableCopy(dest,UpdateFiles.SafePath(Path.Combine(folder,"files"),f.Path));
                originals.Add(new(f.Path,exists,hash));
            }
            foreach(var file in dataFiles){var relative=Path.GetRelativePath(data,file);var hash=UpdateFiles.Hash(file);DurableCopy(file,DataPath(Path.Combine(folder,"data"),relative));snapshot.Add(new(relative,true,hash));}
            var journal=new Journal(1,target,"prepared",originals,snapshot);
            WriteJson(Path.Combine(folder,"inventory.json"),journal);
            PublishHealth(Path.Combine(folder,"inventory.sha256"),UpdateFiles.Hash(Path.Combine(folder,"inventory.json")));
            WriteJson(Path.Combine(folder,"journal.json"),journal);
            WriteJson(Path.Combine(updates,"pending.json"),id);
            return new(target,folder,plan,journal,updateLock,guard);
        }
        catch{guard?.Dispose();updateLock.Dispose();throw;}
    }
    public void Install()
    {
        if(plan is null||journal.Phase!="prepared")throw new InvalidOperationException("Неверное состояние установки.");
        Phase("installing");
        foreach(var f in plan.Files)
        {
            var dest=UpdateFiles.SafePath(target,f.Path);var temporary=dest+".installing";NoLinks(temporary);
            DurableCopy(UpdateFiles.SafePath(plan.Stage,f.Path),temporary);
            if(!UpdateFiles.Hash(temporary).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Не удалось проверить записанный файл: "+f.Path);
            File.Move(temporary,dest,true);
        }
        Phase("installed");
    }
    // Caller may start the new app only after it acquired this directory's startup token.
    public void ReleaseSession(){guard?.Dispose();guard=null;}
    public void Complete()
    {
        if(journal.Phase!="installed")throw new InvalidOperationException("Установка не завершена.");
        Phase("confirmed");File.Delete(Path.Combine(target,".updates","pending.json"));
        // Never prune pending, failed or legacy rollback folders.
        var completed=new List<string>();
        foreach(var directory in Directory.EnumerateDirectories(Path.Combine(target,".updates"),"transaction-*"))
        {
            try
            {
                NoLinks(directory);var file=Path.Combine(directory,"journal.json");NoLinks(file);
                var state=JsonSerializer.Deserialize<Journal>(File.ReadAllText(file));
                if(state?.Format==1&&state.Phase=="confirmed"&&state.Target.Equals(target,StringComparison.OrdinalIgnoreCase))completed.Add(directory);
            }
            catch(IOException){}catch(JsonException){}catch(UnauthorizedAccessException){}
        }
        foreach(var directory in completed.OrderByDescending(x=>x==folder).ThenByDescending(Directory.GetCreationTimeUtc).Skip(2))
            try{Directory.Delete(directory,true);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    public void Rollback()
    {
        if(IsHealthy)throw new IOException("Новая версия уже открыта успешно; автоматический откат отменён для сохранения новых данных.");
        ValidateInventory();
        guard??=Lock(Path.Combine(target,"Data","session.lock"));
        // Validate every original before changing anything during recovery.
        foreach(var f in journal.Files){UpdateFiles.SafePath(target,f.Path);if(f.Existed&&!UpdateFiles.Hash(UpdateFiles.SafePath(Path.Combine(folder,"files"),f.Path)).Equals(f.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Повреждена резервная копия: "+f.Path);}
        foreach(var f in journal.Data)if(!UpdateFiles.Hash(DataPath(Path.Combine(folder,"data"),f.Path)).Equals(f.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Повреждена копия данных: "+f.Path);
        Phase("restoring");
        foreach(var f in journal.Files)
        {
            var dest=UpdateFiles.SafePath(target,f.Path);
            if(f.Existed){var temp=dest+".restoring";DurableCopy(UpdateFiles.SafePath(Path.Combine(folder,"files"),f.Path),temp);File.Move(temp,dest,true);}
            else if(File.Exists(dest))File.Delete(dest);
        }
        var data=Path.Combine(target,"Data");var expected=journal.Data.Select(x=>x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var file in DataFiles(data).ToList())if(!expected.Contains(Path.GetRelativePath(data,file)))File.Delete(file);
        foreach(var f in journal.Data){var dest=DataPath(data,f.Path);DurableCopy(DataPath(Path.Combine(folder,"data"),f.Path),dest);}
        Phase("restored");File.Delete(Path.Combine(target,".updates","pending.json"));
    }
    private void ValidateInventory()
    {
        var index=Path.Combine(folder,"inventory.json");var checksum=Path.Combine(folder,"inventory.sha256");NoLinks(index);NoLinks(checksum);
        if(!UpdateFiles.Hash(index).Equals(File.ReadAllText(checksum),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Повреждён список резервных файлов.");
        var original=JsonSerializer.Deserialize<Journal>(File.ReadAllText(index));
        if(original is null||original.Files is null||original.Data is null||original.Format!=1||original.Target!=journal.Target||journal.Files.Count==0||!original.Files.SequenceEqual(journal.Files)||!original.Data.SequenceEqual(journal.Data))throw new InvalidDataException("Список резервных файлов не совпадает с журналом.");
        foreach(var file in journal.Files)UpdateFiles.SafePath(target,file.Path);
        foreach(var file in journal.Data)DataPath(Path.Combine(target,"Data"),file.Path);
    }
    public static void Recover(string target)
    {
        target=Path.GetFullPath(target);NoLinks(target);
        if(!HasPending(target))return;
        var updates=Path.Combine(target,".updates");NoLinks(updates);
        using var updateLock=Lock(Path.Combine(updates,"update.lock"));using var guard=Lock(Path.Combine(target,"Data","session.lock"));
        var id=JsonSerializer.Deserialize<string>(File.ReadAllText(Path.Combine(updates,"pending.json")));
        if(id is null||!System.Text.RegularExpressions.Regex.IsMatch(id,"^transaction-[a-f0-9]{32}$"))throw new InvalidDataException("Неверный журнал обновления.");
        var folder=Path.Combine(updates,id);NoLinks(folder);var journal=JsonSerializer.Deserialize<Journal>(File.ReadAllText(Path.Combine(folder,"journal.json")))??throw new InvalidDataException("Нет журнала.");
        if(journal.Format!=1||!string.Equals(journal.Target,target,StringComparison.OrdinalIgnoreCase)||journal.Files is null||journal.Data is null||journal.Files.Count>6000||journal.Data.Count>100000)throw new InvalidDataException("Журнал относится к другой папке или повреждён.");
        var tx=new UpdateTransaction(target,folder,null,journal,updateLock,guard);tx.ValidateInventory();
        if(journal.Phase is "confirmed" or "restored"){File.Delete(Path.Combine(updates,"pending.json"));return;}
        if(journal.Phase is not ("prepared" or "installing" or "installed" or "restoring"))throw new InvalidDataException("Неизвестное состояние обновления.");
        if(tx.IsHealthy){tx.Complete();return;}
        tx.Rollback();
    }
    public void Dispose(){guard?.Dispose();updateLock.Dispose();}
}
