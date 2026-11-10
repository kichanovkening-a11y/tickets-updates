using Tickets.Update;
var failures=0;
void Check(bool b,string message){if(!b)throw new Exception(message);}
void Test(string name,Action<string> test){var root=Path.Combine(Path.GetTempPath(),"updater046-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{test(root);Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}finally{Directory.Delete(root,true);}}
Test("renamed leading launcher is a safe managed payload",root=>{
 Check(UpdateFiles.SafePath(root,"! Tickets.exe")==Path.Combine(root,"! Tickets.exe"),"leading launcher rejected");
 foreach(var bad in new[]{"! ../Tickets.exe","../Tickets.exe","Data/tickets.db","! Tickets.exe/evil.dll"}){try{UpdateFiles.SafePath(root,bad);throw new Exception("unsafe path accepted");}catch(InvalidDataException){}}
});
Test("locked later file is rejected before first replacement",root=>{
 var stage=Path.Combine(root,"stage");var app=Path.Combine(root,"app");Directory.CreateDirectory(stage);Directory.CreateDirectory(app);
 foreach(var name in new[]{"Tickets.dll","Tickets.Core.dll"}){File.WriteAllText(Path.Combine(stage,name),"new");File.WriteAllText(Path.Combine(app,name),"old");}
 var files=new List<UpdateFile>{new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll"))),new("Tickets.Core.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.Core.dll")))};
 using var held=new FileStream(Path.Combine(app,"Tickets.Core.dll"),FileMode.Open,FileAccess.Read,FileShare.Read);
 try{UpdateFiles.Apply(new(app,stage,files));throw new Exception("locked install accepted");}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
 Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="old","first file changed");
 Check(!Directory.Exists(Path.Combine(app,".updates"))||!Directory.EnumerateDirectories(Path.Combine(app,".updates"),"rollback-*").Any(),"installation began before checking locked files");
});
Test("unconfirmed installation recovers original binaries and data",root=>{
 var stage=Path.Combine(root,"stage");var app=Path.Combine(root,"app");Directory.CreateDirectory(stage);Directory.CreateDirectory(Path.Combine(app,"Data"));
 File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");File.WriteAllText(Path.Combine(app,"Tickets.dll"),"old");File.WriteAllText(Path.Combine(app,"Data","tickets.db"),"original data");
 var plan=new InstallPlan(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))});
 using(var tx=UpdateTransaction.Prepare(plan)){tx.Install();Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="new","install missing");File.WriteAllText(Path.Combine(app,"Data","tickets.db"),"migrated data");}
 UpdateTransaction.Recover(app);
 Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="old","old binary not restored");
 Check(File.ReadAllText(Path.Combine(app,"Data","tickets.db"))=="original data","data snapshot not restored");
});
Test("confirmed update survives recovery and retains user documents",root=>{
 var stage=Path.Combine(root,"stage");var app=Path.Combine(root,"app");Directory.CreateDirectory(stage);Directory.CreateDirectory(Path.Combine(app,"Data","Documents"));
 File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");File.WriteAllText(Path.Combine(app,"Data","Documents","contract.docx"),"document");
 using(var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}))){tx.Install();tx.Complete();}
 UpdateTransaction.Recover(app);
 Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="new","confirmed binary reverted");
 Check(File.ReadAllText(Path.Combine(app,"Data","Documents","contract.docx"))=="document","document lost");
});
Test("confirmed transaction reserves retain only two generations",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(app);Directory.CreateDirectory(stage);
 for(var i=0;i<3;i++){File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"version "+i);using var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}));tx.Install();tx.Complete();}
 Check(Directory.EnumerateDirectories(Path.Combine(app,".updates"),"transaction-*").Count()==2,"old successful reserves accumulate");
});
Test("damaged recovery snapshot fails closed before replacing new files",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(app);Directory.CreateDirectory(stage);
 File.WriteAllText(Path.Combine(app,"Tickets.dll"),"old");File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");
 string backup;
 using(var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}))){backup=tx.BackupDirectory;tx.Install();}
 File.WriteAllText(Path.Combine(backup,"files","Tickets.dll"),"damaged");
 try{UpdateTransaction.Recover(app);throw new Exception("corrupt backup accepted");}catch(IOException){}
 Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="new","corrupt backup installed");Check(UpdateTransaction.HasPending(app),"recovery marker discarded");
});
Test("second updater cannot prepare while first owns the target",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(app);Directory.CreateDirectory(stage);File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");
 var plan=new InstallPlan(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))});
 using var first=UpdateTransaction.Prepare(plan);
 try{using var second=UpdateTransaction.Prepare(plan);throw new Exception("second updater admitted");}catch(IOException){}
 first.Rollback();Check(!File.Exists(Path.Combine(app,"Tickets.dll")),"uninstalled file appeared");
});
Test("healthy startup receipt commits interrupted confirmation without losing later edits",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(Path.Combine(app,"Data"));Directory.CreateDirectory(stage);
 File.WriteAllText(Path.Combine(app,"Data","tickets.db"),"before");File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");
 using(var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}))){tx.Install();var token=new string('a',32);File.WriteAllText(Path.Combine(tx.BackupDirectory,"startup-token"),token);File.WriteAllText(Path.Combine(tx.BackupDirectory,"health.json"),token);}
 File.WriteAllText(Path.Combine(app,"Data","tickets.db"),"new user edits");
 UpdateTransaction.Recover(app);
 Check(File.ReadAllText(Path.Combine(app,"Data","tickets.db"))=="new user edits","healthy user's edits rolled back");
 Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="new","healthy app reverted");
});
Test("empty corrupted journal inventory cannot delete all user data",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(Path.Combine(app,"Data"));Directory.CreateDirectory(stage);
 File.WriteAllText(Path.Combine(app,"Data","tickets.db"),"valuable");File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");string folder;
 using(var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}))){folder=tx.BackupDirectory;tx.Install();}
 var path=Path.Combine(folder,"journal.json");var journal=System.Text.Json.JsonSerializer.Deserialize<UpdateTransaction.Journal>(File.ReadAllText(path))!;
 File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(journal with {Files=new(),Data=new()}));
 bool refused=false;try{UpdateTransaction.Recover(app);}catch(InvalidDataException){refused=true;}
 Check(refused,"corrupted inventory accepted");Check(File.ReadAllText(Path.Combine(app,"Data","tickets.db"))=="valuable","data deleted");
});
Test("pending update remains recoverable when main DLL is missing",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(app);Directory.CreateDirectory(stage);
 File.WriteAllText(Path.Combine(app,"Tickets.dll"),"old");File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");
 using(var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}))){tx.Install();}
 File.Delete(Path.Combine(app,"Tickets.dll"));
 Check(UpdateFiles.CanUpdate(app),"recoverable folder rejected before recovery");
 UpdateTransaction.Recover(app);Check(File.ReadAllText(Path.Combine(app,"Tickets.dll"))=="old","missing DLL not restored");
});
Test("old client first startup gets leading launcher without overwriting an existing one",root=>{
 File.WriteAllText(Path.Combine(root,"Tickets.exe"),"apphost");UpdateFiles.EnsureLauncher(root);
 Check(File.ReadAllText(Path.Combine(root,"! Tickets.exe"))=="apphost","leading launcher missing");
 File.WriteAllText(Path.Combine(root,"! Tickets.exe"),"existing");UpdateFiles.EnsureLauncher(root);
 Check(File.ReadAllText(Path.Combine(root,"! Tickets.exe"))=="existing","existing launcher overwritten");
});
Test("partial health receipt cannot commit but atomic publication can",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(app);Directory.CreateDirectory(stage);File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");
 using var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}));tx.Install();
 var token=new string('b',32);File.WriteAllText(Path.Combine(tx.BackupDirectory,"startup-token"),token);var path=Path.Combine(tx.BackupDirectory,"health.json");
 File.WriteAllText(path,"bbbb");Check(!tx.IsHealthy,"partial receipt accepted");
 UpdateTransaction.PublishHealth(path,token);Check(tx.IsHealthy,"atomic receipt ignored");tx.Complete();
});
Test("Windows-normalized traversal in recovery inventory is rejected",root=>{
 var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(Path.Combine(app,"Data"));Directory.CreateDirectory(stage);
 File.WriteAllText(Path.Combine(app,"Data","tickets.db"),"valuable");File.WriteAllText(Path.Combine(stage,"Tickets.dll"),"new");string folder;
 using(var tx=UpdateTransaction.Prepare(new(app,stage,new(){new("Tickets.dll",UpdateFiles.Hash(Path.Combine(stage,"Tickets.dll")))}))){folder=tx.BackupDirectory;tx.Install();}
 var index=Path.Combine(folder,"inventory.json");var original=System.Text.Json.JsonSerializer.Deserialize<UpdateTransaction.Journal>(File.ReadAllText(index))!;
 foreach(var path in new[]{".. /outside.txt","../outside.txt","nested./outside.txt","nested /outside.txt"})
 {
  var changed=original with {Data=new(){new(path,true,new string('a',64))}};
  File.WriteAllText(index,System.Text.Json.JsonSerializer.Serialize(changed));File.WriteAllText(Path.Combine(folder,"inventory.sha256"),UpdateFiles.Hash(index));
  File.WriteAllText(Path.Combine(folder,"journal.json"),System.Text.Json.JsonSerializer.Serialize(changed with {Phase="installed"}));
  bool refused=false;try{UpdateTransaction.Recover(app);}catch(InvalidDataException){refused=true;}
  Check(refused,"unsafe recovery path accepted: "+path);Check(File.ReadAllText(Path.Combine(app,"Data","tickets.db"))=="valuable","data changed before path validation");
 }
});

Test("052 download reports real byte progress",root=>{
 var copy=typeof(GitHubUpdates).GetMethod("CopyWithProgressAsync");
 Check(copy is not null,"byte progress missing");
 using var input=new MemoryStream(new byte[200000]);using var output=new MemoryStream();var values=new List<long>();
 Action<long,long?> report=(done,total)=>{Check(done>=0&&done<=200000&&total==200000,"invalid progress");values.Add(done);};
 ((Task)copy!.Invoke(null,[input,output,300000L,200000L,report,CancellationToken.None])!).GetAwaiter().GetResult();
 Check(output.Length==200000&&values.Count>=3&&values[^1]==200000&&values.SequenceEqual(values.Order()),"intermediate progress missing");
});
Test("052 preparation and installation offer progress callbacks",root=>{
 Check(typeof(UpdateTransaction).GetMethods().Any(m=>m.Name=="Prepare"&&m.GetParameters().Length==2),"backup progress missing");
 Check(typeof(UpdateTransaction).GetMethods().Any(m=>m.Name=="Install"&&m.GetParameters().Length==1),"file progress missing");
});

return failures==0?0:1;