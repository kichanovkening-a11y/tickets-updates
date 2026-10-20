using Microsoft.Data.Sqlite;
using Tickets.Core;
using Tickets.Update;
using System.IO.Compression;

// Removing transactional writes, validation, or persistent status must break these tests.
var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Tickets-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
var seed = args[0];
var failed = 0;
var total = 0;
void Test(string name, Action<Store> body)
{
    total++;
    var path = System.IO.Path.Combine(root, total + ".db");
    File.Copy(seed, path);
    try { body(new Store(path)); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Check(bool condition, string message = "assertion failed") { if (!condition) throw new Exception(message); }
void Reject(Action action)
{
    try { action(); } catch (TicketException) { return; }
    throw new Exception("Expected TicketException");
}
(long Event, long[] Seats) Choice(Store s)
{
    foreach (var e in s.Events())
    {
        var seats = s.Seats(e.Id).Where(x => x.Available).Take(3).Select(x => x.Id).ToArray();
        if (seats.Length == 3) return (e.Id, seats);
    }
    throw new Exception("seed has no event with 3 priced seats");
}
Test("real imported halls, labels and no legacy tickets", s => {
    var c = Choice(s); Check(s.Events().Count > 100); Check(s.Seats(c.Event).Count > 100);
    Check(s.Events().All(e => e.IssuedCount == 0));
    Check(s.Seats(c.Event).Any(x => x.Row != "" && x.Number != ""));
});
Test("issue persists recipient, number and price across reopen", s => {
    var c=Choice(s); var price=s.Seats(c.Event).First(x=>x.Id==c.Seats[0]).Amount;
    s.Issue(c.Event,c.Seats,"ООО «Тест»","ТЕСТ",101);
    var reopened=new Store(s.Path); var h=reopened.History(c.Event).OrderBy(x=>x.Number).ToList();
    Check(h.Count==3 && h[0].Number==101 && h[2].Number==103);
    Check(h.All(x=>x.Recipient=="ООО «Тест»")); Check(h[0].Amount==price);
    Check(reopened.Seats(c.Event).Count(x=>x.TicketId.HasValue)==3);
    Check(reopened.NextNumber("тест")==104);
});
Test("mixed duplicate issue rolls back every seat", s => {
    var c=Choice(s); s.Issue(c.Event,new[]{c.Seats[1]},"A","A",1);
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0],c.Seats[1]},"B","B",1));
    Check(s.History(c.Event).Count==1); Check(s.Seats(c.Event).First(x=>x.Id==c.Seats[0]).Available);
});
Test("series and number collision rejects whole batch", s => {
    var c=Choice(s); s.Issue(c.Event,new[]{c.Seats[0]},"A"," aa ",2);
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[1],c.Seats[2]},"B","AA",1));
    Check(s.History(c.Event).Count==1);
});
Test("return keeps audit and reissue gets new number", s => {
    var c=Choice(s); s.Issue(c.Event,new[]{c.Seats[0]},"A","A",1);
    var t=s.History(c.Event).Single(); s.Return(new[]{t.Id});
    Check(s.Seats(c.Event).First(x=>x.Id==c.Seats[0]).Available);
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0]},"B","A",1));
    s.Issue(c.Event,new[]{c.Seats[0]},"B","A",2);
    Check(s.History(c.Event).Count==2); Check(s.History(c.Event).Single(x=>x.Id==t.Id).ReturnedAt!=null);
});
Test("invalid and duplicate seat inputs do not write", s => {
    var c=Choice(s);
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0],-1L},"A","A",1));
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0],c.Seats[0]},"A","A",1));
    Reject(()=>s.Issue(c.Event,Array.Empty<long>(),"A","A",1));
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0]}," ","A",1));
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0]},"A","",1));
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0]},"A","A",0));
    Reject(()=>s.Issue(c.Event,c.Seats,"A","A",long.MaxValue));
    Check(s.History(c.Event).Count==0);
});
Test("invalid or already returned batch is atomic", s => {
    var c=Choice(s); s.Issue(c.Event,c.Seats,"A","A",1); var h=s.History(c.Event);
    Reject(()=>s.Return(new[]{h[0].Id,-1})); Check(s.History(c.Event).All(x=>x.ReturnedAt==null));
    s.Return(new[]{h[0].Id}); Reject(()=>s.Return(new[]{h[1].Id,h[0].Id}));
    Check(s.History(c.Event).Count(x=>x.ReturnedAt!=null)==1);
});
Test("same hall seats are independent across events", s => {
    var c=Choice(s); var other=s.Events().First(e=>e.Id!=c.Event && s.Seats(e.Id).Any(x=>x.Id==c.Seats[0] && x.Available));
    s.Issue(c.Event,new[]{c.Seats[0]},"A","A",1);
    Check(s.Seats(other.Id).First(x=>x.Id==c.Seats[0]).Available);
    s.Issue(other.Id,new[]{c.Seats[0]},"B","B",1); Check(s.History(other.Id).Count==1);
});
Test("seat without event price cannot be issued", s => {
    var c=Choice(s);
    using var cn=new SqliteConnection("Data Source="+s.Path); cn.Open();
    using var cmd=cn.CreateCommand(); cmd.CommandText="DELETE FROM event_price WHERE event_id=$e AND seat_id=$s";
    cmd.Parameters.AddWithValue("$e",c.Event); cmd.Parameters.AddWithValue("$s",c.Seats[0]); cmd.ExecuteNonQuery();
    Reject(()=>s.Issue(c.Event,new[]{c.Seats[0]},"A","A",1)); Check(s.History(c.Event).Count==0);
});
Test("backup contains current issued tickets", s => {
    var c=Choice(s); s.Issue(c.Event,c.Seats,"A","A",1);
    var dest=s.Path+".backup"; s.Backup(dest);
    Check(new Store(dest).History(c.Event).Count==3);
});
Test("create hall performance price plan and issue new event",s=>{
    var venue=s.CreateHall("Новый зал","Адрес",new[]{
        new DraftSeat{Sector="Партер",Row="1",Number="1",X=0,Y=26},
        new DraftSeat{Sector="Партер",Row="1",Number="2",X=18,Y=26}});
    var perf=s.SavePerformance("Новый спектакль"); var seats=s.VenueSeats(venue);
    var plan=s.SavePlan(venue,"Основная",seats.Select(x=>new PriceValue(x.Id,1500,"#FFAA22")).ToArray());
    var e=s.CreateEvent(perf,venue,plan,new DateTime(2027,3,18,19,0,0));
    Check(s.Events().Single(x=>x.Id==e).Title=="Новый спектакль");
    Check(s.Seats(e).Count==2 && s.Seats(e).All(x=>x.Amount==1500));
    s.Issue(e,new[]{seats[0].Id},"Получатель","НОВЫЙ",1); Check(s.History(e).Count==1);
    var reopened=new Store(s.Path); Check(reopened.Plans(venue).Count==1 && reopened.HallDraft(venue).Count==2);
});
Test("invalid hall duplicate seats and nonfinite coordinates rejected atomically",s=>{
    var before=s.Venues().Count;
    Reject(()=>s.CreateHall("Зал","",new[]{new DraftSeat(),new DraftSeat()}));
    Reject(()=>s.CreateHall("Зал","",new[]{new DraftSeat{X=double.NaN}}));
    Reject(()=>s.CreateHall("Зал","",Array.Empty<DraftSeat>()));
    Check(s.Venues().Count==before);
});
Test("plan and event cannot use seats of another hall",s=>{
    var a=s.CreateHall("Зал A","",new[]{new DraftSeat()});
    var b=s.CreateHall("Зал B","",new[]{new DraftSeat()});
    var seat=s.VenueSeats(a).Single();
    Reject(()=>s.SavePlan(b,"Чужие места",new[]{new PriceValue(seat.Id,1000,"#FF0000")}));
    Reject(()=>s.SavePlan(a,"Минус",new[]{new PriceValue(seat.Id,-1,"#FF0000")}));
    var plan=s.SavePlan(a,"Цены",new[]{new PriceValue(seat.Id,1000,"#FF0000")});
    var perf=s.SavePerformance("Тест"); Reject(()=>s.CreateEvent(perf,b,plan,DateTime.Now));
});
Test("new price plan version does not alter issued tickets or previous event",s=>{
    var v=s.CreateHall("Версии","",new[]{new DraftSeat()}); var seat=s.VenueSeats(v).Single();
    var perf=s.SavePerformance("Спектакль"); var p1=s.SavePlan(v,"Первая",new[]{new PriceValue(seat.Id,100,"#FF0000")});
    var e=s.CreateEvent(perf,v,p1,DateTime.Now); s.Issue(e,new[]{seat.Id},"A","A",1);
    s.SavePlan(v,"Вторая",new[]{new PriceValue(seat.Id,200,"#00FF00")});
    Check(s.Seats(e).Single().Amount==100 && s.History(e).Single().Amount==100);
    Check(s.Plans(v).Count==2);
});
Test("updater rejects traversal and protects user files",s=>{
    foreach(var path in new[]{"../Tickets.dll","Data/tickets.db","Backups/save.db","Source/x.dll","C:/x.dll","\\evil.dll","a/../../x.dll","tickets.dll.","settings.json"})
    {
        try{UpdateFiles.SafePath(root,path);throw new Exception("accepted unsafe "+path);}catch(InvalidDataException){}
    }
    Check(UpdateFiles.SafePath(root,"Tickets.dll").StartsWith(root));
});
Test("updater rejects wrong repository and manifest versions",s=>{
    var m=new UpdateManifest(1,"test/repo","0.1.3",new(){new("app","app.zip",new string('a',64),new(){new("Tickets.dll",new string('b',64))})});
    var json=System.Text.Json.JsonSerializer.Serialize(m);
    Check(UpdateFiles.Parse(json,"test/repo").Version=="0.1.3");
    try{UpdateFiles.Parse(json,"other/repo");throw new Exception("wrong repo accepted");}catch(InvalidDataException){}
    Check(UpdateFiles.VersionNumber("0.1.3")==UpdateFiles.VersionNumber("0.1.3.0"));
});
Test("updater extracts checked package and replaces only program files",s=>{
    var app=s.Path+".app";var stage=s.Path+".stage";Directory.CreateDirectory(app);Directory.CreateDirectory(stage);Directory.CreateDirectory(System.IO.Path.Combine(app,"Data"));
    File.WriteAllText(System.IO.Path.Combine(app,"Data/tickets.db"),"USER DATA");File.WriteAllText(System.IO.Path.Combine(app,"Tickets.dll"),"old");
    var payload=s.Path+".payload";File.WriteAllText(payload,"new version");var spec=new UpdateFile("Tickets.dll",UpdateFiles.Hash(payload));
    var zip=s.Path+".zip";using(var z=ZipFile.Open(zip,ZipArchiveMode.Create))z.CreateEntryFromFile(payload,"Tickets.dll");
    var package=new UpdatePackage("app","app.zip",UpdateFiles.Hash(zip),new(){spec});
    Check(UpdateFiles.Needed(app,new(1,"test/repo","0.1.3",new(){package})).Count==1);
    UpdateFiles.Extract(zip,stage,package);UpdateFiles.Apply(new(app,stage,new(){spec}));
    Check(File.ReadAllText(System.IO.Path.Combine(app,"Tickets.dll"))=="new version");
    Check(File.ReadAllText(System.IO.Path.Combine(app,"Data/tickets.db"))=="USER DATA");
    Check(UpdateFiles.Needed(app,new(1,"test/repo","0.1.3",new(){package})).Count==0);
});
Test("updater wrong hash cannot change installed application",s=>{
    var app=s.Path+".app";var stage=s.Path+".stage";Directory.CreateDirectory(app);Directory.CreateDirectory(stage);
    File.WriteAllText(System.IO.Path.Combine(app,"Tickets.dll"),"old");File.WriteAllText(System.IO.Path.Combine(stage,"Tickets.dll"),"corrupt");
    try{UpdateFiles.Apply(new(app,stage,new(){new("Tickets.dll",new string('a',64))}));throw new Exception("corrupt accepted");}catch(InvalidDataException){}
    Check(File.ReadAllText(System.IO.Path.Combine(app,"Tickets.dll"))=="old");
});
Test("updater restores previous files if replacement fails mid-operation",s=>{
    var app=s.Path+".app";var stage=s.Path+".stage";Directory.CreateDirectory(app);Directory.CreateDirectory(stage);
    File.WriteAllText(System.IO.Path.Combine(app,"Tickets.dll"),"old");Directory.CreateDirectory(System.IO.Path.Combine(app,"Tickets.Core.dll"));
    File.WriteAllText(System.IO.Path.Combine(stage,"Tickets.dll"),"new");File.WriteAllText(System.IO.Path.Combine(stage,"Tickets.Core.dll"),"core");
    var files=new List<UpdateFile>{new("Tickets.dll",UpdateFiles.Hash(System.IO.Path.Combine(stage,"Tickets.dll"))),new("Tickets.Core.dll",UpdateFiles.Hash(System.IO.Path.Combine(stage,"Tickets.Core.dll")))};
    try{UpdateFiles.Apply(new(app,stage,files));throw new Exception("failure expected");}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
    Check(File.ReadAllText(System.IO.Path.Combine(app,"Tickets.dll"))=="old");
});
Test("issuance price override persists without changing event pricing",s=>{
    var method=typeof(Store).GetMethods().SingleOrDefault(m=>m.Name=="Issue"&&m.GetParameters().Length==6);
    Check(method is not null,"price override API is missing");
    var c=Choice(s);var original=s.Seats(c.Event).First(x=>x.Id==c.Seats[0]).Amount;
    method!.Invoke(s,new object?[]{c.Event,c.Seats,"Получатель","ЦЕНА",1L,new Dictionary<long,long>{{c.Seats[0],17}}});
    Check(new Store(s.Path).History(c.Event).Single(x=>x.SeatId==c.Seats[0]).Amount==17);
    Check(s.Seats(c.Event).Single(x=>x.Id==c.Seats[0]).Amount==17,"issued seat must show actual price");
    s.Return(s.History(c.Event).Select(x=>x.Id).ToArray());
    Check(s.Seats(c.Event).First(x=>x.Id==c.Seats[0]).Amount==original,"base pricing changed");
});
Test("separate issuances and event filtering survive reopen and partial return",s=>{
    var c=Choice(s);var a=s.Issue(c.Event,c.Seats.Take(2).ToArray(),"A","A",1);
    var b=s.Issue(c.Event,new[]{c.Seats[2]},"A","A",3);
    Check(a!=b);Check(s.Issues(c.Event).Count==2);
    var other=s.Events().First(e=>e.Id!=c.Event);Check(s.Issues(other.Id).Count==0);
    Reject(()=>s.Invoice(other.Id,a));
    var invoice=s.Invoice(c.Event,a);Check(invoice.Tickets.Count==2&&invoice.Recipient=="A");
    s.Return(new[]{invoice.Tickets[0].Id});var reopened=new Store(s.Path);
    Check(reopened.Issues(c.Event).Single(i=>i.Id==a).ReturnedCount==1);
    Check(reopened.Invoice(c.Event,a).Tickets.Count==2&&reopened.Invoice(c.Event,a).ActiveCount==1);
    var count=s.Issues(c.Event).Count;Reject(()=>s.Issue(c.Event,new[]{c.Seats[1]},"B","B",1));Check(s.Issues(c.Event).Count==count);
});
Test("invalid override prices cannot create tickets or orphan issuances",s=>{
    var c=Choice(s);
    Reject(()=>s.Issue(c.Event,c.Seats,"A","A",1,new Dictionary<long,long>{{c.Seats[0],-1}}));
    Reject(()=>s.Issue(c.Event,c.Seats,"A","A",1,new Dictionary<long,long>{{-1,10}}));
    Check(s.Issues(c.Event).Count==0&&s.History(c.Event).Count==0);
});
Test("v2 migration preserves issued prices and returns and is repeatable",s=>{
    var c=Choice(s);s.Issue(c.Event,c.Seats,"A","A",1);s.Return(new[]{s.History(c.Event)[0].Id});
    using(var cn=new SqliteConnection("Data Source="+s.Path)){
        cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="DROP INDEX ticket_issue; ALTER TABLE ticket DROP COLUMN issue_id; DROP TABLE issue_batch; PRAGMA user_version=2;";cmd.ExecuteNonQuery();
    }
    var reopened=new Store(s.Path);Check(reopened.Issues(c.Event).Count==1);var i=reopened.Issues(c.Event)[0];
    Check(i.Count==3&&i.ReturnedCount==1);Check(File.Exists(s.Path+".before-v3.db"));Check(new Store(s.Path).Issues(c.Event).Count==1);
});
Test("Word invoice contains exact issuance tickets totals and escaped text",s=>{
    var writer=typeof(Store).Assembly.GetType("Tickets.Core.InvoiceDocuments");Check(writer is not null,"Word invoice exporter is missing");
    var c=Choice(s);var issue=s.Issue(c.Event,c.Seats,"ООО «Тест & партнёры»","СЕРИЯ",1,new Dictionary<long,long>{{c.Seats[0],123}});
    var invoice=s.Invoice(c.Event,issue);var file=System.IO.Path.Combine(root,"invoice.docx");
    writer!.GetMethod("SaveWord")!.Invoke(null,new object[]{invoice,file});
    using var archive=ZipFile.OpenRead(file);Check(archive.GetEntry("[Content_Types].xml")!=null&&archive.GetEntry("_rels/.rels")!=null);
    using var stream=archive.GetEntry("word/document.xml")!.Open();var xml=System.Xml.Linq.XDocument.Load(stream);var text=string.Join(" ",xml.Descendants().Where(e=>e.Name.LocalName=="t").Select(e=>e.Value));
    Check(text.Contains(invoice.Recipient)&&text.Contains(invoice.Title)&&text.Contains(invoice.Total.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))));
    Check(invoice.Tickets.All(t=>text.Contains(t.Code)&&text.Contains(t.Sector)&&text.Contains(t.Row)&&text.Contains(t.Seat)));
    File.Copy(file,System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tickets-invoice-sample.docx"),true);
    var many=invoice with{Tickets=Enumerable.Range(1,85).Select(n=>invoice.Tickets[0] with{Number=n,Seat=n.ToString()}).ToList()};
    InvoiceDocuments.SaveWord(many,System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tickets-invoice-many.docx"));
});
SqliteConnection.ClearAllPools();
Directory.Delete(root,true);
Console.WriteLine($"{total-failed}/{total} passed");
return failed == 0 ? 0 : 1;
