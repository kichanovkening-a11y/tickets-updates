using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;
internal static class Corrections025UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var assembly=typeof(MainWindow).Assembly;
        CheckFreshDraftUndo(store,assembly);
        CheckLetterOrderUndo(store,assembly);
        CheckLocalSectorCaption();
        Ui020Checks.CheckHallCaptions();
        var draft=HallLayout.Generate("Партер",new[]{"1","2","3"}.Select(r=>new HallRowLayout{Label=r,SeatCount=8,AisleAfter=4}).ToArray(),"1",false,50).ToList();
        draft[10].Hidden=true;
        var venue=store.CreateHall("UI025 rotations", "",draft);
        var hallType=assembly.GetType("Tickets.App.HallEditorWindow")!;
        var hallEditor=(Window)Activator.CreateInstance(hallType,[store,store.Venues().Single(v=>v.Id==venue)])!;
        hallEditor.Show();Pump();
        try
        {
            Check(!Walk(hallEditor).OfType<TabItem>().Any(t=>Equals(t.Header,"Места — редактирование"))&&!Walk(hallEditor).OfType<Button>().Any(b=>Equals(b.Content,"Обновить схему")),"removed hall controls absent");
            Check(!Walk(hallEditor).OfType<Expander>().Any(e=>Equals(e.Header,"Настройки рядов")&&e.IsVisible),"copied hall hides duplicate row settings");
            var hall=(HallView)hallType.GetField("preview",Private)!.GetValue(hallEditor)!;
            var notify=(Action<long,Point>)typeof(HallView).GetField("RowContextRequested",Private)!.GetValue(hall)!;
            var originalSeats=(IEnumerable<DraftSeat>)hallType.GetField("seats",Private)!.GetValue(hallEditor)!;
            var retainedCoordinates=originalSeats.Where(s=>s.Row!="2").Select(s=>(s.X,s.Y)).ToArray();
            notify(9,new Point());hall.ContextMenu!.IsOpen=false;
            Check(hall.Selected.Count==7&&hall.ContextMenu.Items.OfType<MenuItem>().Count(m=>Equals(m.Header,"Назначить букву"))==1,"row context selects complete row and exposes one letter command");
            hall.ContextMenu.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Сделать проход")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();
            var seats=(IEnumerable<DraftSeat>)hallType.GetField("seats",Private)!.GetValue(hallEditor)!;
            Check(seats.Count()==16&&seats.Select(s=>s.Row).Distinct().Count()==2&&seats.All(s=>!s.Hidden)&&retainedCoordinates.SequenceEqual(seats.Select(s=>(s.X,s.Y))),"row-caption passage removes whole row including hidden seat without shifting other seats");
            foreach(var angle in new[]{90d,180d})
            {
                var rotated=seats.Select(s=>new DraftSeat{Sector=s.Sector,Row=s.Row,Number=s.Number,X=s.X,Y=s.Y}).ToArray();HallEditing023.Rotate(rotated,angle);
                hall.Load(rotated.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,100,"#7AA8D9",null,"","",null)).ToArray(),[new SectorInfo("Партер",0,-999)],true);Pump();
                Capture(hallEditor,"025-hall-"+angle);
                var drawing=(DrawingGroup)typeof(HallView).GetField("drawing",Private)!.GetValue(hall)!;
                var glyphs=Glyphs(drawing).Where(g=>Math.Abs(g.GlyphRun.FontRenderingEmSize-14.3)<.01).ToArray();
                Check(glyphs.Length==6&&glyphs.All(g=>g.GlyphRun.IsSideways==false),"rotated rows retain upright three captions");
                Ui020Checks.CheckRowCaptionAnchors(hall,glyphs);
            }
        }
        finally{hallEditor.Close();Pump();}
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
        var ticketType=assembly.GetType("Tickets.App.TicketPrintWindow")!;
        var ticket=(Window)Activator.CreateInstance(ticketType,store,show.Id,new[]{seat.Id},"UI025","UI025",1L)!;
        ticket.Width=1000;ticket.Height=800;ticket.Show();Pump();
        try
        {
            Capture(ticket,"025-ticket-toolbar");
            var print=Walk(ticket).OfType<Button>().Single(b=>Equals(b.Content,"Печать"));
            var fields=Walk(ticket).OfType<ComboBox>().Cast<FrameworkElement>().Concat(Walk(ticket).OfType<TextBox>()).Append(print).ToArray();
            var positions=fields.Select(f=>f.TranslatePoint(new Point(),ticket)).ToArray();
            for(var i=0;i<fields.Length;i++)Console.WriteLine($"025 ticket field {i}: type={fields[i].GetType().Name}; position={positions[i]}; size={fields[i].ActualWidth}x{fields[i].ActualHeight}; margin={fields[i].Margin}");
            Check(print.Background is SolidColorBrush green&&green.Color==Color.FromRgb(27,122,67)&&print.FontSize==16.9&&print.ActualHeight==44,"ticket print action matches issuance green and preserves legible type");
            Check(positions.Max(p=>p.Y)-positions.Min(p=>p.Y)<1&&fields.All(f=>Math.Abs(f.ActualHeight-print.ActualHeight)<1),"ticket settings fit one row at original field height");
            Check(print.TranslatePoint(new Point(print.ActualWidth,0),ticket).X<=ticket.ActualWidth,"ticket print action fits current window width");
        }
        finally{ticket.Close();Pump();}
        CaptureEmbedded((Window)Activator.CreateInstance(ticketType,store,show.Id,new[]{seat.Id},"UI025","UI025",1L)!,"025-ticket-embedded");
        Check(!store.Seats(show.Id).Single(s=>s.Id==seat.Id).TicketId.HasValue,"ticket preview still has no issuance side effect");
        const string recipient="Получатель UI025";store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"025"));
        var issue=store.Issue(show.Id,[seat.Id],recipient,"UI025",1);var sender=store.Sender;
        store.SaveSender("UI025 новый");
        var invoiceType=assembly.GetType("Tickets.App.InvoiceWindow")!;var invoice=(Window)Activator.CreateInstance(invoiceType,store,show.Id,issue,false)!;invoice.Width=1030;invoice.Show();Pump();
        try
        {
            var box=Walk(invoice).OfType<ComboBox>().Single(c=>c.IsEditable);var initial=box.Text;
            box.Text="UI025 новый";ConfirmSender(()=>invoiceType.GetMethod("PersistSender",Private)!.Invoke(invoice,null),false);
            Check(box.Text==initial&&store.Invoice(show.Id,issue).Sender==initial,"declining invoice sender restores previous choice");
            box.Text="UI025 новый";ConfirmSender(()=>invoiceType.GetMethod("PersistSender",Private)!.Invoke(invoice,null),true);
            Check(store.Invoice(show.Id,issue).Sender=="UI025 новый"&&store.Sender==sender,"confirmed issue sender leaves default unchanged");
            var nav=Walk(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Накладная"));var action=Walk(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Открыть в Word"));
            Check(Math.Abs(nav.TranslatePoint(new Point(),invoice).Y-action.TranslatePoint(new Point(),invoice).Y)<1,"navigation and actions share one toolbar row");Capture(invoice,"025-document-toolbar");
        }
        finally{invoice.Close();Pump();}
        var billType=assembly.GetType("Tickets.App.CustomIssueDocumentWindow")!;
        var bill=(Window)Activator.CreateInstance(billType,BindingFlags.Instance|BindingFlags.NonPublic,null,[store,show.Id,issue,IssueDocumentKinds.Bill],null)!;bill.Width=1030;bill.Show();Pump();
        try
        {
            Check(bill.Title==recipient&&Walk(bill).OfType<TextBlock>().Any(t=>t.Text==$"Счёт — выдача № {issue}"&&t.LayoutTransform is RotateTransform{Angle:90}),"bill uses recipient header and vertical issue flag");
            var date=Walk(bill).OfType<DatePicker>().Single();Check(date.Width==140&&date.FontSize==19&&date.FontWeight==FontWeights.Bold,"contract metadata date stays fixed width and larger bold");Capture(bill,"025-bill-metadata");
        }
        finally{bill.Close();Pump();}
        CaptureEmbedded((Window)Activator.CreateInstance(billType,BindingFlags.Instance|BindingFlags.NonPublic,null,[store,show.Id,issue,IssueDocumentKinds.Bill],null)!,"025-bill-embedded");
    }
    private static void CheckLetterOrderUndo(Store store,Assembly assembly)
    {
        var draft=HallLayout.Generate("P",new[]{new HallRowLayout{Label="1",SeatCount=2}},"1",true,0);
        var venue=store.CreateHall("UI025 RTL letter undo","",draft);var type=assembly.GetType("Tickets.App.HallEditorWindow")!;
        var editor=(Window)Activator.CreateInstance(type,[store,store.Venues().Single(v=>v.Id==venue)])!;editor.Show();Pump();
        try
        {
            var seats=(IEnumerable<DraftSeat>)type.GetField("seats",Private)!.GetValue(editor)!;var original=seats.Select(s=>(s.Number,s.X,s.Y)).ToArray();
            var hall=(HallView)type.GetField("preview",Private)!.GetValue(editor)!;var notify=(Action<long,Point>)typeof(HallView).GetField("SeatContextRequested",Private)!.GetValue(hall)!;
            notify(1,new Point());hall.ContextMenu!.IsOpen=false;Exception? error=null;
            editor.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.GetType().Name=="HallLetterWindow025");
                try{Walk(dialog).OfType<TextBox>().Single().Text="б";Walk(dialog).OfType<Button>().Single(b=>Equals(b.Content,"Назначить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                catch(Exception ex){error=ex;dialog.Close();}
            }));
            hall.ContextMenu.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Назначить букву")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();if(error is not null)throw error;
            Check(seats.Select(s=>s.Number).SequenceEqual(new[]{"1","Б"})&&seats.Single(s=>s.Number=="Б").X==0,"RTL letter command retains durable logical order without moving physical seats");
            type.GetMethod("Undo",Private)!.Invoke(editor,null);Pump();seats=(IEnumerable<DraftSeat>)type.GetField("seats",Private)!.GetValue(editor)!;
            Check(original.SequenceEqual(seats.Select(s=>(s.Number,s.X,s.Y))),"undo lettering restores original row order labels and geometry");
        }
        finally{editor.Close();Pump();}
    }
    private static void CheckLocalSectorCaption()
    {
        var seats=Enumerable.Range(0,41).Select(i=>new SeatInfo(i+1,"Партер","1",(i+1).ToString(),i*20,237,100,"#7AA8D9",null,"","",null))
            .Concat(Enumerable.Range(0,41).Select(i=>new SeatInfo(i+100,"Бельэтаж","1",(i+1).ToString(),i*20,370-i*110d/40,100,"#7AA8D9",null,"","",null))).ToArray();
        var hall=new HallView{ShowInactivePlaceholders=false};hall.Load(seats,[new SectorInfo("Бельэтаж",0,240)],true);
        var drawing=(DrawingGroup)typeof(HallView).GetField("drawing",Private)!.GetValue(hall)!;
        var heading=Glyphs(drawing).Single(g=>Math.Abs(g.GlyphRun.FontRenderingEmSize-15.6)<.01);var ink=heading.Bounds;
        var window=new Window{Title="UI025 local sector heading",Width=1000,Height=650,Content=hall};window.Show();Pump();
        try
        {
            Capture(window,"025-slanted-local-caption");
            var upper=seats.Where(s=>s.Sector=="Бельэтаж"&&s.X<ink.Right&&s.X+16>ink.Left).Min(s=>s.Y);
            var formatted=(FormattedText)typeof(HallView).GetMethod("Text",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,["Бельэтаж",15.6,Brushes.SlateGray])!;formatted.TextAlignment=TextAlignment.Center;
            var outlines=formatted.BuildGeometry(new Point()).Bounds;var native=new DrawingGroup();using(var context=native.Open())context.DrawText(formatted,new Point());
            Console.WriteLine($"025 caption metrics: rendered={ink}; upper={upper}; clearance={upper-ink.Bottom}; outlineRelative={outlines}; nativeRelative={native.Bounds}");
            Check(ink.Top>253&&ink.Top>240&&ink.Left>=0&&ink.Right<=816,"slanted sector heading moves below old title into own inter-zone gap");
            Check(seats.All(s=>!ink.IntersectsWith(new Rect(s.X,s.Y,16,16))),"local sector heading does not overlap either zone seats");
            Check(upper-ink.Bottom>=43.9,"local upper-envelope keeps two standard spacings above own seats");
        }
        finally{window.Close();}
    }
    private static void CheckFreshDraftUndo(Store store,Assembly assembly)
    {
        var type=assembly.GetType("Tickets.App.HallEditorWindow")!;var editor=(Window)Activator.CreateInstance(type,[store,null])!;editor.Show();Pump();
        try
        {
            var hall=(HallView)type.GetField("preview",Private)!.GetValue(editor)!;
            var notify=(Action<long,Point>)typeof(HallView).GetField("SeatContextRequested",Private)!.GetValue(hall)!;
            int CountDrawn()=>((IEnumerable<SeatInfo>)typeof(HallView).GetField("seats",Private)!.GetValue(hall)!).Count();
            notify(1,new Point());hall.ContextMenu!.IsOpen=false;type.GetMethod("Undo",Private)!.Invoke(editor,null);Pump();
            Check(((IEnumerable<DraftSeat>)type.GetField("seats",Private)!.GetValue(editor)!).Count()==0&&CountDrawn()==1,"undo materialization restores initial live draft");
            var places=(TextBox)type.GetField("placesInput",Private)!.GetValue(editor)!;places.Text="3";Pump();
            Check(CountDrawn()==3,"row edits after undo regenerate the live initial-sector preview");
            notify(1,new Point());hall.ContextMenu!.IsOpen=false;
            var rowNotify=(Action<long,Point>)typeof(HallView).GetField("RowContextRequested",Private)!.GetValue(hall)!;rowNotify(1,new Point());hall.ContextMenu!.IsOpen=false;
            hall.ContextMenu.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Сделать проход")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();places.Text="4";Pump();
            Check(CountDrawn()==0,"deliberately deleting last row never regenerates draft seats");
        }
        finally{editor.Close();Pump();}
    }
    private static void CaptureEmbedded(Window editor,string name)
    {
        if(Application.Current.MainWindow is not MainWindow main||string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKETS_UI_CAPTURE_DIR"))){editor.Close();return;}
        var separate=main.SeparateWindows;var width=main.Width;var height=main.Height;main.SeparateWindows=false;main.Width=1100;main.Height=850;
        Exception? error=null;
        main.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{try{Pump();Capture(main,name);}catch(Exception ex){error=ex;}finally{editor.Close();}}));
        try{((EditorWindow)editor).ShowDialog();if(error is not null)throw error;}
        finally{main.SeparateWindows=separate;main.Width=width;main.Height=height;}
    }
    internal static void Capture(Window window,string name)
    {
        var folder=Environment.GetEnvironmentVariable("TICKETS_UI_CAPTURE_DIR");if(string.IsNullOrWhiteSpace(folder))return;Directory.CreateDirectory(folder);window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(folder,name+".png"));encoder.Save(file);
    }
    internal static void ConfirmSender(Action action,bool accept)
    {
        var confirmed=false;var timer=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(50)};
        timer.Tick+=(_,_)=>EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out var process);if(process!=(uint)Environment.ProcessId)return true;var title=new StringBuilder(256);GetWindowText(window,title,title.Capacity);if(title.ToString()!="Изменение отправителя")return true;confirmed=true;PostMessage(window,0x111,new IntPtr(accept?6:7),IntPtr.Zero);return false;},IntPtr.Zero);
        Program.ExpectedConfirmationTitle="Изменение отправителя";timer.Start();try{action();}finally{timer.Stop();Program.ExpectedConfirmationTitle=null;}Check(confirmed,"sender change explicitly confirmed");
    }
    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing){if(drawing is GlyphRunDrawing glyph)yield return glyph;else if(drawing is DrawingGroup group)foreach(var child in group.Children)foreach(var g in Glyphs(child))yield return g;}
    private static IEnumerable<DependencyObject> Walk(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr window,StringBuilder text,int capacity);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
}
