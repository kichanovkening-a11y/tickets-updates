using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Corrections044UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Check(bool value,string message){if(!value)throw new Exception("044 "+message);Console.WriteLine("PASS 044 "+message);}
    internal static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Walk(DependencyObject? node)
    {if(node is null)yield break;yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static Rect Screen(FrameworkElement element)=>new(element.PointToScreen(new Point()),element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight)));
    private static T PrivateValue<T>(object target,string name)=>(T)target.GetType().GetField(name,Private)!.GetValue(target)!;
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    private static void Hosted(MainWindow main,EditorWindow editor,Action<FrameworkElement> body)
    {
        Exception? failure=null;
        main.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            try
            {
                var host=Walk((DependencyObject)main.FindName("DialogLayer")).OfType<ContentControl>().Single(c=>ReferenceEquals(c.Tag,editor));
                body((FrameworkElement)host.Content);
            }
            catch(Exception ex){failure=ex;}
            finally{editor.Close();}
        }));
        editor.ShowDialog();Pump();if(failure is not null)throw failure;
    }
    internal static void Run(string seed)
    {
        var work=Path.Combine(Path.GetTempPath(),"Tickets044-"+Guid.NewGuid());Directory.CreateDirectory(work);
        var db=Path.Combine(work,"tickets.db");File.Copy(seed,db);var store=new Store(db);
        var venue=store.CreateHall("UI044","",Enumerable.Range(1,8).Select(i=>new DraftSeat{Number=i.ToString(),X=i*22}).ToArray());
        var ids=store.VenueSeats(venue).Select(s=>s.Id).ToArray();
        var plan=store.SavePlan(venue,"Original044",ids.Select(id=>new PriceValue(id,1000,"#FFEF44")).ToArray());
        var second=store.SavePlan(venue,"Different044",ids.Select((id,i)=>new PriceValue(id,1000*(1+i%4),new[]{"#FFEF44","#F57333","#A003FF","#22AADD"}[i%4])).ToArray());
        var past=store.CreateEvent(store.SavePerformance("Past044"),venue,plan,new DateTime(2000,1,1,19,0,0));
        var future=store.CreateEvent(store.SavePerformance("Future044"),venue,second,new DateTime(2099,1,1,19,0,0));
        var a=store.Issue(past,[ids[0]],"Recipient044","ARC044",1);var b=store.Issue(future,[ids[0]],"Recipient044","ARC044",2);
        var mixed=store.CreateRecipientPackage("Recipient044",[(past,a),(future,b)]);
        var old=store.CreateRecipientPackage("Recipient044",[(past,a)]);
        var originalBytes=store.GetRecipientPackageDocument(old.Id,"invoice").SourceDocument!;
        var main=new MainWindow(store){Width=1536,Height=880,WindowState=WindowState.Normal,WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        Application.Current.MainWindow=main;main.Show();main.SelectEvent(future);Pump();
        try
        {
            var view=(ComboBox)main.FindName("SelectedViewBox");
            Check(view.SelectedIndex==1,"price summary is the default");
            view.SelectedIndex=0;main.SelectEvent(past);Pump();
            Check(view.SelectedIndex==0,"manual view survives event change");main.SelectEvent(future);view.SelectedIndex=1;
            ((HallView)main.FindName("Hall")).SetSelection(ids.Skip(1));Pump();
            var rows=((ListBox)main.FindName("SelectedPriceSummary")).Items.Cast<PriceSummaryRow044>().ToArray();
            Check(rows.Length==5&&rows.All(r=>!r.Text.Contains('₽')&&!r.Text.Contains(" ×")&&!r.Text.Contains("× ")),"price summary removes rubles and spaces around multiplication");
            var refresh=(Button)main.FindName("RefreshTicketlandButton029");var expand=(Button)main.FindName("ToggleHallButton038");
            foreach(var mode in new[]{false,true})
            {
                Check(refresh.Width==32&&refresh.Height==32&&refresh.FontSize==24&&expand.Width==32&&expand.Height==32,"original icon button dimensions restored");
                var hall=(HallView)main.FindName("Hall");
                var tabs=(TabControl)main.FindName("WorkspaceTabs");var border=(FrameworkElement)tabs.Template.FindName("HallContentBorder044",tabs);
                Check(Math.Abs(Screen(border).Top-Screen(expand).Bottom-5)<.7,$"five pixels between icons and hall border: {Screen(border).Top-Screen(expand).Bottom}");
                if(!mode)
                {
                    var title=(FrameworkElement)main.FindName("EventTitle");var subtitle=(FrameworkElement)main.FindName("EventSubtitle");
                    Check(Math.Abs(Screen(subtitle).Top-Screen(title).Bottom-3)<.7,"three physical pixels from heading to statistics");
                }
                Corrections025UiChecks.Capture(main,mode?"044-main-expanded":"044-main-normal");Click(expand);
            }
            Hosted(main,new EventEditorWindow(store),content=>{
                var caption=Walk((DependencyObject)main.FindName("DialogLayer")).OfType<TextBlock>().Single(t=>t.Text=="Новый показ");
                var header=(Grid)caption.Parent;
                Check(Math.Abs(Screen(caption).Left+Screen(caption).Width/2-(Screen(header).Left+Screen(header).Width/2))<1,"new show caption centers across whole header");
                var first=Walk(content).OfType<TextBlock>().First(t=>t.Text.StartsWith("Спектакль —"));
                Check(Screen(first).Top-Screen(header).Bottom<6,"new show form begins immediately below blue header");
                Corrections025UiChecks.Capture(main,"044-new-show");
            });
            var pricing=new PriceEditorWindow(store,venue,plan);
            Hosted(main,pricing,content=>{
                var combos=Walk(content).OfType<ComboBox>().ToArray();var plans=combos[1];
                var prices=PrivateValue<Dictionary<long,PriceValue>>(pricing,"prices");
                Check(prices.Values.All(p=>p.Amount==1000),"caller-selected pricing loads initially");
                plans.SelectedItem=plans.Items.Cast<object>().Single(item=>item.ToString()!.Contains("Different044"));Pump();
                Check(prices.Values.Select(p=>p.Amount).Distinct().Count()==4,"selecting another pricing plan loads immediately without confirmation");
                Check(!Walk(content).OfType<Button>().Any(button=>button.Content is string text&&new[]{"Загрузить выбранную расценовку","Заблокировать","Шаг назад","Назначить выбранным местам","Назначить всему залу","Раскрыть зал","Свернуть зал"}.Contains(text)),"redundant pricing buttons removed");
                var grid=PrivateValue<Grid>(pricing,"priceSummary");var plaques=grid.Children.OfType<Border>().ToArray();
                Check(plaques.Length==4&&plaques.Select(Grid.GetRow).SequenceEqual(new[]{0,1,2,3})&&plaques.All(p=>Grid.GetColumn(p)==0),"pricing plaques form one vertical column");
                Check(Screen(grid).Left>Screen(PrivateValue<HallView>(pricing,"preview")).Right,"pricing plaques moved into right sidebar");
                var toggle=Walk((DependencyObject)main.FindName("DialogLayer")).OfType<Button>().Single(button=>Equals(button.ToolTip,"Раскрыть зал")&&button.IsVisible);
                var close=Walk((DependencyObject)main.FindName("DialogLayer")).OfType<Button>().Single(button=>Equals(button.Content,"Закрыть")&&button.IsVisible);
                Check(Math.Abs(Screen(close).Left-Screen(toggle).Right-5)<.7&&toggle.Width==32,"pricing expand icon is five pixels left of Close at original size");
                var header=(Grid)((FrameworkElement)toggle.Parent).Parent;
                var total=PrivateValue<TextBlock>(pricing,"hallTotals");
                Check(Math.Abs(Screen((FrameworkElement)total.Parent).Top-Screen(header).Bottom-5)<.7,"pricing totals start five physical pixels below blue header");
                var form=(StackPanel)grid.Parent;var blocks=form.Children.OfType<FrameworkElement>().Where(block=>block.Visibility==Visibility.Visible).ToArray();
                for(var i=1;i<blocks.Length;i++)Check(Math.Abs(Screen(blocks[i]).Top-Screen(blocks[i-1]).Bottom-5)<.7,"pricing sidebar consecutive block gap is five pixels: "+i);
                var before=Screen(content).Width;Click(toggle);Check(Screen(content).Width>before&&pricing.HallExpanded038,"embedded pricing expands");
                Corrections025UiChecks.Capture(main,"044-pricing-expanded");Click(toggle);Check(!pricing.HallExpanded038,"embedded pricing restores");
                var hall=PrivateValue<HallView>(pricing,"preview");hall.SetSelection([ids[1]]);
                plaques[0].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
                Check(hall.FocusPrice.HasValue&&hall.Selected.SetEquals([ids[1]]),"moved pricing plaque retains focus behavior and selection");
                var amount=Walk(content).OfType<TextBox>().Last();amount.Text="4321";
                amount.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(amount),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();
                Check(prices[ids[1]].Amount==4321&&hall.Selected.Count==0,"Enter still assigns price after button removal");
                Corrections025UiChecks.Capture(main,"044-pricing-normal");
            });
            Hosted(main,new RecipientPackagesWindow(store,"Recipient044"),content=>{
                Check(Walk(content).OfType<Button>().Where(button=>button.Tag is long).Select(button=>(long)button.Tag).SequenceEqual([mixed.Id]),"mixed-date package remains active");
                Click(Walk(content).OfType<Button>().Single(button=>Equals(button.Content,"Архив")));
                var saved=Walk(content).OfType<Button>().Single(button=>button.Tag is long);
                Check(Equals(saved.Tag,old.Id),"all-past package is accessible under Archive");
                Exception? nestedFailure=null;
                main.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    var child=Application.Current.Windows.OfType<RecipientPackageWindow>().SingleOrDefault();
                    try{Check(child is {IsInline:true}&&ReferenceEquals(child.Owner,main),"archived package opens inside main window with a shown owner");}
                    catch(Exception ex){nestedFailure=ex;}
                    finally{child?.Close();}
                }));
                Click(saved);if(nestedFailure is not null)throw nestedFailure;
            });
            Check(store.GetRecipientPackageDocument(old.Id,"invoice").SourceDocument!.SequenceEqual(originalBytes),"opening archived document preserves saved bytes");
            var separate=new RecipientPackagesWindow(store,"Recipient044"){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
            separate.Show();Pump();
            try
            {
                Exception? failure=null;
                main.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    var child=Application.Current.Windows.OfType<RecipientPackageWindow>().SingleOrDefault();
                    try{Check(child is {IsVisible:true,IsInline:false}&&ReferenceEquals(child.Owner,separate),"saved package also opens with a visible separate-window owner");}
                    catch(Exception ex){failure=ex;}
                    finally{child?.Close();}
                }));
                Click(Walk(separate).OfType<Button>().Single(button=>Equals(button.Tag,mixed.Id)));if(failure is not null)throw failure;
            }
            finally{separate.Close();}

            store.SaveOrganizationDetails("Other044",false,new(Name:"Other044"));
            var recipients=new RecipientsWindow(store);
            Hosted(main,recipients,content=>{
                var organizations=Walk(content).OfType<ListBox>().Single();organizations.SelectedItem=organizations.Items.Cast<RecipientDirectoryInfo>().Single(item=>item.Name=="Recipient044");Pump();
                var history=Walk(content).OfType<TreeView>().Single();
                Check(Walk(history).OfType<TreeViewItem>().Where(i=>i.Tag is RecipientIssueInfo).All(i=>((RecipientIssueInfo)i.Tag).Issue.EventId==future),"active history excludes past showing");
                Walk(history).OfType<CheckBox>().Single().IsChecked=true;
                Click(Walk(content).OfType<Button>().Single(button=>Equals(button.Content,"Архив")));
                var issue=Walk(history).OfType<TreeViewItem>().Single(i=>i.Tag is RecipientIssueInfo);
                Check(((RecipientIssueInfo)issue.Tag).Issue.EventId==past,"archived individual invoice remains listed");
                Walk(history).OfType<CheckBox>().Single().IsChecked=true;
                Click(Walk(content).OfType<Button>().Single(button=>Equals(button.Content,"Активные")));
                Check(Walk(history).OfType<CheckBox>().Single().IsChecked==true,"active issue selection survives an archive round trip");
                Exception? mergeFailure=null;
                main.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    var child=Application.Current.Windows.OfType<RecipientPackageWindow>().SingleOrDefault();
                    try
                    {
                        Check(child is not null,"mixed archive selection opens a package");
                        var combined=store.RecipientPackage(PrivateValue<long>(child!,"packageId"));
                        Check(combined.Issues.Select(i=>i.EventId).ToHashSet().SetEquals([past,future]),"UI creates one package from active and archived selections");
                        Check(!store.DocumentArchive().Contains(combined),"new mixed package remains active");
                    }
                    catch(Exception ex){mergeFailure=ex;}
                    finally{child?.Close();}
                }));
                Click(Walk(content).OfType<Button>().Single(button=>Equals(button.Content,"Объединить выдачи")));if(mergeFailure is not null)throw mergeFailure;
                organizations.SelectedItem=organizations.Items.Cast<RecipientDirectoryInfo>().Single(item=>item.Name=="Other044");Pump();
                Check(PrivateValue<HashSet<(long EventId,long IssueId)>>(recipients,"packageSelection").Count==0,"changing organization clears package membership");
                organizations.SelectedItem=organizations.Items.Cast<RecipientDirectoryInfo>().Single(item=>item.Name=="Recipient044");Pump();
                Click(Walk(content).OfType<Button>().Single(button=>Equals(button.Content,"Архив")));

                Corrections025UiChecks.Capture(main,"044-recipient-archive");
            });
        }
        finally{main.Close();}
    }
}
