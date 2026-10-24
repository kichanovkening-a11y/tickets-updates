using System.Net;
using System.Text;
using System.Reflection;
using Tickets.Core;

internal static class TicketlandCurrent0281Checks
{
    internal const string Metadata="{\"value\":[{\"Name\":\"BD\",\"ParameterType\":\"DateTime\",\"Prompt\":\"С\"},{\"Name\":\"ED\",\"ParameterType\":\"DateTime\",\"Prompt\":\"По\"},{\"Name\":\"Period\",\"ParameterType\":\"Integer\",\"Prompt\":\"Период\",\"ValidValues\":[{\"Label\":\"Период операций\",\"Value\":\"0\"},{\"Label\":\"Период мероприятий\",\"Value\":\"1\"}]}]}";
    private const string Detail="<Подробности SectionName='123456789' Barcode='Партер' PlaceRow='1' PlacePlace='2' Price1='500.0000' Oper1='продажа' TaxName='Без НДС'/>";
    private static string Report(string? rows=null,int sales=1,int returns=0)=>$"<Report Name='(C4at) По вознаграждению со списком билетов' ReportTitle3='По вознаграждению со списком билетов за период мероприятий c 24.10.2026 0:00:00 по 28.10.2026 23:59:59'><Tablix1 SalesSumm4='{sales}' SalesSumm6='{returns}' Oper='{sales-returns}'><QuotaOwnerName_Collection><QuotaOwnerName><idPerformance_Collection><idPerformance PerfName='Дубровский 27.10.2026 19:00:00' SaleChannelName='МДТЗК' Price='{sales}' Price2='{returns}' RetSum='{sales-returns}'><Подробности_Collection>{rows??Detail}</Подробности_Collection></idPerformance></idPerformance_Collection></QuotaOwnerName></QuotaOwnerName_Collection></Tablix1></Report>";
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("0281 current Ticketland report uses event dates and actual XML fields",_=>{
            using var c=Session(Metadata,Report());var r=Fetch(c);var s=r.Sales.Single();
            Check(s.Barcode=="123456789"&&s.Sector=="Партер"&&s.Row=="1"&&s.Seat=="2"&&s.Nominal==500&&s.Title=="Дубровский","swapped field identities");
        });
        run("0281 current Ticketland submits observed forms fields and follows secure login redirect",_=>{
            var step=0;
            using var c=new TicketlandClient028(new Handler(async(req,ct)=>{
                step++;
                Check(req.RequestUri!.Host=="reports.ticketland.ru"&&req.RequestUri.Scheme=="https","login escaped origin");
                if(step==1)return Html("<html><form action='/ReportServer/logon.aspx'><input type='hidden' name='__VIEWSTATE' value='a&amp;b'><input name='TxtUser'><input type='password' name='TxtPwd'><input type='submit' name='BtnLogon' value='Вход'></form></html>");
                if(step==2){var body=await req.Content!.ReadAsStringAsync(ct);Check(req.Method==HttpMethod.Post&&body.Contains("__VIEWSTATE=a%26b")&&body.Contains("TxtUser=secret-user")&&body.Contains("TxtPwd=secret-password")&&body.Contains("BtnLogon="),"lost login fields");var response=new HttpResponseMessage(HttpStatusCode.Found);response.Headers.Location=new Uri("/reports/browse/",UriKind.Relative);return response;}
                if(step==3){Check(req.Method==HttpMethod.Get,"reposted credentials");return Html("<html>reports</html>");}
                if(step==4)return Html(Metadata);
                Check(step==5,"unexpected login/report request");return Html(Report());
            }));Check(Fetch(c).Sales.Count==1&&step==5,"login did not reach current report");
        });
        run("0281 current Ticketland removes returned tickets including partial refunds",_=>{
            var rows=Detail+Detail.Replace("123456789","222222222").Replace("PlacePlace='2'","PlacePlace='3'")+Detail.Replace("Oper1='продажа'","Oper1='возврат'").Replace("500.0000","250.0000");
            using var c=Session(Metadata,Report(rows,2,1));Check(Fetch(c).Sales.Single().Barcode=="222222222","returned ticket still sold");
        });
        run("0281 current Ticketland accepts complete empty and all-returned snapshots",_=>{
            using var empty=Session(Metadata,Report("",0));Check(Fetch(empty).Sales.Count==0,"empty rejected");
            using var returned=Session(Metadata,Report(Detail+Detail.Replace("Oper1='продажа'","Oper1='возврат'"),1,1));Check(Fetch(returned).Sales.Count==0,"returned rejected");
        });
        run("0281 current Ticketland rejects incomplete incompatible or historical XML",_=>{
            foreach(var xml in new[]{Report().Replace("</Report>",""),"<html>error</html>",Report(sales:2),Report().Replace("RetSum='1'","RetSum='0'"),Report().Replace("Oper='1'","Oper='0'"),Report().Replace("PlaceRow='1'",""),Report().Replace("Oper1='продажа'","Oper1='бронь'"),Report().Replace("SectionName='123456789'","SectionName='not-a-barcode'"),Report().Replace("24.10.2026", "01.01.2000"),Report().Replace("период мероприятий","период операций"),Report(Detail+Detail,2),Report(Detail.Replace("Oper1='продажа'","Oper1='возврат'"),0,1),Report().Replace("27.10.2026","27.10.2025"),"<!DOCTYPE Report [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>"+Report().Replace("Партер","&x;")}){using var c=Session(Metadata,xml);Reject(c);}
        });
        run("0281 current Ticketland validates event period metadata",_=>{
            foreach(var m in new[]{Metadata.Replace("Период мероприятий","Неизвестно"),Metadata.Replace("\"Value\":\"1\"","\"Value\":\"0\""),Metadata.Replace("\"Prompt\":\"По\"","\"Prompt\":\"С\""),Metadata.Replace("DateTime","String")}){using var c=Session(m,Report());Reject(c);}
        });
        run("0281 current Ticketland identifies login and report timeout stages",_=>{
            using var login=new TicketlandClient028(new Handler((req,ct)=>throw new TaskCanceledException("secret-password")));
            Check(Reject(login).Contains("вход",StringComparison.OrdinalIgnoreCase),"login timeout says report");
            var calls=0;using var report=new TicketlandClient028(new Handler((req,ct)=>{calls++;if(calls==1)return Task.FromResult(Html("<html>reports</html>"));if(calls==2)return Task.FromResult(Html(Metadata));throw new TaskCanceledException("secret-user");}));
            Check(Reject(report).Contains("отчёт",StringComparison.OrdinalIgnoreCase),"lost report stage");
        });
        run("0281 current Ticketland identifies HTTP failure stage",_=>{
            using var login=new TicketlandClient028(new Handler((r,c)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
            Check(Reject(login).Contains("вход",StringComparison.OrdinalIgnoreCase),"HTTP login error misreported as report failure");
            var n=0;using var report=new TicketlandClient028(new Handler((r,c)=>Task.FromResult(++n switch{1=>Html("<html>reports</html>"),2=>Html(Metadata),_=>new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)})));
            Check(Reject(report).StartsWith("Вход в Ticketland выполнен.",StringComparison.Ordinal),"HTTP report error lost successful login stage");
        });
        run("0281 current Ticketland picks only active current local dates",store=>{
            var method=typeof(Store).GetMethod("TicketlandPeriod0281");Check(method is not null,"current showing period selection missing");
            var today=new DateTime(2099,1,1);Check(method!.Invoke(store,[today]) is null,"past seed causes historic fetch");
            var venue=store.CreateHall("0281 period","",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);var plan=store.SavePlan(venue,"price",[new(store.VenueSeats(venue).Single().Id,500,"#FF0000")]);
            var title=store.SavePerformance("0281 period");store.CreateEvent(title,venue,plan,today.AddDays(20));
            var result=method.Invoke(store,[today]);Check(result is ValueTuple<DateTime,DateTime> dates&&dates.Item1==today&&dates.Item2==today.AddDays(20),"incorrect current range");store.DeleteVenue(venue);Check(method.Invoke(store,[today]) is null,"deleted hall creates report range");
        });
        run("0281 renaming existing hall does not silently create a copy",store=>{
            var a=store.CreateHall("0281 old deleted name","",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);
            var b=store.CreateHall("0281 main hall","",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);store.DeleteVenue(a);
            var before=store.Venues().Count;var draft=store.HallDraft(b);
            var saved=store.SaveEditedHall(b,"0281 renamed hall","new address",draft);
            Check(saved==b&&store.Venues().Count==before,"renaming hall silently created another visible hall");
            Check(!store.Venues().Any(v=>v.Id==a)&&store.Venues().Single(v=>v.Id==b).Name=="0281 renamed hall","deleted hall reappeared or title not updated");
        });
        run("0281 delete hall then edit another preserves deletion",store=>{
            var a=store.CreateHall("0281 deleted","",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);var b=store.CreateHall("0281 active","",[new(){Sector="Партер",Row="1",Number="1",X=0,Y=0}]);store.DeleteVenue(a);
            var draft=store.HallDraft(b);draft[0].Sector="Бельэтаж";store.SaveEditedHall(b,"0281 active","",draft);Check(!store.Venues().Any(v=>v.Id==a),"deleted hall resurrected after sector edit");
            draft[0].X+=30;store.SaveEditedHall(b,"0281 new layout","",draft);Check(!new Store(store.Path).Venues().Any(v=>v.Id==a),"deleted hall resurrected after geometry save");
        });
    }
    private static async Task<TicketlandReport028> Current(TicketlandClient028 c){var m=typeof(TicketlandClient028).GetMethod("FetchCurrentAsync");Check(m is not null,"current event-period report download missing");try{return await (Task<TicketlandReport028>)m!.Invoke(c,["secret-user","secret-password",new DateTime(2026,10,24),new DateTime(2026,10,28),CancellationToken.None])!;}catch(TargetInvocationException e) when(e.InnerException is not null){throw e.InnerException;}}
    private static TicketlandReport028 Fetch(TicketlandClient028 c)=>Current(c).GetAwaiter().GetResult();
    private static string Reject(TicketlandClient028 c){try{Fetch(c);}catch(TicketException e){Check(!e.ToString().Contains("secret-user")&&!e.ToString().Contains("secret-password"),"leaked credentials");return e.Message;}throw new Exception("expected TicketException");}
    private static TicketlandClient028 Session(string metadata,string report){var n=0;return new(new Handler((req,ct)=>{n++;if(n==1)return Task.FromResult(Html("<html>reports</html>"));if(n==2){Check(Uri.UnescapeDataString(req.RequestUri!.AbsolutePath).Contains("(C4at)"),"wrong report");return Task.FromResult(Html(metadata));}var q=Uri.UnescapeDataString(req.RequestUri!.Query);Check(q.Contains("BD=2026-10-24T00:00:00")&&q.Contains("ED=2026-10-28T23:59:59")&&q.Contains("Period=1")&&q.Contains("rs:Format=XML"),"not event-period XML");return Task.FromResult(Html(report));}));}
    private static HttpResponseMessage Html(string text)=>new(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"text/html")};
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>send(r,c);}
}
