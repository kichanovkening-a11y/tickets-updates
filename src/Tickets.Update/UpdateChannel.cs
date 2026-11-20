namespace Tickets.Update;

public sealed class UpdateChannel
{
    public static UpdateChannel Standard { get; } = new(false);
    public static UpdateChannel Classic { get; } = new(true);
    private readonly bool classic;
    private UpdateChannel(bool classic) => this.classic=classic;
    public string ApplicationName => classic?"Tickets.Classic":"Tickets";
    public string ApplicationExecutable => ApplicationName+".exe";
    public string UpdaterExecutable => classic?"Tickets.Classic.Updater.exe":"Tickets.Updater.exe";
    public string LauncherExecutable => classic?ApplicationExecutable:"! Tickets.exe";
    public string ReleaseTag(string version) => classic?"classic-v"+version:"v"+version;
    public Uri ManifestUri(string repository) => new($"https://github.com/{GitHubUpdates.Repository(repository)}/releases/"+(classic?"download/classic":"latest/download")+"/update-manifest.json");
    public bool CanUpdate(string folder) => !File.Exists(Path.Combine(folder,(classic?"Tickets":"Tickets.Classic")+".dll"))
        &&(File.Exists(Path.Combine(folder,ApplicationName+".dll"))||UpdateTransaction.HasPending(folder));
    public void ValidateFiles(IEnumerable<UpdateFile> files)
    {
        var names=files.Select(f=>f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var other=classic?"Tickets":"Tickets.Classic";
        if(names.Contains(other+".exe")||names.Contains(other+".dll"))
            throw new InvalidDataException("Обновление предназначено для другой версии программы.");
        if(classic&&(!names.Contains(ApplicationExecutable)||!names.Contains(ApplicationName+".dll")||!names.Contains(UpdaterExecutable)))
            throw new InvalidDataException("Неполное обновление классической версии.");
    }
}
