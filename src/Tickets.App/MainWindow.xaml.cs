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
    private EventInfo? current;
    private bool ready;
    private readonly Dictionary<long,long> issuePrices=[];

    public MainWindow(Store store)
    {
        this.store=store;
        InitializeComponent();ReadWindowSettings();
        RecipientBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler(RecipientChanged));
        var appVersion=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
        Title="Билеты · "+appVersion;AppBrand.Text="БИЛЕТЫ · "+appVersion;
        var workArea=SystemParameters.WorkArea;
        Width=Math.Min(1420,workArea.Width);
        Height=Math.Min(880,workArea.Height);
        MinWidth=Math.Min(800,workArea.Width);
        MinHeight=Math.Min(500,workArea.Height);
        StateChanged+=(_,_)=>MaximizeButton.Content=WindowState==WindowState.Maximized?"В окно":"Развернуть";
        Hall.SelectionChanged+=UpdateSelection;
        Loaded+=(_,_)=>Run(()=> {
            events=store.Events(); ready=true; ApplyFilter();RefreshRecipients();
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
        var filtered=events.Where(x=>Normalize(x.Title+" "+x.Venue+" "+x.Date).Contains(q)).ToList();
        EventsList.Items.Clear();var now=DateTime.Now;
        TreeViewItem Group(PerformanceShows group,bool past)
        {
            var title=new TextBlock{Text=group.Title,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,MaxWidth=205};
            var root=new TreeViewItem{Header=title,Foreground=past?Brushes.Gray:Brushes.Black,Margin=new Thickness(0,5,0,5)};
            foreach(var show in group.Shows)
            {
                var label=new TextBlock{Text=show.Date+"\n"+show.Venue,TextWrapping=TextWrapping.Wrap,MaxWidth=180,Margin=new Thickness(0,6,0,6)};
                root.Items.Add(new TreeViewItem{Header=label,Tag=show,Foreground=past?Brushes.Gray:Brushes.SteelBlue});
            }
            return root;
        }
        foreach(var group in EventSchedule.Groups(filtered,false,now))EventsList.Items.Add(Group(group,false));
        var past=EventSchedule.Groups(filtered,true,now);if(past.Count>0)
        {
            var old=new TreeViewItem{Header=$"Прошедшие ({past.Sum(g=>g.Shows.Count)})",Foreground=Brushes.Gray,FontWeight=FontWeights.SemiBold};
            foreach(var group in past)old.Items.Add(Group(group,true));EventsList.Items.Add(old);
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
    private void MinimizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void MaximizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void UpdateClick(object sender,RoutedEventArgs e)=>Run(()=>new UpdateWindow(store).ShowDialog());
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void PerformancesClick(object sender,RoutedEventArgs e)=>Run(()=>new PerformanceWindow(store).ShowDialog());
    private void NewHallClick(object sender,RoutedEventArgs e)=>Run(()=>new HallEditorWindow(store).ShowDialog());
    private void PricesClick(object sender,RoutedEventArgs e)=>Run(()=>new PriceEditorWindow(store).ShowDialog());
    private void NewEventClick(object sender,RoutedEventArgs e)=>Run(()=>{
        var dialog=new EventEditorWindow(store);
        if(dialog.ShowDialog()==true){events=store.Events();SearchBox.Clear();ApplyFilter();SelectEvent(dialog.CreatedId);}
    });
    private void HallsClick(object sender,RoutedEventArgs e)=>Run(()=>{
        var window=new EditorWindow(store,"Залы",750,600);
        var panel=new DockPanel{Margin=new Thickness(20)};window.Content=panel;
        var list=new ListBox{ItemsSource=store.Venues(),DisplayMemberPath="Name"};
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(buttons,Dock.Top);panel.Children.Add(buttons);
        var add=new Button{Content="Создать зал"};add.Click+=(_,_)=>Run(()=>{new HallEditorWindow(store).ShowDialog();list.ItemsSource=store.Venues();});buttons.Children.Add(add);
        var edit=new Button{Content="Открыть / изменить копию"};edit.Click+=(_,_)=>Run(()=>{if(list.SelectedItem is VenueInfo v){new HallEditorWindow(store,v).ShowDialog();list.ItemsSource=store.Venues();}});buttons.Children.Add(edit);
        panel.Children.Add(list);window.ShowDialog();
    });
    private void LoadEvent(bool fit)
    {
        if(current is null) return;
        var seats=store.Seats(current.Id);
        EventTitle.Text=current.Title;
        EventSubtitle.Text=$"{current.Date} · {current.Venue}\nВсего мест: {seats.Count} · Свободно с ценой: {seats.Count(s=>s.Available)} · Выдано: {seats.Count(s=>s.TicketId.HasValue)}";
        issuePrices.Clear();PriceBox.Clear();
        Hall.Load(seats,store.Sectors(current.VenueId),fit);
        HistoryGrid.ItemsSource=store.Issues(current.Id);RefreshRecipients();
        LegendPanel.Children.Clear();
        foreach(var price in seats.Where(s=>s.Amount.HasValue).GroupBy(s=>s.Amount).OrderBy(s=>s.Key))
            AddLegend(price.Key+" ₽",HallView.SeatBrush(price.First().Color));
        AddLegend("Выдано",Brushes.SlateGray); AddLegend("Нет цены",Brushes.Gainsboro);
        UpdateNumber();
    }
    private void AddLegend(string text,Brush brush)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,12,4)};
        panel.Children.Add(new Border {Background=brush,Width=12,Height=12,CornerRadius=new CornerRadius(2),Margin=new Thickness(0,0,5,0)});
        panel.Children.Add(new TextBlock {Text=text,FontSize=11,VerticalAlignment=VerticalAlignment.Center});
        LegendPanel.Children.Add(panel);
    }
    private void UpdateSelection()
    {
        if(!ready) return;
        var selected=Hall.SelectedSeats;
        foreach(var id in issuePrices.Keys.Where(id=>!Hall.Selected.Contains(id)).ToArray())issuePrices.Remove(id);
        SelectedList.ItemsSource=selected.Select(s=>new{ s.Label,Status=s.Status+" · "+InvoiceDocuments.Money(issuePrices.TryGetValue(s.Id,out var price)?price:s.Amount??0)}).ToList();
        var free=selected.Count(s=>s.Available); var issued=selected.Count(s=>s.TicketId.HasValue);
        SelectedSummary.Text=selected.Count==0?"Ничего не выбрано":$"Мест: {selected.Count} · Сумма: {selected.Sum(s=>(decimal)(issuePrices.TryGetValue(s.Id,out var price)?price:s.Amount??0)):N0} ₽";
        IssueButton.IsEnabled=current is not null && selected.Count>0 && free==selected.Count &&
            !string.IsNullOrWhiteSpace(RecipientBox.Text) && !string.IsNullOrWhiteSpace(SeriesBox.Text) &&
            long.TryParse(NumberBox.Text,out var n) && n>0;
        IssuePrintButton.IsEnabled=IssueButton.IsEnabled;ApplyPriceButton.IsEnabled=selected.Count>0&&free==selected.Count;
        ReturnButton.IsEnabled=selected.Count>0 && issued==selected.Count;
        SelectionHint.Text=selected.Count==0?"Клик добавляет место, повторный клик снимает его. Протяните левой кнопкой, чтобы добавить несколько мест.":
            free==selected.Count?"Все выбранные места доступны для выдачи.":
            issued==selected.Count?"Выбраны выданные билеты. Их можно вернуть.":
            "Для выдачи выберите только свободные места с ценой. Для возврата — только выданные.";
    }
    private void InputChanged(object sender,TextChangedEventArgs e) { if(ready) UpdateSelection(); }
    private void SeriesChanged(object sender,TextChangedEventArgs e) { if(ready) Run(UpdateNumber); }
    private void UpdateNumber()
    {
        if(!ready) return;
        NumberBox.Text=store.NextNumber(SeriesBox.Text).ToString(CultureInfo.InvariantCulture); UpdateSelection();
    }
    private void ApplyPriceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(!long.TryParse(PriceBox.Text,out var price)||price<0)throw new TicketException("Введите целую неотрицательную цену.");
        var selected=Hall.SelectedSeats;if(selected.Count==0||selected.Any(s=>!s.Available))throw new TicketException("Выберите свободные места с ценой.");
        foreach(var seat in selected)issuePrices[seat.Id]=price;UpdateSelection();
    });
    private void ResetPriceClick(object sender,RoutedEventArgs e){foreach(var id in Hall.Selected)issuePrices.Remove(id);UpdateSelection();}
    private void IssueClick(object sender,RoutedEventArgs e)=>Run(()=>IssueSelected(false));
    private void IssuePrintClick(object sender,RoutedEventArgs e)=>Run(()=>IssueSelected(true));
    private void IssueSelected(bool print)
    {
        if(current is null)return;
        var eventId=current.Id;var selected=Hall.SelectedSeats;
        if(!long.TryParse(NumberBox.Text,out var first))throw new TicketException("Введите целый положительный номер.");
        var issueId=store.Issue(eventId,selected.Select(s=>s.Id).ToArray(),RecipientBox.Text,SeriesBox.Text,first,issuePrices);
        LoadEvent(false);StatusText.Text=$"Выдано билетов: {selected.Count}. Накладная № {issueId}.";
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
        store.Return(ids); LoadEvent(false); StatusText.Text=$"Возвращено билетов: {ids.Length}. Записи сохранены в журнале.";
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
