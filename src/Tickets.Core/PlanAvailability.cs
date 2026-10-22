namespace Tickets.Core;

public sealed partial class Store
{
    private void EnsurePlanAvailability()
    {
        using var c=Open();
        using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS plan_seat_block(plan_id INTEGER NOT NULL REFERENCES pricing_plan(id),seat_id INTEGER NOT NULL REFERENCES seat(id),PRIMARY KEY(plan_id,seat_id))");
        cmd.ExecuteNonQuery();
    }
    public List<long> PlanBlockedSeats(long planId)
    {
        using var c=Open();using var cmd=Command(c,"SELECT b.seat_id FROM plan_seat_block b JOIN seat s ON s.id=b.seat_id WHERE plan_id=$p AND NOT EXISTS(SELECT 1 FROM catalog_hidden h WHERE h.kind='sector' AND h.entity_key=CAST(s.venue_id AS TEXT)||':'||CAST(s.sector_id AS TEXT)) ORDER BY b.seat_id","$p",planId);
        using var read=cmd.ExecuteReader();var result=new List<long>();while(read.Read())result.Add(read.GetInt64(0));return result;
    }
    public List<long> CopyPlanBlockedSeats(long planId,long targetVenueId)
    {
        var plan=AllPlans().FirstOrDefault(p=>p.Id==planId)??throw new TicketException("Расценовка не найдена.");
        var blocked=PlanBlockedSeats(planId).ToHashSet();
        if(plan.VenueId==targetVenueId)return blocked.ToList();
        string Key(SeatInfo s)=>s.Sector.Trim().Normalize().ToUpperInvariant()+"\u001f"+s.Row.Trim().Normalize().ToUpperInvariant()+"\u001f"+s.Number.Trim().Normalize().ToUpperInvariant();
        var source=VenueSeats(plan.VenueId).Where(s=>blocked.Contains(s.Id)).Select(Key).ToHashSet();
        return VenueSeats(targetVenueId).Where(s=>source.Contains(Key(s))).Select(s=>s.Id).ToList();
    }
}
