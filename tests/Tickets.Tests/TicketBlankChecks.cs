using Tickets.Core;
internal static class TicketBlankChecks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("048 print blanks preserve originals and require unused names",s=>{
            var first=new TicketBlank{Name="Первый"};var second=new TicketBlank{Name="Второй"};
            s.SaveTicketBlank(first);s.SaveTicketBlank(second);Check(s.TicketBlanks().Count(b=>b.Legacy is null)==2,"two blanks not saved");
            var original=System.Text.Json.JsonSerializer.Serialize(s.TicketBlanks().Single(x=>x.Id==first.Id));
            first.Fields[0].Enabled=false;
            Reject(()=>s.SaveTicketBlank(first));
            first.Name="Изменённый";Reject(()=>s.SaveTicketBlank(first));
            Check(System.Text.Json.JsonSerializer.Serialize(s.TicketBlanks().Single(x=>x.Id==first.Id))==original,"saved original was overwritten");
            var copy=TicketBlankEditing.Clone(first);copy.Name="  Новый бланк  ";s.SaveTicketBlank(copy);
            Check(s.TicketBlanks().Single(x=>x.Id==copy.Id).Name=="Новый бланк","name not trimmed");
            Check(!s.TicketBlanks().Single(x=>x.Id==copy.Id).Fields[0].Enabled,"copy lost edits");
            Reject(()=>s.SaveTicketBlank(new TicketBlank{Name="  ПЕРВЫЙ  "}));
            Reject(()=>s.SaveTicketBlank(new TicketBlank{Name="новый БЛАНК"}));
            Reject(()=>s.SaveTicketBlank(new TicketBlank{Name="   "}));
            var reopened=new Store(s.Path);Check(reopened.TicketBlanks().Count(b=>b.Legacy is null)==3,"blanks not persisted");
            Check(System.Text.Json.JsonSerializer.Serialize(reopened.TicketBlanks().Single(x=>x.Id==first.Id))==original,"original changed after reopening");
            s.DeleteTicketBlank(copy.Id);
            Reject(()=>s.SaveTicketBlank(first with{PageWidth=double.NaN}));Reject(()=>s.SaveTicketBlank(first with{Positions=[]}));
            Reject(()=>s.SaveTicketBlank(first with{Positions=[new(){X=200}]}));Reject(()=>s.SaveTicketBlank(first with{Fields=[new(){Key="event"}]}));
            Reject(()=>s.SaveTicketBlank(first with{Positions=[new(),new()]}));
            Reject(()=>s.SaveTicketBlank(first with{ScanPath="../private.png"}));Reject(()=>s.TicketBlankScanPath("PrintBlanks/../private.png"));Reject(()=>s.TicketBlankScanPath("/tmp/private.png"));
            first.Fields[0].FontSize=double.PositiveInfinity;Reject(()=>s.SaveTicketBlank(first));
            s.DeleteTicketBlank(second.Id);Check(s.TicketBlanks().Count(b=>b.Legacy is null)==1,"delete failed");
        });
        test("blank scans are copied into portable data paths",s=>{
            var path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!,"input.png");
            System.IO.File.WriteAllBytes(path,new byte[]{137,80,78,71,13,10,26,10});
            var relative=s.ImportTicketBlankScan(path);System.IO.File.Delete(path);
            Check(relative.StartsWith("PrintBlanks/")&&!System.IO.Path.IsPathRooted(relative)&&System.IO.File.Exists(s.TicketBlankScanPath(relative)),"scan not copied to stable relative location");
            var blank=new TicketBlank{ScanPath=relative};s.SaveTicketBlank(blank);Check(!s.TicketBlanks().Single(b=>b.Id==blank.Id).PrintBackground,"scan should not print by default");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("invalid blank accepted");}
}
