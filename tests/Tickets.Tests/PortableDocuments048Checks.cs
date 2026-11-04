using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class PortableDocuments048Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("048 legacy document directory follows copied app across user and drive",store=>
        {
            foreach(var old in new[]{@"C:\Users\kicha\Downloads\Tickets-Windows-x64\Tickets\Data\Documents",@"E:\Билеты\Tickets\Data\Documents\",@"\\server\share\Tickets\Data\Documents"})
            {
                Setting(store,"directory",old);
                Require(store.DocumentsDirectory==Path.Combine(Path.GetDirectoryName(store.Path)!,"Documents"),"old computer's managed path retained: "+store.DocumentsDirectory);
            }
            var oldLocal=Path.Combine(Path.GetDirectoryName(store.Path)!,"old-app","Tickets","Data","Documents");Directory.CreateDirectory(oldLocal);
            File.WriteAllText(Path.Combine(oldLocal,"keep.docx"),"original");Setting(store,"directory",oldLocal);
            Require(store.DocumentsDirectory!=oldLocal,"legacy copy still writes to old app when it exists");
            Require(File.ReadAllText(Path.Combine(oldLocal,"keep.docx"))=="original","legacy original modified");
            Setting(store,"directory",@"C:\Users\kicha\Downloads\Tickets\Data\Documents");
            Setting(store,"manual-save-directory",@"C:\Users\kicha\Downloads\Tickets\Data\Documents\Накладные");
            Require(store.DocumentSaveDirectory==Path.Combine(store.DocumentsDirectory,"Накладные"),"manual export retained old computer path");
            var file=store.SaveSpecificationDocument(new(48,"Получатель",Store.DefaultSender,"Проверка","Документ после переноса",DateTimeOffset.Now.ToString("O")));
            Require(File.Exists(file)&&file.StartsWith(store.DocumentsDirectory+Path.DirectorySeparatorChar),"document was not generated next to copied database");
        });
        test("048 copied application retains internal custom folders and existing documents",store=>
        {
            var root=Path.GetDirectoryName(store.Path)!;
            var original=Path.Combine(root,"original","Data");Directory.CreateDirectory(original);
            File.Copy(store.Path,Path.Combine(original,"tickets.db"));var source=new Store(Path.Combine(original,"tickets.db"));
            var documents=Path.Combine(root,"original","Мои документы");source.SaveDocumentSettings(source.Sender,documents);
            var export=Path.Combine(documents,"Экспорт");Directory.CreateDirectory(export);source.RememberDocumentSaveDirectory(export);
            File.WriteAllText(Path.Combine(documents,"existing.docx"),"unchanged original document");
            var copy=Path.Combine(root,"copied");CopyDirectory(Path.GetDirectoryName(original)!,copy);
            var moved=new Store(Path.Combine(copy,"Data","tickets.db"));
            Require(moved.DocumentsDirectory==Path.Combine(copy,"Мои документы"),"internal custom folder did not follow copied app");
            Require(moved.DocumentSaveDirectory==Path.Combine(copy,"Мои документы","Экспорт"),"internal export folder did not follow copied app");
            Require(File.ReadAllText(Path.Combine(moved.DocumentsDirectory,"existing.docx"))=="unchanged original document","existing copied document lost");
            Require(File.ReadAllText(Path.Combine(documents,"existing.docx"))=="unchanged original document","original document changed");
        });
        test("048 ambiguous legacy external document folder remains unchanged",store=>
        {
            const string external=@"D:\Shared\Data\Documents";
            Setting(store,"directory",external);Setting(store,"manual-save-directory",external+@"\Накладные");
            Require(store.DocumentsDirectory==external,"legacy external document folder reinterpreted as app data");
            Require(store.DocumentSaveDirectory==external+@"\Накладные","legacy external export folder reinterpreted as app data");
        });
        test("048 explicit external document folders remain external",store=>
        {
            var external=Path.Combine(Path.GetDirectoryName(store.Path)!,"external","Data","Documents");Directory.CreateDirectory(external);
            var data=Path.Combine(Path.GetDirectoryName(store.Path)!,"app","Data");Directory.CreateDirectory(data);
            File.Copy(store.Path,Path.Combine(data,"tickets.db"));var app=new Store(Path.Combine(data,"tickets.db"));
            app.SaveDocumentSettings(app.Sender,external);app.RememberDocumentSaveDirectory(external);
            Require(new Store(app.Path).DocumentsDirectory==external,"explicit external folder changed");
            Require(app.DocumentSaveDirectory==external,"explicit export folder changed");
        });
    }
    private static void Setting(Store store,string name,string value)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=store.Path,Pooling=false}.ToString());connection.Open();
        using var command=connection.CreateCommand();command.CommandText="INSERT INTO document_setting(name,value) VALUES($n,$v) ON CONFLICT(name) DO UPDATE SET value=excluded.value";
        command.Parameters.AddWithValue("$n",name);command.Parameters.AddWithValue("$v",value);command.ExecuteNonQuery();
    }
    private static void CopyDirectory(string from,string to)
    {
        Directory.CreateDirectory(to);
        foreach(var file in Directory.GetFiles(from))File.Copy(file,Path.Combine(to,Path.GetFileName(file)));
        foreach(var child in Directory.GetDirectories(from))CopyDirectory(child,Path.Combine(to,Path.GetFileName(child)));
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
