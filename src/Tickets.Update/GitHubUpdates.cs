using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tickets.Update;

public sealed class GitHubUpdates
{
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromMinutes(10)};
    public static string Repository(string value)
    {
        value=value.Trim();if(!Regex.IsMatch(value,"^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}$"))throw new InvalidDataException("Укажите репозиторий в виде владелец/название.");return value;
    }
    private static Uri Asset(string repo,string tag,string asset)=>new($"https://github.com/{Repository(repo)}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(asset)}");
    public async Task<UpdateManifest> CheckAsync(string repository,CancellationToken cancellation=default)
    {
        repository=Repository(repository);
        using var response=await Http.GetAsync($"https://github.com/{repository}/releases/latest/download/update-manifest.json",HttpCompletionOption.ResponseHeadersRead,cancellation);
        if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidDataException("В этом репозитории ещё нет опубликованного обновления или он закрыт.");
        response.EnsureSuccessStatusCode();
        using var stream=await response.Content.ReadAsStreamAsync(cancellation);using var buffer=new MemoryStream();
        await CopyLimited(stream,buffer,2_000_000,cancellation);
        return UpdateFiles.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()),repository);
    }
    public async Task<InstallPlan> DownloadAsync(UpdateManifest manifest,string target,string work,IProgress<string>? progress=null,CancellationToken cancellation=default,bool full=false,IProgress<UpdateProgress>? detailedProgress=null)
    {
        var packages=await Task.Run(()=>full?manifest.Packages:UpdateFiles.Needed(target,manifest),cancellation);
        var stage=System.IO.Path.Combine(work,"payload");Directory.CreateDirectory(stage);
        var files=new List<UpdateFile>();
        foreach(var package in packages)
        {
            progress?.Report("Скачивание: "+package.Name);
            detailedProgress?.Report(new("Скачивание: "+package.Name));
            var zip=System.IO.Path.Combine(work,package.Name+".zip");var lastReport=System.Diagnostics.Stopwatch.StartNew();
            using var response=await Http.GetAsync(Asset(manifest.Repository,"v"+manifest.Version,package.Asset),HttpCompletionOption.ResponseHeadersRead,cancellation);
            response.EnsureSuccessStatusCode();using(var input=await response.Content.ReadAsStreamAsync(cancellation))using(var output=new FileStream(zip,FileMode.CreateNew))await CopyWithProgressAsync(input,output,300_000_000,response.Content.Headers.ContentLength,(done,total)=>{if(done==0||done==total||lastReport.ElapsedMilliseconds>=100){lastReport.Restart();detailedProgress?.Report(new("Скачивание: "+package.Name,done,total,$"{done/1048576.0:0.0} МБ"+(total.HasValue?$" из {total.Value/1048576.0:0.0} МБ":"")));}},cancellation);
            detailedProgress?.Report(new("Проверка и распаковка: "+package.Name));
            await Task.Run(()=>UpdateFiles.Extract(zip,stage,package),cancellation);files.AddRange(package.Files);
        }
        return new(target,stage,files);
    }
    public static async Task CopyWithProgressAsync(Stream input,Stream output,long limit,long? length,Action<long,long?>? report,CancellationToken cancellation=default)
    {
        if(length>limit)throw new InvalidDataException("Файл обновления слишком велик.");
        var known=length is >0?length:null;var bytes=new byte[81920];long total=0;int n;
        report?.Invoke(0,known);
        while((n=await input.ReadAsync(bytes,cancellation))>0)
        {
            total+=n;if(total>limit)throw new InvalidDataException("Файл обновления слишком велик.");
            await output.WriteAsync(bytes.AsMemory(0,n),cancellation);report?.Invoke(total,known);
        }
        if(length.HasValue&&length.Value>=0&&total!=length.Value)throw new EndOfStreamException("Загрузка обновления прервалась. Повторите попытку.");
    }
    private static async Task CopyLimited(Stream input,Stream output,long limit,CancellationToken cancellation)
    {
        var bytes=new byte[81920];long total=0;int n;
        while((n=await input.ReadAsync(bytes,cancellation))>0){total+=n;if(total>limit)throw new InvalidDataException("Файл обновления слишком велик.");await output.WriteAsync(bytes.AsMemory(0,n),cancellation);}
    }
}