using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using Tickets.Core;
using EventInfo=Tickets.Core.EventInfo;

internal static class Program
{
    internal static bool expectedSaveDialog;
    internal static string? ExpectedConfirmationTitle;
    [STAThread]
    private static int Main(string[] args)
    {
        var work=Path.Combine(Path.GetTempPath(),"Tickets-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Tickets;component/UiResources.xaml",UriKind.Relative)});
        var dialogGuard=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(500)};
        dialogGuard.Tick+=(_,_)=>EnumWindows((window,_)=>{
            GetWindowThreadProcessId(window,out var process);var name=new System.Text.StringBuilder(128);GetClassName(window,name,name.Capacity);
            if(process!=(uint)Environment.ProcessId||name.ToString()!="#32770")return true;
            var dialogTitle=new System.Text.StringBuilder(256);GetWindowText(window,dialogTitle,dialogTitle.Capacity);
            if(expectedSaveDialog&&dialogTitle.ToString()=="Сохранить документ")return true;
            if(ExpectedConfirmationTitle is not null&&dialogTitle.ToString()==ExpectedConfirmationTitle)return true;
            var messages=new List<string>();
            EnumChildWindows(window,(child,_)=>{var text=new System.Text.StringBuilder(2048);GetWindowText(child,text,text.Capacity);if(text.Length>0)messages.Add(text.ToString());return true;},IntPtr.Zero);
            Console.Error.WriteLine("Unexpected modal dialog: "+string.Join(" | ",messages));Environment.Exit(1);return false;
        },IntPtr.Zero);dialogGuard.Start();
        try
        {
            if(args.Contains("--corrections044")){Corrections044UiChecks.Run(args[0]);return 0;}
            if(args.Contains("--layout043")){HallLayout043UiChecks.Run(args[0]);return 0;}
            if(args.Contains("--layout042")){HallLayout042UiChecks.Run(args[0]);return 0;}
            if(args.Contains("--ticketpro-queue041")){TicketPro041QueueChecks.Run();return 0;}
            if(args.Contains("--ticketpro-target041")){TicketPro041TargetChecks.Run();return 0;}
            if(args.Contains("--ticketpro-selection041")){TicketPro041UiChecks.Run(args[0]);return 0;}
            if(args.Contains("--ticketpro-selected-live041")){TicketPro041LiveChecks.Run(args[0],args[2]);return 0;}
            if(args.Contains("--ticketpro-scheme040")){TicketPro040SchemeChecks.Run();return 0;}
            if(args.Contains("--ticketpro-catalog039")){TicketPro039CatalogChecks.Run();return 0;}
            if(!args.Contains("--corrections032",StringComparer.Ordinal)&&!args.Contains("--corrections033",StringComparer.Ordinal)){Hall013Checks.Run();DocumentEditor019Checks.Run();}
            var db=Path.Combine(work,"tickets.db");File.Copy(args[0],db);var store=new Store(db);
            var main=new Tickets.App.MainWindow(store);app.MainWindow=main;Check(!main.SeparateWindows,"embedded forms are the default");main.SeparateWindows=true;main.WindowState=WindowState.Normal;main.Width=1420;main.Height=880;main.Show();Pump();
            if(args.Contains("--ticketpro-live038",StringComparer.Ordinal)){TicketPro038LiveChecks.Run(store,main,args[2]);main.Close();return 0;}
            if(args.Contains("--ticketpro037",StringComparer.Ordinal)){TicketPro037UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections033",StringComparer.Ordinal)){Documents035UiChecks.Run(store,main);Transfer033UiChecks.Run(store,main);TicketlandConnection028UiChecks.Run(store,main);TicketlandReview031UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections032",StringComparer.Ordinal)){TicketlandConnection028UiChecks.Run(store,main);TicketlandReview031UiChecks.Run(store,main);TicketlandMatching032UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections031",StringComparer.Ordinal)){TicketlandConnection028UiChecks.Run(store,main);TicketlandReview031UiChecks.Run(store,main);HallConflicts031UiChecks.Run(store);HallCaption031UiChecks.Run(store);HallCaption029UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections030",StringComparer.Ordinal)){TicketlandConnection028UiChecks.Run(store,main);HallStatus030UiChecks.Run(store,main);FutureSessionDeletion030UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections029",StringComparer.Ordinal)){TicketlandConnection028UiChecks.Run(store,main);Hall027UiChecks.Run(store);HallCaption029UiChecks.Run(store,main);HallCaption029UiChecks.CheckCatalogEntryPoint(store,main);main.Close();return 0;}
            if(args.Contains("--corrections028",StringComparer.Ordinal)){PrintSchedule028UiChecks.Run(store,main);ReturnedDetails028UiChecks.Run(store,main);TicketlandCredentials028UiChecks.Run();TicketlandConnection028UiChecks.Run(store,main);HallRename0281UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections027",StringComparer.Ordinal)){Hall027UiChecks.Run(store);ScheduleReturns027UiChecks.Run(store);Document027UiChecks.Run(store,main);Main027UiChecks.Run(store,main);main.Close();return 0;}
            if(args.Contains("--corrections025",StringComparer.Ordinal)){Corrections025UiChecks.Run(store);Corrections026UiChecks.Run(store,main);main.Close();return 0;}
            var actions=Logical(main).OfType<Button>().ToList();
            Check(actions.Count>=9,"main navigation actions found without duplicate home buttons");
            Check(!actions.Any(b=>Equals(b.Content,"Спецификации")||Equals(b.Content,"Свернуть")||Equals(b.Content,"В окно")),"main toolbar omits duplicate window controls and specifications");
            Check(actions.Where(b=>b.IsVisible).All(b=>b.ActualWidth==200&&b.ActualHeight==44),"home and navigation actions keep the shared rendered size");
            foreach(var button in actions.Where(b=>b.IsVisible))
            {
                var label=Visual(button).OfType<TextBlock>().FirstOrDefault();if(label is null)continue;
                var bounds=label.TransformToAncestor(button).TransformBounds(new Rect(0,0,label.ActualWidth,label.ActualHeight));
                Check(bounds.Left>=-1&&bounds.Top>=-1&&bounds.Right<=button.ActualWidth+1&&bounds.Bottom<=button.ActualHeight+1,"button text fits fixed dimensions: "+button.Content);
            }
            store.SavePerformance("ТЕАТР Поисковый спектакль UI");
            var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.EventEditorWindow")!;
            var editor=(Window)Activator.CreateInstance(type,store)!;editor.Show();Pump();
            var combos=Logical(editor).OfType<ComboBox>().ToList();
            Check(combos.All(c=>c.SelectedIndex==-1&&string.IsNullOrEmpty(c.Text))&&Logical(editor).OfType<DatePicker>().Single().SelectedDate is null,"new show opens with every selection date and time empty");
            var fromPerformance=(Window)Activator.CreateInstance(type,store,(long?)store.Performances().First().Id)!;
            try{Check(Logical(fromPerformance).OfType<ComboBox>().First().SelectedItem is PerformanceInfo selectedPerformance&&selectedPerformance.Id==store.Performances().First().Id,"new show opened from a performance uses the known choice");}finally{fromPerformance.Close();}
            Check(combos[0].IsEditable,"performance selection allows typed search");
            combos[0].ApplyTemplate();var performanceEdit=(TextBox)combos[0].Template.FindName("PART_EditableTextBox",combos[0]);performanceEdit.Focus();performanceEdit.Text="поиск";Pump();
            Check(combos[0].Items.Cast<PerformanceInfo>().Any(p=>p.Title=="ТЕАТР Поисковый спектакль UI")&&combos[0].Items.Cast<PerformanceInfo>().All(p=>SearchText.Matches(p.Title,"поиск")),"performance search uses beginning of significant word");
            combos[0].SelectedItem=combos[0].Items.Cast<PerformanceInfo>().Single(p=>p.Title=="ТЕАТР Поисковый спектакль UI");combos[0].IsDropDownOpen=false;
            Check(combos.Count==6,"performance, venue, pricing, hours minutes and seconds selectors");
            Check(combos[3].Items.Count==24&&combos[4].Items.Count==60&&combos[5].Items.Count==60,"complete hours and minutes ranges");
            Check(combos.Skip(3).All(c=>c.SelectedIndex==-1&&string.IsNullOrEmpty(c.Text)),"time has no preset hours minutes or seconds");
            combos[1].SelectedIndex=0;Check(combos[2].Items.Count>0&&combos[2].SelectedIndex==-1,"choosing a venue loads available pricing without choosing one");
            Check(Math.Abs(editor.FontSize-16.9)<.01&&editor.FontWeight==FontWeights.Bold,"event form typography is larger and heavier");
            Check(combos.Skip(3).All(c=>c.IsEditable),"hours minutes and seconds accept manual input");
            var date=Logical(editor).OfType<DatePicker>().Single();Check(date.FontSize==26,"date text is doubled");Click(editor,"Календарь");Pump();
            var popup=(Popup)date.Template.FindName("PART_Popup",date);
            var calendar=Visual(popup.Child).OfType<Calendar>().Single();
            Check(Math.Abs(calendar.LayoutTransform.Value.M11-1.3)<.01&&Math.Abs(calendar.LayoutTransform.Value.M22-1.3)<.01,"calendar popup renders at 130 percent scale");
            Check(calendar.DesiredSize.Width>200&&calendar.DesiredSize.Height>200,"enlarged calendar has usable layout bounds");
            date.IsDropDownOpen=false;
            Exception? nestedError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var pricing=app.Windows.Cast<Window>().Single(w=>w.Title=="Расценовка");
                try
                {
                    Check(((VenueInfo)Logical(pricing).OfType<ComboBox>().First().SelectedItem).Id==((VenueInfo)combos[1].SelectedItem).Id,"pricing creation uses chosen venue");
                    Logical(pricing).OfType<TextBox>().First().Text="UI test pricing";
                    Click(pricing,"Назначить всему залу");Click(pricing,"Сохранить");
                }
                catch(Exception ex){nestedError=ex;pricing.Close();}
            }));
            Click(editor,"Создать");Pump();if(nestedError is not null)throw nestedError;
            Check(combos[2].SelectedItem is PlanInfo plan&&plan.Name=="UI test pricing","new pricing selected after nested editor saves");
            date.SelectedDate=DateTime.Today.AddDays(3);combos[3].Text="19";combos[4].Text="00";combos[5].Text="00";Click(editor,"Добавить дату");date.SelectedDate=DateTime.Today.AddDays(4);combos[3].Text="20";combos[4].Text="45";combos[5].Text="12";Click(editor,"Добавить дату");
            var dateList=Logical(editor).OfType<ListBox>().Single();Check(dateList.Items.Count==2,"multiple showing dates accumulate in one editor");
            Check(dateList.Items.Cast<DateTime>().Any(d=>d.Hour==20&&d.Minute==45&&d.Second==12),"each added date retains its own hours minutes and seconds");editor.Close();
            Hall027UiChecks.Run(store);
            var specType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.SpecificationWindow")!;
            var specWindow=(Window)Activator.CreateInstance(specType,store,"UI organization")!;specWindow.Show();Pump();
            var specFields=Logical(specWindow).OfType<TextBox>().Where(t=>t.Parent is StackPanel).ToArray();specFields[^1].Text="Билеты: Партер, ряд А. Условия согласованы.";
            var manualDocument=Path.Combine(work,"chosen-documents","specification.docx");Directory.CreateDirectory(Path.GetDirectoryName(manualDocument)!);
            SaveDocumentThroughDialog(specWindow,manualDocument);Console.WriteLine($"Manual save expected={manualDocument}; exists={File.Exists(manualDocument)}; remembered={store.DocumentSaveDirectory}; form={string.Join(" | ",Logical(specWindow).OfType<TextBlock>().Select(t=>t.Text).Where(t=>t.Contains("Word")||t.Contains("сохранён")))}; files={string.Join(" | ",Directory.GetFiles(work,"*.docx",SearchOption.AllDirectories))}");Check(File.Exists(manualDocument)&&string.Equals(Path.GetFullPath(store.DocumentSaveDirectory),Path.GetFullPath(Path.GetDirectoryName(manualDocument)!),StringComparison.OrdinalIgnoreCase),"manual Save writes selected path and remembers chosen document folder");
            Check(!Logical(specWindow).OfType<Button>().Any(button=>Equals(button.Content,"Сохранить в Word…")),"specification has one save action");Check(store.Specifications().Single().Recipient=="UI organization","organization specification persists from form");
            Check(File.Exists(Path.Combine(store.DocumentsDirectory,"Спецификации","Спецификация-1.docx")),"specification automatically saves Word");specWindow.Close();
            var reopened=(Window)Activator.CreateInstance(specType,store,"UI organization")!;reopened.Show();Pump();
            Check(Logical(reopened).OfType<TextBox>().Any(t=>t.Text.Contains("Условия согласованы")),"saved organization specification reopens");reopened.Close();
            var venueId=store.CreateHall("UI selection hall","UI selection venue address",new[]{
                new DraftSeat{Row="1",Number="1",X=0,Y=0},new DraftSeat{Row="1",Number="2",X=30,Y=0},
                new DraftSeat{Row="2",Number="1",X=0,Y=30},new DraftSeat{Row="2",Number="2",X=30,Y=30}});
            var seatIds=store.VenueSeats(venueId).Select(s=>s.Id).ToArray();
            var planId=store.SavePlan(venueId,"UI plan",seatIds.Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
            var eventId=store.CreateEvent(store.SavePerformance("UI event"),venueId,planId,DateTime.Today.AddDays(1).AddHours(19));
            var events=(TreeView)main.FindName("EventsList");
            typeof(Tickets.App.MainWindow).GetField("events",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,store.Events());
            typeof(Tickets.App.MainWindow).GetMethod("ApplyFilter",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);main.SelectEvent(eventId);Pump();
            var performanceGroup=events.Items.OfType<TreeViewItem>().First(i=>i.Header is TextBlock text&&text.Text=="UI event");performanceGroup.IsExpanded=false;performanceGroup.IsSelected=true;Pump();
            Check(performanceGroup.Header is TextBlock performanceTitle&&performanceTitle.Background==Brushes.SteelBlue&&performanceTitle.Foreground==Brushes.White,"selected performance title has white text on blue");
            Check(performanceGroup.IsExpanded,"selecting a performance title opens its shows on one click");main.SelectEvent(eventId);Pump();
            var six=new Tickets.App.HallView();var sixWindow=new Window{Content=six,Width=600,Height=400};sixWindow.Show();Pump();
            var sixSeats=Enumerable.Range(0,6).Select(i=>new SeatInfo(i+1,"Партер",(i/3+1).ToString(),(i%3+1).ToString(),i%3*20,i/3*20,1000,"#00E676",null,"","",null)).ToArray();
            six.Load(sixSeats,Array.Empty<SectorInfo>(),true);Pump();sixWindow.Activate();six.Focus();
            Gesture(six,new Point(15,1),new Point(41,21));Check(six.Selected.Count==6,"partial rectangular overlap selects all six touched places");
            Gesture(six,new Point(1,1),new Point(35,35));Check(six.Selected.SetEquals(new long[]{3,6}),"drag from selected place removes a group and preserves others");CheckSeatRenderingAndContext(six,sixSeats);CheckHallNavigation(six,sixSeats);sixWindow.Close();
            var currentShowRow=performanceGroup.Items.OfType<TreeViewItem>().Single(i=>i.Tag is EventInfo e&&e.Id==eventId);
            Check(currentShowRow.Header is TextBlock selectedTitle&&Equals(selectedTitle.Foreground,Brushes.White)&&Equals(selectedTitle.Background,Brushes.DarkSlateBlue),"selected show text has contrast against its background");
            Check(((TextBlock)currentShowRow.Header).Inlines.OfType<Run>().First().FontSize==19,"main tree event dates use approved enlarged type");
            var hall=(Tickets.App.HallView)main.FindName("Hall");CheckSelectionSidebar(main);CheckMainPresentation(main,store,seatIds);main.Activate();hall.Focus();hall.Fit();Pump();
            ((ComboBox)main.FindName("RecipientBox")).Text="UI context availability";hall.SetSelection(seatIds.Take(2));
            var mainContextCalls=0;hall.SeatContextRequested+=(_,_)=>mainContextCalls++;
            Gesture(hall,new Point(8,8),new Point(8,8),true);
            Console.WriteLine($"Main context: callbacks={mainContextCalls}; menuNull={hall.ContextMenu is null}; menuOpen={hall.ContextMenu?.IsOpen}; active={main.IsActive}; hall={hall.ActualWidth}x{hall.ActualHeight}; mouse={Mouse.GetPosition(hall)}; over={Mouse.DirectlyOver?.GetType().Name}");
            Check(hall.Selected.SetEquals(seatIds.Take(2)),"seat context preserves an existing selected group");
            var seatMenu=hall.ContextMenu;Check(seatMenu is not null&&seatMenu.IsOpen,"right click opens main seat actions");
            var contextItems=seatMenu!.Items.OfType<MenuItem>().ToArray();
            Check(new[]{"IssueButton","PrintTicketsButton","ReserveButton","SaleStateButton","ReturnButton"}.All(name=>contextItems.Any(item=>Equals(item.Header,((Button)main.FindName(name)).Content))==((Button)main.FindName(name)).IsEnabled)&&contextItems.All(item=>item.IsEnabled),"context menu shows only available issuance printing reservation sale and return actions");
            seatMenu.IsOpen=false;hall.Clear();((ComboBox)main.FindName("RecipientBox")).Text="";
            Gesture(hall,new Point(8,8),new Point(8,8));
            Gesture(hall,new Point(38,38),new Point(38,38));
            Check(hall.Selected.SetEquals(new[]{seatIds[0],seatIds[3]}),"separate single clicks accumulate places");
            Gesture(hall,new Point(23,-4),new Point(51,21));
            Check(hall.Selected.SetEquals(new[]{seatIds[0],seatIds[1],seatIds[3]}),"left drag adds seats without Shift and preserves previous choices");
            Gesture(hall,new Point(-4,23),new Point(21,51));
            Check(hall.Selected.Count==4,"another left drag accumulates another group");
            Gesture(hall,new Point(38,38),new Point(38,38));
            Check(hall.Selected.Count==3&&!hall.Selected.Contains(seatIds[3]),"repeat click deselects one place only");
            SetSelectionPrice(main,123);
            Check(hall.Selected.Count==0,"applying price clears selection");
            Check(new Store(db).Seats(eventId).Where(s=>seatIds.Take(3).Contains(s.Id)).All(s=>s.Amount==123&&s.Color==store.PriceColors().Single(c=>c.Amount==123).Color),"price and category color persist and update scheme data");
            Click(main,"Отменить изменение цены");Check(store.Seats(eventId).All(s=>s.Amount==500),"price undo restores previous category");
            hall.SetSelection(seatIds.Take(3));SetSelectionPrice(main,123);hall.SetSelection(seatIds.Take(3));
            Check(((ComboBox)main.FindName("RecipientBox")).Text=="","show opens without a previous recipient");
            foreach(var expander in Logical(main).OfType<Expander>())expander.IsExpanded=true;Pump();
            Exception? inlineRecipientError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? creation=null;try{
                creation=app.Windows.Cast<Window>().Single(w=>w.GetType().Name=="RecipientEditorWindow"&&w.IsVisible);
                Logical(creation).OfType<TextBox>().First().Text="Новый получатель формы";Click(creation,"Создать");
            }catch(Exception ex){inlineRecipientError=ex;creation?.Close();}}));Click(main,"Создать получателя");if(inlineRecipientError is not null)throw inlineRecipientError;
            Check(((ComboBox)main.FindName("RecipientBox")).Text=="Новый получатель формы"&&store.Recipients().Contains("Новый получатель формы"),"recipient can be created and selected during issuance");
            var senderBox=(ComboBox)main.FindName("SenderBox");senderBox.Text="UI sender created from main";
            Exception? senderCreationError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? creation=null;try{
                creation=app.Windows.Cast<Window>().Single(w=>w.GetType().Name=="SenderEditorWindow"&&w.IsVisible);
                var inputs=Logical(creation).OfType<TextBox>().ToArray();Check(inputs.Length>1&&!inputs[0].IsReadOnly,"main sender creation includes editable name and requisites");
                inputs[0].Text="UI sender created from main";Click(creation,"Создать");
            }catch(Exception ex){senderCreationError=ex;creation?.Close();}}));Corrections025UiChecks.ConfirmSender(()=>Click(main,"Создать / изменить отправителя"),true);if(senderCreationError is not null)throw senderCreationError;
            Check(senderBox.Text=="UI sender created from main"&&store.Senders().Contains(senderBox.Text),"main sender creation persists and selects sender");senderBox.Text=Store.DefaultSender;
            Corrections025UiChecks.ConfirmSender(()=>typeof(Tickets.App.MainWindow).GetMethod("ConfirmSender025",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null),true);
            ((ComboBox)main.FindName("RecipientBox")).Text="UI recipient";
            store.SaveOrganizationDetails("UI recipient",false,new OrganizationDetails(Name:"UI recipient",Inn:"UI-REQUISITES-MUST-NOT-APPEAR",Signatory:"UI invoice signatory",Position:"UI director"));
            var summaryLabel=(Tickets.App.CompactText043)main.FindName("SelectedSummary");Check(Math.Abs(summaryLabel.FontSize-16.8)<.01&&summaryLabel.FontWeight==FontWeights.Bold&&summaryLabel.Foreground.ToString()=="#FFC1121F","selected quantity and sum retain bold red styling at 70 percent size");
            var issueButton=(Button)main.FindName("IssueButton");Check(issueButton.IsEnabled,"priced selection ready for issuance");
            Exception? invoiceError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var window=app.Windows.Cast<Window>().Single(w=>w.GetType().Name=="InvoiceWindow");
                try{
                    var document=Logical(window).OfType<FlowDocumentScrollViewer>().Single().Document;
                    var text=new TextRange(document.ContentStart,document.ContentEnd).Text;
                    Check(text.Contains("UI recipient")&&text.Contains("369"),"invoice opens immediately with actual prices and correct total");
                    Check(!Logical(window).OfType<ListBox>().Any(),"invoice has no return-ticket list");
                    Check(!text.Contains("UI-REQUISITES-MUST-NOT-APPEAR")&&!text.Contains("Банковские реквизиты")&&!text.Contains("ИНН")&&!text.Contains("Состояние"),"invoice preview omits party requisites and ticket state");
                    Check(text.Replace('\u00a0',' ').Contains("UI invoice signatory"),"invoice retains the actual recipient signatory on an unbroken line");
                    Check(Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Открыть в Word")),"invoice offers opening the saved Word document");
                    CheckDocumentTableLayout(Logical(window).OfType<FlowDocumentScrollViewer>().Single(),"invoice");
                    Check(!Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Сохранить как…")),"invoice has no duplicate Save As action");
                    var compactSave=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить"));var compactPrint=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Печать"));
                    Check(ReferenceEquals(compactSave.Parent,compactPrint.Parent)&&compactSave.Parent is StackPanel {Orientation:Orientation.Horizontal}&&compactSave.FontSize==16.9&&compactPrint.FontSize==16.9,"document save and print share one compact row without shrinking type");
                    CheckDocumentActions(window);
                    CheckNativePrintPreview(app,window);
                    var invoiceTicketPrint=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Печать билетов"));
                    Check(ReferenceEquals(invoiceTicketPrint.Parent,((FrameworkElement)compactSave.Parent).Parent)&&DockPanel.GetDock(invoiceTicketPrint)==Dock.Right&&Math.Abs(invoiceTicketPrint.ActualHeight-compactPrint.ActualHeight)<1&&!Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Дополнить накладную")),"one compact invoice ticket-print action sits at right on the document action row");
                    Check(Logical(window).OfType<FlowDocumentScrollViewer>().Single().ActualHeight>400,"invoice preview uses enlarged content area");
                    var invoiceTab=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Накладная"));Check(Equals(invoiceTab.Background,Brushes.SteelBlue),"invoice tab is visibly active");
                    var senderField=Logical(window).OfType<ComboBox>().Single(b=>b.IsEditable);Check(senderField.IsEditable&&senderField.Items.Contains(Store.DefaultSender),"invoice sender supports saved choice and manual entry");
                    senderField.Text="UI sender";Corrections025UiChecks.ConfirmSender(()=>SaveDocumentThroughDialog(window,Path.Combine(work,"chosen-documents","invoice.docx")),true);
                    Check(File.Exists(Path.Combine(work,"chosen-documents","invoice.docx")),"invoice manual Save writes its selected destination");
                    var issuedId=store.Issues(eventId).Single().Id;
                    Check(store.Invoice(eventId,issuedId).Sender=="UI sender","edited invoice sender persists");
                    Check(store.PrepareIssueDocuments(eventId,issuedId).Count==4&&store.PrepareIssueDocuments(eventId,issuedId).All(d=>d.Invoice.Recipient=="UI recipient"&&d.Body.Contains("UI recipient")),"all four documents are automatically prepared for invoice recipient");
                    Check(text.Contains(DocumentTotals.Summary(3,369))&&text.Contains(DocumentTotals.Total(3,369)),"invoice totals use shared two-line format");
                    Exception? documentError=null;
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                        var form=app.Windows.Cast<Window>().Single(w=>w.IsVisible&&(w.GetType().Name is "CustomIssueDocumentWindow" or "IssueDocumentWindow"));
                        try{
                            Check(Equals(Logical(form).OfType<Button>().Single(b=>Equals(b.Content,"Договор")).Background,Brushes.SteelBlue)&&Logical(form).OfType<Button>().Any(b=>Equals(b.Content,"Накладная")),"contract toolbar marks active document and offers invoice");
                            var rendered=Logical(form).OfType<FlowDocumentScrollViewer>().Single().Document;var contractText=new TextRange(rendered.ContentStart,rendered.ContentEnd).Text;
                            Check(contractText.Contains("UI recipient"),"contract preview substitutes actual recipient");
                            Check(!contractText.Contains("Билеты и места"),"contract omits the ticket item table");
                            Check(Logical(form).OfType<Button>().Any(b=>Equals(b.Content,"Открыть в Word")),"contract offers opening the saved Word document");
                            CheckDocumentTableLayout(Logical(form).OfType<FlowDocumentScrollViewer>().Single(),"contract");
                            Logical(form).OfType<TabControl>().Single().SelectedIndex=1;
                            var nativeEditor=Logical(form).OfType<RichTextBox>().Single();
                            var originalTemplate=CustomDocumentForms.Text(store.CustomIssueTemplate(eventId,issuedId,IssueDocumentKinds.Contract)!);
                            var protectedCount=System.Text.RegularExpressions.Regex.Matches(originalTemplate,@"\{\w+\}").Count;
                            Check(new TextRange(nativeEditor.Document.ContentStart,nativeEditor.Document.ContentEnd).Text.Contains("avto"),"contract native editor displays protected automatic fields");
                            var ordinary=nativeEditor.Document.Blocks.OfType<Paragraph>().SelectMany(p=>p.Inlines.OfType<Run>()).First(r=>!string.IsNullOrWhiteSpace(r.Text)&&r.Text!="avto");
                            ordinary.Text+=" UI019 ordinary edit";Click(form,"Сохранить текст документа");Click(form,"Сохранить как форму получателя");
                            var savedTemplate=CustomDocumentForms.Text(store.RecipientForm("UI recipient",IssueDocumentKinds.Contract)!.Content);
                            Check(savedTemplate.Contains("UI019 ordinary edit")&&System.Text.RegularExpressions.Regex.Matches(savedTemplate,@"\{\w+\}").Count==protectedCount,"native ordinary text and protected token identities save as recipient template for later issuances");
                        }catch(Exception ex){documentError=ex;}finally{form.Close();}
                    }));Click(window,"Договор");if(documentError is not null)throw documentError;
                    var paginator=((IDocumentPaginatorSource)CloneFlowDocument(document)).DocumentPaginator;paginator.ComputePageCount();
                    Check(paginator.PageCount>=1&&paginator.GetPage(0).Visual is not null,"invoice paginates for printing");
                    Check(text.Contains(Store.DefaultSender)&&text.Contains("триста шестьдесят девять рублей")&&!text.Contains("ТЕСТ 000001"),"invoice has sender and words without ticket serials");
                    Check(Directory.GetFiles(Path.Combine(store.DocumentsDirectory,"Накладные"),"*.docx").Length==1,"invoice automatically saves Word inside program data");
                    Check(Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Сохранить")),"Word save action available");
                    Check(IssueDocumentKinds.All.All(k=>File.Exists(Path.Combine(store.DocumentsDirectory,IssueDocumentKinds.Name(k),$"{IssueDocumentKinds.Name(k)}-{issuedId}.docx"))),"all supporting documents automatically save Word");
                }catch(Exception ex){invoiceError=ex;}finally{window.Close();}
            }));
            issueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();if(invoiceError is not null)throw invoiceError;
            var history=(DataGrid)main.FindName("HistoryGrid");Check(history.Items.Count==1,"one journal row for one issuance");
            Check(store.History(eventId).All(t=>t.Amount==123),"UI price overrides were persisted");
            hall.SetSelection(new[]{seatIds[0]});Pump();
            Check(hall.Selected.SetEquals(new[]{seatIds[0]}),"occupied-only selection remains available for original invoice and return");
            var printButton=(Button)main.FindName("PrintTicketsButton");Check(!printButton.IsEnabled,"issued place cannot print from generic selection");
            typeof(Tickets.App.MainWindow).GetMethod("OpenSeatContext",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{seatIds[0],new Point(20,20)});Pump();
            Check(((TextBlock)main.FindName("StatusText")).Text.Contains("UI recipient"),"occupied place status identifies original recipient");
            Check(hall.ContextMenu!.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Открыть исходную накладную"))&&!hall.ContextMenu.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Печать билетов")),"occupied context exposes original invoice without generic printing");hall.ContextMenu.IsOpen=false;
            hall.SetSelection(new[]{seatIds[3]});((ComboBox)main.FindName("RecipientBox")).Text="UI print preview only";Pump();
            Check(printButton.IsVisible&&printButton.IsEnabled&&Equals(printButton.Content,"Печать билетов"),"primary ticket printing action is visible and available");
            var freePreviewSelection=hall.Selected.ToArray();var freePreviewSummary=((Tickets.App.CompactText043)main.FindName("SelectedSummary")).Text;
            var ticketPrintType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketPrintWindow")!;Exception? ticketPreviewError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? previewWindow=null;try{
                previewWindow=app.Windows.Cast<Window>().Single(w=>w.GetType()==ticketPrintType&&w.IsVisible);
                Check(Logical(previewWindow).OfType<DocumentPageView>().Any(),"ticket printing opens actual per-ticket page preview");
                Check(store.History(eventId).Count==3,"opening print preview creates no issuance");
            }catch(Exception ex){ticketPreviewError=ex;}finally{previewWindow?.Close();}}));Click(main,"Печать билетов");if(ticketPreviewError is not null)throw ticketPreviewError;
            Check(store.History(eventId).Count==3&&!store.Recipients().Contains("UI print preview only"),"closing ticket print preview reserves neither seats nor recipient");
            Check(hall.Selected.SetEquals(freePreviewSelection)&&((Tickets.App.CompactText043)main.FindName("SelectedSummary")).Text==freePreviewSummary,"closing free-seat print preview retains selection and summary");
            var originalInvoice=store.Invoice(eventId,store.Issues(eventId).Single().Id);
            CheckTicketBlankPreview(store,eventId,originalInvoice);
            Print014Checks.Run(store);
            FourUp015Checks.Run(store);
            void Isolated(string name,Action<Store> check)
            {
                var folder=Path.Combine(work,name);Directory.CreateDirectory(folder);var isolatedDb=Path.Combine(folder,"tickets.db");File.Copy(args[0],isolatedDb);check(new Store(isolatedDb));
            }
            Isolated("blank016",Blank016Checks.Run);
            Isolated("docs016",Docs016Checks.Run);
            Isolated("print016",Print016Checks.Run);
            Isolated("journal016",Journal016Checks.Run);
            Isolated("blank017",Blank017Checks.Run);
            Isolated("docs017",Docs017Checks.Run);
            Isolated("docs021",Docs021UiChecks.Run);
            Isolated("pricing021",Pricing021UiChecks.Run);
            Isolated("hall021",HallEditor021Checks.Run);
            Isolated("schedule-returns027",ScheduleReturns027UiChecks.Run);
            Isolated("document027",s=>Document027UiChecks.Run(s,main));
            Isolated("main027",s=>{
                var isolatedMain=new Tickets.App.MainWindow(s);isolatedMain.Show();Pump();
                try{Main027UiChecks.Run(s,isolatedMain);}finally{isolatedMain.Close();}
            });
            Isolated("hall024",Hall024Checks.Run);
            Isolated("corrections025",Corrections025UiChecks.Run);
            Isolated("recipients024",Recipients024Checks.Run);
            Isolated("ui023",Ui023Checks.Run);
            Isolated("ui022",Ui022Checks.Run);
            Isolated("legacygrid022",LegacyHallGrid022Checks.Run);
            Isolated("documents018",RecipientDocuments018Checks.Run);
            Isolated("journal017",Journal017Checks.Run);
            Isolated("issuance018",s=>{
                var isolatedMain=new Tickets.App.MainWindow(s);isolatedMain.Show();Pump();
                try{Issuance018Checks.Run(s,isolatedMain);}finally{isolatedMain.Close();}
            });
            Isolated("invoice-editor019",DocumentEditor019Checks.RunInvoice);
            Isolated("document-editor020",DocumentEditor020Checks.Run);
            Isolated("ui020",s=>{
                var isolatedMain=new Tickets.App.MainWindow(s);isolatedMain.Show();Pump();
                try{Ui020Checks.Run(s,isolatedMain);Senders020Checks.Run(s,isolatedMain);}finally{isolatedMain.Close();}
            });
            Isolated("ui019",s=>{
                var isolatedMain=new Tickets.App.MainWindow(s);isolatedMain.Show();Pump();
                try{Ui019Checks.Run(s,isolatedMain);}finally{isolatedMain.Close();}
            });
            Isolated("parties018",Parties018Checks.Run);
            Isolated("merge018",Merge018Checks.Run);
            hall.SetSelection(new[]{seatIds[0],seatIds[3]});((ComboBox)main.FindName("RecipientBox")).Text="UI mixed preview only";Pump();
            Check(hall.Selected.SetEquals(new[]{seatIds[3]})&&printButton.IsEnabled,"mixed selection excludes occupied place and retains free place");
            Exception? mixedPreviewError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? previewWindow=null;try{
                previewWindow=app.Windows.Cast<Window>().Single(w=>w.GetType()==ticketPrintType&&w.IsVisible);
                Check(ticketPrintType.GetField("originalOccupied",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(previewWindow) is null,"generic preview contains no original occupied ticket");
                var page=Logical(previewWindow).OfType<DocumentPageView>().First().DocumentPaginator.GetPage(0).Visual;
                Check(Visual(page).OfType<Border>().Count()==1,"generic preview renders only free selected ticket");
            }catch(Exception ex){mixedPreviewError=ex;}finally{previewWindow?.Close();}}));Click(main,"Печать билетов");if(mixedPreviewError is not null)throw mixedPreviewError;
            Check(hall.Selected.SetEquals(new[]{seatIds[3]}),"closing preview retains free selection without occupied place");
            var invoiceAfterPreview=store.Invoice(eventId,originalInvoice.Id);
            Check(store.History(eventId).Count==3&&store.Issues(eventId).Count==1&&invoiceAfterPreview.Tickets.SequenceEqual(originalInvoice.Tickets)&&invoiceAfterPreview.Sender==originalInvoice.Sender&&!store.Recipients().Contains("UI mixed preview only")&&store.Seats(eventId).Single(s=>s.Id==seatIds[3]).Available,"closing preview preserves original invoice and leaves free seat unissued");
            // Exercise the main-window response to a successful print without submitting a physical print job.
            hall.SetSelection(new[]{seatIds[3]});Exception? printedResultError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? previewWindow=null;try{
                previewWindow=app.Windows.Cast<Window>().Single(w=>w.GetType()==ticketPrintType&&w.IsVisible);
                ticketPrintType.GetProperty("Printed")!.GetSetMethod(true)!.Invoke(previewWindow,new object[]{true});
            }catch(Exception ex){printedResultError=ex;}finally{previewWindow?.Close();}}));Click(main,"Печать билетов");if(printedResultError is not null)throw printedResultError;
            Check(hall.Selected.Count==0,"successful print result clears main selection");
            var extensionType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.InvoiceExtensionWindow")!;
            var extension=(Window)Activator.CreateInstance(extensionType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{store,eventId,originalInvoice.Id},null)!;extension.Show();Pump();
            var extensionHall=Logical(extension).OfType<Tickets.App.HallView>().Single();var extendButton=Logical(extension).OfType<Button>().Single(b=>Equals(b.Content,"Дополнить накладную"));
            Check(extensionHall.ActualWidth>100&&extensionHall.ActualHeight>100&&!extendButton.IsEnabled,"invoice extension renders hall and requires a selection");
            extensionHall.SetSelection(new[]{seatIds[0]});Check(!extendButton.IsEnabled,"invoice extension rejects occupied seat selection");
            extensionHall.SetSelection(new[]{seatIds[3]});Check(extendButton.IsEnabled&&Logical(extension).OfType<TextBlock>().Any(t=>t.Text.Contains("Мест: 1")),"invoice extension enables one free priced seat and updates summary");
            extensionHall.SetSelection(new[]{seatIds[0],seatIds[3]});Check(extendButton.IsEnabled&&extensionHall.Selected.SetEquals(new[]{seatIds[3]}),"invoice extension filters occupied place and retains eligible free place");extension.Close();
            store.SetSeatsBlocked(eventId,new[]{seatIds[3]},true);
            typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{false});hall.SetSelection(new[]{seatIds[3]});
            Check(hall.Selected.SetEquals(new[]{seatIds[3]})&&!issueButton.IsEnabled&&!printButton.IsEnabled&&((Button)main.FindName("SaleStateButton")).IsEnabled,"blocked place can be selected for unblocking but never issued or printed");
            typeof(Tickets.App.MainWindow).GetMethod("OpenSeatContext",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{seatIds[3],new Point(20,20)});Pump();
            Check(hall.ContextMenu!.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Разблокировать")),"withdrawn place context exposes restore action");hall.ContextMenu.IsOpen=false;
            store.SetSeatsBlocked(eventId,new[]{seatIds[3]},false);typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{false});
            var sample=store.TicketReprint(eventId,new[]{seatIds[0]});
            var many=sample with{Tickets=Enumerable.Range(0,25).Select(i=>sample.Tickets[0] with{Number=i+1,Seat=(i+1).ToString()}).ToArray()};
            var printedDocument=(FixedDocument)ticketPrintType.GetMethod("BuildDocument",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{many,794d,1123d})!;
            printedDocument.DocumentPaginator.ComputePageCount();
            Check(printedDocument.Pages.Count==7&&printedDocument.DocumentPaginator.GetPage(0).Visual is not null,"ticket cards paginate without dropping tickets");
            var cardCount=printedDocument.Pages.Sum(p=>p.Child.Children.OfType<Border>().Count());Check(cardCount==25,"each selected ticket has its own printable card");
            var raster=new System.Windows.Media.Imaging.RenderTargetBitmap(794,1123,96,96,PixelFormats.Pbgra32);raster.Render(printedDocument.DocumentPaginator.GetPage(0).Visual);
            var archivedVenue=store.CreateHall("UI archived venue","",new[]{new DraftSeat{Number="1"},new DraftSeat{Number="2",X=26}});
            var archivedSeats=store.VenueSeats(archivedVenue).Select(s=>s.Id).ToArray();var archivePlan=store.SavePlan(archivedVenue,"Archive plan",archivedSeats.Select(id=>new PriceValue(id,1000,store.PriceColors().Single(p=>p.Amount==1000).Color)).ToArray());
            var archivedEvent=store.CreateEvent(store.SavePerformance("UI archived performance"),archivedVenue,archivePlan,DateTime.Today.AddDays(20));store.Issue(archivedEvent,new[]{archivedSeats[0]},"Архив UI организация","ARCHUI",1);
            var reload=typeof(Tickets.App.MainWindow).GetMethod("RefreshCatalogNavigation",BindingFlags.Instance|BindingFlags.NonPublic)!;reload.Invoke(main,null);main.SelectEvent(archivedEvent);Pump();
            store.DeleteVenue(archivedVenue);store.DeleteRecipient("Архив UI организация");reload.Invoke(main,null);Pump();
            Check(events.Items.OfType<TreeViewItem>().Any(i=>i.Header?.ToString()?.StartsWith("Удалённые из справочников")==true)&&((Grid)main.FindName("Workspace")).IsVisible,"deleted shows remain accessible in archive without losing selected context");
            hall.SetSelection(new[]{archivedSeats[0]});Check(hall.Selected.SetEquals(new[]{archivedSeats[0]})&&((Button)main.FindName("ReturnButton")).IsEnabled,"archived occupied place remains selectable for its return and invoice actions");
            typeof(Tickets.App.MainWindow).GetMethod("OpenSeatContext",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{archivedSeats[0],new Point(20,20)});Pump();
            Check(hall.ContextMenu!.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"Открыть исходную накладную")),"archived occupied place retains original invoice access");hall.ContextMenu.IsOpen=false;
            hall.SetSelection(new[]{archivedSeats[1]});((ComboBox)main.FindName("RecipientBox")).Text="Blocked archived issuance";
            Check(!((Button)main.FindName("IssueButton")).IsEnabled&&!((Button)main.FindName("ApplyPriceButton")).IsEnabled,"archived show disables new issuance and price edits");
            main.SelectEvent(store.Events().First(e=>e.Id!=eventId).Id);Pump();
            Check(history.Items.Count==0,"journal hides issuances of other events");
            Check(((ComboBox)main.FindName("RecipientBox")).Text=="","switching shows clears recipient");
            var recipientsType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.RecipientsWindow")!;
            var recipientsWindow=(Window)Activator.CreateInstance(recipientsType,store)!;recipientsWindow.Show();Pump();
            Logical(recipientsWindow).OfType<TextBox>().Single().Text="ui rec";Pump();
            Check(Logical(recipientsWindow).OfType<ListBox>().Single().Items.Count==1,"organization directory searches significant word prefixes");
            Check(Logical(recipientsWindow).OfType<TreeView>().Single().Items.Count>0,"organization directory opens all its issuance history");
            Check(!Logical(recipientsWindow).OfType<Button>().Any(b=>Equals(b.Content,"Спецификация")),"recipient directory has no specification button");
            Exception? newRecipientError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? creation=null;try{
                creation=app.Windows.Cast<Window>().Single(w=>w.GetType().Name=="RecipientEditorWindow"&&w.IsVisible);
                Logical(creation).OfType<TextBox>().First().Text="UI stand-alone recipient";Click(creation,"Создать");
            }catch(Exception ex){newRecipientError=ex;creation?.Close();}}));Click(recipientsWindow,"Создать получателя");if(newRecipientError is not null)throw newRecipientError;
            Check(store.Recipients().Contains("UI stand-alone recipient"),"new recipient saves before any ticket issuance");
            var orgList=Logical(recipientsWindow).OfType<ListBox>().Single();
            Check(!Logical(recipientsWindow).OfType<Button>().Any(b=>Equals(b.Content,"Удалить получателя")),"recipient deletion has no extra action button");
            Check(orgList.ContextMenu!.Items.OfType<MenuItem>().Any(m=>Equals(m.Header,"Удалить")),"recipient deletion is available through context menu");recipientsWindow.Close();
            var directoryType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.PricingDirectoryWindow")!;
            var pricingDirectory=(Window)Activator.CreateInstance(directoryType,store)!;pricingDirectory.Show();Pump();
            var directoryTree=Logical(pricingDirectory).OfType<TreeView>().Single();
            Check(directoryTree.SelectedItem is null&&directoryTree.Items.OfType<TreeViewItem>().All(i=>!i.IsExpanded),"pricing starts with collapsed venue list and no automatic venue");
            var venueNode=directoryTree.Items.OfType<TreeViewItem>().Single(i=>i.Tag is VenueInfo v&&v.Id==venueId);venueNode.IsSelected=true;Pump();
            Check(venueNode.IsExpanded&&venueNode.Items.OfType<TreeViewItem>().All(i=>i.Tag is PlanInfo p&&p.VenueId==venueId),"selecting venue reveals only that venue pricing plans");
            var pricingLeaf=venueNode.Items.OfType<TreeViewItem>().Single(i=>i.Tag is PlanInfo p&&p.Id==planId);
            Check(pricingLeaf.ContextMenu!.Items.OfType<MenuItem>().Any(m=>Equals(m.Header,"Удалить")),"pricing deletion uses context menu");
            Exception? directoryError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? selectedPricing=null;try{
                selectedPricing=app.Windows.Cast<Window>().Single(w=>w.GetType().Name=="PriceEditorWindow"&&w.IsVisible);
                Check(Logical(selectedPricing).OfType<TextBox>().First().Text=="","copied pricing plan requires an explicitly entered new name");
                Check(!Logical(selectedPricing).OfType<CheckBox>().Any(c=>Equals(c.Content,"Расценовки всех площадок")),"cross-venue pricing switch removed");
            }catch(Exception ex){directoryError=ex;}finally{selectedPricing?.Close();}}));
            pricingLeaf.IsSelected=true;if(directoryError is not null)throw directoryError;pricingDirectory.Close();
            var priceEditorType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.PriceEditorWindow")!;
            var enterPriceEditor=(Window)Activator.CreateInstance(priceEditorType,new object?[]{store,(long?)venueId})!;enterPriceEditor.Show();Pump();
            try{Pricing014Checks.Editor(enterPriceEditor);}finally{enterPriceEditor.Close();}
            var categoryEditor=(Window)Activator.CreateInstance(priceEditorType,new object?[]{store,(long?)venueId})!;categoryEditor.Show();Pump();
            var categoryFields=Logical(categoryEditor).OfType<TextBox>().ToList();var categoryHall=Logical(categoryEditor).OfType<Tickets.App.HallView>().Single();
            var planSelector=Logical(categoryEditor).OfType<ComboBox>().Skip(1).First();Check(planSelector.SelectedIndex==-1,"new pricing editor starts without an existing plan selected");
            var amountField=Field(categoryEditor,"Цена, ₽");var colorSelector=Logical(categoryEditor).OfType<ComboBox>().Last();
            var amountBounds=amountField.TransformToAncestor(categoryEditor).TransformBounds(new Rect(amountField.RenderSize));var colorBounds=colorSelector.TransformToAncestor(categoryEditor).TransformBounds(new Rect(colorSelector.RenderSize));
            Check(Math.Abs(amountBounds.Top-colorBounds.Top)<1&&Math.Abs(amountBounds.Height-colorBounds.Height)<1,"pricing amount and color controls render at the same height on one row");
            Check(!Logical(categoryEditor).OfType<Button>().Any(b=>Equals(b.Content,"Весь зал")),"pricing uses empty-space double click instead of a fit button");
            categoryHall.SetSelection(seatIds.Take(2));categoryFields[1].Text="1000";
            categoryFields[1].RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(categoryFields[1]),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();
            Check(categoryHall.Selected.Count==0,"pricing editor clears selection after first price assignment");
            categoryHall.SetSelection(seatIds.Skip(2));categoryFields[1].Text="2000";AssignPrice044(categoryEditor);
            var assigned=(System.Collections.IDictionary)priceEditorType.GetField("prices",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(categoryEditor)!;
            Check(((PriceValue)assigned[seatIds[0]]!).Amount==1000&&((PriceValue)assigned[seatIds[1]]!).Amount==1000&&((PriceValue)assigned[seatIds[2]]!).Amount==2000,"next price assignment preserves earlier differently priced places");
            Check(categoryHall.Selected.Count==0&&!Logical(categoryEditor).OfType<Button>().Any(b=>Equals(b.Content,"Снять выделение")),"pricing uses automatic clearing without extra clear button");
            categoryHall.SetSelection(seatIds.Take(2));Gesture(categoryHall,new Point(8,8),new Point(8,8),true);
            var pricingContext=OpenContextMenu();Check(categoryHall.Selected.SetEquals(seatIds.Take(2))&&pricingContext is not null,"pricing context preserves selected group");pricingContext!.IsOpen=false;
            Gesture(categoryHall,new Point(8,8),new Point(8,8),true);
            Check(categoryHall.Selected.SetEquals(new[]{seatIds[1]})&&OpenContextMenu() is null,"repeated right click removes only its selected seat without reopening the menu");
            for(var categoryIndex=0;categoryIndex<seatIds.Length;categoryIndex++)
            {categoryHall.SetSelection(new[]{seatIds[categoryIndex]});categoryFields[1].Text=((categoryIndex+1)*1000).ToString();AssignPrice044(categoryEditor);}
            Pump();var priceSummary=(Grid)priceEditorType.GetField("priceSummary",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(categoryEditor)!;
            var categoryCells=priceSummary.Children.OfType<Border>().ToArray();
            Check(priceSummary.RowDefinitions.Count==4&&priceSummary.ColumnDefinitions.Count==0&&categoryCells.Length==4,"pricing categories use one sidebar column");
            Check(categoryCells.Select(cell=>(Grid.GetRow(cell),Grid.GetColumn(cell))).SequenceEqual(new[]{(0,0),(1,0),(2,0),(3,0)}),"pricing summary orders categories downward");
            Check(categoryCells.Select(cell=>((Tickets.App.CompactText043)cell.Child).Text).SequenceEqual(new[]{4000L,3000L,2000L,1000L}.Select(amount=>$"{amount}₽×1={amount}₽")),"colored pricing summary uses descending amounts with quantity and total");
            Check(categoryCells.All(cell=>cell.Background is SolidColorBrush&&((Tickets.App.CompactText043)cell.Child).Foreground is SolidColorBrush&&cell.ActualWidth>50&&cell.ActualHeight>10),"pricing summary renders colored readable cells in the sidebar");categoryEditor.Close();
            var copyEditor=(Window)Activator.CreateInstance(priceEditorType,new object?[]{store,(long?)venueId,(long?)planId})!;copyEditor.Show();Pump();
            Check(Field(copyEditor,"Название новой расценовки").Text=="","loaded pricing copy leaves the new name empty");
            var copyHall=Logical(copyEditor).OfType<Tickets.App.HallView>().Single();copyHall.SetSelection(seatIds);Field(copyEditor,"Цена, ₽").Text="777";AssignPrice044(copyEditor);
            Exception? uniqueNameError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                Window? prompt=null;
                try
                {
                    prompt=app.Windows.Cast<Window>().Single(window=>window.Title=="Сохранить новую расценовку"&&window.IsVisible);
                    var input=Logical(prompt).OfType<TextBox>().Single();var apply=Logical(prompt).OfType<Button>().Single(button=>Equals(button.Content,"Применить"));
                    Check(input.Text==""&&!apply.IsEnabled,"new pricing name prompt starts empty with apply disabled");
                    input.Text="   ";Pump();Check(!apply.IsEnabled,"blank pricing name cannot be applied");
                    input.Text="UI plan";Pump();Check(!apply.IsEnabled,"existing pricing name cannot be applied");
                    input.Text="UI unique pending plan";Pump();Check(apply.IsEnabled,"unique pricing name enables apply");
                    foreach(var scale in new[]{1.25,1.5})
                    {
                        ((FrameworkElement)prompt.Content).LayoutTransform=new ScaleTransform(scale,scale);Pump();
                        var bounds=apply.TransformToAncestor((Visual)prompt.Content).TransformBounds(new Rect(apply.RenderSize));
                        Check(bounds.Width>=100&&bounds.Height>=30&&apply.InputHitTest(new Point(apply.ActualWidth/2,apply.ActualHeight/2)) is not null,"pricing prompt apply remains usable at "+scale+" layout scale");
                    }
                }
                catch(Exception ex){uniqueNameError=ex;}
                finally{prompt?.Close();}
            }));
            Click(copyEditor,"Сохранить");if(uniqueNameError is not null)throw uniqueNameError;copyEditor.Close();
            var performanceDirectoryType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.PerformanceWindow")!;
            var performanceDirectory=(Window)Activator.CreateInstance(performanceDirectoryType,store)!;performanceDirectory.Show();Pump();
            var performanceTree=Logical(performanceDirectory).OfType<TreeView>().Single();
            var activePerformance=performanceTree.Items.OfType<TreeViewItem>().Single(i=>i.Tag is PerformanceInfo p&&p.Title=="UI event");activePerformance.BringIntoView();Pump();NativeClick((FrameworkElement)activePerformance.Header);Pump();
            Check(activePerformance.IsExpanded&&Logical(activePerformance).OfType<TreeViewItem>().Any(i=>i.Tag is EventInfo e&&e.Id==eventId),"performance directory opens its active showing list");
            NativeClick((FrameworkElement)activePerformance.Header);Check(!activePerformance.IsExpanded,"repeated performance title click collapses its showing list");
            NativeClick((FrameworkElement)activePerformance.Header);Check(activePerformance.IsExpanded,"another performance title click reopens its showing list");
            Check(activePerformance.ContextMenu!.Items.OfType<MenuItem>().Any(m=>Equals(m.Header,"Удалить"))&&!Logical(performanceDirectory).OfType<Button>().Any(b=>Equals(b.Content,"Удалить спектакль")),"performance deletion uses context menu without extra button");performanceDirectory.Close();
            Exception? leafSelectionError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? catalog=null;try{
                catalog=app.Windows.Cast<Window>().Single(window=>window.GetType()==performanceDirectoryType&&window.IsVisible);
                var tree=Logical(catalog).OfType<TreeView>().Single();
                var group=tree.Items.OfType<TreeViewItem>().Single(item=>item.Tag is PerformanceInfo performance&&performance.Title=="UI event");group.BringIntoView();Pump();NativeClick((FrameworkElement)group.Header);Pump();
                var leaf=Logical(group).OfType<TreeViewItem>().Single(item=>item.Tag is EventInfo showing&&showing.Id==eventId);leaf.BringIntoView();Pump();
                var header=Visual(leaf).OfType<ContentPresenter>().First(presenter=>Equals(presenter.Content,leaf.Header));NativeClick(header);Pump();
                Check(!catalog.IsVisible&&(long)performanceDirectoryType.GetProperty("SelectedEventId")!.GetValue(catalog)! == eventId,"one click on a performance date closes its directory with the selected event");
            }catch(Exception ex){leafSelectionError=ex;catalog?.Close();}}));Click(main,"Спектакли");Pump();if(leafSelectionError is not null)throw leafSelectionError;
            Check(((Tickets.App.CompactText043)main.FindName("EventTitle")).Text=="UI event"&&((Grid)main.FindName("Workspace")).IsVisible,"performance date selection returns to that event in the main window");
            Check(events.Items.OfType<TreeViewItem>().Any(i=>i.Header?.ToString()?.StartsWith("Прошедшие")==true),"past performances have a single expandable section");
            var recipients=(ComboBox)main.FindName("RecipientBox");recipients.Text="СТС";Pump();
            Check(recipients.Items.Cast<string>().Any(n=>n.Contains("СТС")),"recipient search includes imported organizations");recipients.IsDropDownOpen=false;
            main.SeparateWindows=false;Exception? inlineError=null;var targetDate=DateTime.Today.AddDays(7);
            ContentControl Inline(string name)=>Visual(main).OfType<ContentControl>().Single(c=>c.Tag?.GetType().Name==name);
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                Window? formWindow=null;
                try
                {
                    var content=Inline("EventEditorWindow");formWindow=(Window)content.Tag;
                    Check(app.Windows.Cast<Window>().Count(w=>w.IsVisible)==1,"event form is hosted inside main window");
                    var boxes=Logical(content).OfType<ComboBox>().ToList();Check(boxes.All(b=>b.SelectedIndex==-1&&string.IsNullOrEmpty(b.Text)),"embedded new show starts empty too");boxes[0].SelectedIndex=0;boxes[1].SelectedItem=boxes[1].Items.Cast<VenueInfo>().Single(v=>v.Id==venueId);
                    Exception? priceError=null;
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                        Window? priceWindow=null;
                        try{
                            var pricing=Inline("PriceEditorWindow");priceWindow=(Window)pricing.Tag;
                            Check(app.Windows.Cast<Window>().Count(w=>w.IsVisible)==1,"nested pricing form remains inside main window");
                            Logical(pricing).OfType<TextBox>().First().Text="Embedded pricing";Click(pricing,"Назначить всему залу");
                            Check(!Logical(pricing).OfType<Button>().Any(b=>Equals(b.Content,"Сохранить")&&b.IsVisible),"embedded pricing saves from header rather than a duplicate form action");
                            Visual(main).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")&&b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }catch(Exception ex){priceError=ex;priceWindow?.Close();}
                    }));
                    Click(content,"Создать");if(priceError is not null)throw priceError;
                    Check(boxes[2].SelectedItem is PlanInfo newPlan&&newPlan.Name=="Embedded pricing","nested pricing save returns to original form");
                    Logical(content).OfType<DatePicker>().Single().SelectedDate=targetDate;boxes[3].Text="07";boxes[4].Text="08";boxes[5].Text="09";
                    Click(content,"Создать показ");
                }
                catch(Exception ex){inlineError=ex;formWindow?.Close();}
            }));
            Click(main,"Новый показ");Pump();if(inlineError is not null)throw inlineError;
            Check(store.Events().Any(e=>e.VenueId==venueId&&e.SortDate==targetDate.AddHours(7).AddMinutes(8).AddSeconds(9).ToString("yyyy-MM-ddTHH:mm:ss")),"typed hours minutes seconds create the correct show");
            Check(((Grid)main.FindName("DialogLayer")).Visibility==Visibility.Collapsed&&((DockPanel)main.FindName("MainContent")).IsEnabled,"main window is restored after embedded save");
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var settings=Inline("SettingsWindow");Logical(settings).OfType<CheckBox>().Single().IsChecked=true;Click(settings,"Сохранить");
            }));Click(main,"Настройки");
            Check(main.SeparateWindows&&File.ReadAllText(Path.Combine(work,"window-mode.txt"))=="separate","separate window preference is persisted");
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var separate=app.Windows.Cast<Window>().Single(w=>w.Title=="Новый показ"&&w.IsVisible);
                Check(app.Windows.Cast<Window>().Count(w=>w.IsVisible)==2,"separate window mode remains available");separate.Close();
            }));Click(main,"Новый показ");
            ((TextBox)main.FindName("SearchBox")).Text="no-such-performance-unique";Pump();
            Check(((Grid)main.FindName("Workspace")).Visibility==Visibility.Collapsed&&!((Button)main.FindName("IssueButton")).IsEnabled,"empty search clears previous show and issuance context");
            main.SeparateWindows=false;
            CheckInvoiceMergeSelection(store,main);
            var hostType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.EditorWindow")!;
            var guardEditor=(Window)Activator.CreateInstance(hostType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{store,"Проверка закрытия",650d,450d},null)!;guardEditor.Content=new StackPanel();
            var refuseClose=true;guardEditor.Closing+=(_,e)=>e.Cancel=refuseClose;
            Exception? closeError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                try{main.Close();Check(main.IsVisible&&((Grid)main.FindName("DialogLayer")).Visibility==Visibility.Visible,"main-window close respects embedded editor cancellation");}
                catch(Exception ex){closeError=ex;}
                finally{refuseClose=false;guardEditor.Close();}
            }));hostType.GetMethod("ShowDialog",BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly)!.Invoke(guardEditor,null);if(closeError is not null)throw closeError;
            main.Close();Console.WriteLine("PASS complete catalog navigation, embedded forms, selection and invoices");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{foreach(var w in app.Windows.Cast<Window>().ToArray())w.Close();app.Shutdown();Directory.Delete(work,true);}
    }
    private delegate bool EnumWindowCallback(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent,EnumWindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    private static void Gesture(Tickets.App.HallView hall,Point from,Point to,bool right=false)
    {
        // Native resize/activation messages must settle before sampling hall coordinates.
        Pump();WaitForInput();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var zoom=(double)typeof(Tickets.App.HallView).GetField("zoom",flags)!.GetValue(hall)!;
        var offset=(Vector)typeof(Tickets.App.HallView).GetField("offset",flags)!.GetValue(hall)!;
        void Position(Point p){var point=hall.PointToScreen(new Point(p.X*zoom+offset.X,p.Y*zoom+offset.Y));Console.WriteLine($"Gesture point {point}; hall offset {offset}; zoom {zoom}; hit {hall.InputHitTest(new Point(p.X*zoom+offset.X,p.Y*zoom+offset.Y))?.GetType().Name}");if(!SetCursorPos((int)point.X,(int)point.Y))throw new Exception("Windows test desktop cannot move the cursor");WaitForInput();}
        Position(from);mouse_event(right?8u:2u,0,0,0,UIntPtr.Zero);WaitForInput();
        Position(to);mouse_event(right?16u:4u,0,0,0,UIntPtr.Zero);WaitForInput();
        Console.WriteLine($"Mouse gesture {from} -> {to}; selected: {string.Join(',',hall.Selected)}");
    }
    private static void CheckSeatRenderingAndContext(Tickets.App.HallView hall,SeatInfo[] source)
    {
        var states=new[]{source[0] with{Number="",Row="",X=0,Y=0,TicketId=101,IsReserved=false,IsPrinted=false},
            source[1] with{Number="",Row="",X=24,Y=0,TicketId=102,IsReserved=false,IsPrinted=true},
            source[2] with{Number="",Row="",X=48,Y=0,TicketId=103,IsReserved=true,IsPrinted=false},
            source[3] with{Number="",Row="",X=72,Y=0,TicketId=null,IsReserved=false,IsPrinted=false,IsBlocked=true}};
        hall.Load(states,Array.Empty<SectorInfo>(),true);Pump();
        var drawing=(DrawingGroup)typeof(Tickets.App.HallView).GetField("drawing",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(hall)!;
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new ScaleTransform(8,8));dc.DrawDrawing(drawing);}
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(712,144,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var pixels=new byte[712*144*4];bitmap.CopyPixels(pixels,712*4,0);
        int Count(int seat,Func<byte,byte,byte,bool> match)
        {
            var count=0;for(var y=16;y<112;y++)for(var x=seat*192+16;x<seat*192+112;x++){var i=(y*712+x)*4;if(pixels[i+3]==255&&match(pixels[i+2],pixels[i+1],pixels[i]))count++;}return count;
        }
        Check(Count(0,(r,g,b)=>r<20&&g<20&&b<20)>1000&&Count(0,(r,g,b)=>r>220&&g>220&&b>220)>500,"invoice-only occupied seat renders black with dense white hatching");
        Check(Count(1,(r,g,b)=>r>230&&g<20&&b<20)>1000&&Count(1,(r,g,b)=>r<20&&g<20&&b<20)>500,"issued and printed seat renders red with dense black hatching");
        Check(Count(2,(r,g,b)=>r<20&&g<20&&b<20)>1000&&Count(2,(r,g,b)=>r>220&&g>220&&b>220)>500,"reserved seat renders black with white hatching");
        Check(Count(3,(r,g,b)=>r>180&&r<230&&Math.Abs(r-g)<3&&Math.Abs(g-b)<3)>1000&&Count(3,(r,g,b)=>r<20&&g<20&&b<20)>500,"withdrawn seat renders gray with a black cross");
        Check(pixels[(64*712+136)*4+3]==0,"seat hatching remains clipped inside seat bounds");
        // Pricing mode still renders selection over unavailable seats; issuance mode forbids it.
        hall.AllowUnavailableSelection=true;CheckSelectionBorder(hall,states[0]);hall.Clear();hall.AllowUnavailableSelection=false;
        var contexts=new List<long>();hall.SeatContextRequested+=(id,_)=>contexts.Add(id);var offsetField=typeof(Tickets.App.HallView).GetField("offset",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var before=(Vector)offsetField.GetValue(hall)!;Gesture(hall,new Point(8,8),new Point(8,8),true);
        Check(contexts.SequenceEqual(new[]{states[0].Id})&&((Vector)offsetField.GetValue(hall)!)==before,"stationary right click requests seat context without panning");
        Gesture(hall,new Point(8,8),new Point(38,8),true);
        Check(contexts.Count==1&&((Vector)offsetField.GetValue(hall)!)!=before&&hall.Selected.Count==0,"right drag pans without opening seat context or changing selection");
    }
    private static void SetSelectionPrice(Tickets.App.MainWindow main,long price)
    {
        Exception? error=null;Window? popup=null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try
            {
                popup=Application.Current.Windows.Cast<Window>().Single(w=>w.GetType().Name=="SeatPriceWindow");
                var input=Logical(popup).OfType<TextBox>().First();input.Text=price.ToString();
                var enter=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup)!,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};input.RaiseEvent(enter);
                Check(enter.Handled,"Enter confirms the seat price dialog");
            }
            catch(Exception ex){error=ex;popup?.Close();}
        }));
        Click(main,"Изменить цену");Pump();if(error is not null)throw error;
        Check(!Application.Current.Windows.Cast<Window>().Any(w=>w.IsVisible&&w.GetType().Name=="SeatPriceWindow"),"price confirmation closes the dialog");
    }
    private static void CheckTicketBlankPreview(Store store,long eventId,InvoiceInfo originalInvoice)
    {
        var appAssembly=typeof(Tickets.App.App).Assembly;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var blankType=appAssembly.GetType("Tickets.App.TicketBlankWindow")!;
        var editor=(Window)Activator.CreateInstance(blankType,store)!;editor.Show();Pump();
        try
        {
            Check(!Logical(editor).OfType<DataGrid>().Any(),"blank editor uses visual field selection instead of coordinate tables");
            Click(editor,"Новый А4 · 4 билета");Pump();
            var draft=(TicketBlank)blankType.GetField("blank",flags)!.GetValue(editor)!;
            Check(draft.PageWidth==210&&draft.PageHeight==297&&draft.Positions.Count==4&&draft.Legacy is not null,"new blank starts with four ticket blocks and vertical tear-offs on A4");
            blankType.GetMethod("Load",flags)!.Invoke(editor,new object[]{TicketBlankEditing.CreateA4()});Pump();
            var fieldChoice=(ComboBox)blankType.GetField("fieldChoice",flags)!.GetValue(editor)!;fieldChoice.SelectedIndex=0;Pump();
            var properties=(StackPanel)blankType.GetField("properties",flags)!.GetValue(editor)!;
            var printField=properties.Children.OfType<CheckBox>().Single();
            Check(Equals(printField.Content,"Печатать поле")&&printField.GetBindingExpression(CheckBox.IsCheckedProperty)?.ParentBinding.Path.Path=="Enabled","selected blank field exposes its print checkbox");
            var propertyBoxes=Logical(properties).OfType<TextBox>().ToArray();
            Check(new[]{"X","Y","Width","Height","FontFamily","FontSize","Rotation"}.All(path=>propertyBoxes.Any(box=>box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path==path)),"selected blank field exposes geometry typography and rotation properties");
            var xBox=propertyBoxes.Single(box=>box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path=="X");
            var field=(TicketBlankField)xBox.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Source;
            var changedX=field.X+1;xBox.Text=changedX.ToString(System.Globalization.CultureInfo.CurrentCulture);xBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Check(field.X==changedX,"numeric visual-field property writes through to editable ticket model");
            var paper=(Canvas)blankType.GetField("paper",flags)!.GetValue(editor)!;
            Check(paper.Width>0&&paper.Height>0&&paper.ActualWidth>0&&paper.ActualHeight>0,"blank editor renders a paper preview with positive physical dimensions");
            var templates=(ComboBox)blankType.GetField("templates",flags)!.GetValue(editor)!;
            var blankRoot=(Grid)editor.Content;var priorTransform=blankRoot.LayoutTransform;
            try
            {
                foreach(var scale in new[]{1d,1.25,1.5})
                {
                    blankRoot.LayoutTransform=new ScaleTransform(scale,scale);Pump();editor.UpdateLayout();Pump();
                    var columns=blankRoot.Children.OfType<ScrollViewer>().ToArray();
                    Check(columns.Length==2,"blank editor has independent settings and preview scroll surfaces");
                    var left=columns[0].TransformToAncestor(blankRoot).TransformBounds(new Rect(columns[0].RenderSize));var right=columns[1].TransformToAncestor(blankRoot).TransformBounds(new Rect(columns[1].RenderSize));
                    Check(left.Right<=right.Left+1&&right.Right<=blankRoot.ActualWidth+1&&left.Width>100&&right.Width>100,"blank settings and preview occupy separate rendered columns at "+scale+" layout scale");
                    var center=templates.TranslatePoint(new Point(Math.Min(templates.ActualWidth/2,80),templates.ActualHeight/2),blankRoot);var hit=blankRoot.InputHitTest(center) as DependencyObject;
                    Check(hit is not null&&(ReferenceEquals(hit,templates)||Visual(templates).Contains(hit)),"blank template selector is clickable without preview overlay at "+scale+" layout scale");
                }
            }
            finally{blankRoot.LayoutTransform=priorTransform;Pump();}
            Check(templates.ContextMenu?.Items.OfType<MenuItem>().Any(item=>Equals(item.Header,"Удалить"))==true&&!Logical(editor).OfType<Button>().Any(button=>Equals(button.Content,"Удалить")),"saved blank deletion uses a context menu without an extra delete button");
        }
        finally{editor.Close();}
        var blank=new TicketBlank{Name="UI custom blank",PageWidth=210,PageHeight=297};store.SaveTicketBlank(blank);
        var saved=store.TicketBlanks().Single(item=>item.Id==blank.Id);
        Check(saved.Positions.Count==4&&saved.PageWidth==210&&saved.PageHeight==297,"custom one-ticket A4 blank is normalized to four positions for print preview");
        var printType=appAssembly.GetType("Tickets.App.TicketPrintWindow")!;
        var historyCount=store.History(eventId).Count;var issueCount=store.Issues(eventId).Count;
        store.RememberTicketBlank(null);
        var preview=(Window)Activator.CreateInstance(printType,new object[]{store,eventId,originalInvoice.Tickets.Select(ticket=>ticket.SeatId).ToArray()})!;preview.Show();Pump();
        try
        {
            var choice=(ComboBox)printType.GetField("blankChoice",flags)!.GetValue(preview)!;
            Check(choice.IsVisible&&choice.SelectedIndex==0&&Logical(preview).OfType<DocumentPageView>().Count()==1,"ticket preview opens with the standard blank and all three tickets on one page");
            choice.SelectedItem=choice.Items.OfType<TicketBlank>().Single(item=>item.Id==saved.Id);Pump();
            var pages=Logical(preview).OfType<DocumentPageView>().ToArray();
            Check(choice.SelectedItem is TicketBlank selected&&selected.Id==saved.Id&&pages.Length==1,"selecting a normalized saved blank places all three real tickets on one preview page");
            var document=(FixedDocument)pages[0].DocumentPaginator.Source;
            var firstPage=document.Pages[0].Child;var firstField=firstPage.Children.OfType<Viewbox>().First();const double mm=96.0/25.4;
            Check(Math.Abs(firstPage.Width-210*mm)<.001&&Math.Abs(firstPage.Height-297*mm)<.001,"custom preview uses the physical A4 page size");
            Check(Math.Abs(FixedPage.GetLeft(firstField)-5*mm)<.001&&Math.Abs(FixedPage.GetTop(firstField)-3*mm)<.001,"normalized custom ticket keeps its unscaled source field offsets in millimeters");
            Check(firstField.Child is TextBlock title&&title.Text==originalInvoice.Title,"custom ticket preview renders the original invoice event");
        }
        finally{preview.Close();}
        CheckLegacyTicketPreview(store,eventId,originalInvoice);
        var unchanged=store.Invoice(eventId,originalInvoice.Id);
        Check(store.History(eventId).Count==historyCount&&store.Issues(eventId).Count==issueCount&&unchanged.Tickets.SequenceEqual(originalInvoice.Tickets)&&unchanged.Sender==originalInvoice.Sender,"closing custom blank preview preserves original tickets invoice and print state");
    }
    private static void CheckLegacyTicketPreview(Store store,long eventId,InvoiceInfo originalInvoice)
    {
        var printType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketPrintWindow")!;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var ticket=originalInvoice.Tickets[0];
        var history=store.History(eventId).ToArray();var originalTickets=originalInvoice.Tickets.ToArray();
        var preview=(Window)Activator.CreateInstance(printType,new object[]{store,eventId,new[]{ticket.SeatId}})!;preview.Show();Pump();
        try
        {
            var choice=(ComboBox)printType.GetField("blankChoice",flags)!.GetValue(preview)!;
            Check(choice.Items.OfType<TicketBlank>().Count(blank=>blank.Legacy is not null)==11,"ticket preview offers all eleven adapted original report layouts");
            FixedPage Select(string sourceFile)
            {
                choice.SelectedItem=choice.Items.OfType<TicketBlank>().Single(blank=>blank.Legacy?.SourceFile==sourceFile);Pump();
                var views=Logical(preview).OfType<DocumentPageView>().ToArray();
                Check(views.Length==1,"single occupied ticket uses one original "+sourceFile+" page");
                var document=(FixedDocument)views[0].DocumentPaginator.Source;document.DocumentPaginator.ComputePageCount();
                return document.Pages.Single().Child;
            }
            void CheckLegacyField(string sourceFile,string binding,string expectedText)
            {
                var page=Select(sourceFile);
                var blank=choice.Items.OfType<TicketBlank>().Single(item=>item.Legacy?.SourceFile==sourceFile);
                var placement=FourUpTicketLayouts.Paginate(blank,1).Single().Elements.Single(item=>item.Element.Binding==binding);
                var visual=page.Children.OfType<Border>().Single(box=>box.Child is TextBlock text&&text.Text==expectedText);
                var label=(TextBlock)visual.Child;var field=placement.Element;const double mm=96.0/25.4;
                Check(Math.Abs(label.FontSize-field.FontSize*96/72)<.001&&label.FontWeight==(field.Bold?FontWeights.Bold:FontWeights.Normal),sourceFile+" keeps the fitted field typography");
                Check(field.Rotation==0?label.LayoutTransform is not RotateTransform:label.LayoutTransform is RotateTransform rotation&&rotation.Angle==-field.Rotation,sourceFile+" applies the fitted field rotation");
                Check(Math.Abs(FixedPage.GetLeft(visual)-placement.X*mm)<.001&&Math.Abs(FixedPage.GetTop(visual)-placement.Y*mm)<.001&&Math.Abs(visual.Width-field.Width*mm)<.001&&Math.Abs(visual.Height-field.Height*mm)<.001,sourceFile+" renders the physical four-ticket placement geometry");
                Check(!Visual(page).OfType<Viewbox>().Any(),sourceFile+" renders fitted text boxes directly");
            }
            CheckLegacyField("Gift.rpt","theatre_name",originalInvoice.Venue);
            var showDate=DateTime.Parse(originalInvoice.EventDate,System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
            var tearOff=ticket.Series+" № "+ticket.Number.ToString("D6",System.Globalization.CultureInfo.InvariantCulture)+" Цена: "+ticket.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)+"р. "+showDate.ToString("dd.MM.yyyy HH:mm",System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
            CheckLegacyField("Стандарт1.rpt","small_info1",tearOff);
            CheckLegacyField("Стандарт1.rpt","perf_name",originalInvoice.Title.Trim().ToUpper(System.Globalization.CultureInfo.GetCultureInfo("ru-RU")));

        }
        finally{preview.Close();}
        Check(store.History(eventId).SequenceEqual(history)&&store.Invoice(eventId,originalInvoice.Id).Tickets.SequenceEqual(originalTickets),"closing original report previews preserves history ticket identity and print state");
    }
    private static FlowDocument CloneFlowDocument(FlowDocument document)
        =>(FlowDocument)System.Windows.Markup.XamlReader.Parse(System.Windows.Markup.XamlWriter.Save(document));
    private static void CheckNativePrintPreview(Application app,Window documentWindow)
    {
        Exception? error=null;
        app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            Window? preview=null;
            try
            {
                preview=app.Windows.Cast<Window>().Single(window=>window.IsVisible&&window.Title.StartsWith("Предварительный просмотр печати · ",StringComparison.Ordinal));
                Check(ReferenceEquals(preview.Owner,documentWindow)||preview.Owner is not null,"print preview opens inside the current application");
                var pages=Logical(preview).OfType<DocumentPageView>().ToArray();
                Check(pages.Length>0&&pages.All(page=>page.DocumentPaginator is not null),"print preview renders paginated content before any print job");
                Check(!Logical(preview).OfType<DocumentViewer>().Any(),"print preview excludes native-dialog print controls");
                var settings=Logical(preview).OfType<ComboBox>().ToArray();
                Check(settings.Length>=3&&settings.Any(box=>box.Items.Contains("Книжная")&&box.Items.Contains("Альбомная")),"shared print preview exposes printer paper and orientation settings");
                var orientation=settings.Single(box=>box.Items.Contains("Книжная"));var originalOrientation=orientation.SelectedIndex;
                orientation.SelectedIndex=1-originalOrientation;Pump();
                var rotated=Logical(preview).OfType<DocumentPageView>().First().DocumentPaginator.GetPage(0).Size;
                Check(orientation.SelectedIndex==1?rotated.Width>rotated.Height:rotated.Height>rotated.Width,"changing print orientation rebuilds the actual preview page geometry");
                orientation.SelectedIndex=originalOrientation;Pump();
                Check(Logical(preview).OfType<Button>().Any(button=>Equals(button.Content,"Печатать")),"preview requires a separate explicit print action");
            }
            catch(Exception ex){error=ex;}
            finally{preview?.Close();}
        }));
        Click(documentWindow,"Печать");if(error is not null)throw error;
    }
    private static void CheckDocumentActions(Window window)
    {
        var root=(FrameworkElement)window.Content;
        var previous=root.LayoutTransform;
        try
        {
            foreach(var scale in new[]{1d,1.25,1.5})
            {
                root.LayoutTransform=new ScaleTransform(scale,scale);Pump();window.UpdateLayout();Pump();
                var actions=Logical(root).OfType<Button>().Where(button=>button.IsVisible&&new[]{"Сохранить","Печать","Открыть в Word"}.Contains(button.Content?.ToString())).ToArray();
                Check(actions.Length==3,"document exposes save print and open at "+scale+" layout scale");
                foreach(var scroll in Logical(root).OfType<ScrollViewer>().Where(scroll=>scroll.Content is StackPanel panel&&panel.Children.OfType<Button>().Any(b=>Equals(b.Content,"Накладная"))))scroll.ScrollToRightEnd();Pump();
                var rectangles=actions.Select(button=>button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize))).ToArray();
                Console.WriteLine($"Document actions scale={scale}; root={root.ActualWidth}x{root.ActualHeight}; buttons={string.Join("; ",rectangles.Select(rect=>rect.ToString()))}");
                Check(rectangles.All(rect=>rect.Width>0&&rect.Height>0&&rect.Left>=0&&rect.Right<=root.ActualWidth+1&&rect.Top>=0&&rect.Bottom<=root.ActualHeight+1),"document actions remain visible at "+scale+" layout scale");
                for(var index=0;index<actions.Length;index++)
                {
                    var center=actions[index].TranslatePoint(new Point(actions[index].ActualWidth/2,actions[index].ActualHeight/2),root);
                    var hit=root.InputHitTest(center) as DependencyObject;
                    Check(hit is not null&&(ReferenceEquals(hit,actions[index])||Visual(actions[index]).Contains(hit)),"document action center is hit-testable at "+scale+": "+actions[index].Content);
                }
            }
        }
        finally{root.LayoutTransform=previous;Pump();}
    }
    private static void CheckDocumentTableLayout(FlowDocumentScrollViewer viewer,string caption)
    {
        // A FlowDocument switches formatter when its paginator is used. Measure the
        // live scroll viewer first and paginate an independent copy afterwards.
        Pump();viewer.UpdateLayout();Pump();var document=viewer.Document;
        var tables=document.Blocks.OfType<Table>().ToArray();Check(tables.Length>0,caption+" contains its formatted table");
        foreach(var table in tables)
        {
            Check(table.Columns.Count>1&&table.Columns.Cast<TableColumn>().All(column=>column.Width.IsAbsolute&&column.Width.Value>=20)&&table.Columns.Cast<TableColumn>().Sum(column=>column.Width.Value)<=document.PageWidth-document.PagePadding.Left-document.PagePadding.Right+1,caption+" table columns use usable pixel widths within the page");
            var rows=table.RowGroups.Cast<TableRowGroup>().SelectMany(group=>group.Rows.Cast<TableRow>()).ToArray();
            foreach(var row in rows)
            {
                row.Cells[0].BringIntoView();Pump();viewer.UpdateLayout();
                Check(ReferenceEquals(viewer.Document,document),caption+" geometry uses the current viewer document");
                var first=row.Cells[0].ContentStart.GetNextInsertionPosition(LogicalDirection.Forward)!.GetCharacterRect(LogicalDirection.Forward);
                var second=row.Cells[1].ContentStart.GetNextInsertionPosition(LogicalDirection.Forward)!.GetCharacterRect(LogicalDirection.Forward);
                var last=row.Cells[0].ContentEnd.GetNextInsertionPosition(LogicalDirection.Backward)!.GetCharacterRect(LogicalDirection.Backward);
                Console.WriteLine($"Document geometry: {caption}; row={Array.IndexOf(rows,row)}; text={new TextRange(row.Cells[0].ContentStart,row.Cells[0].ContentEnd).Text.Trim()}; first={first}; second={second}; last={last}; deltaX={second.Left-first.Left}; pageWidth={document.PageWidth}; pointerLayoutValid={row.Cells[0].ContentStart.HasValidLayout}; widths={string.Join(",",table.Columns.Cast<TableColumn>().Select(column=>column.Width.Value))}");
                var rightAligned=row.Cells[1].Blocks.OfType<Paragraph>().FirstOrDefault()?.TextAlignment==TextAlignment.Right;
                if(rightAligned)
                {
                    Check(!first.IsEmpty&&!second.IsEmpty&&second.Left>=first.Left+table.Columns[0].Width.Value,caption+" right-aligned recipient starts inside the second column");
                    var bounds=new List<Rect>();
                    for(var pointer=row.Cells[1].ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);pointer is not null&&pointer.CompareTo(row.Cells[1].ContentEnd)<0;pointer=pointer.GetNextInsertionPosition(LogicalDirection.Forward))
                    {if(pointer.GetPointerContext(LogicalDirection.Forward)!=TextPointerContext.Text)continue;var rectangle=pointer.GetCharacterRect(LogicalDirection.Forward);if(!rectangle.IsEmpty)bounds.Add(rectangle);}
                    var columnLeft=document.PagePadding.Left+table.Columns[0].Width.Value;var columnRight=columnLeft+table.Columns[1].Width.Value;
                    Check(bounds.Count>0&&bounds.All(rectangle=>rectangle.Left>=columnLeft-1&&rectangle.Right<=columnRight+1),caption+" right-aligned recipient text stays inside its column and the page");
                    if(caption=="invoice"&&new TextRange(row.Cells[0].ContentStart,row.Cells[0].ContentEnd).Text.StartsWith("Передал:",StringComparison.Ordinal))Check(row.Cells.Count==2&&row.Cells[0].Blocks.OfType<Paragraph>().Count()==2&&row.Cells[1].Blocks.OfType<Paragraph>().Count()==2&&row.Cells[0].Blocks.OfType<Paragraph>().All(p=>p.TextAlignment==TextAlignment.Left)&&row.Cells[1].Blocks.OfType<Paragraph>().All(p=>p.TextAlignment==TextAlignment.Right)&&new TextRange(row.Cells[0].ContentStart,row.Cells[0].ContentEnd).Text.StartsWith("Передал:",StringComparison.Ordinal)&&new TextRange(row.Cells[1].ContentStart,row.Cells[1].ContentEnd).Text.StartsWith("Получил:",StringComparison.Ordinal),"invoice signatures explicitly align sender left and recipient right");
                }
                else Check(!first.IsEmpty&&!second.IsEmpty&&second.Left-first.Left>=20&&second.Left-first.Left<=document.PageWidth/2,caption+" rendered first column has a sensible width");
                if(new TextRange(row.Cells[0].ContentStart,row.Cells[0].ContentEnd).Text.Trim().Length<=20)Check(!last.IsEmpty&&last.Bottom-first.Top<160,caption+" rendered short row does not expand into a giant empty cell");
            }
        }
        var printDocument=CloneFlowDocument(document);var paginator=((IDocumentPaginatorSource)printDocument).DocumentPaginator;paginator.ComputePageCount();
        Check(paginator.PageCount>0&&paginator.GetPage(0).Visual is not null,caption+" document paginates");
        Check(ReferenceEquals(viewer.Document,document)&&document.ContentStart.HasValidLayout,caption+" pagination preserves the live preview layout");
    }
    private static void CheckInvoiceMergeSelection(Store store,Tickets.App.MainWindow main)
    {
        var venue=store.CreateHall("UI merge hall","",Enumerable.Range(0,4).Select(index=>new DraftSeat{Row="1",Number=(index+1).ToString(),X=index*24}).ToArray());
        var seats=store.VenueSeats(venue).Select(seat=>seat.Id).ToArray();var plan=store.SavePlan(venue,"UI merge plan",seats.Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
        var eventId=store.CreateEvent(store.SavePerformance("UI merge event"),venue,plan,DateTime.Today.AddDays(4));
        var first=store.Issue(eventId,new[]{seats[0]},"UI merge recipient","UIM",1);
        var second=store.Issue(eventId,new[]{seats[1]}," ui merge recipient ","UIM",2);
        var other=store.Issue(eventId,new[]{seats[2]},"UI other merge recipient","UIM",3);
        typeof(Tickets.App.MainWindow).GetMethod("RefreshCatalogNavigation",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);((TextBox)main.FindName("SearchBox")).Clear();main.SelectEvent(eventId);Pump();
        var history=(DataGrid)main.FindName("HistoryGrid");var tabs=Logical(main).OfType<TabControl>().Single(tab=>Logical(tab).Contains(history));
        var previousTab=tabs.SelectedIndex;tabs.SelectedIndex=1;Pump();main.Activate();
        var merge=(Button)main.FindName("MergeInvoicesButton");
        Check(merge.Visibility==Visibility.Collapsed&&!Logical(tabs).OfType<Button>().Any(button=>Equals(button.Content,"Открыть накладную")),"journal initially hides merge and uses row opening without duplicate action");
        Check(history.IsVisible&&history.Items.Count==3,"merge journal displays the selected event after clearing the earlier search filter");
        Check(history.Columns[0] is DataGridTemplateColumn selector&&selector.DisplayIndex==0&&selector.Visibility==Visibility.Visible,"inline merge selection stays in the leftmost visible column");
        var journalBoxes=Visual(history).OfType<CheckBox>().Where(box=>box.Tag is long).ToArray();
        Check(journalBoxes.Length==3,"journal exposes one inline merge checkbox per invoice");
        var firstBox=journalBoxes.Single(box=>Equals(box.Tag,first));NativeClick(firstBox);Pump();
        Check(firstBox.IsChecked==true&&merge.Visibility==Visibility.Collapsed&&!Application.Current.Windows.Cast<Window>().Any(window=>window.IsVisible&&window.GetType().Name=="InvoiceWindow"),"one inline journal check selects invoice without opening its document");
        var secondBox=journalBoxes.Single(box=>Equals(box.Tag,second));secondBox.IsChecked=true;Pump();
        Check(merge.IsVisible,"checking a second journal invoice reveals merge");
        secondBox.IsChecked=false;Pump();Check(merge.Visibility==Visibility.Collapsed,"unchecking back to one invoice hides contextual merge");
        firstBox.IsChecked=false;tabs.SelectedIndex=previousTab;Pump();
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.InvoiceMergeWindow")!;
        var window=(Window)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object?[]{store,eventId,new long[]{first}},null)!;window.Show();Pump();
        try
        {
            var boxes=Logical(window).OfType<CheckBox>().ToArray();var action=Logical(window).OfType<Button>().Single(button=>Equals(button.Content,"Объединить в одну накладную"));
            Check(boxes.Length==3&&boxes.Count(box=>box.IsChecked==true)==1&&!action.IsEnabled,"merge requires two checked invoices and retains initial selection");
            CheckBox Box(long id)=>boxes.Single(box=>box.Content?.ToString()?.StartsWith("№ "+id+" ·")==true);
            Box(second).IsChecked=true;Pump();
            Check(action.IsEnabled&&!Logical(window).OfType<ComboBox>().Any(),"merge accepts matching parties and allocates a new number without primary selector");
            Box(other).IsChecked=true;Pump();Check(!action.IsEnabled,"merge disables invoices with a different recipient");
            Box(other).IsChecked=false;Box(first).IsChecked=false;Pump();
            Check(!action.IsEnabled,"unchecking invoices disables single-invoice merge");
            Check(store.Issues(eventId).Count==3,"merge selection alone preserves all original issuances");
        }
        finally{window.Close();}
    }
    private static void CheckSelectionBorder(Tickets.App.HallView hall,SeatInfo seat)
    {
        byte[] Render()
        {
            var width=(int)Math.Ceiling(hall.ActualWidth);var height=(int)Math.Ceiling(hall.ActualHeight);
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(hall);
            var data=new byte[width*height*4];image.CopyPixels(data,width*4,0);return data;
        }
        hall.Clear();Pump();var before=Render();hall.SetSelection(new[]{seat.Id});Pump();var selected=Render();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var zoom=(double)typeof(Tickets.App.HallView).GetField("zoom",flags)!.GetValue(hall)!;
        var offset=(Vector)typeof(Tickets.App.HallView).GetField("offset",flags)!.GetValue(hall)!;
        int Pixel(double x,double y)=>((int)Math.Round(y)*(int)Math.Ceiling(hall.ActualWidth)+(int)Math.Round(x))*4;
        var middle=Pixel(offset.X+(seat.X+8)*zoom,offset.Y+(seat.Y+8)*zoom);
        Check(before.AsSpan(middle,4).SequenceEqual(selected.AsSpan(middle,4)),"selection border preserves the original seat fill and hatching");
        var outer=Pixel(offset.X+seat.X*zoom-3,offset.Y+(seat.Y+8)*zoom);
        var inner=Pixel(offset.X+seat.X*zoom-1,offset.Y+(seat.Y+8)*zoom);
        Check(selected[outer]<30&&selected[outer+1]<30&&selected[outer+2]<30&&selected[inner]>225&&selected[inner+1]>225&&selected[inner+2]>225,"selected seat has black outer and white inner borders");
    }
    private static TextBox Field(DependencyObject root,string label)
    {
        var caption=Logical(root).OfType<TextBlock>().Single(text=>text.Text==label);
        var labelHost=caption.Parent is Decorator decorator?(FrameworkElement)decorator:caption;
        var panel=(Panel)labelHost.Parent;return (TextBox)panel.Children[panel.Children.IndexOf(labelHost)+1];
    }
    private static void AssignPrice044(DependencyObject root)
    {
        var amount=Field(root,"Цена, ₽");
        amount.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(amount),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();
    }
    private static ContextMenu? OpenContextMenu()
        =>PresentationSource.CurrentSources.Cast<PresentationSource>().Where(source=>source.RootVisual is not null).SelectMany(source=>Visual(source.RootVisual!)).OfType<ContextMenu>().SingleOrDefault(menu=>menu.IsOpen);
    private static void NativeClick(FrameworkElement element)
    {
        var point=element.PointToScreen(new Point(Math.Min(10,element.ActualWidth/2),element.ActualHeight/2));
        Check(SetCursorPos((int)point.X,(int)point.Y),"Windows test desktop can move cursor");WaitForInput();
        mouse_event(2,0,0,0,UIntPtr.Zero);WaitForInput();mouse_event(4,0,0,0,UIntPtr.Zero);WaitForInput();
    }
    private static void CheckMainPresentation(Tickets.App.MainWindow main,Store store,long[] seatIds)
    {
        var hall=(Tickets.App.HallView)main.FindName("Hall");hall.SetSelection(seatIds.Take(2));Pump();
        var selection=hall.Selected.ToHashSet();var detail=(ListBox)main.FindName("SelectedList");var summary=(ListBox)main.FindName("SelectedPriceSummary");var mode=(ComboBox)main.FindName("SelectedViewBox");
        mode.SelectedIndex=1;Pump();Check(!detail.IsVisible&&summary.IsVisible&&summary.Items.Count>=2&&hall.Selected.SetEquals(selection),"price summary replaces place details while preserving selection");
        var summaryBounds=summary.TransformToAncestor(main).TransformBounds(new Rect(summary.RenderSize));
        mode.SelectedIndex=0;Pump();var detailBounds=detail.TransformToAncestor(main).TransformBounds(new Rect(detail.RenderSize));
        Check(detail.IsVisible&&!summary.IsVisible&&hall.Selected.SetEquals(selection)&&Math.Abs(summaryBounds.Left-detailBounds.Left)<1&&Math.Abs(summaryBounds.Width-detailBounds.Width)<1,"details and price summary occupy the same sidebar area");
        var legend=(Grid)main.FindName("LegendPanel");Check(legend.RowDefinitions.Count is >=1 and <=3&&legend.Children.OfType<Border>().Any(),"main prices use two rows with a third only when needed");
        var cells=legend.Children.OfType<Border>().ToArray();
        Check(legend.ColumnDefinitions.All(column=>column.Width.IsAuto)&&cells.All(cell=>cell.Child is TextBlock {TextWrapping:TextWrapping.NoWrap,FontSize:14.3,FontWeight:var weight}&&weight==FontWeights.Bold),"main price legend retains unbroken bold expressions in content-sized columns");
        var priceGroups=store.Seats(store.Events().Single(show=>show.Title=="UI event").Id).Where(seat=>seat.Amount.HasValue&&!seat.IsBlocked).GroupBy(seat=>seat.Amount!.Value).OrderByDescending(group=>group.Key);
        Check(cells.Select(cell=>((Tickets.App.CompactText043)cell.Child).Text).SequenceEqual(priceGroups.Select(group=>$"{group.Key:0.##}₽×{group.Count()}={group.Sum(seat=>(decimal)seat.Amount!.Value):0.##}₽")),"main price legend shows exact compact price quantity and sum");
        Check(cells.Select(cell=>(Grid.GetRow(cell),Grid.GetColumn(cell))).SequenceEqual(Enumerable.Range(0,cells.Length).Select(index=>(index%legend.RowDefinitions.Count,index/legend.RowDefinitions.Count))),"main price legend fills each column downward");
        NativeClick(cells[0]);Pump();Check(hall.FocusPrice.HasValue&&hall.Selected.SetEquals(selection),"clicking price legend focuses its price without changing selection");
        NativeClick(legend.Children.OfType<Border>().First());Pump();Check(hall.FocusPrice is null&&hall.Selected.SetEquals(selection),"clicking focused price again restores all prices without changing selection");
        var recipient=(ComboBox)main.FindName("RecipientBox");recipient.ApplyTemplate();var input=(TextBox)recipient.Template.FindName("PART_EditableTextBox",recipient);
        const string newName="UI previously unknown recipient with spaces";Check(!store.Recipients().Contains(newName),"typed recipient fixture is new");
        input.Focus();input.Text="";
        foreach(var character in newName){input.AppendText(character.ToString());input.CaretIndex=input.Text.Length;Pump();}
        recipient.IsDropDownOpen=false;hall.Focus();Pump();
        var effective=(string)typeof(Tickets.App.MainWindow).GetProperty("RecipientName",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        Check(input.Text==newName&&recipient.Text==newName&&effective==newName,"typing and blurring a new recipient preserves its full name including spaces");
        Check(!store.Recipients().Contains(newName),"typing a recipient alone does not save it");recipient.Text="";hall.Clear();Pump();
    }
    private static void CheckSelectionSidebar(Tickets.App.MainWindow main)
    {
        main.WindowState=WindowState.Normal;main.Left=0;main.Top=0;main.Width=1420;main.Height=880;Pump();WaitForInput();
        var sidebar=(Border)main.FindName("SelectionSidebar");var list=(ListBox)main.FindName("SelectedList");
        var actions=(System.Windows.Controls.Primitives.UniformGrid)main.FindName("SelectionActions");
        var options=(Expander)main.FindName("SelectionOptions");options.IsExpanded=false;Pump();
        Check(actions.Columns==3&&actions.Children.OfType<Button>().Count()==3&&actions.Children.OfType<Button>().All(button=>double.IsNaN(button.Width)&&button.ActualWidth>0&&button.ActualWidth<=actions.ActualWidth/3&&button.ActualHeight==72),"sidebar has three equal-column actions that fit their cells at the existing height");
        Check(actions.Children.OfType<Button>().Select(button=>button.Name).ToHashSet().SetEquals(new[]{"ReserveButton","SaleStateButton","ReturnButton"}),"sidebar actions are reservation sale and return");
        var print=(Button)main.FindName("PrintTicketsButton");var issue=(Button)main.FindName("IssueButton");var extend=(Button)main.FindName("ExtendInvoiceButton");
        var upper=(Grid)main.FindName("UpperActions");var toolbarScroll=(ScrollViewer)upper.Parent;
        Check(ReferenceEquals(print.Parent,issue.Parent)&&print.Parent is StackPanel {Orientation:Orientation.Horizontal}&&((FrameworkElement)print.Parent).Parent is Canvas&&!Logical(sidebar).Contains(print)&&!Logical(sidebar).Contains(issue),"printing and issuance share one row in the show toolbar");
        Check(Grid.GetColumn(extend)==2&&upper.ColumnDefinitions.All(column=>column.Width.IsAuto),"three content-sized show actions precede the colored action stack");
        Check(toolbarScroll.HorizontalScrollBarVisibility==ScrollBarVisibility.Auto&&toolbarScroll.VerticalScrollBarVisibility==ScrollBarVisibility.Disabled&&Grid.GetColumnSpan(toolbarScroll)==1,"show toolbar scrolls horizontally within the hall column");
        Check(print.ActualWidth==72&&print.ActualHeight==72&&issue.ActualWidth==72&&issue.ActualHeight==72&&Equals(print.Style,issue.Style),"main printing and issuance use the same compact square style");
        var whiteActions=upper.Children.OfType<Button>().ToArray();
        var whiteBounds=whiteActions.Select(button=>button.TransformToAncestor(main).TransformBounds(new Rect(button.RenderSize))).ToArray();
        Check(whiteActions.Length==3&&whiteBounds.Max(rect=>rect.Top)-whiteBounds.Min(rect=>rect.Top)<2&&whiteActions.All(button=>double.IsNaN(button.Width)&&button.FontSize==14),"three white show actions retain their type size and share one row");
        var printBounds=print.TransformToAncestor(main).TransformBounds(new Rect(print.RenderSize));var issueBounds=issue.TransformToAncestor(main).TransformBounds(new Rect(issue.RenderSize));
        Check(whiteBounds.All(rect=>rect.Right<=printBounds.Left+1)&&Math.Abs(printBounds.Top-issueBounds.Top)<1&&issueBounds.Left>=printBounds.Right,"colored actions sit immediately right of extension with issuance to the right of printing");
        var title=(Tickets.App.CompactText043)main.FindName("EventTitle");var date=(Tickets.App.CompactText043)main.FindName("EventDate");
        var dateBounds=date.TransformToAncestor(title.Parent as Visual ?? main).TransformBounds(new Rect(date.RenderSize));
        var titleBounds=title.TransformToAncestor(title.Parent as Visual ?? main).TransformBounds(new Rect(title.RenderSize));
        Check(title.FontSize==22&&date.FontSize==19&&!string.IsNullOrWhiteSpace(date.Text)&&dateBounds.Left>=titleBounds.Right&&Math.Abs(dateBounds.Top-titleBounds.Top)<10,"event date appears in approved 19-point text to the right of the 22-point title");
        Console.WriteLine($"Sidebar normal viewport: window {main.ActualWidth}x{main.ActualHeight}; sidebar {sidebar.ActualHeight}; list {list.ActualHeight}; explicit height {list.Height}");
        Check(double.IsNaN(list.Height)&&list.ActualHeight>=220,"selected places fill available height at 1420 by 880");
        var workspace=(Grid)main.FindName("Workspace");var sidebarBounds=sidebar.TransformToAncestor(workspace).TransformBounds(new Rect(sidebar.RenderSize));
        Check(Grid.GetRow(sidebar)==0&&Grid.GetRowSpan(sidebar)==3&&sidebarBounds.Top<=1&&sidebarBounds.Bottom>=workspace.ActualHeight-1,"selection sidebar spans from the workspace top through the hall area");
        var selectedSummary=(Tickets.App.CompactText043)main.FindName("SelectedSummary");var recipient=(ComboBox)main.FindName("RecipientBox");
        Check(Logical(sidebar).Contains(selectedSummary)&&Logical(sidebar).Contains(recipient),"selection summary recipient and selected-place list share one sidebar");
        var recipientBounds=recipient.TransformToAncestor(sidebar).TransformBounds(new Rect(recipient.RenderSize));var selectedBounds=selectedSummary.TransformToAncestor(sidebar).TransformBounds(new Rect(selectedSummary.RenderSize));
        var normalListBounds=list.TransformToAncestor(sidebar).TransformBounds(new Rect(list.RenderSize));
        Check(selectedBounds.Bottom<=recipientBounds.Top&&recipientBounds.Bottom<=normalListBounds.Top&&normalListBounds.Top-recipientBounds.Bottom<90,"recipient and selected-place list remain contiguous below the selection summary");
        foreach(var button in actions.Children.OfType<Button>())
        {
            var bounds=button.TransformToAncestor(sidebar).TransformBounds(new Rect(button.RenderSize));
            Check(bounds.Top>=0&&bounds.Bottom<=sidebar.ActualHeight+1,"sidebar action is visible: "+button.Content);
        }
        main.Width=1000;main.Height=700;Pump();WaitForInput();
        Console.WriteLine($"Sidebar small viewport: window {main.ActualWidth}x{main.ActualHeight}; sidebar {sidebar.ActualHeight}; list {list.ActualHeight}");
        Check(list.ActualHeight>=120,"small viewport retains a usable selected-place list");
        Console.WriteLine($"Small hall height={((Tickets.App.HallView)main.FindName("Hall")).ActualHeight}; workspace rows={string.Join(",",((Grid)main.FindName("Workspace")).RowDefinitions.Select(row=>row.ActualHeight))}");
        Check(((Tickets.App.HallView)main.FindName("Hall")).ActualHeight>=100,"small viewport retains a visible and clickable hall beside the sidebar");
        var listBounds=list.TransformToAncestor(sidebar).TransformBounds(new Rect(list.RenderSize));
        var actionBounds=actions.TransformToAncestor(sidebar).TransformBounds(new Rect(actions.RenderSize));
        Check(listBounds.Bottom<=actionBounds.Top+1&&actionBounds.Bottom<=sidebar.ActualHeight+1,"small viewport keeps selection list and all action rows separate and visible");
        main.Width=1420;main.Height=880;Pump();WaitForInput();
        var mainRoot=(FrameworkElement)main.Content;var oldTransform=mainRoot.LayoutTransform;
        try
        {
            foreach(var scale in new[]{1.25,1.5})
            {
                mainRoot.LayoutTransform=new ScaleTransform(scale,scale);Pump();main.UpdateLayout();Pump();
                var rects=whiteActions.Select(button=>button.TransformToAncestor(main).TransformBounds(new Rect(button.RenderSize))).ToArray();
                Check(rects.Max(rect=>rect.Top)-rects.Min(rect=>rect.Top)<2&&whiteActions.All(button=>button.FontSize==14),"show actions preserve one row and readable type at "+scale+" layout scale");
                foreach(var button in whiteActions.Concat(new[]{print,issue}))
                {
                    button.BringIntoView();Pump();
                    var wasEnabled=button.IsEnabled;button.IsEnabled=true;
                    try
                    {
                        var center=button.TranslatePoint(new Point(button.ActualWidth/2,button.ActualHeight/2),mainRoot);var hit=mainRoot.InputHitTest(center) as DependencyObject;Console.WriteLine($"Toolbar scale={scale}; viewport={toolbarScroll.ViewportWidth}; offset={toolbarScroll.HorizontalOffset}; button={button.Content}; size={button.ActualWidth}x{button.ActualHeight}; center={center}; root={mainRoot.ActualWidth}x{mainRoot.ActualHeight}; hit={hit?.GetType().Name}");
                        Check(hit is not null&&(ReferenceEquals(hit,button)||Visual(button).Contains(hit)),"show action is hit-testable at "+scale+": "+button.Name);
                    }
                    finally{button.IsEnabled=wasEnabled;}
                }
            }
        }
        finally{mainRoot.LayoutTransform=oldTransform;Pump();}
    }
    private static void CheckHallNavigation(Tickets.App.HallView hall,SeatInfo[] source)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var hallType=typeof(Tickets.App.HallView);
        var zoomField=hallType.GetField("zoom",flags)!;var offsetField=hallType.GetField("offset",flags)!;
        hall.Load(source,Array.Empty<SectorInfo>(),true);Pump();
        var fittedZoom=(double)zoomField.GetValue(hall)!;var fittedOffset=(Vector)offsetField.GetValue(hall)!;
        hall.Zoom(.5);Pump();
        var empty=new Point(hall.ActualWidth-15,hall.ActualHeight-15);var screen=hall.PointToScreen(empty);
        Check(SetCursorPos((int)screen.X,(int)screen.Y),"cursor reaches empty hall space");WaitForInput();
        for(var i=0;i<2;i++){mouse_event(2,0,0,0,UIntPtr.Zero);WaitForInput();mouse_event(4,0,0,0,UIntPtr.Zero);WaitForInput();}
        Check(Math.Abs((double)zoomField.GetValue(hall)!-fittedZoom)<.001&&((Vector)offsetField.GetValue(hall)!-fittedOffset).Length<.001,"double clicking empty hall space restores fit");
        var tip=(ToolTip)hallType.GetField("seatTip",flags)!.GetValue(hall)!;
        var offset=(Vector)offsetField.GetValue(hall)!;var zoom=(double)zoomField.GetValue(hall)!;
        var hover=new Point(source[0].X*zoom+offset.X+8*zoom,source[0].Y*zoom+offset.Y+8*zoom);
        hallType.GetMethod("UpdateTip",flags)!.Invoke(hall,new object[]{hover});Pump();
        Check(tip.IsOpen&&tip.Content?.ToString()?.Contains(source[0].Status)==true,"cursor tooltip includes seat status");
        Check(tip.HorizontalOffset>=0&&tip.VerticalOffset>=0&&tip.HorizontalOffset+tip.DesiredSize.Width<=hall.ActualWidth&&tip.VerticalOffset+tip.DesiredSize.Height<=hall.ActualHeight,"cursor tooltip stays inside hall bounds");
        hallType.GetMethod("UpdateTip",flags)!.Invoke(hall,new object[]{empty});Check(!tip.IsOpen,"tooltip closes over empty space");
        var wide=Enumerable.Range(0,80).Select(i=>source[0] with{Id=i+1,X=i*20,Y=0,Number="",Row="",TicketId=null,IsReserved=false,IsPrinted=false}).ToArray();
        hall.Load(wide,Array.Empty<SectorInfo>(),true);hall.Zoom(3);Pump();
        offset=(Vector)offsetField.GetValue(hall)!;zoom=(double)zoomField.GetValue(hall)!;
        var start=new Point(hall.ActualWidth/2,offset.Y-2*zoom);var edge=new Point(hall.ActualWidth-2,offset.Y+18*zoom);
        void Position(Point p){var point=hall.PointToScreen(p);Check(SetCursorPos((int)point.X,(int)point.Y),"cursor moves during edge marquee");WaitForInput();}
        Position(start);mouse_event(2,0,0,0,UIntPtr.Zero);WaitForInput();
        var anchor=(Point)hallType.GetField("worldAnchor",flags)!.GetValue(hall)!;Position(edge);
        for(var i=0;i<4;i++)WaitForInput();
        var after=(Vector)offsetField.GetValue(hall)!;var anchorAfter=(Point)hallType.GetField("worldAnchor",flags)!.GetValue(hall)!;
        mouse_event(4,0,0,0,UIntPtr.Zero);WaitForInput();
        // Native cursor coordinates are integer screen pixels and the timer can tick
        // until mouse-up is dispatched. Derive the final rectangle from actual input.
        var finalOffset=(Vector)offsetField.GetValue(hall)!;var actualEnd=Mouse.GetPosition(hall);
        var end=new Point((actualEnd.X-finalOffset.X)/zoom,(actualEnd.Y-finalOffset.Y)/zoom);
        var expected=SeatSelection.ApplyRectangle(wide,new HashSet<long>(),anchor.X,anchor.Y,end.X,end.Y,false);
        Console.WriteLine($"Edge marquee: offset {offset} -> {after} -> {finalOffset}; anchor {anchor} -> {anchorAfter}; expected {string.Join(",",expected)}; selected {string.Join(",",hall.Selected)}");
        Check(after.X<offset.X,"marquee auto-pans at viewport edge");
        Check(anchorAfter==anchor,"edge marquee preserves its world anchor");
        Check(expected.Count>0&&hall.Selected.SetEquals(expected),"edge marquee selects every seat in its final rectangle");
    }
    private static void SaveDocumentThroughDialog(Window owner,string path)
    {
        Exception? error=null;var submitted=false;var entered=false;var started=DateTime.UtcNow;
        var timer=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(100)};
        timer.Tick+=(_,_)=>EnumWindows((window,_)=>{
            GetWindowThreadProcessId(window,out var process);if(process!=(uint)Environment.ProcessId)return true;
            var title=new System.Text.StringBuilder(256);GetWindowText(window,title,title.Capacity);
            if(title.ToString()!="Сохранить документ")return true;
            try
            {
                IntPtr fileName=IntPtr.Zero,fallback=IntPtr.Zero,saveButton=IntPtr.Zero;
                EnumChildWindows(window,(child,_)=>{
                    var name=new System.Text.StringBuilder(64);GetClassName(child,name,name.Capacity);
                    if(name.ToString()=="Edit"){
                        var value=new System.Text.StringBuilder(2048);GetWindowText(child,value,value.Capacity);
                        if(GetDlgCtrlID(child)==1001)fallback=child;
                        if(value.ToString().EndsWith(".docx",StringComparison.OrdinalIgnoreCase))fileName=child;
                    }
                    if(name.ToString()=="Button"&&GetDlgCtrlID(child)==1)saveButton=child;
                    return true;
                },IntPtr.Zero);
                if(fileName==IntPtr.Zero)fileName=fallback;
                if(fileName==IntPtr.Zero){if(DateTime.UtcNow-started>TimeSpan.FromSeconds(10))throw new Exception("Save dialog filename edit was not found");return true;}
                if(!entered){
                    var before=new System.Text.StringBuilder(2048);GetWindowText(fileName,before,before.Capacity);Console.WriteLine("Save dialog filename before typing: "+before);
                    SetFocus(fileName);SendMessage(fileName,0xB1,IntPtr.Zero,new IntPtr(-1));
                    SendMessageText(fileName,0xC2,new IntPtr(1),path);entered=true;return true;
                }
                var after=new System.Text.StringBuilder(2048);GetWindowText(fileName,after,after.Capacity);
                Check(after.ToString()==path,"native save dialog accepts chosen full document path");
                submitted=true;timer.Stop();
                if(saveButton!=IntPtr.Zero)PostMessage(saveButton,0xF5,IntPtr.Zero,IntPtr.Zero);
                else{SetFocus(fileName);keybd_event(0x0D,0,0,UIntPtr.Zero);keybd_event(0x0D,0,2,UIntPtr.Zero);}
            }
            catch(Exception ex){error=ex;timer.Stop();PostMessage(window,0x111,new IntPtr(2),IntPtr.Zero);}
            return true;
        },IntPtr.Zero);
        expectedSaveDialog=true;timer.Start();
        try{Click(owner,"Сохранить");if(error is not null)throw error;Check(submitted,"manual Save opens the application's document save dialog");}
        finally{timer.Stop();expectedSaveDialog=false;}
    }
    [DllImport("user32.dll")]private static extern IntPtr SetFocus(IntPtr window);
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")]private static extern IntPtr SendMessageText(IntPtr window,uint message,IntPtr wParam,string text);
    [DllImport("user32.dll")]private static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
    [DllImport("user32.dll")]private static extern int GetDlgCtrlID(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool SetWindowText(IntPtr window,string text);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    private static void WaitForInput()
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(80)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    private static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static void Click(DependencyObject root,string label){Console.WriteLine("Click "+label);Logical(root).OfType<Button>().Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Logical(child))yield return node;
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        yield return root;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var node in Visual(VisualTreeHelper.GetChild(root,i)))yield return node;
    }
}
