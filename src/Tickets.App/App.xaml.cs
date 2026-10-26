using System.IO;
using System.Windows;
using Tickets.Core;

namespace Tickets.App;

public partial class App : Application
{
    private FileStream? sessionLock;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==3&&e.Args[0]=="--apply-update") {UpdateRunner.Apply(e.Args[1],e.Args[2]);Shutdown();return;}
        try
        {
            var data=Path.Combine(AppContext.BaseDirectory,"Data");
            Directory.CreateDirectory(data);
            try { sessionLock=new FileStream(Path.Combine(data,"session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) { MessageBox.Show("Программа из этой папки уже открыта. Вернитесь в её окно.","Билеты"); Shutdown(); return; }
            var db=Path.Combine(data,"tickets.db");
            if(!File.Exists(db)) File.Copy(Path.Combine(data,"seed.db"),db);
            var store=new Store(db);
            var backup=Path.Combine(AppContext.BaseDirectory,"Backups");
            Directory.CreateDirectory(backup);
            var daily=Path.Combine(backup,$"tickets-{DateTime.Now:yyyy-MM-dd}.db");
            if(!File.Exists(daily)) store.Backup(daily);
            LocalBackupRetention036.Prune(store,backup);
            MainWindow=new MainWindow(store); MainWindow.Show();
            try{Shortcut023.Ensure(AppContext.BaseDirectory);}catch(IOException){}catch(System.Runtime.InteropServices.COMException){}catch(System.Reflection.TargetInvocationException){}catch(UnauthorizedAccessException){}
        }
        catch(Exception ex)
        {
            MessageBox.Show("Не удалось открыть программу. Распакуйте всю папку в место, где разрешена запись, и запустите Tickets.exe.\n\n"+ex.Message,"Билеты");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { sessionLock?.Dispose(); base.OnExit(e); }
}
