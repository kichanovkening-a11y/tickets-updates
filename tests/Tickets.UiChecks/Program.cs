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
        try
        {
            var db=Path.Combine(work,"tickets.db");File.Copy(args[0],db);var store=new Store(db);
            var main=new Tickets.App.MainWindow(store);app.MainWindow=main;Check(!main.SeparateWindows,"embedded forms are the default");main.SeparateWindows=true;main.Show();Pump();
            var actions=Logical(main).OfType<Button>().ToList();
            Check(actions.Count>=15,"main actions found");
            Check(actions.Where(b=>b.IsVisible).All(b=>b.ActualWidth==200&&b.ActualHeight==44),"all main action buttons have equal rendered size");
            var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.EventEditorWindow")!;
            var editor=(Window)Activator.CreateInstance(type,store)!;editor.Show();Pump();
            var combos=Logical(editor).OfType<ComboBox>().ToList();
            Check(combos.Count==6,"performance, venue, pricing, hours minutes and seconds selectors");
            Check(combos[3].Items.Count==24&&combos[4].Items.Count==60&&combos[5].Items.Count==60,"complete hours and minutes ranges");
            Check(combos[3].SelectedItem?.ToString()=="19"&&combos[4].SelectedItem?.ToString()=="00","default time is 19:00");
            Check(Math.Abs(editor.FontSize-16.9)<.01&&editor.FontWeight==FontWeights.SemiBold,"event form typography is larger and heavier");
            Check(combos.Skip(3).All(c=>c.IsEditable),"hours minutes and seconds accept manual input");
            var date=Logical(editor).OfType<DatePicker>().Single();Check(date.FontSize==26,"date text is doubled");Click(editor,"Календарь");Pump();
            var popup=(Popup)date.Template.FindName("PART_Popup",date);
            var calendar=Visual(popup.Child).OfType<Calendar>().Single();
            Check(calendar.LayoutTransform.Value.IsIdentity,"calendar remains at normal size");
            Check(calendar.DesiredSize.Width<300,"calendar popup has original layout bounds");
            date.IsDropDownOpen=false;
            Exception? nestedError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var pricing=app.Windows.Cast<Window>().Single(w=>w.Title=="Расценовки");
                try
                {
                    Check(((VenueInfo)Logical(pricing).OfType<ComboBox>().First().SelectedItem).Id==((VenueInfo)combos[1].SelectedItem).Id,"pricing creation uses chosen venue");
                    Logical(pricing).OfType<TextBox>().First().Text="UI test pricing";
                    Click(pricing,"Назначить всему залу");Click(pricing,"Сохранить новую расценовку");
                }
                catch(Exception ex){nestedError=ex;pricing.Close();}
            }));
            Click(editor,"Создать");Pump();if(nestedError is not null)throw nestedError;
            Check(combos[2].SelectedItem is PlanInfo plan&&plan.Name=="UI test pricing","new pricing selected after nested editor saves");
            editor.Close();
            var hallType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.HallEditorWindow")!;
            var hallEditor=(Window)Activator.CreateInstance(hallType,new object?[]{store,null})!;
            Exception? hallError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{try{
            var hallFields=Logical(hallEditor).OfType<TextBox>().Where(t=>t.Parent is StackPanel).ToList();
            hallFields[0].Text="UI row hall";hallFields[3].Text="20";hallFields[5].Text="А";
            var rowGrid=Logical(hallEditor).OfType<DataGrid>().First(g=>g.Columns.Any(c=>Equals(c.Header,"Проход")));
            Check(!rowGrid.IsReadOnly,"per-row seats and aisles are editable");
            Check(rowGrid.Items.Count==20,"row count produces a configuration row for every hall row");
            var configurations=rowGrid.Items.Cast<HallRowLayout>().ToArray();
            foreach(var row in configurations){row.SeatCount=1;row.AisleAfter=0;}configurations[0].SeatCount=23;configurations[0].AisleAfter=10;
            Click(hallEditor,"Добавить сектор");Pump();
            var draftPreview=Logical(hallEditor).OfType<Tickets.App.HallView>().Single();draftPreview.Selected.UnionWith(new long[]{1,2,3});
            Click(hallEditor,"Удалить выбранные (Del)");Click(hallEditor,"Сохранить как новый зал");
            }catch(Exception ex){hallError=ex;hallEditor.Close();}}));
            hallEditor.ShowDialog();Pump();if(hallError is not null)throw hallError;
            var editedHall=store.Venues().Single(v=>v.Name=="UI row hall");var remaining=store.HallDraft(editedHall.Id);
            Check(remaining.Count==39&&remaining.Where(x=>x.Row=="А").Select(x=>x.Number).Contains("4")&&!remaining.Where(x=>x.Row=="А").Any(x=>x.Number=="1"),"drawing deletion preserves surviving seat labels");
            Check(remaining.Single(x=>x.Row=="А"&&x.Number=="4").X==60,"drawing deletion preserves original seat coordinates");
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
            ((ComboBox)main.FindName("RecipientBox")).Text="UI recipient";
            var issueButton=(Button)main.FindName("IssueButton");Check(issueButton.IsEnabled,"priced selection ready for issuance");
            Exception? invoiceError=null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var window=app.Windows.Cast<Window>().Single(w=>w.Title.StartsWith("Накладная №"));
                try{
                    var document=Logical(window).OfType<FlowDocumentReader>().Single().Document;
                    var text=new TextRange(document.ContentStart,document.ContentEnd).Text;
                    Check(text.Contains("UI recipient")&&text.Contains("369"),"invoice opens immediately with actual prices and correct total");
                    var returnList=Logical(window).OfType<ListBox>().Single();returnList.SelectedIndex=0;
                    var senderField=Logical(window).OfType<TextBox>().Single();senderField.Text="UI sender";Click(window,"Сохранить в Word");
                    Check(returnList.SelectedItems.Count==1,"saving sender preserves tickets selected for return");
                    Check(store.Invoice(eventId,store.Issues(eventId).Single().Id).Sender=="UI sender","edited invoice sender persists");
                    var paginator=((IDocumentPaginatorSource)document).DocumentPaginator;paginator.ComputePageCount();
                    Check(paginator.PageCount>=1&&paginator.GetPage(0).Visual is not null,"invoice paginates for printing");
                    Check(text.Contains(Store.DefaultSender)&&text.Contains("триста шестьдесят девять рублей")&&!text.Contains("ТЕСТ 000001"),"invoice has sender and words without ticket serials");
                    Check(Directory.GetFiles(Path.Combine(store.DocumentsDirectory,"Накладные"),"*.docx").Length==1,"invoice automatically saves Word inside program data");
                    Check(Logical(window).OfType<Button>().Any(b=>Equals(b.Content,"Сохранить в Word")),"Word save action available");
                }catch(Exception ex){invoiceError=ex;}finally{window.Close();}
            }));
            issueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();if(invoiceError is not null)throw invoiceError;
            var history=(DataGrid)main.FindName("HistoryGrid");Check(history.Items.Count==1,"one journal row for one issuance");
            Check(store.History(eventId).All(t=>t.Amount==123),"UI price overrides were persisted");
            hall.Selected.Add(seatIds[0]);typeof(Tickets.App.MainWindow).GetMethod("UpdateSelection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);
            Check(((TextBlock)main.FindName("IssuedRecipients")).Text.Contains("UI recipient"),"issued selection explicitly identifies recipient");
            main.SelectEvent(store.Events().First(e=>e.Id!=eventId).Id);Pump();
            Check(history.Items.Count==0,"journal hides issuances of other events");
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
                            Logical(pricing).OfType<TextBox>().First().Text="Embedded pricing";Click(pricing,"Назначить всему залу");Click(pricing,"Сохранить новую расценовку");
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
            main.Close();Console.WriteLine("PASS complete catalog navigation, embedded forms, selection and invoices");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{foreach(var w in app.Windows.Cast<Window>().ToArray())w.Close();app.Shutdown();Directory.Delete(work,true);}
    }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    private static void Gesture(Tickets.App.HallView hall,Point from,Point to)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var zoom=(double)typeof(Tickets.App.HallView).GetField("zoom",flags)!.GetValue(hall)!;
        var offset=(Vector)typeof(Tickets.App.HallView).GetField("offset",flags)!.GetValue(hall)!;
        void Position(Point p){var point=hall.PointToScreen(new Point(p.X*zoom+offset.X,p.Y*zoom+offset.Y));if(!SetCursorPos((int)point.X,(int)point.Y))throw new Exception("Windows test desktop cannot move the cursor");WaitForInput();}
        Position(from);mouse_event(2,0,0,0,UIntPtr.Zero);WaitForInput();
        Position(to);mouse_event(4,0,0,0,UIntPtr.Zero);WaitForInput();
        Console.WriteLine($"Mouse gesture {from} -> {to}; selected: {string.Join(',',hall.Selected)}");
    }
    private static void WaitForInput()
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(80)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    private static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static void Click(DependencyObject root,string label)=>Logical(root).OfType<Button>().Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
