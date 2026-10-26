using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow : Window
{
    private readonly Store store;
    private List<EventInfo> events=[];
    private List<EventInfo> archivedEvents=[];
    private EventInfo? current;
    private bool ready;
    private bool currentArchived;
    private readonly Stack<PriceChange> priceUndo=[];

    public MainWindow(Store store)
    {
        this.store=store;
        InitializeComponent();ReadWindowSettings();InitializeJournalLayout();PriceSummaryLayout.Install(LegendPanel);InitializeTicketland028();
        SeriesBox.Text=store.LastIssueSeries;
        Workspace.SizeChanged+=(_,_)=>UpdateCompactLayout();
        UpperActions.SizeChanged+=(_,_)=>UpdateCompactLayout();
        SquareShowActions.RequestBringIntoView+=(_,e)=>{
            if(e.OriginalSource is FrameworkElement target){
                var right=UpperActions.ActualWidth-156+target.TranslatePoint(new Point(target.ActualWidth,0),SquareShowActions).X;
                OrdinaryActionsScroll.ScrollToHorizontalOffset(Math.Max(0,right-OrdinaryActionsScroll.ViewportWidth));e.Handled=true;
            }
        };
        PriceColorBox.ItemTemplate=ColorChoices.Template();
        RecipientTypography.Apply(RecipientBox);
        RecipientBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler(RecipientChanged));
        var appVersion=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
        Title="Билеты · "+appVersion;AppBrand.Text="БИЛЕТЫ · "+appVersion;
        var workArea=SystemParameters.WorkArea;
        Width=Math.Min(1420,workArea.Width);
        Height=Math.Min(880,workArea.Height);
        MinWidth=Math.Min(800,workArea.Width);
        MinHeight=Math.Min(500,workArea.Height);
        Hall.AllowOccupiedSelection=true;
        Hall.SelectionChanged+=UpdateSelection;
        Hall.EmptySpaceClicked+=(_,_)=>SetPriceFocus(null);
        Hall.SeatContextRequested+=OpenSeatContext;
        Hall.SeatSelectionRejected+=seat=>StatusText.Text=$"{seat.Label}: {seat.Status}. В смешанном выборе оставлены свободные места. Повторная печать — из исходной накладной.";
        Loaded+=(_,_)=>Run(()=> {
            ReadEventCatalog(); ready=true; ApplyFilter();RefreshRecipients();RefreshSenders();
            HomeSummary.Text=$"В базе мероприятий: {events.Count}. Старые выдачи не переносились.";
            ShowHome();
            UpdateNumber();
        });
    }
    private void Run(Action action)
    {
        try { action(); }
        catch(TicketException ex) { MessageBox.Show(this,ex.Message,"Билеты",MessageBoxButton.OK,MessageBoxImage.Information); }
        catch(Exception ex) { MessageBox.Show(this,"Не удалось выполнить действие.\n\n"+ex.Message,"Билеты",MessageBoxButton.OK,MessageBoxImage.Error); }
    }
    private static string Normalize(string s)=>s.ToUpperInvariant().Replace('Ё','Е');
    private void SearchChanged(object sender,TextChangedEventArgs e) { if(ready) ApplyFilter(); }
    private void ApplyFilter()
    {
        var id=current?.Id; var q=Normalize(SearchBox.Text.Trim());
        var filtered=events.Where(x=>SearchText.Matches(x.Title+" "+x.Venue+" "+x.Date,q)).ToList();
        EventsList.Items.Clear();var now=DateTime.Now;
        TreeViewItem Group(PerformanceShows group,bool past)
        {
            var title=new TextBlock{Text=group.Title,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,FontSize=13,MaxWidth=205};
            var root=new TreeViewItem{Header=title,Foreground=past?Brushes.Gray:Brushes.Black,Margin=new Thickness(0,5,0,5)};
            root.Selected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,root)){title.Background=Brushes.SteelBlue;title.Foreground=Brushes.White;}};
            root.Unselected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,root)){title.Background=Brushes.Transparent;title.ClearValue(TextBlock.ForegroundProperty);}};
            foreach(var show in group.Shows)
            {
                var label=new TextBlock{TextWrapping=TextWrapping.Wrap,MaxWidth=180,Margin=new Thickness(0,6,0,6)};
                label.Inlines.Add(new System.Windows.Documents.Run(show.Date){FontSize=19});label.Inlines.Add(new System.Windows.Documents.Run("\n"+show.Venue));
                var item=new TreeViewItem{Header=label,Tag=show};
                item.ContextMenu=CleanupMenu023(new[]{show.Id},"Удалить показ окончательно");
                var normal=past?Brushes.Gray:Brushes.SteelBlue;label.Foreground=normal;
                item.Selected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,item)){label.Background=Brushes.DarkSlateBlue;label.Foreground=Brushes.White;}};
                item.Unselected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,item)){label.Background=Brushes.Transparent;label.Foreground=normal;}};
                root.Items.Add(item);
            }
            root.PreviewMouseLeftButtonDown+=(_,e)=>{
                if(e.OriginalSource is not DependencyObject source||NearestTreeItem(source) is not TreeViewItem clicked||!ReferenceEquals(clicked,root))return;
                // Leave the built-in expander arrow to TreeView; only a header click toggles the group.
                for(DependencyObject? node=source;node is not null&&!ReferenceEquals(node,root);node=node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D?System.Windows.Media.VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
                    if(node is System.Windows.Controls.Primitives.ToggleButton)return;
                var expand=!root.IsExpanded;root.IsSelected=true;root.IsExpanded=expand;e.Handled=true;
            };
            return root;
        }
        foreach(var group in EventSchedule.Groups(filtered,false,now))EventsList.Items.Add(Group(group,false));
        var past=EventSchedule.Groups(filtered,true,now);if(past.Count>0)
        {
            var old=new TreeViewItem{Header=$"Прошедшие ({past.Sum(g=>g.Shows.Count)})",Foreground=Brushes.Gray,FontWeight=FontWeights.Bold};
            old.ContextMenu=CleanupMenu023(past.SelectMany(g=>g.Shows).Select(e=>e.Id).ToArray(),"Удалить прошедшие окончательно");
            foreach(var group in past)old.Items.Add(Group(group,true));EventsList.Items.Add(old);
        }
        var deleted=archivedEvents.Where(x=>SearchText.Matches(x.Title+" "+x.Venue+" "+x.Date,q)).ToList();
        if(deleted.Count>0)
        {
            var archive=new TreeViewItem{Header=$"Удалённые из справочников ({deleted.Count})",Foreground=Brushes.Gray};
            archive.ContextMenu=CleanupMenu023(deleted.Select(e=>e.Id).ToArray(),"Очистить удалённые окончательно");
            foreach(var group in deleted.GroupBy(x=>CatalogText.Key(x.Title)).OrderBy(g=>g.First().Title))archive.Items.Add(Group(new PerformanceShows(group.First().Title,group.OrderBy(x=>x.SortDate).ToList()),true));
            EventsList.Items.Add(archive);
        }
        if(id.HasValue)SelectEvent(id.Value);
        StatusText.Text=$"Мероприятий: {filtered.Count} · База: {store.Path}";
    }
    private void EventChanged(object sender,RoutedPropertyChangedEventArgs<object> e)
    {
        if(!ready) return;
        if(EventsList.SelectedItem is TreeViewItem group&&group.Tag is not EventInfo){group.IsExpanded=true;return;}
        current=(EventsList.SelectedItem as TreeViewItem)?.Tag as EventInfo;
        if(current is null)
        {
            EventTitle.Text="Выберите мероприятие"; EventSubtitle.Text="";
            Hall.Load([],[],true); HistoryGrid.ItemsSource=null; LegendPanel.Children.Clear();
            ShowHome(); return;
        }
        HomePanel.Visibility=Visibility.Collapsed; Workspace.Visibility=Visibility.Visible;
        Run(()=>LoadEvent(true));
    }
    private void ShowHome()
    {
        HomePanel.Visibility=Visibility.Visible; Workspace.Visibility=Visibility.Collapsed;
    }
    private void HomeClick(object sender,RoutedEventArgs e) { current=null;foreach(var row in EventRows())row.IsSelected=false;ShowHome(); }
    private void FindEventClick(object sender,RoutedEventArgs e) { SearchBox.Focus(); SearchBox.SelectAll(); }
    private void UpdateClick(object sender,RoutedEventArgs e)=>Run(()=>new UpdateWindow(store).ShowDialog());
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void PerformancesClick(object sender,RoutedEventArgs e)=>Run(()=>{var catalog=new PerformanceWindow(store);catalog.ShowDialog();RefreshCatalogNavigation();if(catalog.SelectedEventId!=0){SearchBox.Clear();ApplyFilter();SelectEvent(catalog.SelectedEventId);}});
    private void NewHallClick(object sender,RoutedEventArgs e)=>Run(()=>new HallEditorWindow(store).ShowDialog());
    private void PricesClick(object sender,RoutedEventArgs e)=>Run(()=>new PricingDirectoryWindow(store).ShowDialog());
    private void NewEventClick(object sender,RoutedEventArgs e)=>Run(()=>{
        var dialog=new EventEditorWindow(store);
        if(dialog.ShowDialog()==true){ReadEventCatalog();SearchBox.Clear();ApplyFilter();SelectEvent(dialog.CreatedId);}
    });
    private void HallsClick(object sender,RoutedEventArgs e)=>Run(()=>{
        var window=new EditorWindow(store,"Залы",750,600);
        var panel=new DockPanel{Margin=new Thickness(20)};window.Content=panel;
        var list=new ListBox{ItemsSource=store.Venues(),DisplayMemberPath="Name"};
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(buttons,Dock.Top);panel.Children.Add(buttons);
        var add=new Button{Content="Создать зал"};add.Click+=(_,_)=>Run(()=>{new HallEditorWindow(store).ShowDialog();list.ItemsSource=store.Venues();});buttons.Children.Add(add);
        list.PreviewMouseLeftButtonUp+=(_,e)=>{
            if(e.OriginalSource is not DependencyObject source||ItemsControl.ContainerFromElement(list,source) is not ListBoxItem {Content:VenueInfo venue})return;
            e.Handled=true;Run(()=>{new HallEditorWindow(store,venue,true).ShowDialog();list.ItemsSource=store.Venues();});
        };
        var edit=new MenuItem{Header="Редактировать зал"};edit.Click+=(_,_)=>Run(()=>{
            if(list.SelectedItem is not VenueInfo selected)throw new TicketException("Выберите площадку.");
            new HallEditorWindow(store,selected,true).ShowDialog();list.ItemsSource=store.Venues();
        });
        var delete=new MenuItem{Header="Удалить"};delete.Click+=(_,_)=>Run(()=>{
            if(list.SelectedItem is not VenueInfo selected)throw new TicketException("Выберите площадку.");
            if(MessageBox.Show(window.DialogOwnerForHost,$"Удалить «{selected.Name}» из справочника? Выдачи и документы сохранятся в истории.","Удаление площадки",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            store.DeleteVenue(selected.Id);list.ItemsSource=store.Venues();
        });
        list.ContextMenu=new ContextMenu();list.ContextMenu.Items.Add(edit);list.ContextMenu.Items.Add(delete);
        list.PreviewMouseRightButtonDown+=(_,e)=>{if(e.OriginalSource is DependencyObject source&&ItemsControl.ContainerFromElement(list,source) is ListBoxItem item)item.IsSelected=true;else e.Handled=true;};
        list.ContextMenuOpening+=(_,e)=>{if(list.SelectedItem is not VenueInfo)e.Handled=true;};
        panel.Children.Add(list);window.ShowDialog();RefreshCatalogNavigation();
    });
    private void LoadEvent(bool fit)
    {
        if(current is null) return;
        currentArchived=store.IsEventArchived(current.Id);
        var seats=store.Seats(current.Id);
        EventTitle.Text=current.Title;EventDate.Text=current.Date;
        EventVenue.Text=current.Venue;
        EventSubtitle.Text=$"Всего мест: {seats.Count} · Свободно с ценой: {seats.Count(s=>s.Available)} · Бронь: {seats.Count(s=>s.IsReserved)} · Без печати: {seats.Count(s=>s.TicketId.HasValue&&!s.IsReserved&&!s.IsPrinted)} · Напечатано: {seats.Count(s=>s.IsPrinted)}";
        EventTitle.ToolTip=EventSubtitle.Text;UpdateCompactLayout();
        PriceBox.Clear();
        if(fit){Hall.ClearFocus030();ClearIssuanceRecipient();}
        Hall.Load(seats,store.Sectors(current.VenueId),fit);
        LoadJournal();RefreshRecipients();RefreshSenders();
        RenderHallLegend(seats);
        RefreshHallTotals030(seats);
        if(store.TicketlandLastSync028.HasValue)UpdateSelectedTicketlandStatus034();
        UpdateNumber();
    }
    private void UpdateSelection()
    {
        if(!ready) return;
        var selected=Hall.SelectedSeats;
        SelectedList.ItemsSource=selected.Select(s=>new{ s.Label,Status=s.Status+" · "+InvoiceDocuments.Money(s.Amount??0),AlertBrush=s.TicketId.HasValue||s.IsBlocked||s.IsTicketlandSold?Brushes.Red:Brushes.Black}).ToList();
        SelectedPriceSummary.ItemsSource=selected.GroupBy(s=>s.Amount).OrderByDescending(g=>g.Key).Select(g=>$"{(g.Key.HasValue?InvoiceDocuments.Money(g.Key.Value):"Без цены")} × {g.Count()} = {InvoiceDocuments.Money(g.Sum(s=>s.Amount??0))}").Append($"Всего: {selected.Count} · {InvoiceDocuments.Money(selected.Sum(s=>s.Amount??0))}").ToList();
        IssuedRecipients.Text=string.Join("\n",selected.Where(s=>s.TicketId.HasValue).GroupBy(s=>s.Recipient).Select(g=>$"Получатель: {g.Key} — мест: {g.Count()}"));
        IssuedRecipients.Visibility=string.IsNullOrEmpty(IssuedRecipients.Text)?Visibility.Collapsed:Visibility.Visible;
        var archived=currentArchived;
        var free=selected.Count(s=>s.Available); var issued=selected.Count(s=>s.TicketId.HasValue);
        SelectedSummary.Text=selected.Count==0?"Ничего не выбрано":$"Мест: {selected.Count} · Сумма: {selected.Sum(s=>(decimal)(s.Amount??0)):N0} ₽";
        IssueButton.IsEnabled=!archived && current is not null && selected.Count>0 && free==selected.Count &&
            !string.IsNullOrWhiteSpace(RecipientName) && !string.IsNullOrWhiteSpace(SeriesBox.Text) &&
            long.TryParse(NumberBox.Text,out var n) && n>0;
        ReserveButton.IsEnabled=IssueButton.IsEnabled;
        IssueButton.Content="Выдать накладную";
        ExtendInvoiceButton.IsEnabled=!archived&&selected.Count>0&&free==selected.Count;
        PrintTicketsButton.IsEnabled=IssueButton.IsEnabled&&selected.All(SeatSelection.CanSelect);
        PrintTicketsButton.Content="Печать билетов";
        var allBlocked=selected.Count>0&&selected.All(x=>x.IsBlocked);
        SaleStateButton.Content=allBlocked?"Разблокировать":"Заблокировать";
        SaleStateButton.ToolTip=null;
        SaleStateButton.IsEnabled=!archived&&selected.Count>0&&issued==0&&selected.All(s=>!s.IsTicketlandSold)&&(allBlocked||selected.All(x=>!x.IsBlocked));
        ApplyPriceButton.IsEnabled=!archived&&selected.Count>0&&selected.All(s=>!s.TicketId.HasValue&&!s.IsTicketlandSold);
        UndoPriceButton.IsEnabled=!archived&&priceUndo.TryPeek(out var change)&&current?.Id==change.EventId;
        ReturnButton.IsEnabled=selected.Count>0 && issued==selected.Count;
        SelectionHint.Text=archived?"Показ удалён из рабочих списков. Накладные доступны в журнале.":selected.Count==0?"Клик и рамка выбирают места. Для занятых мест доступны исходная накладная и возврат.":
            free==selected.Count?(string.IsNullOrWhiteSpace(RecipientName)?"Выберите или создайте получателя для каждой новой выдачи и печати.":"Все выбранные места доступны для выдачи и печати."):
            "Назначьте цену выбранным местам для выдачи и печати.";
    }
    private void InputChanged(object sender,TextChangedEventArgs e) { if(ready) UpdateSelection(); }
    private void SeriesChanged(object sender,TextChangedEventArgs e) { if(ready) Run(()=>{if(!string.IsNullOrWhiteSpace(SeriesBox.Text))store.RememberIssueSeries(SeriesBox.Text);UpdateNumber();}); }
    private void UpdateNumber()
    {
        if(!ready) return;
        NumberBox.Text=store.NextNumber(SeriesBox.Text).ToString(CultureInfo.InvariantCulture); UpdateSelection();
    }
    private void TogglePriceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null||currentArchived)return;
        var selected=Hall.SelectedSeats;if(selected.Count==0)throw new TicketException("Выберите места на схеме.");
        if(selected.Any(s=>s.TicketId.HasValue))throw new TicketException("Для изменения цены выберите свободные места.");
        var initial=selected.First().Amount??0;
        var editor=new SeatPriceWindow(store,initial,selected.Count);
        if(editor.ShowDialog()!=true)return;
        PriceBox.Text=editor.Amount.ToString(CultureInfo.InvariantCulture);PriceColorBox.SelectedItem=editor.Color;
        ApplyPriceClick(this,new RoutedEventArgs());
    });
    private void PriceAmountChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready)return;
        if(!long.TryParse(PriceBox.Text,out var amount)||amount<0){PriceColorBox.ItemsSource=null;return;}
        var colors=store.PriceColors();var mapped=colors.FirstOrDefault(p=>p.Amount==amount)?.Color;
        var used=colors.Where(p=>p.Amount!=amount).Select(p=>p.Color).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices=PricePalette.Colors.Where(c=>!used.Contains(c)).ToList();if(mapped is not null&&!choices.Contains(mapped))choices.Insert(0,mapped);
        var excluded=used.Concat(choices).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while(choices.Count<8){var next=PricePalette.NextAvailable(excluded);choices.Add(next);excluded.Add(next);}
        PriceColorBox.ItemsSource=choices;PriceColorBox.SelectedItem=mapped??choices[0];PriceColorBox.IsEnabled=mapped is null;
    }
    private void ApplyPriceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;
        if(!long.TryParse(PriceBox.Text,out var price)||price<0)throw new TicketException("Введите целую неотрицательную цену.");
        var ids=Hall.SelectedSeats.Select(s=>s.Id).ToArray();
        var change=store.SetEventPrices(current.Id,ids,price,PriceColorBox.SelectedItem as string);priceUndo.Push(change);
        LoadEvent(false);Hall.Clear();PriceEditorPanel.Visibility=Visibility.Collapsed;
        StatusText.Text=$"Цена {InvoiceDocuments.Money(price)} сохранена для {ids.Length} мест. Выделение снято.";
    });
    private void UndoPriceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(!priceUndo.TryPeek(out var change)||current?.Id!=change.EventId)throw new TicketException("В этом показе нет изменений цены для отмены.");
        store.UndoPriceChange(change);priceUndo.Pop();LoadEvent(false);StatusText.Text="Последнее изменение цены отменено.";
    });
    private void RecipientsClick(object sender,RoutedEventArgs e)=>Run(()=>{new RecipientsWindow(store).ShowDialog();
        if(!store.Recipients().Contains(RecipientBox.Text)){RecipientBox.SelectedItem=null;RecipientBox.Text="";}RefreshRecipients();
    });
    private void NewRecipientClick(object sender,RoutedEventArgs e)=>Run(()=>{
        var editor=new RecipientEditorWindow(store,RecipientBox.Text);
        if(editor.ShowDialog()==true){RefreshRecipients();RecipientBox.SelectedItem=null;RecipientBox.Text=editor.CreatedName;UpdateSelection();}
    });
    private void SaleStateClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;var selected=Hall.SelectedSeats;if(selected.Count==0)return;
        var blocked=!selected.All(x=>x.IsBlocked);
        var action=blocked?"Заблокировать":"Разблокировать";
        if(MessageBox.Show(this,$"{action} выбранные места ({selected.Count})?",action,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        if(!blocked&&selected.Any(s=>!s.Amount.HasValue))
        {
            var price=new SeatPriceWindow(store,0,selected.Count);if(price.ShowDialog()!=true)return;
            priceUndo.Push(store.SetEventPrices(current.Id,selected.Select(s=>s.Id).ToArray(),price.Amount,price.Color));
        }
        store.SetSeatsBlocked(current.Id,selected.Select(x=>x.Id).ToArray(),blocked);LoadEvent(false);Hall.Clear();
        StatusText.Text=blocked?$"Заблокировано: {selected.Count} мест.":$"Разблокировано: {selected.Count} мест.";
    });
    private void PrintTicketsClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;var selected=Hall.SelectedSeats;if(selected.Count==0)throw new TicketException("Выберите места для печати.");
        var ids=selected.Select(x=>x.Id).ToArray();
        if(selected.Any(x=>!x.Available))throw new TicketException("Для печати выберите только свободные места с ценой. Ранее выданные билеты печатайте из исходной накладной.");
        if(string.IsNullOrWhiteSpace(RecipientName))throw new TicketException("Выберите получателя для новой выдачи.");
        if(!long.TryParse(NumberBox.Text,out var first))throw new TicketException("Укажите первый номер билета.");
        PersistSelectedSender();var window=new TicketPrintWindow(store,current.Id,ids,RecipientName,SeriesBox.Text,first);
        window.ShowDialog();
        if(window.Printed||window.IssueId>0){LoadEvent(false);if(!window.Printed)Hall.SetSelection(ids);}
        if(window.IssueId>0){ClearIssuanceRecipient();try{store.SaveAllDocuments(current.Id,window.IssueId);}catch(Exception ex){StatusText.Text="Выдача сохранена. Не удалось обновить документы: "+ex.Message;}}
    });
    private void ReadEventCatalog()
    {
        events=store.Events();var active=events.Select(e=>e.Id).ToHashSet();archivedEvents=store.Events(true).Where(e=>!active.Contains(e.Id)).ToList();
    }
    private void RefreshCatalogNavigation()
    {
        ReadEventCatalog();ApplyFilter();
        if(current is not null&&!events.Concat(archivedEvents).Any(e=>e.Id==current.Id)){current=null;Hall.Load([],[],true);HistoryGrid.ItemsSource=null;ShowHome();}
    }
    private void IssueClick(object sender,RoutedEventArgs e)=>Run(()=>IssueSelected(false));
    private void IssueSelected(bool print)
    {
        if(current is null)return;
        var eventId=current.Id;var selected=Hall.SelectedSeats;
        if(selected.Count==0||selected.Any(s=>!s.Available))throw new TicketException("Выберите только свободные места с ценой. Занятые места доступны в исходной накладной.");
        if(string.IsNullOrWhiteSpace(RecipientName))throw new TicketException("Выберите получателя для новой выдачи.");
        PersistSelectedSender();
        if(!long.TryParse(NumberBox.Text,out var first))throw new TicketException("Введите целый положительный номер.");
        var issueId=store.Issue(eventId,selected.Select(s=>s.Id).ToArray(),RecipientName,SeriesBox.Text,first);
        LoadEvent(false);Hall.Clear();ClearIssuanceRecipient();StatusText.Text=$"Выдано билетов: {selected.Count}. Накладная № {issueId}.";
        SaveIssuedDocuments(eventId,issueId);OpenInvoice(issueId,print);
    }
    private void ReturnClick(object sender,RoutedEventArgs e)=>Run(()=>ReturnTickets(
        Hall.SelectedSeats.Where(s=>s.TicketId.HasValue).Select(s=>s.TicketId!.Value).ToArray()));
    private void HistoryRowClick(object sender,System.Windows.Input.MouseButtonEventArgs e)
    {
        var source=e.OriginalSource as DependencyObject;
        if(ReturnedTicketDetails027.IsToggleSource(source))return;
        if(ReturnedTicketDetails027.IsEmptySpaceSource(HistoryGrid,source)){CollapseReturnedDetails028();return;}
        for(var node=source;node is not null&&node!=HistoryGrid;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
            if(node is CheckBox)return;
        var row=source is null?null:ItemsControl.ContainerFromElement(HistoryGrid,source) as DataGridRow;
        if(row?.Item is JournalIssueRow selected){e.Handled=true;Run(()=>OpenInvoice(selected.Id));}
    }
    private void OpenInvoice(long issueId,bool print=false)
    {
        if(current is null)return;
        issueId=store.ResolveInvoiceId(issueId);
        var selectedIds=Hall.Selected.ToArray();new InvoiceWindow(store,current.Id,issueId,print){Owner=this}.ShowDialog();LoadEvent(false);Hall.SetSelection(selectedIds);
    }
    private void ReturnTickets(long[] ids)
    {
        if(ids.Length==0) throw new TicketException("Выберите билеты для возврата.");
        if(MessageBox.Show(this,$"Вернуть билеты: {ids.Length}? Места снова станут свободными.","Возврат",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
        var eventId=current!.Id;var issueIds=store.History(eventId).Where(t=>ids.Contains(t.Id)).SelectMany(t=>new[]{t.IssueId,store.ResolveInvoiceId(t.IssueId)}).Distinct().ToArray();
        store.Return(ids); LoadEvent(false); StatusText.Text=$"Возвращено билетов: {ids.Length}. Записи сохранены в журнале.";
        foreach(var issue in issueIds)try{store.SaveAllDocuments(eventId,issue);}catch(Exception ex){StatusText.Text+=" Не удалось обновить Word: "+ex.Message;}
    }
    internal void BackupFromSettings()=>BackupClick(this,new RoutedEventArgs());
    private void BackupClick(object sender,RoutedEventArgs e)=>Run(()=> {
        var dialog=new SaveFileDialog {Filter="База билетов (*.db)|*.db",FileName=$"tickets-{DateTime.Now:yyyy-MM-dd-HHmm}.db"};
        if(dialog.ShowDialog(this)==true) { store.Backup(dialog.FileName); StatusText.Text="Резервная копия сохранена: "+dialog.FileName; }
    });
    private void FitClick(object sender,RoutedEventArgs e)=>Hall.Fit();
    private void ZoomInClick(object sender,RoutedEventArgs e)=>Hall.Zoom(1.2);
    private void ZoomOutClick(object sender,RoutedEventArgs e)=>Hall.Zoom(1/1.2);
    private void ClearClick(object sender,RoutedEventArgs e)=>Hall.Clear();
}
