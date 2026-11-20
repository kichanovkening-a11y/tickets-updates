using Tickets.Update;
internal static class ClassicChannelChecks
{
    public static void Run()
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Wrong edition accepted");}
        var classic=UpdateChannel.Classic;var standard=UpdateChannel.Standard;
        Check(classic.ManifestUri("owner/repo").AbsoluteUri=="https://github.com/owner/repo/releases/download/classic/update-manifest.json","Classic channel URL");
        Check(standard.ManifestUri("owner/repo").AbsoluteUri=="https://github.com/owner/repo/releases/latest/download/update-manifest.json","Standard channel changed");
        Check(classic.ReleaseTag("0.52.2")=="classic-v0.52.2","Classic versioned assets");
        Check(standard.ReleaseTag("0.52.1")=="v0.52.1","Standard assets changed");
        var names=new[]{"Tickets.Classic.exe","Tickets.Classic.dll","Tickets.Classic.Updater.exe"};
        var files=names.Select(n=>new UpdateFile(n,new string('a',64))).ToList();
        classic.ValidateFiles(files);Reject(()=>standard.ValidateFiles(files));
        Reject(()=>classic.ValidateFiles(new[]{new UpdateFile("Tickets.dll",new string('a',64))}));
        Reject(()=>classic.ValidateFiles(files.Take(2)));
        Reject(()=>classic.ValidateFiles(files.Append(new("Tickets.exe",new string('a',64)))));
        Console.WriteLine("PASS classic and standard updates use separate manifests, versioned assets and reject mixed editions");
        var root=Path.Combine(Path.GetTempPath(),"classic-update-"+Guid.NewGuid().ToString("N"));
        try
        {
            var target=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");
            Directory.CreateDirectory(target);Directory.CreateDirectory(stage);Directory.CreateDirectory(Path.Combine(target,"Data"));
            File.WriteAllText(Path.Combine(target,"Data","tickets.db"),"existing tickets");
            File.WriteAllText(Path.Combine(target,"Data","settings.txt"),"user settings");
            foreach(var name in names){File.WriteAllText(Path.Combine(target,name),"old "+name);File.WriteAllText(Path.Combine(stage,name),"new "+name);}
            Check(classic.CanUpdate(target),"Classic installation not recognized");Check(!standard.CanUpdate(target),"Standard updater accepts classic installation");
            var plan=new InstallPlan(target,stage,names.Select(n=>new UpdateFile(n,UpdateFiles.Hash(Path.Combine(stage,n)))).ToList());
            string backup;
            using(var transaction=UpdateTransaction.Prepare(plan))
            {
                backup=transaction.BackupDirectory;transaction.Install();
                Check(File.ReadAllText(Path.Combine(target,"Tickets.Classic.dll"))=="new Tickets.Classic.dll","Classic binaries not updated");
                Check(File.ReadAllText(Path.Combine(target,"Data","tickets.db"))=="existing tickets","User database changed");
                Check(File.ReadAllText(Path.Combine(target,"Data","settings.txt"))=="user settings","Settings changed");
                transaction.Rollback();
            }
            Check(File.ReadAllText(Path.Combine(target,"Tickets.Classic.dll"))=="old Tickets.Classic.dll","Classic rollback failed");
            Check(File.ReadAllText(Path.Combine(target,"Data","tickets.db"))=="existing tickets","Rollback changed data");
            Check(!File.Exists(Path.Combine(target,"Tickets.exe"))&&!File.Exists(Path.Combine(target,"! Tickets.exe")),"Standard launchers created");
            Console.WriteLine("PASS classic transactional installation preserves user data and restores old classic binaries on rollback");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
