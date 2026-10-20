using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed partial class Store
{
    // Imported databases may have prices only on shows. Preserve a reusable snapshot
    // without depending on whether that show or its performance remains active.
    public void EnsureEventPlans(long venueId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        EnsureEventPlanSources(c,tx);
        if(IsCatalogHidden(c,tx,"venue",venueId.ToString(System.Globalization.CultureInfo.InvariantCulture)))return;
        var events=new List<long>();
        using(var cmd=Command(c,"SELECT id FROM event WHERE venue_id=$v AND NOT EXISTS(SELECT 1 FROM event_plan_source x WHERE x.event_id=event.id) AND EXISTS(SELECT 1 FROM event_price p WHERE p.event_id=event.id) ORDER BY id","$v",venueId))
        {
            cmd.Transaction=tx;using var r=cmd.ExecuteReader();while(r.Read())events.Add(r.GetInt64(0));
        }
        foreach(var id in events)GetOrCreateEventPlan(c,tx,id);
        tx.Commit();
    }

    public long GetOrCreateEventPlan(long eventId)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        EnsureEventPlanSources(c,tx);
        var id=GetOrCreateEventPlan(c,tx,eventId);tx.Commit();return id;
    }

    private static void EnsureEventPlanSources(SqliteConnection c,SqliteTransaction tx)
    {
        using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS event_plan_source(event_id INTEGER PRIMARY KEY REFERENCES event(id),plan_id INTEGER NOT NULL REFERENCES pricing_plan(id))");
        cmd.Transaction=tx;cmd.ExecuteNonQuery();
    }

    private static long GetOrCreateEventPlan(SqliteConnection c,SqliteTransaction tx,long eventId)
    {
        using(var mapped=Command(c,"SELECT plan_id FROM event_plan_source WHERE event_id=$e","$e",eventId))
        {
            mapped.Transaction=tx;if(mapped.ExecuteScalar() is long id)return id;
        }
        long venue;string title,date;
        using(var source=Command(c,"SELECT venue_id,title,legacy_date FROM event WHERE id=$e AND EXISTS(SELECT 1 FROM event_price WHERE event_id=$e)","$e",eventId))
        {
            source.Transaction=tx;using var r=source.ExecuteReader();
            if(!r.Read())throw new TicketException("У показа нет расценовки для копирования.");
            venue=r.GetInt64(0);title=r.GetString(1);date=r.GetString(2);
        }
        // Compare both directions, including partial pricing; equal totals alone
        // must never merge different seat assignments. Hidden plans also match so
        // an explicitly deleted template is not silently recreated from its shows.
        using var match=Command(c,"""
            SELECT p.id FROM pricing_plan p WHERE p.venue_id=$v
            AND NOT EXISTS(SELECT 1 FROM event_price e WHERE e.event_id=$e AND NOT EXISTS(
                SELECT 1 FROM plan_price x WHERE x.plan_id=p.id AND x.seat_id=e.seat_id AND x.amount=e.amount))
            AND NOT EXISTS(SELECT 1 FROM plan_price x WHERE x.plan_id=p.id AND NOT EXISTS(
                SELECT 1 FROM event_price e WHERE e.event_id=$e AND e.seat_id=x.seat_id AND e.amount=x.amount))
            ORDER BY p.id LIMIT 1
            ""","$v",venue,"$e",eventId);match.Transaction=tx;
        long planId;
        if(match.ExecuteScalar() is long existing)planId=existing;
        else
        {
            var basis=$"{title} — {date} (показ №{eventId})";var name=basis;var suffix=1;
            while(true)
            {
                using var collision=Command(c,"SELECT count(*) FROM pricing_plan WHERE venue_id=$v AND name_key=$k","$v",venue,"$k",CatalogText.Key(name));collision.Transaction=tx;
                if(Convert.ToInt64(collision.ExecuteScalar())==0)break;
                name=basis+" — "+(++suffix);
            }
            using var add=Command(c,"INSERT INTO pricing_plan(venue_id,name,name_key) VALUES($v,$n,$k) RETURNING id","$v",venue,"$n",name,"$k",CatalogText.Key(name));add.Transaction=tx;
            planId=Convert.ToInt64(add.ExecuteScalar());
            using var copy=Command(c,"INSERT INTO plan_price(plan_id,seat_id,amount,color) SELECT $p,seat_id,amount,color FROM event_price WHERE event_id=$e","$p",planId,"$e",eventId);copy.Transaction=tx;copy.ExecuteNonQuery();
        }
        using var link=Command(c,"INSERT INTO event_plan_source(event_id,plan_id) VALUES($e,$p)","$e",eventId,"$p",planId);link.Transaction=tx;link.ExecuteNonQuery();
        return planId;
    }
}
