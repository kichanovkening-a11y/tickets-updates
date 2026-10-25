using Tickets.Core;
using System.Text.Json.Nodes;
internal static class Transfer033Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("033 hall transfer detects content and preserves geometry metadata",s=>{
            var cap=new SectorCaption029("Балкон\nправый",3,4,90);
            var draft=new[]{new DraftSeat{Sector="Балкон правый",Row="А",Number="Б",X=30,Y=40,SectorCaption=cap,RowAisleAnchors=[new(90,48,true)]},new DraftSeat{Sector="Балкон правый",Row="А",Number="1",X=60,Y=40,Hidden=true,SectorCaption=cap,RowAisleAnchors=[new(90,48,true)]}};
            var id=s.CreateHall("Проверка переноса 033","Адрес",draft);
            var bytes=s.ExportTransfer033("hall",id.ToString());var p=TransferPackage033.Read(bytes);
            Check(p.Kind=="hall"&&p.Name=="Проверка переноса 033","wrong identification");
            var before=s.Events().Count;var imported=s.ImportTransfer033(bytes,"Проверка переноса 033 — копия");
            Check(s.Events().Count==before,"import altered events");
            var read=s.HallDraft(long.Parse(imported.Id));Check(read.Count==2&&read[1].Hidden&&read[0].SectorCaption==cap&&read[0].RowAisleAnchors!.Single().X==90,"metadata lost");
            Check(read[0].Number=="Б"&&read[0].X==30&&read[0].Y==40,"places altered");
        });
        run("033 invalid unknown and future packages never modify catalog",s=>{
            var before=s.Venues().Count;
            foreach(var text in new[]{"{}","not json","{\"Format\":\"Tickets.Exchange\",\"Version\":999,\"Kind\":\"hall\",\"Name\":\"x\"}"})Reject(()=>s.ImportTransfer033(System.Text.Encoding.UTF8.GetBytes(text)));
            var id=s.CreateHall("033 source","",[new DraftSeat()]);var node=JsonNode.Parse(s.ExportTransfer033("hall",id.ToString()))!;
            node["Hall"]!["Seats"]![0]!["X"]=1000001;
            Reject(()=>s.ImportTransfer033(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()),"bad geometry"));
            Check(s.Venues().Count==before+1,"invalid package modified database");
        });
        run("033 duplicate requires explicit new name and performance imports",s=>{
            var id=s.CreateHall("033 duplicate","",[new DraftSeat()]);var bytes=s.ExportTransfer033("hall",id.ToString());
            Reject(()=>s.ImportTransfer033(bytes));Check(s.HallDraft(id).Count==1,"existing hall changed");
            var performance=s.SavePerformance("033 transferable show");var pack=s.ExportTransfer033("performance",performance.ToString());
            var result=s.ImportTransfer033(pack,"033 transferred show copy");Check(s.Performances().Any(p=>p.Id==long.Parse(result.Id)&&p.Title=="033 transferred show copy"),"show not imported");
        });
        run("033 pricing package remaps seat identities and keeps source",s=>{
            var hall=s.CreateHall("033 pricing hall","",[new DraftSeat{Number="1"},new DraftSeat{Number="2",X=20}]);var seats=s.VenueSeats(hall);
            var plan=s.SavePlan(hall,"033 pricing",[new(seats[0].Id,1200,"#123456"),new(seats[1].Id,1200,"#123456")]);
            var result=s.ImportTransfer033(s.ExportTransfer033("pricing",plan.ToString()),"033 pricing copy");var prices=s.PlanPrices(long.Parse(result.Id));
            Check(prices.Count==2&&prices.All(p=>p.Amount==1200)&&prices.All(p=>seats.All(old=>old.Id!=p.SeatId)),"prices attached to wrong hall");Check(s.PlanPrices(plan).Count==2,"source plan changed");
        });
        run("033 missing identifiers and missing seat fields are rejected",s=>{
            foreach(var text in new[]{"{\"Kind\":\"performance\",\"Name\":\"Unexpected\"}","{\"Format\":\"Tickets.Exchange\",\"Version\":1,\"Kind\":\"hall\",\"Name\":\"Incomplete\",\"Hall\":{\"Name\":\"Incomplete\",\"Address\":\"\",\"Seats\":[{}]}}"})Reject(()=>s.ImportTransfer033(System.Text.Encoding.UTF8.GetBytes(text)));
        });
        run("033 imported prices preserve existing palette and resolve foreign collisions",s=>{
            var hall=s.CreateHall("033 palette hall","",[new DraftSeat()]);var seat=s.VenueSeats(hall).Single();var plan=s.SavePlan(hall,"033 palette source",[new(seat.Id,1234567,"#123456")]);
            var node=JsonNode.Parse(s.ExportTransfer033("pricing",plan.ToString()))!;node["Prices"]![0]!["Color"]="#654321";
            s.ImportTransfer033(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()),"033 palette copy");Check(s.PlanPrices(plan).Single().Color=="#123456","existing global color changed");
            node["Prices"]![0]!["Color"]="#123456";node["Prices"]![0]!["Amount"]=2345678;
            var result=s.ImportTransfer033(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()),"033 palette different amount");
            Check(s.PlanPrices(long.Parse(result.Id)).Single().Color!="#123456"&&s.PlanPrices(plan).Single().Color=="#123456","foreign color conflict not resolved");
        });
        run("033 standalone blank transfer assigns new identity",s=>{
            var b=s.TicketBlanks().First(x=>string.IsNullOrEmpty(x.ScanPath));var result=s.ImportTransfer033(s.ExportTransfer033("blank",b.Id),"033 blank copy");
            Check(result.Id!=b.Id&&s.TicketBlanks().Any(x=>x.Id==result.Id&&x.Name=="033 blank copy"),"blank overwritten");
        });
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected rejection");}
}
