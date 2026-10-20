namespace Tickets.Core;

public sealed partial class Store
{
    public List<PlanInfo> AllPlans()
    {
        using var c=Open();using var cmd=Command(c,"SELECT id,venue_id,name FROM pricing_plan WHERE NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE (h.kind='plan' AND h.entity_key=CAST(pricing_plan.id AS TEXT)) OR (h.kind='venue' AND h.entity_key=CAST(pricing_plan.venue_id AS TEXT))) ORDER BY name");using var read=cmd.ExecuteReader();var result=new List<PlanInfo>();
        while(read.Read())result.Add(new(read.GetInt64(0),read.GetInt64(1),read.GetString(2)));return result;
    }
    public List<PriceValue> CopyPlanPrices(long planId,long targetVenueId)
    {
        var plans=AllPlans();var plan=plans.FirstOrDefault(p=>p.Id==planId)??throw new TicketException("Расценовка не найдена.");
        if(plan.VenueId==targetVenueId)return PlanPrices(planId);
        string Key(SeatInfo seat)=>seat.Sector.Trim().Normalize().ToUpperInvariant()+"\u001f"+seat.Row.Trim().Normalize().ToUpperInvariant()+"\u001f"+seat.Number.Trim().Normalize().ToUpperInvariant();
        var source=VenueSeats(plan.VenueId).ToDictionary(s=>s.Id);var amounts=PlanPrices(planId).Where(p=>source.ContainsKey(p.SeatId)).ToDictionary(p=>Key(source[p.SeatId]));
        return VenueSeats(targetVenueId).Where(s=>amounts.ContainsKey(Key(s))).Select(s=>new PriceValue(s.Id,amounts[Key(s)].Amount,amounts[Key(s)].Color)).ToList();
    }
}
