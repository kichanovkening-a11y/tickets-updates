using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
using Tickets.Update;

namespace Tickets.App;

internal sealed class UpdateWindow : EditorWindow
{
    private UpdateManifest? manifest;
    private readonly GitHubUpdates client=new();
    private readonly CancellationTokenSource cancel=new();
    private bool busy;
    public UpdateWindow(Store store):base(store,"Обновление программы",700,540)
    {
        var form=new StackPanel{Margin=new Thickness(26)};Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var installed=Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
        form.Children.Add(new TextBlock{Text="Обновление через GitHub",FontSize=23,FontWeight=FontWeights.SemiBold});
        form.Children.Add(new TextBlock{Text="Установлена версия: "+installed,Margin=new Thickness(0,12,0,0)});
        var source=Path.Combine(AppContext.BaseDirectory,"Data","update-source.txt");
        var repository=Field(form,"Репозиторий обновлений",File.Exists(source)?File.ReadAllText(source).Trim():"kichanovkening-a11y/tickets-updates");
        var status=new TextBlock{Text="Нажмите «Проверить обновления».",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,10)};form.Children.Add(status);
        var check=new Button{Content="Проверить обновления"};form.Children.Add(check);
        var install=new Button{Content="Скачать и установить",IsEnabled=false,Margin=new Thickness(0,12,0,0)};form.Children.Add(install);
        form.Children.Add(new TextBlock{Text="Скачиваются только изменившиеся компоненты. Перед установкой сохраняется копия базы; затем программа перезапускается.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,20,0,0)});
        check.Click+=async(_,_)=>{
            if(busy)return;busy=true;check.IsEnabled=false;install.IsEnabled=false;repository.IsEnabled=false;
            try
            {
                var repo=GitHubUpdates.Repository(repository.Text);status.Text="Проверка…";
                manifest=await client.CheckAsync(repo,cancel.Token);File.WriteAllText(source,repo);
                var newer=UpdateFiles.VersionNumber(manifest.Version)>UpdateFiles.VersionNumber(installed);
                status.Text=newer?"Доступна версия "+manifest.Version:"Установлена актуальная версия.";install.IsEnabled=newer;
            }
            catch(OperationCanceledException){}
            catch(Exception ex){status.Text="Обновление пока недоступно. "+ex.Message;}
            finally{busy=false;check.IsEnabled=true;repository.IsEnabled=true;}
        };
        repository.TextChanged+=(_,_)=>{manifest=null;install.IsEnabled=false;};
        install.Click+=async(_,_)=>{
            if(busy||manifest is null)return;busy=true;check.IsEnabled=false;install.IsEnabled=false;repository.IsEnabled=false;
            try
            {
                var work=Path.Combine(Path.GetTempPath(),"TicketsUpdates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
                var plan=await client.DownloadAsync(manifest,AppContext.BaseDirectory,work,new Progress<string>(text=>status.Text=text),cancel.Token);
                if(plan.Files.Count==0){status.Text="Все файлы уже обновлены.";return;}
                cancel.Token.ThrowIfCancellationRequested();status.Text="Подготовка к установке…";
                store.Backup(Path.Combine(AppContext.BaseDirectory,"Backups",$"tickets-before-update-{DateTime.Now:yyyyMMdd-HHmmss}.db"));
                var runner=Path.Combine(work,"runner");Directory.CreateDirectory(runner);
                await Task.Run(()=>{
                    foreach(var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
                    {
                        var name=Path.GetFileName(file);
                        if(name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".deps.json",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".runtimeconfig.json",StringComparison.OrdinalIgnoreCase))File.Copy(file,Path.Combine(runner,name));
                    }
                },cancel.Token);
                cancel.Token.ThrowIfCancellationRequested();
                var planPath=Path.Combine(work,"install-plan.json");File.WriteAllText(planPath,JsonSerializer.Serialize(plan));
                var start=new ProcessStartInfo(Path.Combine(runner,"Tickets.exe")){UseShellExecute=false,WorkingDirectory=runner};
                start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(planPath);start.ArgumentList.Add(Environment.ProcessId.ToString());
                if(Process.Start(start) is null)throw new IOException("Не удалось запустить установку обновления.");
                Application.Current.Shutdown();
            }
            catch(OperationCanceledException){}
            catch(Exception ex){status.Text="Установка не началась. "+ex.Message;}
            finally{busy=false;check.IsEnabled=true;repository.IsEnabled=true;}
        };
        Closing+=(_,_)=>cancel.Cancel();
    }
}

internal static class UpdateRunner
{
    public static void Apply(string planFile,string parentPid)
    {
        try
        {
            var plan=JsonSerializer.Deserialize<InstallPlan>(File.ReadAllText(planFile))??throw new InvalidDataException("Нет плана установки.");
            if(!int.TryParse(parentPid,out var pid))throw new InvalidDataException("Неизвестен процесс программы.");
            try{using var parent=Process.GetProcessById(pid);if(!parent.WaitForExit(60000))throw new IOException("Закройте программу перед обновлением.");}catch(ArgumentException){}
            UpdateFiles.Apply(plan);
            Process.Start(new ProcessStartInfo(Path.Combine(plan.Target,"Tickets.exe")){UseShellExecute=true,WorkingDirectory=plan.Target});
        }
        catch(Exception ex){MessageBox.Show("Обновление не выполнено.\n\n"+ex.Message,"Билеты",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
}
