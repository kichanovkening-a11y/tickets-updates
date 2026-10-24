using System.Net;
using System.Reflection;
using System.Text;
using Tickets.Core;

internal static class TicketlandAllChannels029Checks
{
    internal const string Metadata="""
    {"value":[{"Name":"BD","ParameterType":"DateTime","Prompt":"Данные с"},{"Name":"ED","ParameterType":"DateTime","Prompt":"По"},{"Name":"Period","ParameterType":"Integer","Prompt":"Отбор данных","ValidValues":[{"Label":"Период мероприятий","Value":"1"}]},{"Name":"Nominal","ParameterType":"Boolean","Prompt":"Стоимость билета","ValidValues":[{"Label":"По фактической цене","Value":"false"}]},{"Name":"Filter","ParameterType":"Integer","MultiValue":true,"Prompt":"Агент","ValidValues":[{"Label":"Агенты (МДТЗК)","Value":"1"},{"Label":"субагенты","Value":"2"},{"Label":"автоматизированные","Value":"3"},{"Label":"продажи поставщика","Value":"4"}]},{"Name":"IdSubAgent","ParameterType":"Integer","Prompt":"Субагент","ValidValues":[{"Label":"<Все субагенты>","Value":"0"}]},{"Name":"filterShow","ParameterType":"String","Prompt":"Выбрать зрелище"},{"Name":"IdShow","ParameterType":"String","Prompt":"Зрелище","ValidValues":[{"Label":"Все зрелища","Value":"0"}]}]}
    """;
    internal static string Report(string? groups=null,int sold=226,int returned=4)=>$"<Report Name='(Sub1T) Актуальные продажи по распространителям и МДТЗК с субагентами' ReportTitle3='АКТУАЛЬНЫЕ ПРОДАЖИ ПО СУБАГЕНТАМ По операциям с 24.10.2026 0:00:00 по 28.10.2026 23:59:59 за период мероприятий Стоимость билета считать как: По фактической цене Субагент: &lt;Все субагенты&gt; Зрелище: Все зрелища'><Tablix1 ETicketCnt4='{sold}' RetAnotherCnt4='{returned}'><ShowName_Collection>{groups??Groups()}</ShowName_Collection></Tablix1></Report>";
    internal static string Groups()
    {
        var offset=0;var b=new StringBuilder();foreach(var (channel,sales,returns) in new[]{("МТС Лайв",67,0),("extAPI ИНТИКЕТС",90,4),("extAPI Тикет Бэст",30,0),("extAPI КАССИР.РУ",13,0),("extAPI Афиша",8,0),("Поставщик",18,0)})
        {
            b.Append($"<ShowName ShowName='Дубровский' PerformanceTime='2026-10-27T19:00:00' HallBuildingName='Театр Русская Песня' PerformanceBuildingName='ВИП-театр' AgentName='{channel}' ETicketCnt3='{sales}' RetAnotherCnt3='{returns}' ETicketCnt6='{sales-returns}'><SectionName_Collection><SectionName><Details_Collection>");
            string Row(int i,string op)=>$"<Details Barcode2='{100000+offset+i}' SectionName4='Партер' PlaceRow2='{1+(offset+i)/30}' PlacePlace2='{1+(offset+i)%30}' NonPlaceSumm2='500.0000' Oper1='{op}'/>";
            for(var i=0;i<sales;i++)b.Append(Row(i,"продажа"));for(var i=0;i<returns;i++)b.Append(Row(i,"возврат"));offset+=sales;
            b.Append("</Details_Collection></SectionName></SectionName_Collection></ShowName>");
        }return b.ToString();
    }
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("029 all-channel snapshot proves 67 web plus 155 other confirmed seats",_=>{var rows=Parse(Report());Check(rows.Count==222,"all-channel report must contain 222 active seats");Check(rows.Select(x=>(x.Sector,x.Row,x.Seat)).Distinct().Count()==222,"duplicate active seats");Check(rows.Count(x=>x.SourceChannel=="МТС Лайв")==67&&rows.Count(x=>x.SourceChannel!="МТС Лайв")==155,"missing distributor channels");Check(rows.All(x=>x.Venue=="Театр Русская Песня"&&x.Nominal==500),"lost venue or actual amount");});
        run("029 all-channel snapshot accepts empty and cross-year event periods",_=>{Check(Parse(Report("",0,0)).Count==0,"empty rejected");var g=Groups();Check(Parse(Report(g.Replace("2026-10-27T19:00:00","2027-01-03T19:00:00"),226,4).Replace("28.10.2026","04.01.2027"),new(2026,10,24),new(2027,1,4)).Count==222,"cross-year event snapshot needs no operation history");});
        run("029 all-channel snapshot rejects malformed incomplete duplicate and orphan returns",_=>{
            var good=Report();foreach(var bad in new[]{good[..^9],good.Replace("ETicketCnt4='226'","ETicketCnt4='225'"),good.Replace("ETicketCnt6='86'","ETicketCnt6='85'"),good.Replace("PlaceRow2='1'","PlaceRow2=''"),good.Replace("Oper1='продажа'","Oper1='бронь'"),good.Replace("Barcode2='100001'","Barcode2='100000'"),good.Replace("Oper1='возврат'","Oper1='сбой'"),good.Replace("PlacePlace2='2'","PlacePlace2='1'"),System.Text.RegularExpressions.Regex.Replace(good,@"Barcode2='100067'(?=[^>]*Oper1='возврат')","Barcode2='999999'"),good.Replace("за период мероприятий","за период операций"),good.Replace("28.10.2026","29.10.2026"),good.Replace("PerformanceTime='2026-10-27T19:00:00'","PerformanceTime='2025-10-27T19:00:00'"),"<!DOCTYPE Report [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>"+good})Reject(()=>Parse(bad));
        });
        run("029 all-channel snapshot preserves distinct unreserved gift tickets",_=>{var xml=System.Text.RegularExpressions.Regex.Replace(Report(),"PlaceRow2='[^']*' PlacePlace2='[^']*'","PlaceRow2='--' PlacePlace2='--'");Check(Parse(xml).Count==222,"unreserved tickets incorrectly share a numbered seat identity");});
        run("029 new unknown agent category cannot replace a complete snapshot",_=>{
            var metadata=Metadata.Replace("{\"Label\":\"продажи поставщика\",\"Value\":\"4\"}","{\"Label\":\"продажи поставщика\",\"Value\":\"4\"},{\"Label\":\"новый канал\",\"Value\":\"5\"}");
            var calls=0;using var client=new TicketlandClient028(new Handler(req=>++calls==1?"<html>reports</html>":metadata));
            Reject(()=>client.FetchCurrentAsync("u","p",new(2026,10,24),new(2026,10,28)).GetAwaiter().GetResult());Check(calls==2,"unknown category allowed authoritative download");
        });
        run("029 current download explicitly selects all channels and future event period",_=>{var n=0;using var client=new TicketlandClient028(new Handler(req=>{n++;var u=Uri.UnescapeDataString(req.RequestUri!.ToString());if(n==1)return "<html>reports</html>";if(n==2){Check(u.Contains("(Sub1T)"),"must authenticate all-channel source");return Metadata;}Check(u.Contains("(Sub1T)")&&u.Contains("Period=1")&&u.Contains("Nominal=false")&&u.Contains("IdSubAgent=0")&&u.Contains("IdShow=0"),"missing current all-channel selection");foreach(var id in new[]{1,2,3,4})Check(u.Contains("Filter="+id),"excluded agent category");Check(!u.Contains("2000")&&u.Contains("BD=2026-10-24T00:00:00")&&u.Contains("ED=2026-10-28T23:59:59"),"download scanned operation history");return Report();}));Check(client.FetchCurrentAsync("u","p",new(2026,10,24),new(2026,10,28)).GetAwaiter().GetResult().Sales.Count==222&&n==3,"wrong current download result");});
    }
    private static IReadOnlyList<TicketlandSale028> Parse(string xml,DateTime? from=null,DateTime? to=null)
    {
        var type=typeof(Store).Assembly.GetType("Tickets.Core.TicketlandAllChannels029");Check(type is not null,"all-channel parser missing");try{return (IReadOnlyList<TicketlandSale028>)type!.GetMethod("Parse",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[xml,from??new DateTime(2026,10,24),to??new DateTime(2026,10,28)])!;}catch(TargetInvocationException e)when(e.InnerException is not null){throw e.InnerException;}
    }
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("incomplete snapshot accepted");}
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private sealed class Handler(Func<HttpRequestMessage,string> respond):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(respond(request),Encoding.UTF8)});}
}
