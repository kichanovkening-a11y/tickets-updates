using Microsoft.Data.Sqlite;
namespace Tickets.Core;

public sealed record TicketlandSectorCandidate030(string Sector,string Caption);
public sealed record TicketlandSectorReview030(string Key,long EventId,string Title,DateTime StartsAt,string Venue,string SourceSector,int TicketCount,string Reason,IReadOnlyList<string> MissingSeats,IReadOnlyList<TicketlandSectorCandidate030> Candidates);
public sealed record TicketlandSourceTotal030(long EventId,string Title,DateTime StartsAt,string Venue,int TicketCount,decimal Nominal);

public sealed partial class Store
{
    public IReadOnlyList<TicketlandSectorReview030> TicketlandSectorReviews030()
    {
        using var c=Open();using var tx=c.BeginTransaction();var sales=ReadTicketlandSales029(c,tx);var shows=Local029(c,tx);
        var matches=Matches029(c,tx,sales,shows,TicketlandBindings028(c,tx),TicketlandSectorBindings028(c,tx));
        var result=new List<TicketlandSectorReview030>();
        foreach(var group in sales.Where(s=>matches[s.Barcode].Issue is not null).GroupBy(s=>matches[s.Barcode].Issue!.Key))
        {
            var sale=group.First();var show=Exact030(sale,shows).Single();
            var all=sales.Where(s=>!General030(s)&&Exact030(s,shows) is var exact&&exact.Length==1&&exact[0].Id==show.Id&&Text029(s.Sector)==Text029(sale.Sector)).ToArray();
            var resolution=ResolveSector032(sale.Sector,show);
            var decision=SectorDecision032(c,tx,sale,show);
            var sector=ApprovedSector032(sale.Sector,show,decision.Sector)??resolution.Sector;
            var target=sector??(resolution.Aliases.Count==1?resolution.Aliases[0]:null);
            var missing=target is null?[]:all.Where(s=>show.Seats.Count(p=>p.Sector==target&&CatalogText.Key(p.Row)==CatalogText.Key(s.Row)&&CatalogText.Key(p.Number)==CatalogText.Key(s.Seat))!=1)
                .Select(s=>$"сектор «{sale.Sector}» → «{target}», ряд {s.Row}, место {s.Seat}").Distinct().OrderBy(s=>s).ToArray();
            var groupBarcodes=all.Select(s=>s.Barcode).ToHashSet(StringComparer.Ordinal);
            var outside=sales.Where(s=>!groupBarcodes.Contains(s.Barcode)).Select(s=>s.Barcode).ToHashSet(StringComparer.Ordinal);
            var claimed=matches.Where(x=>outside.Contains(x.Key)&&x.Value.Event==show.Id&&x.Value.Seat.HasValue).Select(x=>x.Value.Seat!.Value).ToHashSet();
            // Only current matches claim seats: unchecked legacy IDs cannot reserve a
            // different sector or authorize an alias through coincident positions.
            var occupied=target is not null&&show.Seats.Any(p=>p.Sector==target&&claimed.Contains(p.Id)&&all.Any(s=>CatalogText.Key(p.Row)==CatalogText.Key(s.Row)&&CatalogText.Key(p.Number)==CatalogText.Key(s.Seat)));
            var collision=group.Any(s=>matches[s.Barcode].Issue!.Reason.Contains("Разные исходные",StringComparison.Ordinal));
            var candidates=sector is null&&resolution.Aliases.Count==1&&missing.Length==0&&!occupied&&!collision
                ?new[]{new TicketlandSectorCandidate030(resolution.Aliases[0],resolution.Aliases[0])}:Array.Empty<TicketlandSectorCandidate030>();
            var reasons=group.Select(s=>matches[s.Barcode].Issue!.Reason).ToList();
            if(target is not null)reasons.AddRange(all.Where(s=>show.Seats.Count(p=>p.Sector==target&&CatalogText.Key(p.Row)==CatalogText.Key(s.Row)&&CatalogText.Key(p.Number)==CatalogText.Key(s.Seat))!=1).Select(s=>MissingPosition032(show,target,s)));
            if(occupied)reasons.Add($"Сектор «{sale.Sector}» → «{target}»: место занято другим исходным сектором.");
            result.Add(new(group.Key,show.Id,show.Title,show.At,show.Venue,sale.Sector,all.Length,string.Join(" ",reasons.Distinct()),missing,candidates));
        }
        return result.OrderBy(x=>x.StartsAt).ThenBy(x=>x.Title).ThenBy(x=>x.SourceSector).ToArray();
    }
    public void AcceptTicketlandSector030(string key,string sector)
    {
        var group=TicketlandSectorReviews030().SingleOrDefault(x=>x.Key==key);
        var candidate=group?.Candidates.SingleOrDefault(x=>Text029(x.Sector)==Text029(sector));
        if(candidate is null)throw new TicketException("Предложение изменилось. Обновите список секторов Ticketland.");
        SaveDecision030(key,false,candidate.Sector);
    }
    public void RejectTicketlandSector030(string key)
    {
        if(!TicketlandSectorReviews030().Any(x=>x.Key==key))throw new TicketException("Предложение изменилось. Обновите список секторов Ticketland.");
        SaveDecision030(key,true,null);
    }
    private void SaveDecision030(string key,bool rejected,string? sector)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);EnsureDecisions030(c,tx);
        using var q=Command(c,"INSERT OR REPLACE INTO ticketland_sector_decision_030(issue_key,rejected,local_sector) VALUES($k,$r,$s)","$k",key,"$r",rejected?1:0,"$s",(object?)sector??DBNull.Value);q.Transaction=tx;q.ExecuteNonQuery();tx.Commit();
    }
    public IReadOnlyList<TicketlandSourceTotal030> TicketlandSourceTotals030()
    {
        using var c=Open();using var tx=c.BeginTransaction();var shows=Local029(c,tx,loadSeats:false);var sales=ReadTicketlandSales029(c,tx);
        return sales.DistinctBy(s=>s.Barcode).Select(s=>(Sale:s,Exact:Exact030(s,shows))).Where(x=>x.Exact.Length==1).GroupBy(x=>x.Exact[0].Id)
            .Select(g=>{var show=g.First().Exact[0];return new TicketlandSourceTotal030(show.Id,show.Title,show.At,show.Venue,g.Count(),g.Sum(x=>x.Sale.Nominal));}).OrderBy(x=>x.StartsAt).ThenBy(x=>x.Title).ToArray();
    }
}
