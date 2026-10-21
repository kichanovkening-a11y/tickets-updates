namespace Tickets.Core;

/// <summary>Display names never change the persisted, user editable catalogue name.</summary>
public static class PricePlanPresentation
{
    public static string Caption(string name,decimal total)=>$"{name} · {total:N0} ₽";
    public static string ImportedName(string name,string title,string date,long eventId)
    {
        var basis=$"{title} — {date} (показ №{eventId})";
        if(name==basis)return title;
        if(name.StartsWith(basis+" — ",StringComparison.Ordinal)&&int.TryParse(name[(basis.Length+3)..],out var suffix)&&suffix>=2)return title;
        return name;
    }
}

public sealed partial class Store
{
    public string PricePlanDisplayName(PlanInfo plan)
    {
        using var c=Open();
        using(var exists=Command(c,"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='event_plan_source'"))
            if(Convert.ToInt64(exists.ExecuteScalar())==0)return plan.Name;
        using var cmd=Command(c,"SELECT e.title,e.legacy_date,e.id FROM event_plan_source x JOIN event e ON e.id=x.event_id WHERE x.plan_id=$p","$p",plan.Id);
        using var r=cmd.ExecuteReader();
        while(r.Read())
        {
            var display=PricePlanPresentation.ImportedName(plan.Name,r.GetString(0),r.GetString(1),r.GetInt64(2));
            if(display!=plan.Name)return display;
        }
        return plan.Name;
    }
    public string PricePlanCaption(PlanInfo plan)
    {
        var blocked=PlanBlockedSeats(plan.Id).ToHashSet();
        return PricePlanPresentation.Caption(PricePlanDisplayName(plan),PlanPrices(plan.Id).Where(p=>!blocked.Contains(p.SeatId)).Sum(p=>(decimal)p.Amount));
    }
}
