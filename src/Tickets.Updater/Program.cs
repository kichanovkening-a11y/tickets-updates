using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Tickets.Update;

namespace Tickets.Updater;
internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        var application=new Application();var window=new Window{Title="Обновление Билетов",Width=620,Height=300,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        var panel=new StackPanel{Margin=new Thickness(20)};var text=new TextBlock{Text="Обновление программы с сохранением данных",FontSize=18,TextWrapping=TextWrapping.Wrap};
        var button=new Button{Content="Выбрать папку и обновить",Padding=new Thickness(10),Margin=new Thickness(0,18,0,0)};panel.Children.Add(text);panel.Children.Add(button);window.Content=panel;bool busy=false;
        window.Closing+=(_,e)=>{if(busy)e.Cancel=true;};
        async Task Run()
        {
            if(busy)return;busy=true;button.IsEnabled=false;
            string? work=null;UpdateTransaction? transaction=null;
            try
            {
                string Arg(string key,string fallback=""){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
                var target=Arg("--target");
                if(target.Length==0){var picker=new Microsoft.Win32.OpenFolderDialog{Title="Папка программы Билеты"};if(picker.ShowDialog(window)!=true)return;target=picker.FolderName;}
                target=Path.GetFullPath(target);
                if(!UpdateFiles.CanUpdate(target))throw new IOException("В выбранной папке нет программы Билеты.");
                if(int.TryParse(Arg("--parent"),out var pid))
                {try{using var parent=Process.GetProcessById(pid);using var wait=new CancellationTokenSource(TimeSpan.FromSeconds(60));await parent.WaitForExitAsync(wait.Token);}catch(ArgumentException){}}
                work=Path.Combine(Path.GetTempPath(),"TicketsUpdates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
                // Running from the selected folder would lock precisely the files being replaced.
                if(!args.Contains("--temporary"))
                {
                    var runner=Path.Combine(work,"runner");Directory.CreateDirectory(runner);
                    foreach(var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
                    {
                        var name=Path.GetFileName(file);
                        if(name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".deps.json")||name.EndsWith(".runtimeconfig.json"))File.Copy(file,Path.Combine(runner,name));
                    }
                    var start=new ProcessStartInfo(Path.Combine(runner,"Tickets.Updater.exe")){UseShellExecute=false,WorkingDirectory=runner};
                    foreach(var arg in new[]{"--target",target,"--repository",Arg("--repository","kichanovkening-a11y/tickets-updates"),"--temporary"})start.ArgumentList.Add(arg);
                    _ = Process.Start(start)??throw new IOException("Не удалось запустить обновлятор.");busy=false;window.Close();return;
                }
                text.Text="Проверка незавершённых установок…";await Task.Run(()=>UpdateTransaction.Recover(target));
                InstallPlan plan;
                if(Arg("--plan").Length==0)
                {
                    var client=new GitHubUpdates();var manifest=await client.CheckAsync(Arg("--repository","kichanovkening-a11y/tickets-updates"));
                    plan=await client.DownloadAsync(manifest,target,work,new Progress<string>(s=>text.Text=s),full:true);
                    var helper=Path.Combine(plan.Stage,"Tickets.Updater.exe");
                    if(!File.Exists(helper))throw new IOException("В обновлении нет независимого обновлятора.");
                    var planFile=Path.Combine(work,"install-plan.json");File.WriteAllText(planFile,JsonSerializer.Serialize(plan));
                    var handoff=new ProcessStartInfo(helper){UseShellExecute=false,WorkingDirectory=plan.Stage};
                    foreach(var arg in new[]{"--target",target,"--plan",planFile,"--parent",Environment.ProcessId.ToString(),"--temporary"})handoff.ArgumentList.Add(arg);
                    _ = Process.Start(handoff)??throw new IOException("Не удалось запустить новый обновлятор.");
                    busy=false;window.Close();return;
                }
                plan=JsonSerializer.Deserialize<InstallPlan>(File.ReadAllText(Arg("--plan")))??throw new IOException("Не найден план установки.");
                if(!Path.GetFullPath(plan.Target).Equals(target,StringComparison.OrdinalIgnoreCase))throw new IOException("План относится к другой папке.");
                File.Copy(Path.Combine(plan.Stage,"Tickets.exe"),Path.Combine(plan.Stage,"! Tickets.exe"),true);
                plan.Files.Add(new("! Tickets.exe",UpdateFiles.Hash(Path.Combine(plan.Stage,"! Tickets.exe"))));
                text.Text="Резервное копирование и проверка файлов…";
                transaction=await Task.Run(()=>UpdateTransaction.Prepare(plan));
                // Probe migrations against a separate database copy; never mutate the recovery snapshot.
                var probe=Path.Combine(work,"probe");CopyTree(Path.Combine(transaction.BackupDirectory,"data"),probe);
                var seed=Path.Combine(target,"Data","seed.db");if(!File.Exists(Path.Combine(probe,"tickets.db"))&&File.Exists(seed))File.Copy(seed,Path.Combine(probe,"tickets.db"));
                text.Text="Проверка совместимости данных…";
                await Validate(plan.Stage,probe);
                text.Text="Установка файлов…";await Task.Run(transaction.Install);
                var token=Guid.NewGuid().ToString("N");var receipt=Path.Combine(transaction.BackupDirectory,"health.json");File.WriteAllText(Path.Combine(transaction.BackupDirectory,"startup-token"),token);
                transaction.ReleaseSession();
                var startApp=new ProcessStartInfo(Path.Combine(target,"! Tickets.exe")){UseShellExecute=false,WorkingDirectory=target};
                foreach(var arg in new[]{"--updated",token,receipt})startApp.ArgumentList.Add(arg);
                using var child=Process.Start(startApp)??throw new IOException("Не удалось запустить новую версию.");
                try
                {
                    text.Text="Проверка запуска новой версии…";
                    var until=DateTime.UtcNow.AddSeconds(60);
                    while(DateTime.UtcNow<until&&!child.HasExited&&!transaction.IsHealthy)await Task.Delay(200);
                    if(!transaction.IsHealthy)throw new IOException("Новая версия не подтвердила успешный запуск.");
                    transaction.Complete();
                }
                catch{if(!transaction.IsHealthy&&!child.HasExited){child.Kill();await child.WaitForExitAsync();}throw;}
                text.Text="Обновление завершено. Резерв сохранён.";
            }
            catch(Exception ex)
            {
                var details=ex.ToString();var result="Установка не выполнена.";
                if(transaction?.IsHealthy==true)result="Новая версия работает. Не удалось завершить служебную запись обновления; данные сохранены.";
                else if(transaction is not null)
                {
                    try{await Task.Run(transaction.Rollback);result+=" Предыдущая версия и данные восстановлены.";}
                    catch(Exception rollback){details+="\nROLLBACK:\n"+rollback;result+=" Автовосстановление не завершено. Не удаляйте резерв: "+transaction.BackupDirectory;}
                }
                var log=Path.Combine(work??Path.GetTempPath(),"update-error.log");try{File.WriteAllText(log,details);}catch{}
                text.Text=result+"\n"+ex.Message+"\nЖурнал: "+log;
            }
            finally{transaction?.Dispose();busy=false;button.IsEnabled=true;button.Content="Повторить обновление";}
        }
        button.Click+=async(_,_)=>await Run();window.Loaded+=async(_,_)=>{if(args.Contains("--target"))await Run();};application.Run(window);
    }
    private static void CopyTree(string source,string target)
    {
        Directory.CreateDirectory(target);if(!Directory.Exists(source))return;
        foreach(var file in Directory.EnumerateFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)));
        foreach(var dir in Directory.EnumerateDirectories(source)){if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылка в резерве данных.");CopyTree(dir,Path.Combine(target,Path.GetFileName(dir)));}
    }
    private static async Task Validate(string app,string data)
    {
        var start=new ProcessStartInfo(Path.Combine(app,"Tickets.exe")){UseShellExecute=false,WorkingDirectory=app};start.ArgumentList.Add("--validate-update");start.ArgumentList.Add(data);
        using var child=Process.Start(start)??throw new IOException("Не удалось проверить новую версию.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try{await child.WaitForExitAsync(timeout.Token);}catch{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}throw;}
        if(child.ExitCode!=0)throw new IOException("Новая версия не смогла открыть копию данных. "+(File.Exists(Path.Combine(data,"validation-error.txt"))?File.ReadAllText(Path.Combine(data,"validation-error.txt")):"Код: "+child.ExitCode));
    }
}
