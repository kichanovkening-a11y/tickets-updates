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
        var activity=new ProgressBar{Height=18,Margin=new Thickness(0,0,0,12),Visibility=Visibility.Collapsed,IsIndeterminate=true};form.Children.Add(activity);
        var check=new Button{Content="Проверить обновления"};form.Children.Add(check);
        var install=new Button{Content="Скачать и установить",IsEnabled=false,Margin=new Thickness(0,12,0,0)};form.Children.Add(install);
        form.Children.Add(new TextBlock{Text="Обновлятор проверяет полный комплект файлов и копию базы. Перед установкой сохраняется резерв программы и данных; затем программа перезапускается.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,20,0,0)});
        check.Click+=async(_,_)=>{
            if(busy)return;busy=true;check.IsEnabled=false;install.IsEnabled=false;repository.IsEnabled=false;
            try
            {
                var repo=GitHubUpdates.Repository(repository.Text);status.Text="Проверка…";activity.Visibility=Visibility.Visible;
                manifest=await client.CheckAsync(repo,cancel.Token);File.WriteAllText(source,repo);
                var newer=UpdateFiles.VersionNumber(manifest.Version)>UpdateFiles.VersionNumber(installed);
                status.Text=newer?"Доступна версия "+manifest.Version:"Установлена актуальная версия.";install.IsEnabled=newer;
            }
            catch(OperationCanceledException){}
            catch(Exception ex){status.Text="Обновление пока недоступно. "+ex.Message;}
            finally{busy=false;check.IsEnabled=true;repository.IsEnabled=true;activity.Visibility=Visibility.Collapsed;}
        };
        repository.TextChanged+=(_,_)=>{manifest=null;install.IsEnabled=false;};
        install.Click+=(_,_)=>{
            if(busy||manifest is null)return;
            try
            {
                var exe=Path.Combine(AppContext.BaseDirectory,"Tickets.Updater.exe");
                if(!File.Exists(exe))throw new IOException("Не найден Tickets.Updater.exe. Перенесите полную папку программы.");
                var start=new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=AppContext.BaseDirectory};
                foreach(var arg in new[]{"--target",AppContext.BaseDirectory,"--parent",Environment.ProcessId.ToString(),"--repository",manifest.Repository})start.ArgumentList.Add(arg);
                if(Process.Start(start) is null)throw new IOException("Не удалось запустить обновлятор.");
                Application.Current.Shutdown();
            }
            catch(Exception ex){status.Text="Установка не началась. "+ex.Message;}
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