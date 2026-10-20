using System.Text.RegularExpressions;

namespace Tickets.Core;

public sealed partial class Store
{
    public string SaveRecipient(string name)
    {
        name=Regex.Replace(Required(name,"Получатель").Normalize(),@"\s+"," ");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        AddRecipient(c,tx,name);
        using var cmd=Command(c,"SELECT name FROM recipient WHERE name_key=$k","$k",CatalogText.Key(name));cmd.Transaction=tx;
        var saved=(string)cmd.ExecuteScalar()!;tx.Commit();return saved;
    }
}
