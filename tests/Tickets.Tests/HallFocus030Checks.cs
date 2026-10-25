using Tickets.Core;
internal static class HallFocus030Checks
{
    internal static void Register(Action<string,Action<Store>> run)=>run("030 status focus follows visible seat states without selecting seats",_=>CheckFocus());
    internal static void CheckFocus()
    {
        var type=typeof(SeatInfo).Assembly.GetType("Tickets.Core.HallFocus030");
        if(type is null)throw new Exception("Status focus predicate is missing");
        var statusType=typeof(SeatInfo).Assembly.GetType("Tickets.Core.HallStatus030")!;
        var method=type.GetMethod("Matches")!;
        var free=new SeatInfo(1,"p","1","1",0,0,500,"#123456",null,"","",null);
        var samples=new[]{free with{TicketId=2},free with{TicketId=2,IsReserved=true,IsPrinted=true},free with{TicketId=2,IsPrinted=true},free with{IsBlocked=true},free with{Amount=null},free with{IsTicketlandSold=true}};
        var expected=new[]{"Reserved","Reserved","Printed","Blocked","NoPrice","TicketlandSold"};
        foreach(var status in Enum.GetNames(statusType))
        for(var i=0;i<samples.Length;i++)
        {
            var result=(bool)method.Invoke(null,new[]{samples[i],Enum.Parse(statusType,status)})!;
            if(result!=(expected[i]==status))throw new Exception($"{status} focus incorrectly classified {expected[i]}");
        }
        foreach(var status in Enum.GetNames(statusType))
        if((bool)method.Invoke(null,new[]{free,Enum.Parse(statusType,status)})!)throw new Exception("Status focus included a free priced seat");
    }
}
