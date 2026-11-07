namespace Tickets.Core;

public sealed partial class Store
{
    public string HomeLayout => DocumentSetting("home-repertoire", "");
    public void SaveHomeLayout(string layout)
    {
        using var db = Open();
        using var cmd = Command(db, "INSERT INTO document_setting(name,value) VALUES('home-repertoire',$layout) ON CONFLICT(name) DO UPDATE SET value=excluded.value", "$layout", layout);
        cmd.ExecuteNonQuery();
    }
}
