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
        InitializeComponent();ReadWindowSettings();
        PriceColorBox.ItemTemplate=ColorChoices.Template();
        RecipientBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler(RecipientChanged));
        var appVersion=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
        Title="Билеты · "+appVersion;AppBrand.Text="БИЛЕТЫ · "+appVersion;
        var workArea=SystemParameters.WorkArea;
        Width=Math.Min(1420,workArea.Width);
        Height=Math.Min(880,workArea.Height);
        MinWidth=Math.Min(800,workArea.Width);
        MinHeight=Math.Min(500,workArea.Height);
        Hall.SelectionChanged+=UpdateSelection;
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
            foreach(var show in group.Shows)
            {
                var label=new TextBlock{Text=show.Date+"\n"+show.Venue,TextWrapping=TextWrapping.Wrap,MaxWidth=180,Margin=new Thickness(0,6,0,6)};
                var item=new TreeViewItem{Header=label,Tag=show};
                var normal=past?Brushes.Gray:Brushes.SteelBlue;label.Foreground=normal;
                item.Selected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,item)){label.Background=Brushes.DarkSlateBlue;label.Foreground=Brushes.White;}};
                item.Unselected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,item)){label.Background=Brushes.Transparent;label.Foreground=normal;}};
                root.Items.Add(item);
            }
            return root;
        }
        foreach(var group in EventSchedule.Groups(filtered,false,now))EventsList.Items.Add(Group(group,false));
        var past=EventSchedule.Groups(filtered,true,now);if(past.Count>0)
        {
            var old=new TreeViewItem{Header=$"Прошедшие ({past.Sum(g=>g.Shows.Count)})",Foreground=Brushes.Gray,FontWeight=FontWeights.Bold};
            foreach(var group in past)old.Items.Add(Group(group,true));EventsList.Items.Add(old);
        }
        var deleted=archivedEvents.Where(x=>SearchText.Matches(x.Title+" "+x.Venue+" "+x.Date,q)).ToList();
        if(deleted.Count>0)
        {
            var archive=new TreeViewItem{Header=$"Удалённые из справочников ({deleted.Count})",Foreground=Brushes.Gray};
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
        var edit=new Button{Content="Открыть / изменить копию"};edit.Click+=(_,_)=>Run(()=>{if(list.SelectedItem is VenueInfo v){new HallEditorWindow(store,v).ShowDialog();list.ItemsSource=store.Venues();}});buttons.Children.Add(edit);
        var delete=new MenuItem{Header="Удалить"};delete.Click+=(_,_)=>Run(()=>{
            if(list.SelectedItem is not VenueInfo selected)throw new TicketException("Выберите площадку.");
            if(MessageBox.Show(window.DialogOwnerForHost,$"Удалить «{selected.Name}» из справочника? Выдачи и документы сохранятся в истории.","Удаление площадки",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            store.DeleteVenue(selected.Id);list.ItemsSource=store.Venues();
        });
        list.ContextMenu=new ContextMenu();list.ContextMenu.Items.Add(delete);
        list.PreviewMouseRightButtonDown+=(_,e)=>{if(e.OriginalSource is DependencyObject source&&ItemsControl.ContainerFromElement(list,source) is ListBoxItem item)item.IsSelected=true;else e.Handled=true;};
        list.ContextMenuOpening+=(_,e)=>{if(list.SelectedItem is not VenueInfo)e.Handled=true;};
        panel.Children.Add(list);window.ShowDialog();RefreshCatalogNavigation();
    });
    private void LoadEvent(bool fit)
    {
        if(current is null) return;
        currentArchived=store.IsEventArchived(current.Id);
        var seats=store.Seats(current.Id);
        EventTitle.Text=current.Title;
        EventSubtitle.Text=$"{current.Date} · {current.Venue}\nВсего мест: {seats.Count} · Свободно с ценой: {seats.Count(s=>s.Available)} · Выдано: {seats.Count(s=>s.TicketId.HasValue)}";
        PriceBox.Clear();
        if(fit){recipientUpdating=true;try{RecipientBox.SelectedItem=null;RecipientBox.Text="";}finally{recipientUpdating=false;}}
        Hall.Load(seats,store.Sectors(current.VenueId),fit);
        HistoryGrid.ItemsSource=store.Issues(current.Id);RefreshRecipients();
        LegendPanel.Children.Clear();
        foreach(var price in seats.Where(s=>s.Amount.HasValue&&!s.IsBlocked).GroupBy(s=>s.Amount).OrderBy(s=>s.Key))
            AddLegend($"{price.Key:N0} ₽ × {price.Count()} = {price.Sum(s=>(decimal)s.Amount!.Value):N0} ₽",HallView.SeatBrush(price.First().Color));
        HallTotals.Text=$"Всего мест: {seats.Count} · В продаже с ценой: {seats.Count(s=>s.Amount.HasValue&&!s.IsBlocked)} · Общая сумма: {seats.Where(s=>!s.IsBlocked).Sum(s=>(decimal)(s.Amount??0)):N0} ₽";
        AddLegend("Бронь / накладная",Brushes.Black);AddLegend("Распечатаны",Brushes.Red);AddLegend("Снято с продажи",Brushes.DarkOrange); AddLegend("Нет цены",Brushes.Gainsboro);
        UpdateNumber();
    }
    private void AddLegend(string text,Brush brush)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,12,4)};
        panel.Children.Add(new Border {Background=brush,Width=12,Height=12,CornerRadius=new CornerRadius(2),Margin=new Thickness(0,0,5,0)});
        panel.Children.Add(new TextBlock {Text=text,FontSize=14.3,VerticalAlignment=VerticalAlignment.Center});
        LegendPanel.Children.Add(panel);
    }
    private void UpdateSelection()
    {
        if(!ready) return;
        var selected=Hall.SelectedSeats;
        SelectedList.ItemsSource=selected.Select(s=>new{ s.Label,Status=s.Status+" · "+InvoiceDocuments.Money(s.Amount??0),AlertBrush=s.TicketId.HasValue||s.IsBlocked?Brushes.Red:Brushes.Black}).ToList();
        IssuedRecipients.Text=string.Join("\n",selected.Where(s=>s.TicketId.HasValue).GroupBy(s=>s.Recipient).Select(g=>$"Выдано: {g.Key} — мест: {g.Count()}"));
        var archived=currentArchived;
        var free=selected.Count(s=>s.Available); var issued=selected.Count(s=>s.TicketId.HasValue);
        SelectedSummary.Text=selected.Count==0?"Ничего не выбрано":$"Мест: {selected.Count}\nСумма: {selected.Sum(s=>(decimal)(s.Amount??0)):N0} ₽";
        IssueButton.IsEnabled=!archived && current is not null && selected.Count>0 && free==selected.Count &&
            !string.IsNullOrWhiteSpace(RecipientBox.Text) && !string.IsNullOrWhiteSpace(SeriesBox.Text) &&
            long.TryParse(NumberBox.Text,out var n) && n>0;
        ReserveButton.IsEnabled=IssueButton.IsEnabled;
        var reservedInvoice=selected.Count>0&&selected.All(s=>s.IsReserved)&&selected.Select(s=>s.IssueId).Distinct().Count()==1;
        if(reservedInvoice)IssueButton.IsEnabled=!archived;
        IssueButton.Content=reservedInvoice?"Оформить накладную":"Выдать накладную";
        ExtendInvoiceButton.IsEnabled=!archived&&selected.Count>0&&free==selected.Count;
        PrintTicketsButton.IsEnabled=(selected.Count>0&&issued==selected.Count)||(!archived&&selected.Count>0&&selected.All(s=>s.Available||s.TicketId.HasValue)&&free>0&&!string.IsNullOrWhiteSpace(RecipientBox.Text)&&!string.IsNullOrWhiteSpace(SeriesBox.Text)&&long.TryParse(NumberBox.Text,out var printNumber)&&printNumber>0);
        PrintTicketsButton.Content=selected.Count>0&&selected.All(s=>s.IsPrinted)?"Повторная печать":"Печать билетов";
        var allBlocked=selected.Count>0&&selected.All(x=>x.IsBlocked);
        SaleStateButton.Content=allBlocked?"Вернуть в продажу":"Удалить из продажи";
        SaleStateButton.IsEnabled=!archived&&selected.Count>0&&issued==0&&(allBlocked||selected.All(x=>!x.IsBlocked));
        ApplyPriceButton.IsEnabled=!archived&&selected.Count>0&&selected.All(s=>!s.TicketId.HasValue);
        UndoPriceButton.IsEnabled=!archived&&priceUndo.TryPeek(out var change)&&current?.Id==change.EventId;
        ReturnButton.IsEnabled=selected.Count>0 && issued==selected.Count;
        SelectionHint.Text=archived?"Показ удалён из рабочих списков. Доступны просмотр накладных и возврат мест на схеме.":selected.Count==0?"Клик добавляет место, повторный клик снимает его. Рамка добавляет места; начните на выбранном месте, чтобы снять группу.":
            free==selected.Count?(string.IsNullOrWhiteSpace(RecipientBox.Text)?"Выберите или создайте получателя для выдачи и печати.":"Все выбранные места доступны для выдачи и печати."):
            issued==selected.Count?"Печать сохранит привязку к исходной накладной. Серии и номера останутся прежними.":
            selected.Any(x=>x.IsBlocked)?"Есть снятые с продажи места. Для выдачи или печати верните их в продажу.":
            "Можно печатать свободные и ранее выданные билеты вместе. Ранее выданные останутся в своих накладных.";
    }
    private void InputChanged(object sender,TextChangedEventArgs e) { if(ready) UpdateSelection(); }
    private void SeriesChanged(object sender,TextChangedEventArgs e) { if(ready) Run(UpdateNumber); }
    private void UpdateNumber()
    {
        if(!ready) return;
        NumberBox.Text=store.NextNumber(SeriesBox.Text).ToString(CultureInfo.InvariantCulture); UpdateSelection();
    }
    private void TogglePriceClick(object sender,RoutedEventArgs e){PriceEditorPanel.Visibility=PriceEditorPanel.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;if(PriceEditorPanel.Visibility==Visibility.Visible)PriceBox.Focus();}
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
        var action=blocked?"Удалить из продажи":"Вернуть в продажу";
        if(MessageBox.Show(this,$"{action} выбранные места ({selected.Count})?",action,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        store.SetSeatsBlocked(current.Id,selected.Select(x=>x.Id).ToArray(),blocked);LoadEvent(false);
        StatusText.Text=blocked?$"Снято с продажи: {selected.Count} мест.":$"Возвращено в продажу: {selected.Count} мест.";
    });
    private void PrintTicketsClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;var selected=Hall.SelectedSeats;if(selected.Count==0)throw new TicketException("Выберите места для печати.");
        var ids=selected.Select(x=>x.Id).ToArray();
        if(selected.Any(x=>x.IsPrinted)&&MessageBox.Show(this,$"В выборе есть уже распечатанные билеты. Распечатать выбранные {ids.Length} билетов ещё раз? Номера и накладные сохранятся.","Повторная печать",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        TicketPrintWindow window;
        if(selected.All(x=>x.TicketId.HasValue))
        {
            window=new TicketPrintWindow(store,current.Id,ids);
        }
        else
        {
            if(!long.TryParse(NumberBox.Text,out var first))throw new TicketException("Укажите первый номер билета.");
            PersistSelectedSender();window=new TicketPrintWindow(store,current.Id,ids,RecipientBox.Text,SeriesBox.Text,first);
        }
        window.ShowDialog();LoadEvent(false);
        if(window.IssueId>0){try{store.SaveAllDocuments(current.Id,window.IssueId);}catch(Exception ex){StatusText.Text="Выдача сохранена. Не удалось обновить документы: "+ex.Message;}}
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
        if(selected.Count>0&&selected.All(s=>s.IsReserved)&&selected.Select(s=>s.IssueId).Distinct().Count()==1)
        {var reservedId=selected[0].IssueId!.Value;store.ConfirmReservation(eventId,reservedId);LoadEvent(false);Hall.Clear();OpenInvoice(reservedId);return;}
        PersistSelectedSender();
        if(!long.TryParse(NumberBox.Text,out var first))throw new TicketException("Введите целый положительный номер.");
        var issueId=store.Issue(eventId,selected.Select(s=>s.Id).ToArray(),RecipientBox.Text,SeriesBox.Text,first);
        LoadEvent(false);Hall.Clear();StatusText.Text=$"Выдано билетов: {selected.Count}. Накладная № {issueId}.";
        OpenInvoice(issueId,print);
    }
    private void ReturnClick(object sender,RoutedEventArgs e)=>Run(()=>ReturnTickets(
        Hall.SelectedSeats.Where(s=>s.TicketId.HasValue).Select(s=>s.TicketId!.Value).ToArray()));
    private void OpenInvoiceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(HistoryGrid.SelectedItem is not IssueInfo issue)throw new TicketException("Выберите выдачу.");OpenInvoice(issue.Id);
    });
    private void HistoryRowClick(object sender,System.Windows.Input.MouseButtonEventArgs e)
    {
        var source=e.OriginalSource as DependencyObject;
        var row=source is null?null:ItemsControl.ContainerFromElement(HistoryGrid,source) as DataGridRow;
        if(row?.Item is IssueInfo issue){e.Handled=true;Run(()=>OpenInvoice(issue.Id));}
    }
    private void OpenInvoice(long issueId,bool print=false)
    {
        if(current is null)return;
        new InvoiceWindow(store,current.Id,issueId,print){Owner=this}.ShowDialog();LoadEvent(false);
    }
    private void ReturnTickets(long[] ids)
    {
        if(ids.Length==0) throw new TicketException("Выберите билеты для возврата.");
        if(MessageBox.Show(this,$"Вернуть билеты: {ids.Length}? Места снова станут свободными.","Возврат",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
        var eventId=current!.Id;var issueIds=store.History(eventId).Where(t=>ids.Contains(t.Id)).Select(t=>t.IssueId).Distinct().ToArray();
        store.Return(ids); LoadEvent(false); StatusText.Text=$"Возвращено билетов: {ids.Length}. Записи сохранены в журнале.";
        foreach(var issue in issueIds)try{store.SaveAllDocuments(eventId,issue);}catch(Exception ex){StatusText.Text+=" Не удалось обновить Word: "+ex.Message;}
    }
    private void BackupClick(object sender,RoutedEventArgs e)=>Run(()=> {
        var dialog=new SaveFileDialog {Filter="База билетов (*.db)|*.db",FileName=$"tickets-{DateTime.Now:yyyy-MM-dd-HHmm}.db"};
        if(dialog.ShowDialog(this)==true) { store.Backup(dialog.FileName); StatusText.Text="Резервная копия сохранена: "+dialog.FileName; }
    });
    private void FitClick(object sender,RoutedEventArgs e)=>Hall.Fit();
    private void ZoomInClick(object sender,RoutedEventArgs e)=>Hall.Zoom(1.2);
    private void ZoomOutClick(object sender,RoutedEventArgs e)=>Hall.Zoom(1/1.2);
    private void ClearClick(object sender,RoutedEventArgs e)=>Hall.Clear();
}
