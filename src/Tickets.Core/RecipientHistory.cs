namespace Tickets.Core;

public sealed record RecipientDirectoryInfo(string Name,int IssueCount,int TicketCount,int ReturnedCount,bool IsDeleted=false)
{
    public string Caption=>$"{Name}{(IsDeleted?" · удалён":"")} · выдач: {IssueCount}";
}

public sealed record RecipientIssueInfo(IssueInfo Issue,string Title,string Venue,string EventDate,string SortDate)
{
    public DateTime StartsAt=>DateTime.TryParse(SortDate,out var date)?date:
        DateTime.TryParse(EventDate,System.Globalization.CultureInfo.GetCultureInfo("ru-RU"),System.Globalization.DateTimeStyles.None,out date)?date:DateTime.MinValue;
}

public sealed partial class Store
{
    public List<RecipientDirectoryInfo> RecipientDirectory(bool includeDeleted=false)
    {
        var history=ReadRecipientHistory();
        var groups=history.GroupBy(x=>CatalogText.Key(x.Issue.Recipient)).ToDictionary(g=>g.Key,g=>g.ToList());
        var hidden=HiddenCatalogKeys("recipient");
        using var c=Open();using var cmd=Command(c,"SELECT name FROM recipient ORDER BY name COLLATE NOCASE");
        using var r=cmd.ExecuteReader();var names=new List<string>();while(r.Read())names.Add(r.GetString(0));
        return names.Concat(history.Select(x=>x.Issue.Recipient)).Where(name=>includeDeleted||!hidden.Contains(CatalogText.Key(name)))
            .GroupBy(CatalogText.Key).Select(g=>{
                groups.TryGetValue(g.Key,out var issues);
                return new RecipientDirectoryInfo(g.First(),issues?.Count??0,issues?.Sum(x=>x.Issue.Count)??0,issues?.Sum(x=>x.Issue.ReturnedCount)??0,hidden.Contains(g.Key));
            }).OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public List<RecipientIssueInfo> RecipientHistory(string recipient)
    {
        var key=CatalogText.Key(recipient);
        return ReadRecipientHistory().Where(x=>CatalogText.Key(x.Issue.Recipient)==key).ToList();
    }

    private List<RecipientIssueInfo> ReadRecipientHistory()
    {
        using var c=Open();using var cmd=Command(c,"""
            SELECT b.id,b.event_id,b.recipient,b.issued_at,b.title,b.venue,b.event_date,e.starts_at,
              t.id,t.returned_at,t.amount
            FROM issue_batch b JOIN event e ON e.id=b.event_id
            LEFT JOIN ticket t ON t.issue_id=b.id AND t.event_id=b.event_id
            ORDER BY b.issued_at DESC,b.id DESC,t.id
            """);
        using var r=cmd.ExecuteReader();var result=new List<RecipientIssueInfo>();
        while(r.Read())
        {
            var id=r.GetInt64(0);
            if(result.Count==0||result[^1].Issue.Id!=id)
                result.Add(new(new(id,r.GetInt64(1),r.GetString(2),r.GetString(3),0,0,0),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7)));
            if(r.IsDBNull(8))continue;
            var current=result[^1];
            result[^1]=current with{Issue=current.Issue with{Count=current.Issue.Count+1,ReturnedCount=current.Issue.ReturnedCount+(r.IsDBNull(9)?0:1),Total=current.Issue.Total+r.GetInt64(10)}};
        }
        // A merged invoice owns links rather than physical ticket rows. Display
        // current canonical invoices with their aggregate counts; originals remain
        // available through their historical invoice IDs.
        var headers=result.ToDictionary(item=>item.Issue.Id);
        return result.Select(item=>item.Issue.EventId).Distinct()
            .SelectMany(eventId=>Issues(eventId).Select(issue=>headers[issue.Id] with{Issue=issue}))
            .OrderByDescending(item=>item.Issue.IssuedAt).ThenByDescending(item=>item.Issue.Id).ToList();
    }
}
