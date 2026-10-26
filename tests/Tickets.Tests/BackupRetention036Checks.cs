using Tickets.Core;
internal static class BackupRetention036Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("036 backups retain two valid automatic copies and preserve documents",s=>{
            var root=Path.GetDirectoryName(s.Path)!;foreach(var old in Directory.GetFiles(root,"tickets.db.before-v*.db"))File.Delete(old);var backups=Path.Combine(root,"Backups");Directory.CreateDirectory(backups);
            for(int i=0;i<4;i++){var p=Path.Combine(backups,$"tickets-2026-10-{20+i}.db");s.Backup(p);File.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddDays(i-8));}
            File.WriteAllText(Path.Combine(backups,"tickets-2026-10-26.db"),"corrupt");
            File.WriteAllText(Path.Combine(backups,"manual.db"),"manual");
            var archive=Path.Combine(root,"Data","Backups","delete-performance-1");Directory.CreateDirectory(archive);File.WriteAllText(Path.Combine(archive,"document.docx"),"keep");
            LocalBackupRetention036.Prune(s,backups);
            if(Directory.GetFiles(backups,"tickets-*.db").Length!=2)throw new Exception("wrong retention");
            if(!File.Exists(Path.Combine(backups,"manual.db"))||!File.Exists(Path.Combine(archive,"document.docx")))throw new Exception("unrelated file deleted");
        });
        test("036 backups do not prune when fewer than two validate",s=>{
            var root=Path.GetDirectoryName(s.Path)!;foreach(var old in Directory.GetFiles(root,"tickets.db.before-v*.db"))File.Delete(old);var backups=Path.Combine(root,"Backups");Directory.CreateDirectory(backups);
            s.Backup(Path.Combine(backups,"tickets-2026-10-20.db"));File.WriteAllText(Path.Combine(backups,"tickets-2026-10-21.db"),"broken");
            LocalBackupRetention036.Prune(s,backups);
            if(Directory.GetFiles(backups).Length!=2)throw new Exception("deleted without sufficient copies");
        });
    }
}
