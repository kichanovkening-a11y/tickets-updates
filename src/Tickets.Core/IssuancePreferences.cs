namespace Tickets.Core;

public sealed partial class Store
{
    public string LastIssueSeries=>DocumentSetting("issue-series","ТЕСТ");
    public void RememberIssueSeries(string series)
    {
        series=Required(series,"Серия").Trim();
        if(series.Length>32||series.Any(char.IsControl))throw new TicketException("Серия — до 32 символов без переносов строк.");
        using var c=Open();using var command=Command(c,"INSERT INTO document_setting(name,value) VALUES('issue-series',$s) ON CONFLICT(name) DO UPDATE SET value=excluded.value","$s",series);command.ExecuteNonQuery();
    }
}
