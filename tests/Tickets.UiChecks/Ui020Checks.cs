using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Ui020Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Count(s=>s.Available)>=3);
        var seats=store.Seats(show.Id).Where(s=>s.Available).Take(3).ToArray();
        var invoice=store.Issue(show.Id,[seats[0].Id,seats[1].Id],"Получатель UI020","UI020",store.NextNumber("UI020"));
        main.SelectEvent(show.Id);Pump();
        var hall=(HallView)main.FindName("Hall");hall.SetSelection([seats[0].Id,seats[1].Id]);Pump();
        Check(hall.Selected.Count==2&&((Button)main.FindName("ReturnButton")).IsEnabled,"occupied-only group can be selected for return");
        Check(!((Button)main.FindName("IssueButton")).IsEnabled&&!((Button)main.FindName("PrintTicketsButton")).IsEnabled,"occupied-only group never enables generic issuance or printing");
        var click=(ValueTuple<SeatInfo?,SeatInfo?>)typeof(HallView).GetMethod("ClickSeat",Private)!.Invoke(hall,[hall.SelectedSeats.First()])!;
        Check(click.Item1 is null&&click.Item2 is null&&hall.Selected.SetEquals([seats[1].Id]),"left click toggles an occupied seat without opening menu or warning");
        typeof(HallView).GetMethod("ClickSeat",Private)!.Invoke(hall,[store.Seats(show.Id).First(s=>s.Id==seats[0].Id)]);
        Check(hall.Selected.SetEquals([seats[0].Id,seats[1].Id]),"left click adds occupied seat back to occupied-only group");
        typeof(MainWindow).GetMethod("OpenSeatContext",Private)!.Invoke(main,[seats[0].Id,new Point(10,10)]);
        var entries=hall.ContextMenu!.Items.OfType<MenuItem>().ToArray();
        Check(entries.Any(m=>Equals(m.Header,"Вернуть выбранные"))&&entries.Any(m=>Equals(m.Header,"Открыть исходную накладную")),"occupied group menu preserves invoice access and group return");hall.ContextMenu.IsOpen=false;
        hall.SetSelection([seats[0].Id,seats[2].Id]);Pump();
        Check(hall.Selected.SetEquals([seats[2].Id]),"mixed selection excludes occupied tickets from free operations");
        var freeSeat=hall.SelectedSeats.Single();
        typeof(HallView).GetMethod("ClickSeat",Private)!.Invoke(hall,[freeSeat]);
        Check(hall.Selected.Count==0,"left click still removes a selected free place");
        typeof(HallView).GetMethod("ClickSeat",Private)!.Invoke(hall,[freeSeat]);
        Check(hall.Selected.SetEquals([freeSeat.Id]),"left click still adds a free place");
        var geometry=new HallView{AllowOccupiedSelection=true};
        geometry.Load([seats[0] with{Id=50001,X=0,Y=0,TicketId=1},seats[1] with{Id=50002,X=20,Y=0,TicketId=2},seats[2] with{Id=50003,X=40,Y=0}],[],true);
        typeof(HallView).GetField("worldAnchor",Private)!.SetValue(geometry,new Point(-1,-1));
        object? Geometry(string name,params object[] args)=>typeof(HallView).GetMethod(name,Private)!.Invoke(geometry,args);
        var occupiedEnd=(Point)Geometry("Screen",new Point(39,18))!;
        Geometry("SelectRange",occupiedEnd);
        Check(geometry.Selected.SetEquals([50001L,50002L])&&((IReadOnlyList<SeatInfo>)Geometry("RejectedSeats",occupiedEnd)!).Count==0,"occupied-only rectangle retains group without a red warning");
        var mixedEnd=(Point)Geometry("Screen",new Point(60,18))!;
        Geometry("SelectRange",mixedEnd);
        Check(geometry.Selected.SetEquals([50003L])&&((IReadOnlyList<SeatInfo>)Geometry("RejectedSeats",mixedEnd)!).Count==2,"mixed rectangle retains only free places and reports occupied places for the deferred warning");
        typeof(HallView).GetField("gestureSelection",Private)!.SetValue(geometry,geometry.Selected.ToHashSet());
        typeof(HallView).GetField("removing",Private)!.SetValue(geometry,true);
        Geometry("SelectRange",mixedEnd);
        Check(geometry.Selected.Count==0,"removal drag still removes selected free places");
        CheckHallCaptions();
        var type=typeof(MainWindow).Assembly.GetType("Tickets.App.RecipientsWindow")!;
        var window=(Window)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[store],null)!;
        try
        {
            var organizations=(ListBox)type.GetField("organizations",Private)!.GetValue(window)!;
            Check(organizations.FontSize>=16&&organizations.FontWeight==FontWeights.Bold,"organization names are larger and bold");
            Check(((Grid)window.Content).ColumnDefinitions[0].Width.Value==330,"organization column retains original width");
            organizations.SelectedItem=organizations.Items.Cast<RecipientDirectoryInfo>().Single(r=>r.Name=="Получатель UI020");
            var history=(TreeView)type.GetField("history",Private)!.GetValue(window)!;
            IEnumerable<TreeViewItem> Nodes(ItemsControl parent){foreach(var child in parent.Items.OfType<TreeViewItem>()){yield return child;foreach(var nested in Nodes(child))yield return nested;}}
            var date=Nodes(history).Select(n=>n.Header).OfType<TextBlock>().First(t=>t.Inlines.OfType<System.Windows.Documents.Run>().Any(r=>r.FontSize==19));
            Check(date.Inlines.OfType<System.Windows.Documents.Run>().First().FontSize==19,"recipient history event date and time are larger");
            var issueHeader=Nodes(history).Where(n=>n.Tag is RecipientIssueInfo).Select(n=>n.Header).OfType<DockPanel>().First();
            Check(issueHeader.Children.OfType<CheckBox>().Count()==1&&issueHeader.Children.OfType<TextBlock>().Count()==1,"recipient history has separate package checkbox and single-click issue text");
        }
        finally{window.Close();}
        store.Return(store.Invoice(show.Id,invoice).Tickets.Select(t=>t.Id).ToArray());main.SelectEvent(show.Id);Pump();
    }
    internal static void CheckHallCaptions()
    {
        foreach(var (name,layout) in new[]{("russian-song",VenueLayouts019.RussianSongSeats()),("estrada",VenueLayouts019.EstradaSeats())})
        {
            var diagramSeats=layout.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"#336699",null,"","",null)).ToArray();
            var sectors=layout.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(s=>s.X),g.Min(s=>s.Y)-26)).ToArray();
            var diagram=new HallView();diagram.Load(diagramSeats,sectors,true);
            var drawing=(System.Windows.Media.DrawingGroup)typeof(HallView).GetField("drawing",Private)!.GetValue(diagram)!;
            var renderedSeats=(IReadOnlyList<SeatInfo>)typeof(HallView).GetField("seats",Private)!.GetValue(diagram)!;
            IEnumerable<System.Windows.Media.GlyphRunDrawing> Glyphs(System.Windows.Media.Drawing node)
            {if(node is System.Windows.Media.GlyphRunDrawing glyph)yield return glyph;else if(node is System.Windows.Media.DrawingGroup group)foreach(var child in group.Children)foreach(var descendant in Glyphs(child))yield return descendant;}
            var headings=Glyphs(drawing).Where(g=>Math.Abs(g.GlyphRun.FontRenderingEmSize-15.6)<.01).ToArray();
            var capture=new Window{Title="UI025 real hall "+name,Width=1100,Height=900,Content=diagram};capture.Show();Pump();
            try{Corrections025UiChecks.Capture(capture,"025-real-hall-"+name);}finally{capture.Close();}
            foreach(var heading in headings)
            {
                var text=heading.GlyphRun.Characters is null?"?":new string(heading.GlyphRun.Characters.ToArray());
                Console.WriteLine($"025 {name} heading '{text}': bounds={heading.Bounds}");
                foreach(var seat in renderedSeats.Where(seat=>heading.Bounds.IntersectsWith(new Rect(seat.X,seat.Y,16,16))))
                    Console.WriteLine($"025 {name} heading collision '{text}' {heading.Bounds}: seat {seat.Sector}/{seat.Row}/{seat.Number} at {seat.X},{seat.Y}");
                foreach(var other in headings.Where(other=>!ReferenceEquals(other,heading)&&other.Bounds.IntersectsWith(heading.Bounds)))
                    Console.WriteLine($"025 {name} heading/heading collision: {heading.Bounds}; other={other.Bounds}");
            }
            var headingIndex=0;
            foreach(var sector in sectors)
            {
                var wrapped=sector.Name.StartsWith("Ложа партера (",StringComparison.Ordinal)||sector.Name.StartsWith("Балкон №",StringComparison.Ordinal);
                var label=(System.Windows.Media.FormattedText)typeof(HallView).GetMethod("Text",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[wrapped?sector.Name.Replace(" (","\n("):sector.Name,15.6,System.Windows.Media.Brushes.SlateGray])!;
                label.TextAlignment=TextAlignment.Center;
                var native=new System.Windows.Media.DrawingGroup();using(var context=native.Open())context.DrawText(label,new Point());
                var ink=Rect.Empty;foreach(var heading in headings.Skip(headingIndex).Take(Glyphs(native).Count()))ink.Union(heading.Bounds);headingIndex+=Glyphs(native).Count();
                var own=renderedSeats.Where(seat=>seat.Sector==sector.Name).ToArray();
                double Distance(SeatInfo seat)=>Math.Max(0,Math.Max(ink.Left-seat.X-16,seat.X-ink.Right));
                var nearest=own.Min(Distance);var upper=own.Where(seat=>Distance(seat)<=nearest+.001).Min(seat=>seat.Y);
                var left=own.Min(seat=>seat.X);var right=own.Max(seat=>seat.X)+16;
                Console.WriteLine($"025 {name} sector '{sector.Name}': ink={ink}; ownX={left}..{right}; localUpper={upper}; clearance={upper-ink.Bottom}; sideExtension={Math.Max(0,Math.Max(left-ink.Left,ink.Right-right))}");
                Check(upper-ink.Bottom>=43.9,"real sector heading retains two standard spacings above its local seats");
            }
            Check(headings.Length>=sectors.Length,"actual hall drawing contains each sector heading");
            Check(headings.All(heading=>!renderedSeats.Any(seat=>heading.Bounds.IntersectsWith(new Rect(seat.X,seat.Y,16,16)))),"actual sector heading glyph bounds do not intersect any seat");
            Check(headings.All(heading=>!headings.Any(other=>!ReferenceEquals(other,heading)&&heading.Bounds.IntersectsWith(other.Bounds))),"actual sector heading glyph bounds do not intersect other headings");
            var captions=Glyphs(drawing).Where(g=>Math.Abs(g.GlyphRun.FontRenderingEmSize-14.3)<.01&&g.ForegroundBrush==System.Windows.Media.Brushes.Black).ToArray();
            Check(captions.Length>=2*layout.GroupBy(s=>(s.Sector,s.Row)).Count(),"each logical row has endpoint captions and any actual aisle caption");
            foreach(var caption in captions)
            {
                foreach(var seat in renderedSeats.Where(seat=>caption.Bounds.IntersectsWith(new Rect(seat.X,seat.Y,16,16))))Console.WriteLine($"Caption collision: {caption.Bounds}; seat {seat.Sector}/{seat.Row}/{seat.Number} at {seat.X},{seat.Y}");
                foreach(var heading in headings.Where(heading=>heading.Bounds.IntersectsWith(caption.Bounds)))Console.WriteLine($"Caption collision: {caption.Bounds}; heading {heading.Bounds}");
            }
            Check(captions.All(caption=>!renderedSeats.Any(seat=>caption.Bounds.IntersectsWith(new Rect(seat.X,seat.Y,16,16)))&&!headings.Any(heading=>heading.Bounds.IntersectsWith(caption.Bounds))),"row captions remain clear of real seats and sector headings");
        }
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
