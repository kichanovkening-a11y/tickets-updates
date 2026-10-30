using Tickets.Core;
using System.Text.Json;
using System.Reflection;

internal static class TicketPro037Checks
{
    static object Call(object? target,Type type,string name,params object[] args)
    {
        var method=type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).SingleOrDefault(x=>x.Name==name&&x.GetParameters().Length==args.Length);
        if(method is null)throw new Exception("Missing Ticket PRO behavior: "+name);
        try{return method.Invoke(target,args)!;}catch(TargetInvocationException ex){throw ex.InnerException!;}
    }
    static object Parse(string json,DateTimeOffset at)
    {
        var type=typeof(Store).Assembly.GetType("Tickets.Core.TicketPro037");
        if(type is null)throw new Exception("Ticket PRO snapshot reader is missing");
        return Call(null,type,"Parse",json,at);
    }
    static void Apply(Store s,long id,string json,DateTimeOffset at)=>Call(s,typeof(Store),"ApplyTicketPro037",id,Parse(json,at));
    static void Release(Store s,long id,long[] seats)=>Call(s,typeof(Store),"ReleaseTicketPro037",id,seats);
    static void Check(bool c,string m){if(!c)throw new Exception(m);}
    static void Reject(Action a){try{a();}catch(TicketException){return;}throw new Exception("Expected rejected Ticket PRO snapshot/action");}
    internal static void Register(Action<string,Action<Store>> run)
    {
        (long Event,long[] Seats,string Title,DateTime At) Fixture(Store s)
        {
            var v=s.CreateHall("PRO037 "+Guid.NewGuid(),"",Enumerable.Range(1,3).Select(n=>new DraftSeat{Sector="Партер",Row="5",Number=n.ToString(),X=n*22,Y=0}).ToArray());
            var seats=s.VenueSeats(v).Select(x=>x.Id).ToArray();var p=s.SavePlan(v,"PRO prices",seats.Select(id=>new PriceValue(id,5000,"#FF0000")).ToArray());
            var title="PRO test "+Guid.NewGuid();var at=new DateTime(2030,11,1,19,0,0);return(s.CreateEvent(s.SavePerformance(title),v,p,at),seats,title,at);
        }
        string Json(string title,string[] states,int expected=3)=>JsonSerializer.Serialize(new{url="https://pro.ticketland.ru/management/show/21540221/ticket-control/28886198",title,date="01.11.2030 19:00",expectedCount=expected,places=states.Select((state,i)=>new{id="p"+(100+i),sector="партер",row="5",seat=(i+1).ToString(),status=state,fund="1003",price="5000"}).ToArray()});
        run("037 PRO three exact states override invitations in old sales reports and persist",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;
            s.ApplyTicketlandReport028(new(new[]{new TicketlandSale028(f.Title,f.At,"Партер","5","2","invitation-old",0){Venue=s.Events().Single(e=>e.Id==f.Event).Venue}},at),true);
            Apply(s,f.Event,Json(f.Title,["Продано агентами","Забронировано","В продаже"]),at.AddSeconds(1));
            var seats=new Store(s.Path).Seats(f.Event);
            Check(seats[0].IsTicketlandSold&&!seats[0].Available,"agent sale must block issue");
            Check(!seats[1].IsTicketlandSold&&!seats[1].Available&&seats[1].Status.Contains("Занято"),"invitation must be occupied, not sale");
            Check(seats[2].Available,"in-sale seat not available");Check(s.TicketlandShowSummary034(f.Event).Matched==1&&s.TicketlandShowSummary034(f.Event).Unmatched==0,"PRO summary still reports old invitations as unmatched sales");Reject(()=>s.Issue(f.Event,[f.Seats[1]],"test","P37",1));
            Release(s,f.Event,[f.Seats[1]]);Check(s.Seats(f.Event)[1].Available,"local release did not allow issuance");
            Apply(s,f.Event,Json(f.Title,["Продано агентами","Забронировано","В продаже"]),at.AddSeconds(2));
            Check(new Store(s.Path).Seats(f.Event)[1].Available,"refresh/restart lost local release");
            var issue=s.Issue(f.Event,[f.Seats[1]],"recipient","P37",1);Check(s.Invoice(f.Event,issue).Tickets.Count==1,"released invitation cannot be issued");
            Check(s.History(f.Event).Count==1,"source states polluted local history");
        });
        run("037 PRO real sale revokes local release and blocks printing extension and issue",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;Apply(s,f.Event,Json(f.Title,["Забронировано","Не использовать","В продаже"]),at);
            Release(s,f.Event,[f.Seats[0],f.Seats[1]]);var issue=s.Issue(f.Event,[f.Seats[0]],"recipient","P37B",1);
            Apply(s,f.Event,Json(f.Title,["Продано","Продано агентами","В продаже"]),at.AddSeconds(1));
            Check(s.Seats(f.Event).Take(2).All(x=>x.IsTicketlandSold&&!x.Available),"sale failed to revoke local release");
            Reject(()=>Release(s,f.Event,[f.Seats[1]]));Reject(()=>s.Issue(f.Event,[f.Seats[1]],"x","P37B",2));Reject(()=>s.ExtendInvoice(f.Event,issue,[f.Seats[1]]));Reject(()=>s.TicketReprint(f.Event,[f.Seats[0]]));
            Apply(s,f.Event,Json(f.Title,["Забронировано","Не использовать","В продаже"]),at.AddSeconds(2));
            Check(!s.Seats(f.Event)[1].Available,"old release resurrected after sold->occupied");
        });
        run("037 PRO malformed partial stale wrong-show and unknown states preserve previous snapshot",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;var good=Json(f.Title,["Продано","Забронировано","В продаже"]);Reject(()=>Apply(s,f.Event,Json("Another show",["В продаже","В продаже","В продаже"]),at));Apply(s,f.Event,good,at);
            Reject(()=>Apply(s,f.Event,Json(f.Title,["В продаже","В продаже"],3),at.AddSeconds(1)));
            Reject(()=>Apply(s,f.Event,Json(f.Title,["В продаже","Новый статус","В продаже"]),at.AddSeconds(1)));
            Reject(()=>Apply(s,f.Event,good,at.AddSeconds(-1)));
            Reject(()=>Apply(s,f.Event,Json("Another show",["В продаже","В продаже","В продаже"]).Replace("28886198","28886199"),at.AddSeconds(1)));
            Reject(()=>Apply(s,f.Event,good.Replace("p101","p100"),at.AddSeconds(1)));
            Reject(()=>Apply(s,f.Event,good.Replace("pro.ticketland.ru","example.com"),at.AddSeconds(1)));
            Check(new Store(s.Path).Seats(f.Event)[0].IsTicketlandSold&&!s.Seats(f.Event)[1].Available,"failed update replaced prior data");
        });
        run("037 PRO bound identity survives a local title edit",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;var raw=Json(f.Title,["Продано","Забронировано","В продаже"]);Apply(s,f.Event,raw,at);
            using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={s.Path}")){c.Open();using var q=c.CreateCommand();q.CommandText="UPDATE event SET title='Local edited caption' WHERE id=$e";q.Parameters.AddWithValue("$e",f.Event);q.ExecuteNonQuery();}
            Apply(s,f.Event,raw,at.AddSeconds(1));Check(s.Seats(f.Event)[0].IsTicketlandSold,"caption edit lost established source identity");
        });
        run("037 PRO complete larger source allows absent sectors but never a missing local seat",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;
            var raw=JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(Json(f.Title,["Продано","Забронировано","В продаже"]))!;
            raw["expectedCount"]=4;
            raw["places"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject{["id"]="p999",["sector"]="Балкон №1 левая сторона",["row"]="1",["seat"]="1",["status"]="Не использовать",["fund"]="1003",["price"]="0"});
            Apply(s,f.Event,raw.ToJsonString(),at);Check(s.Seats(f.Event).Count==3&&s.Seats(f.Event)[0].IsTicketlandSold,"larger full source cannot map a smaller local hall");
            raw["places"]!.AsArray().RemoveAt(1);raw["expectedCount"]=3;
            Reject(()=>Apply(s,f.Event,raw.ToJsonString(),at.AddSeconds(1)));Check(s.Seats(f.Event)[0].IsTicketlandSold&&!s.Seats(f.Event)[1].Available,"missing local seat replaced complete snapshot");
        });
        if(Environment.GetEnvironmentVariable("TICKETS_PRO_LOCAL_GEOMETRY") is string geometry&&Environment.GetEnvironmentVariable("TICKETS_PRO_LIVE_SCHEME") is string scheme)
        run("037 PRO live 1010 source maps all 989 actual local seats",s=>{
            var rows=JsonSerializer.Deserialize<string[][]>(File.ReadAllText(geometry))!;
            var v=s.CreateHall("Live PRO geometry "+Guid.NewGuid(),"",rows.Select(r=>new DraftSeat{Sector=r[5],Row=r[6],Number=r[7],X=double.Parse(r[8],System.Globalization.CultureInfo.InvariantCulture),Y=double.Parse(r[9],System.Globalization.CultureInfo.InvariantCulture)}).ToArray());
            var seats=s.VenueSeats(v);var plan=s.SavePlan(v,"Live geometry prices",seats.Select(x=>new PriceValue(x.Id,5000,"#FF0000")).ToArray());
            var source=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(scheme))!.AsObject();var title="Live PRO test "+Guid.NewGuid();source["title"]=title;
            var id=s.CreateEvent(s.SavePerformance(title),v,plan,new DateTime(2026,11,1,19,0,0));
            Apply(s,id,source.ToJsonString(),DateTimeOffset.UtcNow);var mapped=s.Seats(id);
            Check(mapped.Count==989&&mapped.Count(x=>x.IsTicketlandSold)==198&&mapped.Count(x=>x.IsTicketlandOccupied)==445&&mapped.Count(x=>x.Available)==346,"live full geometry/state counts disagree");
            var invitation=mapped.Single(x=>x.Sector.Equals("Партер",StringComparison.OrdinalIgnoreCase)&&x.Row=="5"&&x.Number=="20");Check(invitation.IsTicketlandOccupied&&!invitation.IsTicketlandSold,"actual invitation row5 seat20 classified as sale");
        });
        run("037 PRO local release is atomic and keeps existing local blocks",s=>{
            var f=Fixture(s);var at=DateTimeOffset.UtcNow;s.SetSeatsBlocked(f.Event,[f.Seats[0]],true);Apply(s,f.Event,Json(f.Title,["Забронировано","Продано","В продаже"]),at);
            Reject(()=>Release(s,f.Event,[f.Seats[0],f.Seats[1]]));Check(!s.Seats(f.Event)[0].Available,"partial release committed");
            Release(s,f.Event,[f.Seats[0]]);Check(s.Seats(f.Event)[0].IsBlocked&&!s.Seats(f.Event)[0].Available,"release erased local block");
            s.SetSeatsBlocked(f.Event,[f.Seats[0]],false);Check(s.Seats(f.Event)[0].Available,"local block cannot be cleared independently");
        });
    }
}
