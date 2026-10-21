using Tickets.Core;
internal static class TicketBlankChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("print blanks persist independently, update and validate millimetre geometry",s=>{
            var first=new TicketBlank{Name="Первый"};var second=new TicketBlank{Name="Второй"};
            s.SaveTicketBlank(first);s.SaveTicketBlank(second);Check(s.TicketBlanks().Count==2,"two blanks not saved");
            first.Fields[0].Enabled=false;s.SaveTicketBlank(first);
            Check(!s.TicketBlanks().Single(x=>x.Id==first.Id).Fields[0].Enabled,"disabled field did not persist");
            var disabled=first with{Fields=first.Fields.Select(f=>f with{Enabled=false}).ToList()};Reject(()=>s.SaveTicketBlank(disabled));
            first.Fields[0].X=500;s.SaveTicketBlank(first); // Disabled fields may lie outside resized ticket positions.
            first.Fields[0].X=5;
            first.Name="Изменённый";s.SaveTicketBlank(first);Check(s.TicketBlanks().Count==2&&s.TicketBlanks().Single(x=>x.Id==first.Id).Name==first.Name,"update duplicated blank");
            var reopened=new Store(s.Path);Check(reopened.TicketBlanks().Count==2,"blanks not persisted");
            Reject(()=>s.SaveTicketBlank(first with{PageWidth=double.NaN}));Reject(()=>s.SaveTicketBlank(first with{Positions=[]}));
            Reject(()=>s.SaveTicketBlank(first with{Positions=[new(){X=200}]}));Reject(()=>s.SaveTicketBlank(first with{Fields=[new(){Key="event"}]}));
            Reject(()=>s.SaveTicketBlank(first with{Positions=[new(),new()]}));
            Reject(()=>s.SaveTicketBlank(first with{ScanPath="../private.png"}));Reject(()=>s.TicketBlankScanPath("PrintBlanks/../private.png"));Reject(()=>s.TicketBlankScanPath("/tmp/private.png"));
            first.Fields[0].FontSize=double.PositiveInfinity;Reject(()=>s.SaveTicketBlank(first));
            s.DeleteTicketBlank(second.Id);Check(s.TicketBlanks().Count==1,"delete failed");
        });
        test("blank scans are copied into portable data paths",s=>{
            var path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!,"input.png");
            System.IO.File.WriteAllBytes(path,new byte[]{137,80,78,71,13,10,26,10});
            var relative=s.ImportTicketBlankScan(path);System.IO.File.Delete(path);
            Check(relative.StartsWith("PrintBlanks/")&&!System.IO.Path.IsPathRooted(relative)&&System.IO.File.Exists(s.TicketBlankScanPath(relative)),"scan not copied to stable relative location");
            var blank=new TicketBlank{ScanPath=relative};s.SaveTicketBlank(blank);Check(!s.TicketBlanks().Single().PrintBackground,"scan should not print by default");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("invalid blank accepted");}
}
