using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class TicketlandMatching032UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var token=Guid.NewGuid().ToString("N");
        var previous=typeof(MainWindow).GetField("current",Private)!.GetValue(main) as Tickets.Core.EventInfo;
        var search=(TextBox)main.FindName("SearchBox");var oldSearch=search.Text;
        var oldFetch=main.TicketlandFetch028;var fetches=0;
        var initialWindows=Application.Current.Windows.Cast<Window>().ToHashSet();
        var color=store.PriceColors().FirstOrDefault(p=>p.Amount==500)?.Color??PricePalette.NextAvailable(store.PriceColors().Select(p=>p.Color));
        long Show(string title,string venueName,DateTime at,DraftSeat[] seats)
        {
            var venue=store.CreateHall(venueName,"",seats);
            var plan=store.SavePlan(venue,"UI032 "+token,store.VenueSeats(venue).Select(s=>new PriceValue(s.Id,500,color)).ToArray());
            return store.CreateEvent(store.SavePerformance(title),venue,plan,at);
        }
        static DraftSeat Seat(string sector,string row,string number,double x,double y)=>new(){Sector=sector,Row=row,Number=number,X=x,Y=y};
        var vinokurTitle="Владимир Винокур «Винокур-Шоу»";
        var estrada="Театр Эстрады UI032 "+token;var russian="Русская песня UI032 "+token;
        var vinokurAt=new DateTime(2027,1,3,20,0,0);
        var vinokur=Show(vinokurTitle,estrada,vinokurAt,[Seat("Партер","1","1",40,40)]);
        var title="Русская песня UI032 "+token;var at=vinokurAt.AddDays(1);
        var seats=new[]{Seat("Ложа левая","1","1",40,40),Seat("Правая ложа","1","1",140,40)}.ToList();
        foreach(var (sector,x) in new[]{("Балкон 2 левый",40d),("Балкон 2 правый",140d),("VIP 2",240d)})
            foreach(var row in new[]{"1","2"})seats.Add(Seat(sector,row,"1",x,100+int.Parse(row)*40));
        seats.AddRange([Seat("Балкон 2 левый","3","99",40,240),Seat("VIP 2","3","99",240,240),
            Seat("Балкон 2 правый","4","88",140,280),Seat("VIP 2","4","88",240,280),Seat("Балкон 2 правый","7","7",140,320)]);
        var song=Show(title,russian,at,seats.ToArray());
        TicketlandSale028 Sale(string source,string row,string number,string suffix,decimal amount)=>new(title,at,source,row,number,token+suffix,amount){Venue=russian};
        var report=new TicketlandReport028([
            new("Владимир Винокур. \"Винокур - Шоу\" . Новогодняя программа.",vinokurAt,"Партер","1","1",token+"V",731){Venue=estrada},
            Sale("ЛОЖА ПАРТЕРА ЛЕВАЯ","1","1","L",111),Sale("ЛОЖА ПАРТЕРА ПРАВАЯ","1","1","R",222),
            Sale("Балкон Правый 2","1","1","B1",333),Sale("БАЛКОН №2 ПРАВАЯ СТОРОНА","2","1","B2",444),
            Sale("Балкон Правый 2","3","99","MR",555),Sale("Балкон Левый 2","4","88","ML",666),
            Sale("Балкон №2","7","7","A",777)],(store.TicketlandLastSync028??DateTimeOffset.UtcNow).AddMinutes(1));
        try
        {
            // A complete synthetic cached snapshot exercises the same reconciliation used after download.
            main.TicketlandFetch028=(_,_)=>{fetches++;return Task.FromException<TicketlandReport028>(new IOException("UI032 API unavailable"));};
            store.ApplyTicketlandReport028(report,replace:true);
            search.Clear();typeof(MainWindow).GetMethod("ReadEventCatalog",Private)!.Invoke(main,null);typeof(MainWindow).GetMethod("ApplyFilter",Private)!.Invoke(main,null);
            main.SelectEvent(vinokur);main.ReapplyTicketlandMappings028();Pump();
            var hall=(HallView)main.FindName("Hall");hall.SetSelection(store.Seats(vinokur).Select(s=>s.Id).ToArray());Pump();
            Check(hall.SelectedSeats.Single().IsTicketlandSold,"Vinokur programme suffix and typography resolve to the exact local showing");
            CheckTotals(main,1,731);
            Corrections025UiChecks.Capture(main,"032-ticketland-vinokur-hall");
            main.SelectEvent(song);main.ReapplyTicketlandMappings028();Pump();
            var marked=store.Seats(song).Where(s=>s.IsTicketlandSold).ToArray();
            Check(marked.Length==4&&marked.Count(s=>s.Sector=="Ложа левая")==1&&marked.Count(s=>s.Sector=="Правая ложа")==1,"identical lodge row and place resolve independently by left and right names");
            Check(marked.Count(s=>s.Sector=="Балкон 2 правый")==2&&!marked.Any(s=>s.Sector=="Балкон 2 левый"||s.Sector=="VIP 2"),"both right balcony spellings mark only the correct balcony despite identical positions");
            CheckTotals(main,7,3108);
            var selected=store.Seats(song).Single(s=>s.Sector=="VIP 2"&&s.Row=="1").Id;
            hall.SetSelection([selected]);hall.FocusPrice=500;Pump();
            var before=store.Seats(song).ToArray();
            Corrections025UiChecks.Capture(main,"032-ticketland-matched-hall");
            InspectReview(store,main,song,"Балкон Правый 2","3","99","Балкон 2 правый","032-ticketland-right-missing");
            InspectReview(store,main,song,"Балкон Левый 2","4","88","Балкон 2 левый","032-ticketland-left-missing");
            var ambiguous=store.TicketlandSectorReviews030().Single(i=>i.EventId==song&&CatalogText.Key(i.SourceSector)==CatalogText.Key("Балкон №2"));
            Check(ambiguous.Candidates.Count==0&&!store.Seats(song).Single(s=>s.Row=="7").IsTicketlandSold,"ambiguous sector is never solved by unique row and place labels");
            Check(hall.Selected.SetEquals([selected])&&hall.FocusPrice==500&&store.Seats(song).SequenceEqual(before),"opening and inspecting reviews preserves hall focus, selection and accounting");
            Check(double.IsFinite((double)typeof(HallView).GetField("zoom",Private)!.GetValue(hall)!)&&hall.ActualWidth>0&&hall.ActualHeight>0,"matched hall retains finite usable geometry");
            var reopened=new Store(store.Path);var cached=reopened.CachedTicketlandReport028();reopened.ApplyTicketlandReport028(cached,replace:true);
            Check(cached.Sales.Count==report.Sales.Count&&cached.FetchedAt==report.FetchedAt,"reopen retains the whole cached report including unresolved source rows");
            Check(reopened.Seats(song).SequenceEqual(before)&&reopened.Seats(vinokur).Single().IsTicketlandSold,"reopen and cached reconciliation preserve all automatic markers");
            Check(reopened.TicketlandSourceTotals030().Single(t=>t.EventId==song) is {TicketCount:7,Nominal:3108},"reopen retains source totals rather than local price sums");
            Check(reopened.TicketlandSectorReviews030().Count(i=>i.EventId==song)==3&&fetches==0,"offline cached review survives reopen without any API call");
            var offlineReview=new TicketlandMappingWindow028(reopened,main,song);offlineReview.Show();Pump();
            try
            {
                var lists=Walk(offlineReview).OfType<ListBox>().ToArray();var issues=lists.Single(l=>l.Name=="TicketlandIssues029");
                Check(issues.Items.Count==3&&issues.Items.Cast<TicketlandSectorReview030>().All(i=>i.EventId==song),"reopened cached database populates the scoped native review offline");
                issues.SelectedItem=issues.Items.Cast<TicketlandSectorReview030>().Single(i=>CatalogText.Key(i.SourceSector)==CatalogText.Key("Балкон №2"));Pump();
                Check(lists.Single(l=>l.Name=="TicketlandCandidates029").Items.Count==0,"native ambiguous-sector review offers no seat-derived sector candidate");
            }
            finally{offlineReview.Close();Pump();}
            Check(Application.Current.Windows.Cast<Window>().All(initialWindows.Contains),"cached matching opens no unrelated popup");
        }
        finally
        {
            main.TicketlandFetch028=oldFetch;
            if(previous is not null)main.SelectEvent(previous.Id);
            search.Text=oldSearch;Pump();
        }
    }
    private static void InspectReview(Store store,MainWindow main,long eventId,string source,string row,string number,string local,string capture)
    {
        var window=new TicketlandMappingWindow028(store,main,eventId);window.Show();Pump();
        try
        {
            var controls=Walk(window).ToArray();var issues=controls.OfType<ListBox>().Single(l=>l.Name=="TicketlandIssues029");
            Check(window.EventId031==eventId&&issues.Items.Cast<TicketlandSectorReview030>().All(i=>i.EventId==eventId),"review remains scoped to the selected exact show");
            var issue=issues.Items.Cast<TicketlandSectorReview030>().Single(i=>CatalogText.Key(i.SourceSector)==CatalogText.Key(source));issues.SelectedItem=issue;Pump();
            var candidates=controls.OfType<ListBox>().Single(l=>l.Name=="TicketlandCandidates029");
            Check(candidates.Items.Count==0&&issue.Candidates.Count==0&&!controls.OfType<Button>().Single(b=>Equals(b.Content,"Согласиться")).IsEnabled,"missing position cannot propose another side or VIP: "+source);
            var visible=string.Join("\n",controls.OfType<TextBlock>().Select(t=>t.Text));
            Check(CatalogText.Key(visible).Contains(CatalogText.Key(source))&&CatalogText.Key(visible).Contains(CatalogText.Key(local))&&visible.Contains("ряд "+row)&&visible.Contains("место "+number)&&issue.MissingSeats.Any(s=>s.Contains("ряд "+row)&&s.Contains("место "+number)),"review explains source sector, correct local sector and missing row/place: "+source);
            Corrections025UiChecks.Capture(window,capture);
        }
        finally{window.Close();Pump();}
    }
    private static void CheckTotals(MainWindow main,int count,decimal nominal)
    {
        var totals=((TextBlock)main.FindName("HallTotals")).Text;
        Check(totals.Contains("Продано Ticketland: "+count)&&totals.Contains(nominal.ToString("N2")),"hall displays source ticket count and source nominal "+nominal);
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 032 "+message);}
}
