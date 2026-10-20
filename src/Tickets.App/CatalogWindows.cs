using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tickets.Core;

namespace Tickets.App;

internal class EditorWindow : Window
{
    protected readonly Store Store;
    internal EditorWindow(Store store,string title,double width=1100,double height=760)
    {
        Store=store; Title=title; Owner=Application.Current.MainWindow; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Width=Math.Min(width,SystemParameters.WorkArea.Width); Height=Math.Min(height,SystemParameters.WorkArea.Height);
        MinWidth=Math.Min(650,Width); MinHeight=Math.Min(450,Height);
        Loaded+=(_,_)=>NotifyShown();
    }
    internal bool IsInline{get;set;}
    internal bool? InlineResult{get;private set;}
    private Action? shown;
    private bool wasShown;
    protected Window DialogOwner=>IsInline?Application.Current.MainWindow:this;
    protected void OnShown(Action action)=>shown+=action;
    internal void NotifyShown(){if(wasShown)return;wasShown=true;shown?.Invoke();}
    public new bool? ShowDialog()=>Application.Current.MainWindow is MainWindow main?main.PresentEditor(this):base.ShowDialog();
    internal bool? ShowSeparateDialog()=>base.ShowDialog();
    protected void Finish(bool result){if(IsInline){InlineResult=result;Close();}else DialogResult=result;}
    protected void Try(Action action)
    {
        try { action(); }
        catch(Exception ex) { MessageBox.Show(DialogOwner,ex.Message,"Билеты",MessageBoxButton.OK,MessageBoxImage.Information); }
    }
    protected static TextBox Field(Panel panel,string label,string value="")
    {
        panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,10,0,4),TextWrapping=TextWrapping.Wrap});
        var box=new TextBox{Text=value}; panel.Children.Add(box); return box;
    }
    protected static Button Button(Panel panel,string label,Action action)
    {
        var b=new Button{Content=label,Margin=new Thickness(0,10,0,0),HorizontalContentAlignment=HorizontalAlignment.Center};
        b.Click+=(_,_)=>action(); panel.Children.Add(b); return b;
    }
    protected static ComboBox Combo(Panel panel,string label,System.Collections.IEnumerable items,string display)
    {
        panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,10,0,4)});
        var box=new ComboBox{ItemsSource=items,DisplayMemberPath=display,Padding=new Thickness(5),MinHeight=32};
        panel.Children.Add(box); if(box.Items.Count>0) box.SelectedIndex=0; return box;
    }
    protected static int Number(TextBox box,int min,int max)
    {
        if(!int.TryParse(box.Text,out var n)||n<min||n>max) throw new TicketException($"Введите целое число от {min} до {max}."); return n;
    }
}

internal sealed class PerformanceWindow : EditorWindow
{
    public PerformanceWindow(Store store):base(store,"Спектакли",650,600)
    {
        var panel=new StackPanel{Margin=new Thickness(24)}; Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="Спектакли",FontSize=23,FontWeight=FontWeights.SemiBold});
        var name=Field(panel,"Название нового спектакля");
        var list=new ListBox{Height=330,Margin=new Thickness(0,20,0,0),DisplayMemberPath="Title",ItemsSource=store.Performances()};
        Button(panel,"Создать спектакль",()=>Try(()=>{store.SavePerformance(name.Text); list.ItemsSource=store.Performances(); name.Clear();}));
        panel.Children.Add(list);
    }
}

internal sealed class HallEditorWindow : EditorWindow
{
    public long CreatedId{get;private set;}
    private readonly ObservableCollection<DraftSeat> seats=[];
    private readonly HallView preview=new();
    private readonly DataGrid table=new(){IsReadOnly=false,AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Extended};
    private readonly TabControl tabs=new();
    private readonly TextBlock summary=new(){Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap};
    public HallEditorWindow(Store store,VenueInfo? copy=null):base(store,"Создание зала")
    {
        if(copy is not null) foreach(var s in store.HallDraft(copy.Id)) seats.Add(s);
        var grid=new Grid{Margin=new Thickness(16)}; grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)}); grid.ColumnDefinitions.Add(new ColumnDefinition()); Content=grid;
        var form=new StackPanel{Margin=new Thickness(0,0,15,0)};
        grid.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var name=Field(form,"Название зала",copy is null?"":copy.Name+" (копия)");
        var address=Field(form,"Адрес",copy?.Address??"");
        var sector=Field(form,"Сектор","Партер");
        var rows=Field(form,"Количество рядов","10"); var places=Field(form,"Мест в каждом ряду","20");
        var firstRow=Field(form,"Первый номер ряда","1"); var firstSeat=Field(form,"Первый номер места","1");
        var reverse=new CheckBox{Content="Нумерация справа налево",Margin=new Thickness(0,10,0,0)}; form.Children.Add(reverse);
        var aisle=Field(form,"Проход после места по порядку (0 — нет)","0");
        Button(form,"Добавить сектор",()=>Try(()=>{
            var rc=Number(rows,1,200);var sc=Number(places,1,300);var fr=Number(firstRow,0,9999);var fs=Number(firstSeat,0,9999);var gap=Number(aisle,0,sc);
            var title=sector.Text.Trim(); if(title=="") throw new TicketException("Введите название сектора.");
            if(seats.Count+rc*sc>50000) throw new TicketException("Максимум 50 000 мест в одном зале.");
            var y=seats.Count==0?26:seats.Max(s=>s.Y)+80;
            for(var r=0;r<rc;r++) for(var c=0;c<sc;c++) seats.Add(new DraftSeat{
                Sector=title,Row=(fr+r).ToString(),Number=(fs+(reverse.IsChecked==true?sc-1-c:c)).ToString(),
                X=c*20+(gap>0&&c>=gap?36:0),Y=y+r*22});
            Refresh();
        }));
        var dx=Field(form,"Сдвиг выбранных по X","0"); var dy=Field(form,"Сдвиг выбранных по Y","0");
        Button(form,"Сдвинуть выбранные места",()=>Try(()=>{
            if(!double.TryParse(dx.Text,out var x)||!double.TryParse(dy.Text,out var y)||!double.IsFinite(x)||!double.IsFinite(y)) throw new TicketException("Введите числовой сдвиг.");
            var selected=Selection(); if(selected.Count==0) throw new TicketException("Выберите места на схеме или в таблице.");
            foreach(var s in selected){s.X+=x;s.Y+=y;} Refresh();
        }));
        Button(form,"Удалить выбранные места",()=>{foreach(var s in Selection())seats.Remove(s);Refresh();});
        Button(form,"Сохранить как новый зал",()=>Try(()=>{
            if(!CommitEdits())return;
            CreatedId=store.CreateHall(name.Text,address.Text,seats.ToArray()); Finish(true);
        }));
        form.Children.Add(summary);
        form.Children.Add(new TextBlock{Text="Ряды, номера и координаты можно менять в таблице «Места». Существующий зал сохраняется новой копией.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
        var area=new DockPanel(); Grid.SetColumn(area,1); grid.Children.Add(area);
        var toolbar=new StackPanel{Orientation=Orientation.Horizontal}; DockPanel.SetDock(toolbar,Dock.Top);area.Children.Add(toolbar);
        Button(toolbar,"Весь зал",()=>preview.Fit()); Button(toolbar,"Обновить схему",()=>Try(Refresh));
        foreach(var (header,path,width) in new[]{("Сектор","Sector",150),("Ряд","Row",60),("Место","Number",70),("X","X",80),("Y","Y",80)})
            table.Columns.Add(new DataGridTextColumn{Header=header,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=width});
        table.ItemsSource=seats;
        tabs.Items.Add(new TabItem{Header="Схема",Content=preview}); tabs.Items.Add(new TabItem{Header="Места — редактирование",Content=table}); area.Children.Add(tabs);
        OnShown(Refresh);
    }
    private List<DraftSeat> Selection()=>tabs.SelectedIndex==1?table.SelectedItems.Cast<DraftSeat>().ToList():
        preview.Selected.Where(id=>id>0&&id<=seats.Count).Select(id=>seats[(int)id-1]).ToList();
    private void Refresh()
    {
        if(!CommitEdits())return;table.Items.Refresh();
        if(seats.Any(s=>!double.IsFinite(s.X)||!double.IsFinite(s.Y)||string.IsNullOrWhiteSpace(s.Sector)||string.IsNullOrWhiteSpace(s.Row)||string.IsNullOrWhiteSpace(s.Number)))
        {MessageBox.Show(DialogOwner,"Заполните сектор, ряд, место и корректные координаты в таблице.","Зал");return;}
        var view=seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,"#7AA8D9",null,"","",null)).ToList();
        preview.Load(view,seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(x=>x.X),g.Min(x=>x.Y)-26)).ToList(),true);
        summary.Text=$"Мест: {seats.Count} · Секторов: {seats.Select(s=>s.Sector).Distinct().Count()}";
    }
    private bool CommitEdits()
    {
        if(table.CommitEdit(DataGridEditingUnit.Cell,true)&&table.CommitEdit(DataGridEditingUnit.Row,true))return true;
        MessageBox.Show(DialogOwner,"Исправьте ошибку в таблице: координаты X и Y должны быть числами.","Зал");return false;
    }
}

internal sealed class PriceEditorWindow : EditorWindow
{
    private readonly HallView preview=new();
    private List<SeatInfo> seats=[];
    private readonly Dictionary<long,PriceValue> prices=[];
    public long CreatedId{get;private set;}
    public PriceEditorWindow(Store store,long? venueId=null):base(store,"Расценовки")
    {
        var grid=new Grid{Margin=new Thickness(16)}; grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(290)});grid.ColumnDefinitions.Add(new ColumnDefinition());Content=grid;
        var form=new StackPanel{Margin=new Thickness(0,0,15,0)}; grid.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var venue=Combo(form,"Зал",store.Venues(),"Name");
        if(venueId.HasValue) venue.SelectedItem=venue.Items.Cast<VenueInfo>().FirstOrDefault(v=>v.Id==venueId.Value);
        var plans=Combo(form,"Существующая расценовка",Array.Empty<PlanInfo>(),"Name");
        var oldEvent=Combo(form,"Или цены существующего показа",Array.Empty<EventInfo>(),"Caption");
        var name=Field(form,"Название новой расценовки");
        Button(form,"Загрузить выбранную расценовку",()=>Try(()=>{
            if(plans.SelectedItem is not PlanInfo p) throw new TicketException("Выберите расценовку.");
            prices.Clear();foreach(var item in store.PlanPrices(p.Id))prices[item.SeatId]=item;name.Text=p.Name+" (копия)";Refresh();
        }));
        Button(form,"Взять цены из показа",()=>Try(()=>{
            if(oldEvent.SelectedItem is not EventInfo e)throw new TicketException("Выберите показ.");
            prices.Clear();foreach(var s in store.Seats(e.Id).Where(s=>s.Amount.HasValue))prices[s.Id]=new(s.Id,s.Amount!.Value,s.Color);
            Refresh();
        }));
        var amount=Field(form,"Цена, ₽","1000");
        var color=Field(form,"Цвет категории (#RRGGBB)","#6EA8E0");
        var palette=Combo(form,"Готовый цвет",new[]{"#6EA8E0","#80C5A2","#E8B96D","#D990AF","#A59BDC","#E87E70"},"");
        palette.SelectionChanged+=(_,_)=>color.Text=palette.SelectedItem?.ToString()??"#6EA8E0";
        var selected=new TextBlock{Margin=new Thickness(0,10,0,0)}; form.Children.Add(selected);
        preview.SelectionChanged+=()=>selected.Text=$"Выбрано мест: {preview.Selected.Count}";
        void Assign(bool all)=>Try(()=>{
            if(!long.TryParse(amount.Text,out var value)||value<0||!System.Text.RegularExpressions.Regex.IsMatch(color.Text,"^#[0-9a-fA-F]{6}$"))throw new TicketException("Введите неотрицательную цену и цвет вида #6EA8E0.");
            var ids=all?seats.Select(s=>s.Id).ToArray():preview.Selected.ToArray(); if(ids.Length==0)throw new TicketException("Выберите места на схеме.");
            foreach(var id in ids)prices[id]=new(id,value,color.Text);Refresh();
        });
        Button(form,"Назначить выбранным местам",()=>Assign(false)); Button(form,"Назначить всему залу",()=>Assign(true));
        Button(form,"Убрать цену у выбранных",()=>{foreach(var id in preview.Selected)prices.Remove(id);Refresh();});
        Button(form,"Сохранить новую расценовку",()=>Try(()=>{
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            CreatedId=store.SavePlan(v.Id,name.Text,prices.Values.ToArray());Finish(true);
        }));
        form.Children.Add(new TextBlock{Text="Цены сохраняются отдельным вариантом. Уже созданные показы и выданные билеты не изменяются.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)});
        var area=new DockPanel();Grid.SetColumn(area,1);grid.Children.Add(area);var toolbar=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(toolbar,Dock.Top);area.Children.Add(toolbar);
        Button(toolbar,"Весь зал",()=>preview.Fit());Button(toolbar,"Снять выделение",()=>preview.Clear());area.Children.Add(preview);
        void LoadVenue()
        {
            if(venue.SelectedItem is not VenueInfo v)return;
            seats=store.VenueSeats(v.Id);prices.Clear();plans.ItemsSource=store.Plans(v.Id);oldEvent.ItemsSource=store.Events().Where(e=>e.VenueId==v.Id).ToList();
            if(plans.Items.Count>0)plans.SelectedIndex=0;if(oldEvent.Items.Count>0)oldEvent.SelectedIndex=0;Refresh(true);
        }
        venue.SelectionChanged+=(_,_)=>Try(LoadVenue);OnShown(()=>Try(LoadVenue));
    }
    private void Refresh(bool fit=false)
    {
        var ids=preview.Selected.ToArray();
        preview.Load(seats.Select(s=>prices.TryGetValue(s.Id,out var p)?s with{Amount=p.Amount,Color=p.Color}:s).ToList(),
            seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(x=>x.X),g.Min(x=>x.Y)-26)).ToList(),fit);
        preview.SetSelection(ids);
    }
}

internal sealed class EventEditorWindow : EditorWindow
{
    public long CreatedId{get;private set;}
    public EventEditorWindow(Store store):base(store,"Новый показ",650,650)
    {
        FontSize=16.9;FontWeight=FontWeights.SemiBold;
        var form=new StackPanel{Margin=new Thickness(25)};Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var performance=Combo(form,"Спектакль",store.Performances(),"Title");
        var venue=Combo(form,"Площадка / зал",store.Venues(),"Name");
        var plan=Combo(form,"Расценовка",Array.Empty<PlanInfo>(),"Name");
        form.Children.Remove(plan);
        var planRow=new Grid();planRow.ColumnDefinitions.Add(new ColumnDefinition());planRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        plan.Margin=new Thickness(0,0,10,0);plan.VerticalAlignment=VerticalAlignment.Center;planRow.Children.Add(plan);
        var createPlan=new Button{Content="Создать",Margin=new Thickness(0)};Grid.SetColumn(createPlan,1);planRow.Children.Add(createPlan);form.Children.Add(planRow);
        void LoadPlans(){if(venue.SelectedItem is VenueInfo v){plan.ItemsSource=store.Plans(v.Id);if(plan.Items.Count>0)plan.SelectedIndex=0;}}
        createPlan.Click+=(_,_)=>Try(()=>{
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            var editor=new PriceEditorWindow(store,v.Id);
            if(editor.ShowDialog()==true){LoadPlans();plan.SelectedItem=plan.Items.Cast<PlanInfo>().FirstOrDefault(p=>p.Id==editor.CreatedId);}
        });
        venue.SelectionChanged+=(_,_)=>Try(LoadPlans);LoadPlans();
        form.Children.Add(new TextBlock{Text="Дата",Margin=new Thickness(0,12,0,5)});
        var dateRow=new Grid();dateRow.ColumnDefinitions.Add(new ColumnDefinition());dateRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});form.Children.Add(dateRow);
        var date=new DatePicker{SelectedDate=DateTime.Today,CalendarStyle=(Style)Application.Current.FindResource(typeof(Calendar)),FontSize=26,Height=44,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
        date.Loaded+=(_,_)=>{date.ApplyTemplate();if(date.Template.FindName("PART_Button",date) is UIElement button)button.Visibility=Visibility.Collapsed;};dateRow.Children.Add(date);
        var calendarButton=new Button{Content="Календарь",Margin=new Thickness(0)};Grid.SetColumn(calendarButton,1);dateRow.Children.Add(calendarButton);calendarButton.Click+=(_,_)=>date.IsDropDownOpen=!date.IsDropDownOpen;
        form.Children.Add(new TextBlock{Text="Время",Margin=new Thickness(0,12,0,5)});
        var timeRow=new WrapPanel();form.Children.Add(timeRow);
        ComboBox Part(string title,int count,int selected)
        {
            var group=new StackPanel{Margin=new Thickness(0,0,15,0)};timeRow.Children.Add(group);
            var box=Combo(group,title,Enumerable.Range(0,count).Select(n=>n.ToString("00")),"");box.IsEditable=true;box.IsTextSearchEnabled=false;box.Width=100;box.SelectedIndex=selected;return box;
        }
        var hours=Part("Часы",24,19);var minutes=Part("Минуты",60,0);var seconds=Part("Секунды",60,0);
        Button(form,"Создать показ",()=>Try(()=>{
            if(performance.SelectedItem is not PerformanceInfo p||venue.SelectedItem is not VenueInfo v||plan.SelectedItem is not PlanInfo r||date.SelectedDate is null)
                throw new TicketException("Выберите спектакль, зал, расценовку и дату. Если расценовок нет, сначала создайте её.");
            var t=CatalogText.Time(hours.Text,minutes.Text,seconds.Text);
            CreatedId=store.CreateEvent(p.Id,v.Id,r.Id,date.SelectedDate.Value.Date+t);Finish(true);
        }));
    }
}
