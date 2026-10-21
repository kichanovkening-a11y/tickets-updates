namespace Tickets.Core;

public sealed partial class Store
{
    // Empty means the built-in standard blank; selection is shared by all shows.
    public string? LastTicketBlankId
    {
        get
        {
            var id=DocumentSetting("last-ticket-blank","");
            return id.Length==0?null:id;
        }
    }
    public void RememberTicketBlank(string? id)
    {
        if(id is not null&&!TicketBlanks().Any(b=>b.Id==id))throw new TicketException("Бланк печати не найден.");
        using var c=Open();
        using var cmd=Command(c,"INSERT INTO document_setting(name,value) VALUES('last-ticket-blank',$id) ON CONFLICT(name) DO UPDATE SET value=excluded.value","$id",id??"");
        cmd.ExecuteNonQuery();
    }
}
