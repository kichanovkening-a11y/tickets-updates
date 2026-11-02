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
        if(e.Args.Length==2&&e.Args[0]=="--validate-update")
        {
            try
            {
                var db=Path.Combine(e.Args[1],"tickets.db");
                if(!File.Exists(db))throw new IOException("Не найдена база данных для проверки.");
                var checkedStore=new Store(db);
                using var connection=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+db);connection.Open();
                using var command=connection.CreateCommand();command.CommandText="PRAGMA integrity_check";
                if(!string.Equals(command.ExecuteScalar()?.ToString(),"ok",StringComparison.OrdinalIgnoreCase))throw new IOException("Проверка целостности базы не пройдена.");
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Shutdown(0);
            }
            catch(Exception ex){try{File.WriteAllText(Path.Combine(e.Args[1],"validation-error.txt"),ex.ToString());}catch{}Shutdown(1);}
            return;
        }
        if(e.Args.Length==4&&e.Args[0]=="--render-documents"&&int.TryParse(e.Args[2],out var parentId)&&long.TryParse(e.Args[3],out var parentStarted)) {Shutdown(WordRenderWorker.RunPersistent(e.Args[1],parentId,parentStarted));return;}
        if(e.Args.Length==2&&e.Args[0]=="--render-document") {Shutdown(WordRenderWorker.Run(e.Args[1]));return;}
        if(e.Args.Length==3&&e.Args[0]=="--apply-update") {UpdateRunner.Apply(e.Args[1],e.Args[2]);Shutdown();return;}
        try
        {
            string? updateReceipt=null;
            if(Tickets.Update.UpdateTransaction.HasPending(AppContext.BaseDirectory))
            {
                if(e.Args.Length!=3||e.Args[0]!="--updated")throw new IOException("Обновление не завершено. Запустите Tickets.Updater.exe для восстановления.");
                var updates=Path.Combine(AppContext.BaseDirectory,".updates");
                var id=System.Text.Json.JsonSerializer.Deserialize<string>(File.ReadAllText(Path.Combine(updates,"pending.json")));
                if(id is null||!System.Text.RegularExpressions.Regex.IsMatch(id,"^transaction-[a-f0-9]{32}$"))throw new IOException("Повреждён журнал обновления.");
                updateReceipt=Path.Combine(updates,id,"health.json");
                if(Path.GetFullPath(e.Args[2])!=Path.GetFullPath(updateReceipt)||File.ReadAllText(Path.Combine(updates,id,"startup-token"))!=e.Args[1])throw new IOException("Неверное подтверждение запуска.");
            }
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
            try{Tickets.Update.UpdateFiles.EnsureLauncher(AppContext.BaseDirectory);}catch(IOException){}catch(UnauthorizedAccessException){}
            MainWindow=new MainWindow(store); MainWindow.Show();
            if(updateReceipt is not null)Tickets.Update.UpdateTransaction.PublishHealth(updateReceipt,e.Args[1]);
            if(e.Args.Length==2&&e.Args[0]=="--ticketpro-event"&&long.TryParse(e.Args[1],out var proEventId))
                Dispatcher.BeginInvoke(new Action(()=>{if(MainWindow is MainWindow main){main.SelectEvent(proEventId);main.OpenTicketPro037();}}));
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
