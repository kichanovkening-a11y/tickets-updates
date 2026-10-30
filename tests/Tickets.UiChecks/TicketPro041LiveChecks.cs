using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Wpf;
using Tickets.App;
using Tickets.Core;

// Explicit opt-in. Copies the database and encrypted sign-in into a unique profile.
// Never opens the user's main application or a visible login window.
internal static class TicketPro041LiveChecks
{
    internal static void Run(string sourceDatabase,string connectionFile)
    {
        var task=Application.Current.Dispatcher.InvokeAsync(CheckAsync).Task.Unwrap();
        var frame=new DispatcherFrame();
        _=task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
        async Task CheckAsync()
        {
            using var connection=JsonDocument.Parse(File.ReadAllText(connectionFile));
            var sourceId=connection.RootElement.GetProperty("ConnectionId").GetString();
            if(!Guid.TryParse(sourceId,out _))throw new Exception("Invalid source profile identity");
            var id=Guid.NewGuid().ToString();var work=Path.Combine(Path.GetTempPath(),"Tickets-live041-"+id);
            var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Tickets","TicketPRO",id);
            var sourceCache=Path.Combine(Path.GetDirectoryName(profile)!,sourceId!,"session039.bin");
            if(!File.Exists(sourceCache))throw new Exception("Saved sign-in is unavailable; no login window opened");
            Directory.CreateDirectory(work);Directory.CreateDirectory(profile);
            File.Copy(sourceCache,Path.Combine(profile,"session039.bin"));
            var database=Path.Combine(work,"tickets.db");
            using(var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=sourceDatabase,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()))
            using(var copy=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Pooling=false}.ToString()))
            {source.Open();copy.Open();source.BackupDatabase(copy);}
            File.WriteAllText(Path.Combine(work,"ticketland-connection.json"),JsonSerializer.Serialize(new{ConnectionId=id,Enabled=false}));
            File.WriteAllText(Path.Combine(work,"ticketpro-settings.json"),"true");
            var store=new Store(database);var original=store.TicketProBindings037().ToDictionary(x=>x.EventId);
            var selected=store.Events().First(e=>e.Title.Contains("Филадельфия")&&!store.IsEventArchived(e.Id)&&original.ContainsKey(e.Id));
            var main=new MainWindow(store){WindowState=WindowState.Normal,WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false};
            WebView2? web=null;
            var schemes=new List<string>();
            try
            {
                main.Show();await Application.Current.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var session=(TicketProSession037)typeof(MainWindow).GetMethod("ProSession037",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null)!;
                web=(WebView2)typeof(TicketProSession037).GetField("view",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
                web.NavigationStarting+=(_,e)=>{if(TicketPro037.IsSchemeUrl(e.Uri))schemes.Add(e.Uri);};
                if(schemes.Count!=0||main.TicketlandIsUpdating028)throw new Exception("Startup refreshed before a showing was selected");
                var clock=Stopwatch.StartNew();main.SelectEvent(selected.Id);await main.RefreshTicketland028();
                Verify("known binding",original[selected.Id].FetchedAt);Console.WriteLine("SELECTED_KNOWN_MS="+clock.ElapsedMilliseconds);
                Corrections025UiChecks.Capture(main,"041-live-hall");
                var first=store.TicketProBindings037().Single(x=>x.EventId==selected.Id).FetchedAt;
                using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Pooling=false}.ToString()))
                {
                    c.Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM ticketpro_place_037 WHERE event_id=$e; DELETE FROM ticketpro_show_037 WHERE event_id=$e";q.Parameters.AddWithValue("$e",selected.Id);q.ExecuteNonQuery();
                }
                schemes.Clear();clock.Restart();await main.RefreshTicketland028();
                Verify("targeted discovery",first);Console.WriteLine("SELECTED_DISCOVERY_MS="+clock.ElapsedMilliseconds);
                Console.WriteLine("PASS LIVE selected-only refresh and targeted discovery leave all other showings unchanged");
                void Verify(string stage,DateTimeOffset before)
                {
                    var after=store.TicketProBindings037().ToDictionary(x=>x.EventId);
                    if(!after.TryGetValue(selected.Id,out var refreshed)||refreshed.FetchedAt<=before)throw new Exception(stage+": selected showing not refreshed: "+main.TicketlandStatus028);
                    if(after.Count!=original.Count||original.Where(x=>x.Key!=selected.Id).Any(x=>after[x.Key]!=x.Value))throw new Exception(stage+": another showing was changed");
                    if(schemes.Count!=1||schemes[0]!=original[selected.Id].SourceUrl)throw new Exception(stage+": another scheme was navigated or the current scheme repeated");
                    var invitation=store.Seats(selected.Id).Single(s=>s.Sector.Equals("Партер",StringComparison.OrdinalIgnoreCase)&&s.Row=="5"&&s.Number=="20");
                    if(!invitation.IsTicketlandOccupied||invitation.IsTicketlandSold)throw new Exception("Invitation classification changed");
                    Console.WriteLine("PASS LIVE "+stage+": one scheme, other "+(original.Count-1)+" showings unchanged");
                }
            }
            finally
            {
                var exited=new TaskCompletionSource();
                if(web?.CoreWebView2 is {} core)core.Environment.BrowserProcessExited+=(_,_)=>exited.TrySetResult();else exited.TrySetResult();
                main.Close();
                try{await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));Directory.Delete(profile,true);}catch(Exception ex) when(ex is TimeoutException or IOException or UnauthorizedAccessException){}
                SqliteConnection.ClearAllPools();try{Directory.Delete(work,true);}catch(IOException){}
            }
        }
    }
}
