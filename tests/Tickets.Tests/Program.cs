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
    var caseDirectory=System.IO.Path.Combine(root,total.ToString());
    Directory.CreateDirectory(caseDirectory);
    var path = System.IO.Path.Combine(caseDirectory,"tickets.db");
    File.Copy(seed, path);
    try { body(new Store(path)); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    finally { SqliteConnection.ClearAllPools(); Directory.Delete(caseDirectory,true); }
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
SeatSelectionChecks.Register(Test);
RecipientForms018Checks.Register(Test);
Docs019Checks.Register(Test);
Docs020Checks.Register(Test);
Docs021Checks.Register(Test);
Docs022Checks.Register(Test);
Catalog021Checks.Register(Test);
Hall022Checks.Register(Test);
LegacyHallGrid022Checks.Register(Test);
Test("021 scan trial detects actual chair geometry without inventing places",_=>ScanRecognition021Checks.Run());
Senders020Checks.Register(Test);
Compact020Checks.Register(Test);
Hall019Checks.Register(Test);
Merge018Checks.Register(Test);
Issuance018Checks.Register(Test);
Parties018Checks.Register(Test);
Hall012Checks.Register(Test);
Pricing012Checks.Register(Test);
OrgMerge012Checks.Register(Test);
Legacy012Checks.Register(Test);
FourUp015Checks.Register(Test);
Journal016Checks.Register(Test);
Docs016Checks.Register(Test);
Print016Checks.Register(Test);
Recipient016Checks.Register(Test);
Issuance017Checks.Register(Test);
Test("all ticket blanks support independent visual editing and new vertical tear-offs",Blank016Checks.Run);
RecipientCreationChecks.Register(Test);
ArchivePricingChecks.Register(Test);
SaleStateChecks.Register(Test);
TicketPrintingChecks.Register(Test);
Test("multiple showing dates save atomically and reject conflicts", s => {
    var venue=s.CreateHall("Multi-date hall","",new[]{new DraftSeat{Row="1",Number="1",X=0,Y=0}});
    var plan=s.SavePlan(venue,"multi-date plan",s.VenueSeats(venue).Select(x=>new PriceValue(x.Id,500,"#FF0000")).ToArray());
    var performance=s.SavePerformance("Multi-date performance");
    var method=typeof(Store).GetMethod("CreateEvents",new[]{typeof(long),typeof(long),typeof(long),typeof(IReadOnlyList<DateTime>)});
    Check(method is not null,"batch date creation is missing");
    List<long> Save(params DateTime[] dates){try{return (List<long>)method!.Invoke(s,new object[]{performance,venue,plan,dates})!;}catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is TicketException){throw ex.InnerException;}}
    var date=new DateTime(2030,1,2,19,30,15);var ids=Save(date,date.AddDays(1));
    Check(ids.Count==2&&ids.All(id=>s.Seats(id).Single().Amount==500));
    Reject(()=>Save(date.AddDays(2),date));
    Check(s.Events().Count(e=>e.VenueId==venue)==2,"conflicting batch must roll back its first insert");
    Reject(()=>Save(date.AddDays(3),date.AddDays(3)));Reject(()=>Save());
});
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
    Check(text.Contains(invoice.Recipient)&&text.Contains(invoice.Title)&&text.Contains(DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total))&&text.Contains(DocumentTotals.Total(invoice.Tickets.Count,invoice.Total)));
    Check(invoice.Tickets.All(t=>!text.Contains(t.Code)&&text.Contains(t.Sector)&&text.Contains(t.Row)&&text.Contains(t.Seat)));
    File.Copy(file,System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tickets-invoice-sample.docx"),true);
    var many=invoice with{Tickets=Enumerable.Range(1,85).Select(n=>invoice.Tickets[0] with{Number=n,Row=n.ToString(),Seat="1"}).ToList()};
    InvoiceDocuments.SaveWord(many,System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!,"tickets-invoice-many.docx"));
});
Test("same venue and instant cannot be booked twice",s=>{
    var venue=s.CreateHall("Schedule venue","",new[]{new DraftSeat{Row="1",Number="1"}});var seat=s.VenueSeats(venue)[0];
    var plan=s.SavePlan(venue,"Schedule plan",new[]{new PriceValue(seat.Id,100,"#112233")});var perf=s.SavePerformance("Schedule show");var date=new DateTime(2030,1,1,19,20,21);
    s.CreateEvent(perf,venue,plan,date);Reject(()=>s.CreateEvent(perf,venue,plan,date));
    var other=s.SavePerformance("Different show");Reject(()=>s.CreateEvent(other,venue,plan,date));
    s.CreateEvent(perf,venue,plan,date.AddSeconds(1));
});
Test("recipient directory is shared searchable and survives returns",s=>{
    var method=typeof(Store).GetMethod("Recipients");Check(method is not null,"recipient directory missing");
    var c=Choice(s);s.Issue(c.Event,new[]{c.Seats[0]},"Новая Ёлка Организация","R",1);
    s.Return(s.History(c.Event).Select(t=>t.Id).ToArray());
    var names=(IEnumerable<string>)method!.Invoke(new Store(s.Path),new object[]{"елка"})!;
    Check(names.Contains("Новая Ёлка Организация"));
});
Test("schedule groups by performance with chronological upcoming and accessible past",s=>{
    EventInfo Show(long id,string title,string date)=>new(id,1,title,"Зал",date,date,10,0);
    var now=new DateTime(2030,1,1,12,0,0);var data=new[]{Show(1,"Ёлка","2030-01-02T19:00:00"),Show(2,"Ёлка","2030-01-01T19:00:00"),Show(3,"Ёлка","2029-12-31T19:00:00"),Show(4,"Концерт","2030-01-03T19:00:00")};
    var upcoming=EventSchedule.Groups(data,false,now);Check(upcoming.Count==2&&upcoming[0].Shows.Select(e=>e.Id).SequenceEqual(new long[]{2,1}));
    var past=EventSchedule.Groups(data,true,now);Check(past.Single().Shows.Single().Id==3);
    Check(EventSchedule.Groups(data,false,now,"елка").Single().Shows.Count==2);
});
Test("editable clock accepts seconds and rejects invalid times",s=>{
    Check(CatalogText.Time("7","08","09")==new TimeSpan(7,8,9));Reject(()=>CatalogText.Time("24","0","0"));Reject(()=>CatalogText.Time("1","60","0"));Reject(()=>CatalogText.Time("1","0","60"));Reject(()=>CatalogText.Time("","0","0"));
    Check(s.Recipients().Count>=90&&s.Recipients("СТС").Count>0,"legacy organizations absent");
});
Test("hall labels per-row aisles and stable survivors",s=>HallLayoutChecks.Run());
Test("document sender snapshot and auto save survives reload",s=>{
    var c=Choice(s);var folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!,"Документы");
    s.SaveDocumentSettings("Новый отправитель",folder);var id=s.Issue(c.Event,c.Seats,"Организация","DOC",1);
    Check(s.Invoice(c.Event,id).Sender=="Новый отправитель");
    s.SaveDocumentSettings("Другой отправитель",folder);Check(new Store(s.Path).Invoice(c.Event,id).Sender=="Новый отправитель");
    s.UpdateInvoiceSender(c.Event,id,"Изменённый отправитель");Check(s.Invoice(c.Event,id).Sender=="Изменённый отправитель");
    Reject(()=>s.UpdateInvoiceSender(-1,id,"Чужой"));Check(File.Exists(s.SaveInvoiceDocument(c.Event,id)));
    Check(File.Exists(s.Path+".before-v5.db"));
});
Test("one persistent specification per normalized organization",s=>{
    var first=s.SaveSpecification("Организация Ёлка",s.Sender,"Спецификация","Партер — билеты по согласованию.\nУсловия: __________");
    var second=s.SaveSpecification(" организация елка ","Отправитель","Обновление","Новый текст");
    Check(first.Id==second.Id&&s.Specifications().Count==1);var loaded=new Store(s.Path).Specifications().Single();Check(loaded.Body=="Новый текст");
    Check(File.Exists(s.SaveSpecificationDocument(loaded)));Check(s.Recipients("елка").Count>0);
});
Test("group invoice places by row price and return state without serial",s=>{
    var tickets=Enumerable.Range(1,5).Select(n=>new TicketInfo(n,n,"Партер","9",n.ToString(),"Получатель","SECRET",n,n==5?200:100,"2030-01-01T12:00:00Z",n==4?"2030-01-02T12:00:00Z":null)).ToList();
    var invoice=new InvoiceInfo(1,1,"Спектакль","Зал","01.01.2030","Получатель","2030-01-01T12:00:00Z",tickets);
    var lines=InvoiceDocuments.Lines(invoice);Check(lines.Count==2);Check(lines.Any(l=>l.Seats=="1, 2, 3, 4"&&l.Count==4&&l.Total==400));
    Check(invoice.Total==600);Check(RussianWords.Quantity(21)=="двадцать один билет");Check(RussianWords.Quantity(11)=="одиннадцать билетов");
    Check(RussianWords.Money(2001)=="две тысячи один рубль ноль копеек");Check(RussianWords.Number(0)=="ноль");
    Check(RussianWords.Money((decimal)long.MaxValue*50000).Length>10);
});
Test("recipient search matches word prefixes and ignores organization prefixes",s=>{
    s.SaveSpecification("ООО Билетмаркет",s.Sender,"Спецификация","Текст");
    s.SaveSpecification("ТЕАТР Левашова Иван",s.Sender,"Спецификация","Текст");
    Check(s.Recipients("ле").All(n=>!n.Contains("Билетмаркет")),"substring search incorrectly matches middle of word");
    Check(s.Recipients("ле ив").Contains("ТЕАТР Левашова Иван"),"name word prefixes not matched");
    Check(s.Recipients("ООО бил").Contains("ООО Билетмаркет"));
});
Test("plan copying matches labeled places across halls without changing source",s=>{
    var first=s.CreateHall("Первый шаблон", "",new[]{new DraftSeat{Row="А",Number="1"},new DraftSeat{Row="Ё",Number="2",X=20}});
    var second=s.CreateHall("Вторая схема", "",new[]{new DraftSeat{Row="А",Number="1"},new DraftSeat{Row="Е",Number="2",X=20},new DraftSeat{Row="Ё",Number="2",X=40}});
    var seats=s.VenueSeats(first);var color=s.PriceColors().First(c=>c.Amount==1000).Color;
    var plan=s.SavePlan(first,"Шаблон",seats.Select(x=>new PriceValue(x.Id,1000,color)).ToArray());
    var copied=s.CopyPlanPrices(plan,second);Check(copied.Count==2&&copied.All(p=>s.VenueSeats(second).Any(x=>x.Id==p.SeatId)));
    Check(!copied.Any(p=>s.VenueSeats(second).Single(x=>x.Id==p.SeatId).Row=="Е"),"letter Ё must remain a distinct seat row");
    Check(s.PlanPrices(plan).Select(p=>p.SeatId).SequenceEqual(seats.Select(x=>x.Id)));Check(s.AllPlans().Any(p=>p.Id==plan));
});
Test("41-ticket complete document package uses recipient and exact totals",s=>{
    var e=s.Events().First(e=>s.Seats(e.Id).Count(x=>x.Available)>=41);var ids=s.Seats(e.Id).Where(x=>x.Available).Take(41).Select(x=>x.Id).ToArray();
    var issue=s.Issue(e.Id,ids,"ООО \"Пупкин\"","DOC41",1,ids.ToDictionary(id=>id,id=>1000L));
    var docs=s.PrepareIssueDocuments(e.Id,issue);Check(docs.Count==4&&docs.All(d=>d.Body.Contains("ООО \"Пупкин\"")&&d.Body.Contains(DocumentTotals.Amount(41000))&&d.Body.Contains(RussianWords.Money(41000))));
    var directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tickets-docqa");Directory.CreateDirectory(directory);
    foreach(var file in s.SaveAllDocuments(e.Id,issue))File.Copy(file,System.IO.Path.Combine(directory,System.IO.Path.GetFileName(file)),true);
});
DocsLayoutChecks.Register(Test);
Docs012Checks.Register(Test);
Docs013Checks.Register(Test);
Pricing013Checks.Register(Test);
TicketBlankChecks.Register(Test);
Test("editable four-ticket A4 blanks retain independent geometry and originals",s=>TicketBlankEditingChecks.Run());
Test("hall and pricing presentation refinements",s=>HallEditorRefinementChecks.Run());
BusinessDocumentChecks.Register(Test);
TicketLifecycleChecks.Register(Test);
PricingEditorChecks.Register(Test);
SenderRequisitesChecks.Register(Test);
PackageDocumentsChecks.Register(Test);
CatalogDeletionChecks.Register(Test);
PricingChecks.Register(Test);
RecipientHistoryChecks.Register(Test);
SqliteConnection.ClearAllPools();
Directory.Delete(root,true);
Console.WriteLine($"{total-failed}/{total} passed");
return failed == 0 ? 0 : 1;
