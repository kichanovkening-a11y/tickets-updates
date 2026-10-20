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
            var messages=new List<string>();
            EnumChildWindows(window,(child,_)=>{var text=new System.Text.StringBuilder(2048);GetWindowText(child,text,text.Capacity);if(text.Length>0)messages.Add(text.ToString());return true;},IntPtr.Zero);
            Console.Error.WriteLine("Unexpected modal dialog: "+string.Join(" | ",messages));Environment.Exit(1);return false;
        },IntPtr.Zero);dialogGuard.Start();
        try
        {
            var db=Path.Combine(work,"tickets.db");File.Copy(args[0],db);var store=new Store(db);
            var main=new Tickets.App.MainWindow(store);app.MainWindow=main;Check(!main.SeparateWindows,"embedded forms are the default");main.SeparateWindows=true;main.Show();Pump();
            var actions=Logical(main).OfType<Button>().ToList();
            Check(actions.Count>=15,"main actions found");
            Check(!actions.Any(b=>Equals(b.Content,"Спецификации")||Equals(b.Content,"Свернуть")||Equals(b.Content,"В окно")),"main toolbar omits duplicate window controls and specifications");
            Check(actions.Where(b=>b.IsVisible).All(b=>b.ActualWidth==200&&b.ActualHeight==44),"all main action buttons have equal rendered size");
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
            Check(combos[0].IsEditable,"performance selection allows typed search");
            combos[0].ApplyTemplate();var performanceEdit=(TextBox)combos[0].Template.FindName("PART_EditableTextBox",combos[0]);performanceEdit.Focus();performanceEdit.Text="поиск";Pump();
            Check(combos[0].Items.Cast<PerformanceInfo>().Any(p=>p.Title=="ТЕАТР Поисковый спектакль UI")&&combos[0].Items.Cast<PerformanceInfo>().All(p=>SearchText.Matches(p.Title,"поиск")),"performance search uses beginning of significant word");
            combos[0].SelectedItem=combos[0].Items.Cast<PerformanceInfo>().Single(p=>p.Title=="ТЕАТР Поисковый спектакль UI");combos[0].IsDropDownOpen=false;
            Check(combos.Count==6,"performance, venue, pricing, hours minutes and seconds selectors");
            Check(combos[3].Items.Count==24&&combos[4].Items.Count==60&&combos[5].Items.Count==60,"complete hours and minutes ranges");
            Check(combos[3].SelectedItem?.ToString()=="19"&&combos[4].SelectedItem?.ToString()=="00","default time is 19:00");
            Check(Math.Abs(editor.FontSize-16.9)<.01&&editor.FontWeight==FontWeights.Bold,"event form typography is larger and heavier");
            Check(combos.Skip(3).All(c=>c.IsEditable),"hours minutes and seconds accept manual input");
            var date=Logical(editor).OfType<DatePicker>().Single();Check(date.FontSize==26,"date text is doubled");Click(editor,"Календарь");Pump();
            var popup=(Popup)date.Template.FindName("PART_Popup",date);
            var calendar=Visual(popup.Child).OfType<Calendar>().Single();
            Check(calendar.LayoutTransform.Value.IsIdentity,"calendar remains at normal size");
            Check(calendar.DesiredSize.Width<300,"calendar popup has original layout bounds");
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
            Click(editor,"Добавить дату");date.SelectedDate=date.SelectedDate!.Value.AddDays(1);combos[3].Text="20";combos[4].Text="45";combos[5].Text="12";Click(editor,"Добавить дату");
            var dateList=Logical(editor).OfType<ListBox>().Single();Check(dateList.Items.Count==2,"multiple showing dates accumulate in one editor");
            Check(dateList.Items.Cast<DateTime>().Any(d=>d.Hour==20&&d.Minute==45&&d.Second==12),"each added date retains its own hours minutes and seconds");editor.Close();
            var hallType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.HallEditorWindow")!;
            var hallEditor=(Window)Activator.CreateInstance(hallType,new object?[]{store,null})!;
            Exception? hallError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{try{
            var hallFields=Logical(hallEditor).OfType<TextBox>().Where(t=>t.Parent is StackPanel).ToList();
            hallFields[0].Text="UI row hall";hallFields[3].Text="20";hallFields[5].Text="А";
            var rowGrid=Logical(hallEditor).OfType<DataGrid>().First(g=>g.Columns.Any(c=>Equals(c.Header,"Проход")));
            Check(!rowGrid.IsReadOnly,"per-row seats and aisles are editable");
            Check(rowGrid.Items.Count==20,"row count produces a configuration row for every hall row");
            ((HallRowLayout)rowGrid.Items[9]).SeatCount=17;hallFields[3].Text="2";hallFields[3].Text="20";
            Check(((HallRowLayout)rowGrid.Items[9]).SeatCount==17,"intermediate row-count typing preserves customized row settings");
            var configurations=rowGrid.Items.Cast<HallRowLayout>().ToArray();
            foreach(var row in configurations){row.SeatCount=1;row.AisleAfter=0;}configurations[0].SeatCount=23;configurations[0].AisleAfter=10;
            Click(hallEditor,"Добавить сектор");Pump();
            var draftPreview=Logical(hallEditor).OfType<Tickets.App.HallView>().Single();draftPreview.Selected.UnionWith(new long[]{1,2,3});
            Click(hallEditor,"Удалить выбранные (Del)");Click(hallEditor,"Шаг назад");
            var draftSeats=(System.Collections.ICollection)hallType.GetField("seats",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(hallEditor)!;
            Check(draftSeats.Count==42,"hall undo restores deleted places");
            Click(hallEditor,"Удалить выбранные (Del)");Click(hallEditor,"Сохранить как новый зал");
            }catch(Exception ex){hallError=ex;hallEditor.Close();}}));
            hallEditor.ShowDialog();Pump();if(hallError is not null)throw hallError;
            var editedHall=store.Venues().Single(v=>v.Name=="UI row hall");var remaining=store.HallDraft(editedHall.Id);
            Check(remaining.Count==39&&remaining.Where(x=>x.Row=="А").Select(x=>x.Number).Contains("4")&&!remaining.Where(x=>x.Row=="А").Any(x=>x.Number=="1"),"drawing deletion preserves surviving seat labels");
            Check(remaining.Single(x=>x.Row=="А"&&x.Number=="4").X==60,"drawing deletion preserves original seat coordinates");
            var automaticHall=(Window)Activator.CreateInstance(hallType,new object?[]{store,null})!;
            Exception? automaticError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{try{
                var inputs=Logical(automaticHall).OfType<TextBox>().Where(t=>t.Parent is StackPanel).ToList();
                inputs[0].Text="UI mixed automatic hall";inputs[3].Text="3";inputs[5].Text="А, 1";inputs[6].Text="А, 1";
                var layoutGrid=Logical(automaticHall).OfType<DataGrid>().First(g=>g.Columns.Any(c=>Equals(c.Header,"Проход")));
                foreach(var row in layoutGrid.Items.Cast<HallRowLayout>()){row.SeatCount=3;row.AisleAfter=0;}
                var save=Logical(automaticHall).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить как новый зал"));
                Check(save.IsVisible,"save hall is visible without scrolling");
                Click(automaticHall,"Сохранить как новый зал");
            }catch(Exception ex){automaticError=ex;automaticHall.Close();}}));
            automaticHall.ShowDialog();Pump();if(automaticError is not null)throw automaticError;
            var mixedHall=store.Venues().Single(v=>v.Name=="UI mixed automatic hall");
            var mixedSeats=store.HallDraft(mixedHall.Id);
            Check(mixedSeats.Count==9&&mixedSeats.Select(x=>x.Row).Distinct().OrderBy(x=>x).SequenceEqual(new[]{"1","2","А"}),"save builds configured first sector without a separate add action");
            Check(mixedSeats.Where(x=>x.Row=="А").OrderBy(x=>x.X).Select(x=>x.Number).SequenceEqual(new[]{"А","1","2"}),"mixed seat labels preserve drawing order");
            var specType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.SpecificationWindow")!;
            var specWindow=(Window)Activator.CreateInstance(specType,store,"UI organization")!;specWindow.Show();Pump();
            var specFields=Logical(specWindow).OfType<TextBox>().Where(t=>t.Parent is StackPanel).ToArray();specFields[^1].Text="Билеты: Партер, ряд А. Условия согласованы.";
            Click(specWindow,"Сохранить");Check(store.Specifications().Single().Recipient=="UI organization","organization specification persists from form");
            Check(File.Exists(Path.Combine(store.DocumentsDirectory,"Спецификации","Спецификация-1.docx")),"specification automatically saves Word");specWindow.Close();
            var reopened=(Window)Activator.CreateInstance(specType,store,"UI organization")!;reopened.Show();Pump();
            Check(Logical(reopened).OfType<TextBox>().Any(t=>t.Text.Contains("Условия согласованы")),"saved organization specification reopens");reopened.Close();
            var venueId=store.CreateHall("UI selection hall","",new[]{
                new DraftSeat{Row="1",Number="1",X=0,Y=0},new DraftSeat{Row="1",Number="2",X=30,Y=0},
                new DraftSeat{Row="2",Number="1",X=0,Y=30},new DraftSeat{Row="2",Number="2",X=30,Y=30}});
            var seatIds=store.VenueSeats(venueId).Select(s=>s.Id).ToArray();
            var planId=store.SavePlan(venueId,"UI plan",seatIds.Select(id=>new PriceValue(id,500,"#6EA8E0")).ToArray());
            var eventId=store.CreateEvent(store.SavePerformance("UI event"),venueId,planId,DateTime.Today.AddHours(19));
            var events=(TreeView)main.FindName("EventsList");
            typeof(Tickets.App.MainWindow).GetField("events",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,store.Events());
            typeof(Tickets.App.MainWindow).GetMethod("ApplyFilter",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);main.SelectEvent(eventId);Pump();
            var performanceGroup=events.Items.OfType<TreeViewItem>().First(i=>i.Header is TextBlock text&&text.Text=="UI event");performanceGroup.IsExpanded=false;performanceGroup.IsSelected=true;Pump();
            Check(performanceGroup.IsExpanded,"selecting a performance title opens its shows on one click");main.SelectEvent(eventId);Pump();
            var six=new Tickets.App.HallView();var sixWindow=new Window{Content=six,Width=600,Height=400};sixWindow.Show();Pump();
            var sixSeats=Enumerable.Range(0,6).Select(i=>new SeatInfo(i+1,"Партер",(i/3+1).ToString(),(i%3+1).ToString(),i%3*20,i/3*20,1000,"#00E676",null,"","",null)).ToArray();
            six.Load(sixSeats,Array.Empty<SectorInfo>(),true);Pump();sixWindow.Activate();six.Focus();
            Gesture(six,new Point(15,1),new Point(41,21));Check(six.Selected.Count==6,"partial rectangular overlap selects all six touched places");
            Gesture(six,new Point(1,1),new Point(35,35));Check(six.Selected.SetEquals(new long[]{3,6}),"drag from selected place removes a group and preserves others");CheckSeatRenderingAndContext(six,sixSeats);sixWindow.Close();
            var currentShowRow=performanceGroup.Items.OfType<TreeViewItem>().Single(i=>i.Tag is EventInfo e&&e.Id==eventId);
            Check(currentShowRow.Header is TextBlock selectedTitle&&Equals(selectedTitle.Foreground,Brushes.White)&&Equals(selectedTitle.Background,Brushes.DarkSlateBlue),"selected show text has contrast against its background");
            var hall=(Tickets.App.HallView)main.FindName("Hall");main.Activate();hall.Focus();hall.Fit();Pump();
            Gesture(hall,new Point(8,8),new Point(8,8));
            Gesture(hall,new Point(38,38),new Point(38,38));
            Check(hall.Selected.SetEquals(new[]{seatIds[0],seatIds[3]}),"separate single clicks accumulate places");
            Gesture(hall,new Point(23,-4),new Point(51,21));
            Check(hall.Selected.SetEquals(new[]{seatIds[0],seatIds[1],seatIds[3]}),"left drag adds seats without Shift and preserves previous choices");
            Gesture(hall,new Point(-4,23),new Point(21,51));
            Check(hall.Selected.Count==4,"another left drag accumulates another group");
            Gesture(hall,new Point(38,38),new Point(38,38));
            Check(hall.Selected.Count==3&&!hall.Selected.Contains(seatIds[3]),"repeat click deselects one place only");
            Click(main,"Изменить цену");Check(((StackPanel)main.FindName("PriceEditorPanel")).IsVisible,"price editor opens above selection summary");
            ((TextBox)main.FindName("PriceBox")).Text="123";Click(main,"Применить цену");
            Check(hall.Selected.Count==0,"applying price clears selection");
            Check(new Store(db).Seats(eventId).Where(s=>seatIds.Take(3).Contains(s.Id)).All(s=>s.Amount==123&&s.Color==store.PriceColors().Single(c=>c.Amount==123).Color),"price and category color persist and update scheme data");
            Click(main,"Отменить изменение цены");Check(store.Seats(eventId).All(s=>s.Amount==500),"price undo restores previous category");
            hall.SetSelection(seatIds.Take(3));Click(main,"Изменить цену");((TextBox)main.FindName("PriceBox")).Text="123";Click(main,"Применить цену");hall.SetSelection(seatIds.Take(3));
            Check(((ComboBox)main.FindName("RecipientBox")).Text=="","show opens without a previous recipient");
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
            }catch(Exception ex){senderCreationError=ex;creation?.Close();}}));Click(main,"Создать / изменить отправителя");if(senderCreationError is not null)throw senderCreationError;
            Check(senderBox.Text=="UI sender created from main"&&store.Senders().Contains(senderBox.Text),"main sender creation persists and selects sender");senderBox.Text=Store.DefaultSender;
            ((ComboBox)main.FindName("RecipientBox")).Text="UI recipient";
            var summaryLabel=(TextBlock)main.FindName("SelectedSummary");Check(Math.Abs(summaryLabel.FontSize-16.8)<.01&&summaryLabel.FontWeight==FontWeights.Bold&&summaryLabel.Foreground.ToString()=="#FFC1121F","selected quantity and sum retain bold red styling at 70 percent size");
            var issueButton=(Button)main.FindName("IssueButton");Check(issueButton.IsEnabled,"priced selection ready for issuance");
            Exception? invoiceError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var window=app.Windows.Cast<Window>().Single(w=>w.Title.StartsWith("Накладная №"));
                try{
                    var document=Logical(window).OfType<FlowDocumentScrollViewer>().Single().Document;
                    var text=new TextRange(document.ContentStart,document.ContentEnd).Text;
                    Check(text.Contains("UI recipient")&&text.Contains("369"),"invoice opens immediately with actual prices and correct total");
                    Check(!Logical(window).OfType<ListBox>().Any(),"invoice has no return-ticket list");
                    Check(!Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Сохранить как…")),"invoice has no duplicate Save As action");
                    var squareSave=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить"));var squarePrint=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Распечатать"));
                    Check(squareSave.Width==squareSave.Height&&squarePrint.Width==squarePrint.Height&&squareSave.Width==112,"save and print are prominent square buttons");
                    Check(Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Печать билетов"))&&Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Дополнить накладную")),"invoice ticket actions use readable text labels in the shared button template");
                    Check(Logical(window).OfType<FlowDocumentScrollViewer>().Single().ActualHeight>400,"invoice preview uses enlarged content area");
                    var invoiceTab=Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"Накладная"));Check(Equals(invoiceTab.Background,Brushes.SteelBlue),"invoice tab is visibly active");
                    var senderField=Logical(window).OfType<ComboBox>().Single();Check(senderField.IsEditable&&senderField.Items.Contains(Store.DefaultSender),"invoice sender supports saved choice and manual entry");
                    senderField.Text="UI sender";Click(window,"Сохранить");
                    var issuedId=store.Issues(eventId).Single().Id;
                    Check(store.Invoice(eventId,issuedId).Sender=="UI sender","edited invoice sender persists");
                    Check(store.PrepareIssueDocuments(eventId,issuedId).Count==4&&store.PrepareIssueDocuments(eventId,issuedId).All(d=>d.Invoice.Recipient=="UI recipient"&&d.Body.Contains("UI recipient")),"all four documents are automatically prepared for invoice recipient");
                    Check(text.Contains(DocumentTotals.Summary(3,369))&&text.Contains(DocumentTotals.Total(3,369)),"invoice totals use shared two-line format");
                    Exception? documentError=null;
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                        var form=app.Windows.Cast<Window>().Single(w=>w.Title.StartsWith("Договор · выдача №"));
                        try{
                            Check(Equals(Logical(form).OfType<Button>().Single(b=>Equals(b.Content,"Договор")).Background,Brushes.SteelBlue)&&Logical(form).OfType<Button>().Any(b=>Equals(b.Content,"Накладная")),"contract toolbar marks active document and offers invoice");
                            var rendered=Logical(form).OfType<FlowDocumentScrollViewer>().Single().Document;Check(new TextRange(rendered.ContentStart,rendered.ContentEnd).Text.Contains("UI recipient"),"contract preview substitutes actual recipient");
                            Logical(form).OfType<TabControl>().Single().SelectedIndex=1;Logical(form).OfType<TextBox>().Single().Text="Договор для {recipient}. Отправитель: {sender}.";Click(form,"Сохранить шаблон");
                            Check(store.DocumentTemplate(IssueDocumentKinds.Contract).Contains("{recipient}"),"document template saves for later issuances");
                        }catch(Exception ex){documentError=ex;}finally{form.Close();}
                    }));Click(window,"Договор");if(documentError is not null)throw documentError;
                    var paginator=((IDocumentPaginatorSource)document).DocumentPaginator;paginator.ComputePageCount();
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
            hall.Selected.Add(seatIds[0]);typeof(Tickets.App.MainWindow).GetMethod("UpdateSelection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);
            Check(((TextBlock)main.FindName("IssuedRecipients")).Text.Contains("UI recipient"),"issued selection explicitly identifies recipient");
            var warning=((ListBox)main.FindName("SelectedList")).Items.Cast<object>().Single();
            Check(Equals(warning.GetType().GetProperty("AlertBrush")!.GetValue(warning),Brushes.Red),"occupied selected place is red in the selected list");
            var printButton=(Button)main.FindName("PrintTicketsButton");Check(printButton.IsEnabled&&Equals(printButton.Content,"Печать билетов"),"issued but unprinted places expose initial printing without changing ticket numbers");
            hall.SetSelection(new[]{seatIds[3]});((ComboBox)main.FindName("RecipientBox")).Text="UI print preview only";Pump();
            Check(printButton.IsVisible&&printButton.IsEnabled&&Equals(printButton.Content,"Печать билетов"),"primary ticket printing action is visible and available");
            var ticketPrintType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketPrintWindow")!;Exception? ticketPreviewError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? previewWindow=null;try{
                previewWindow=app.Windows.Cast<Window>().Single(w=>w.GetType()==ticketPrintType&&w.IsVisible);
                Check(Logical(previewWindow).OfType<DocumentPageView>().Any(),"ticket printing opens actual per-ticket page preview");
                Check(store.History(eventId).Count==3,"opening print preview creates no issuance");
            }catch(Exception ex){ticketPreviewError=ex;}finally{previewWindow?.Close();}}));Click(main,"Печать билетов");if(ticketPreviewError is not null)throw ticketPreviewError;
            Check(store.History(eventId).Count==3&&!store.Recipients().Contains("UI print preview only"),"closing ticket print preview reserves neither seats nor recipient");
            var originalInvoice=store.Invoice(eventId,store.Issues(eventId).Single().Id);
            hall.SetSelection(new[]{seatIds[0],seatIds[3]});((ComboBox)main.FindName("RecipientBox")).Text="UI mixed preview only";Pump();
            Check(printButton.IsEnabled,"mixed free and occupied selection permits printing preview");
            Exception? mixedPreviewError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{Window? previewWindow=null;try{
                previewWindow=app.Windows.Cast<Window>().Single(w=>w.GetType()==ticketPrintType&&w.IsVisible);
                var occupied=(InvoiceInfo)ticketPrintType.GetField("originalOccupied",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(previewWindow)!;
                Check(occupied.Tickets.Count==1&&occupied.Tickets[0]==originalInvoice.Tickets.Single(t=>t.SeatId==seatIds[0]),"mixed preview retains occupied ticket identity, price, series and invoice");
                var page=Logical(previewWindow).OfType<DocumentPageView>().First().DocumentPaginator.GetPage(0).Visual;
                Check(Visual(page).OfType<Border>().Count()==2,"mixed preview renders both free and occupied ticket cards");
            }catch(Exception ex){mixedPreviewError=ex;}finally{previewWindow?.Close();}}));Click(main,"Печать билетов");if(mixedPreviewError is not null)throw mixedPreviewError;
            var invoiceAfterPreview=store.Invoice(eventId,originalInvoice.Id);
            Check(store.History(eventId).Count==3&&store.Issues(eventId).Count==1&&invoiceAfterPreview.Tickets.SequenceEqual(originalInvoice.Tickets)&&invoiceAfterPreview.Sender==originalInvoice.Sender&&!store.Recipients().Contains("UI mixed preview only")&&store.Seats(eventId).Single(s=>s.Id==seatIds[3]).Available,"closing mixed preview preserves original invoice and leaves free seat unissued");
            var extensionType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.InvoiceExtensionWindow")!;
            var extension=(Window)Activator.CreateInstance(extensionType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{store,eventId,originalInvoice.Id},null)!;extension.Show();Pump();
            var extensionHall=Logical(extension).OfType<Tickets.App.HallView>().Single();var extendButton=Logical(extension).OfType<Button>().Single(b=>Equals(b.Content,"Дополнить накладную"));
            Check(extensionHall.ActualWidth>100&&extensionHall.ActualHeight>100&&!extendButton.IsEnabled,"invoice extension renders hall and requires a selection");
            extensionHall.SetSelection(new[]{seatIds[0]});Check(!extendButton.IsEnabled,"invoice extension rejects occupied seat selection");
            extensionHall.SetSelection(new[]{seatIds[3]});Check(extendButton.IsEnabled&&Logical(extension).OfType<TextBlock>().Any(t=>t.Text.Contains("Мест: 1")),"invoice extension enables one free priced seat and updates summary");
            extensionHall.SetSelection(new[]{seatIds[0],seatIds[3]});Check(!extendButton.IsEnabled,"invoice extension rejects mixed occupied and free selection");extension.Close();
            store.SetSeatsBlocked(eventId,new[]{seatIds[3]},true);
            typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{false});hall.SetSelection(new[]{seatIds[3]});
            Check(Equals(((Button)main.FindName("SaleStateButton")).Content,"Вернуть в продажу")&&!issueButton.IsEnabled&&!printButton.IsEnabled,"withdrawn places cannot issue or print and expose restore action");
            store.SetSeatsBlocked(eventId,new[]{seatIds[3]},false);typeof(Tickets.App.MainWindow).GetMethod("LoadEvent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{false});
            var sample=store.TicketReprint(eventId,new[]{seatIds[0]});
            var many=sample with{Tickets=Enumerable.Range(0,25).Select(i=>sample.Tickets[0] with{Number=i+1,Seat=(i+1).ToString()}).ToArray()};
            var printedDocument=(FixedDocument)ticketPrintType.GetMethod("BuildDocument",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{many,794d,1123d})!;
            printedDocument.DocumentPaginator.ComputePageCount();
            Check(printedDocument.Pages.Count==4&&printedDocument.DocumentPaginator.GetPage(0).Visual is not null,"ticket cards paginate without dropping tickets");
            var cardCount=printedDocument.Pages.Sum(p=>p.Child.Children.OfType<Border>().Count());Check(cardCount==25,"each selected ticket has its own printable card");
            var raster=new System.Windows.Media.Imaging.RenderTargetBitmap(794,1123,96,96,PixelFormats.Pbgra32);raster.Render(printedDocument.DocumentPaginator.GetPage(0).Visual);
            var archivedVenue=store.CreateHall("UI archived venue","",new[]{new DraftSeat{Number="1"},new DraftSeat{Number="2",X=26}});
            var archivedSeats=store.VenueSeats(archivedVenue).Select(s=>s.Id).ToArray();var archivePlan=store.SavePlan(archivedVenue,"Archive plan",archivedSeats.Select(id=>new PriceValue(id,1000,store.PriceColors().Single(p=>p.Amount==1000).Color)).ToArray());
            var archivedEvent=store.CreateEvent(store.SavePerformance("UI archived performance"),archivedVenue,archivePlan,DateTime.Today.AddDays(20));store.Issue(archivedEvent,new[]{archivedSeats[0]},"Архив UI организация","ARCHUI",1);
            var reload=typeof(Tickets.App.MainWindow).GetMethod("RefreshCatalogNavigation",BindingFlags.Instance|BindingFlags.NonPublic)!;reload.Invoke(main,null);main.SelectEvent(archivedEvent);Pump();
            store.DeleteVenue(archivedVenue);store.DeleteRecipient("Архив UI организация");reload.Invoke(main,null);Pump();
            Check(events.Items.OfType<TreeViewItem>().Any(i=>i.Header?.ToString()?.StartsWith("Удалённые из справочников")==true)&&((Grid)main.FindName("Workspace")).IsVisible,"deleted shows remain accessible in archive without losing selected context");
            hall.SetSelection(new[]{archivedSeats[0]});Check(((Button)main.FindName("ReturnButton")).IsEnabled,"archived show still permits returning issued seats on scheme");
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
                Check(Logical(selectedPricing).OfType<TextBox>().First().Text=="UI plan (копия)","selected pricing plan loads directly in its editor");
                Check(!Logical(selectedPricing).OfType<CheckBox>().Any(c=>Equals(c.Content,"Расценовки всех площадок")),"cross-venue pricing switch removed");
            }catch(Exception ex){directoryError=ex;}finally{selectedPricing?.Close();}}));
            pricingLeaf.IsSelected=true;if(directoryError is not null)throw directoryError;pricingDirectory.Close();
            var priceEditorType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.PriceEditorWindow")!;
            var categoryEditor=(Window)Activator.CreateInstance(priceEditorType,new object?[]{store,(long?)venueId})!;categoryEditor.Show();Pump();
            var categoryFields=Logical(categoryEditor).OfType<TextBox>().ToList();var categoryHall=Logical(categoryEditor).OfType<Tickets.App.HallView>().Single();
            categoryHall.SetSelection(seatIds.Take(2));categoryFields[1].Text="1000";Click(categoryEditor,"Назначить выбранным местам");
            Check(categoryHall.Selected.Count==0,"pricing editor clears selection after first price assignment");
            categoryHall.SetSelection(seatIds.Skip(2));categoryFields[1].Text="2000";Click(categoryEditor,"Назначить выбранным местам");
            var assigned=(System.Collections.IDictionary)priceEditorType.GetField("prices",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(categoryEditor)!;
            Check(((PriceValue)assigned[seatIds[0]]!).Amount==1000&&((PriceValue)assigned[seatIds[1]]!).Amount==1000&&((PriceValue)assigned[seatIds[2]]!).Amount==2000,"next price assignment preserves earlier differently priced places");
            Check(categoryHall.Selected.Count==0&&!Logical(categoryEditor).OfType<Button>().Any(b=>Equals(b.Content,"Снять выделение")),"pricing uses automatic clearing without extra clear button");categoryEditor.Close();
            var performanceDirectoryType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.PerformanceWindow")!;
            var performanceDirectory=(Window)Activator.CreateInstance(performanceDirectoryType,store)!;performanceDirectory.Show();Pump();
            var performanceTree=Logical(performanceDirectory).OfType<TreeView>().Single();
            var activePerformance=performanceTree.Items.OfType<TreeViewItem>().Single(i=>i.Tag is PerformanceInfo p&&p.Title=="UI event");activePerformance.IsSelected=true;Pump();
            Check(activePerformance.IsExpanded&&activePerformance.Items.OfType<TreeViewItem>().Any(i=>i.Tag is EventInfo e&&e.Id==eventId),"performance directory opens its active showing list");
            Check(activePerformance.ContextMenu!.Items.OfType<MenuItem>().Any(m=>Equals(m.Header,"Удалить"))&&!Logical(performanceDirectory).OfType<Button>().Any(b=>Equals(b.Content,"Удалить спектакль")),"performance deletion uses context menu without extra button");performanceDirectory.Close();
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
                    var boxes=Logical(content).OfType<ComboBox>().ToList();boxes[1].SelectedItem=boxes[1].Items.Cast<VenueInfo>().Single(v=>v.Id==venueId);
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
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var zoom=(double)typeof(Tickets.App.HallView).GetField("zoom",flags)!.GetValue(hall)!;
        var offset=(Vector)typeof(Tickets.App.HallView).GetField("offset",flags)!.GetValue(hall)!;
        void Position(Point p){var point=hall.PointToScreen(new Point(p.X*zoom+offset.X,p.Y*zoom+offset.Y));if(!SetCursorPos((int)point.X,(int)point.Y))throw new Exception("Windows test desktop cannot move the cursor");WaitForInput();}
        Position(from);mouse_event(right?8u:2u,0,0,0,UIntPtr.Zero);WaitForInput();
        Position(to);mouse_event(right?16u:4u,0,0,0,UIntPtr.Zero);WaitForInput();
        Console.WriteLine($"Mouse gesture {from} -> {to}; selected: {string.Join(',',hall.Selected)}");
    }
    private static void CheckSeatRenderingAndContext(Tickets.App.HallView hall,SeatInfo[] source)
    {
        var states=new[]{source[0] with{Number="",Row="",X=0,Y=0,TicketId=101,IsReserved=false,IsPrinted=false},
            source[1] with{Number="",Row="",X=24,Y=0,TicketId=102,IsReserved=false,IsPrinted=true},
            source[2] with{Number="",Row="",X=48,Y=0,TicketId=103,IsReserved=true,IsPrinted=false}};
        hall.Load(states,Array.Empty<SectorInfo>(),true);Pump();
        var drawing=(DrawingGroup)typeof(Tickets.App.HallView).GetField("drawing",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(hall)!;
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new ScaleTransform(8,8));dc.DrawDrawing(drawing);}
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(520,144,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var pixels=new byte[520*144*4];bitmap.CopyPixels(pixels,520*4,0);
        int Count(int seat,Func<byte,byte,byte,bool> match)
        {
            var count=0;for(var y=16;y<112;y++)for(var x=seat*192+16;x<seat*192+112;x++){var i=(y*520+x)*4;if(pixels[i+3]==255&&match(pixels[i+2],pixels[i+1],pixels[i]))count++;}return count;
        }
        Check(Count(0,(r,g,b)=>r<20&&g<20&&b<20)>1000&&Count(0,(r,g,b)=>r>220&&g>220&&b>220)>500,"invoice-only occupied seat renders black with dense white hatching");
        Check(Count(1,(r,g,b)=>r>230&&g<20&&b<20)>1000&&Count(1,(r,g,b)=>r<20&&g<20&&b<20)>500,"issued and printed seat renders red with dense black hatching");
        Check(Count(2,(r,g,b)=>r<20&&g<20&&b<20)>1000&&Count(2,(r,g,b)=>r>220&&g>220&&b>220)>500,"reserved seat renders black with white hatching");
        Check(pixels[(64*520+136)*4+3]==0,"seat hatching remains clipped inside seat bounds");
        var contexts=new List<long>();hall.SeatContextRequested+=(id,_)=>contexts.Add(id);var offsetField=typeof(Tickets.App.HallView).GetField("offset",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var before=(Vector)offsetField.GetValue(hall)!;Gesture(hall,new Point(8,8),new Point(8,8),true);
        Check(contexts.SequenceEqual(new[]{states[0].Id})&&((Vector)offsetField.GetValue(hall)!)==before,"stationary right click requests seat context without panning");
        Gesture(hall,new Point(8,8),new Point(38,8),true);
        Check(contexts.Count==1&&((Vector)offsetField.GetValue(hall)!)!=before&&hall.Selected.Count==0,"right drag pans without opening seat context or changing selection");
    }
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
