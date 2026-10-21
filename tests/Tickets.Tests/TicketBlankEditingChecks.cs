using System.Text.Json;
using Tickets.Core;

internal static class TicketBlankEditingChecks
{
    public static void Run()
    {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected invalid blank geometry to fail");}
        var stock=TicketBlankEditing.CreateA4();stock.Validate();
        Check(stock.PageWidth==210&&stock.PageHeight==297&&stock.Positions.Count==4,"A4 must contain four physical ticket blocks");
        Check(stock.Positions[^1].Y+stock.Positions[^1].Height<=297,"Fourth ticket extends beyond A4");
        stock.Fields[0].X=12.25;stock.Fields[0].FontFamily="Times New Roman";stock.Fields[0].Rotation=90;
        TicketBlankEditing.CopyFirstPlacement(stock);
        var first=stock.Positions[0];var last=stock.Positions[3];var lastY=last.Y;
        Check(TicketBlankEditing.FieldsFor(stock,last)[0].X==12.25,"Copy must preserve precise relative placement");
        last.Fields![0].X=15.75;
        Check(first.Fields![0].X==12.25&&stock.Fields[0].X==12.25,"Ticket overrides must be independent deep copies");
        Check(last.Y==lastY,"Copy must preserve sheet placement");
        var persisted=JsonSerializer.Deserialize<TicketBlank>(JsonSerializer.Serialize(stock))!;persisted.Validate();
        Check(persisted.Positions[3].Fields![0].X==15.75&&persisted.Positions[3].Fields![0].Rotation==90,"Placement / font metadata must survive saved templates");
        TicketBlankEditing.MovePosition(stock,first,-20,900);
        Check(first.X==0&&first.Y==227,"Block movement must clamp to physical sheet bounds");
        var invalid=TicketBlankEditing.CreateA4();TicketBlankEditing.CopyFirstPlacement(invalid);invalid.Positions[2].Fields![0].Width=1000;Reject(invalid.Validate);
        invalid=TicketBlankEditing.CreateA4();invalid.Fields[0].Rotation=45;Reject(invalid.Validate);
        foreach(var original in LegacyTicketTemplates.All())
        {
            var before=JsonSerializer.Serialize(original);var clone=TicketBlankEditing.Clone(original);clone.Validate();
            Reject(()=>(clone with{PrintBackground=true,ScanPath=""}).Validate());
            Check(clone.Id!=original.Id&&clone.IsEditableCopy,"An imported original needs a distinct editable copy identity");
            Check(JsonSerializer.Serialize(clone.Legacy)==JsonSerializer.Serialize(original.Legacy),"Clone must retain all source sections, coordinates, typography and provenance");
            clone.Legacy!.Bands.SelectMany(b=>b.Elements).First().X+=1;
            Check(JsonSerializer.Serialize(original)==before,"Editing a clone must never mutate its imported original");
            var roundtrip=JsonSerializer.Deserialize<TicketBlank>(JsonSerializer.Serialize(clone))!;
            Check(roundtrip.IsEditableCopy&&roundtrip.Legacy!.SourceSha256==original.Legacy!.SourceSha256,"Editable original provenance must persist");
        }
    }
}
