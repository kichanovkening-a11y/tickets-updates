using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
        Loaded+=(_,_)=>NotifyShown();Closed+=(_,_)=>HasClosedForHost=true;
    }
    internal bool HasClosedForHost{get;private set;}
    internal bool IsInline{get;set;}
    internal bool? InlineResult{get;private set;}
    private Action? shown;
    private bool wasShown;
    internal Window DialogOwnerForHost=>DialogOwner;
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
        var box=new ComboBox{ItemsSource=items,DisplayMemberPath=display,Padding=new Thickness(5,3,5,3),MinHeight=32};
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
        var delete=Button(panel,"Удалить спектакль",()=>Try(()=>{
            if(list.SelectedItem is not PerformanceInfo performance)throw new TicketException("Выберите спектакль.");
            if(MessageBox.Show(DialogOwner,$"Удалить спектакль «{performance.Title}» из справочника?\n\nСпектакль и его показы переместятся в раздел «Удалённые из справочников». Выданные билеты, документы и возвраты останутся доступны.","Удалить спектакль",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            store.DeletePerformance(performance.Id);
            list.ItemsSource=store.Performances();
            if(list.Items.Count>0)list.SelectedIndex=0;
        }));
        delete.IsEnabled=false;list.SelectionChanged+=(_,_)=>delete.IsEnabled=list.SelectedItem is PerformanceInfo;
    }
}

internal sealed class PriceEditorWindow : EditorWindow
{
    private readonly HallView preview=new();
    private List<SeatInfo> seats=[];
    private readonly Dictionary<long,PriceValue> prices=[];
    private sealed record PriceState(Dictionary<long,PriceValue> Prices,long[] Selected,string Name);
    private sealed record PlanChoice(PlanInfo Plan,string Name);
    public long CreatedId{get;private set;}
    public PriceEditorWindow(Store store,long? venueId=null):base(store,"Расценовки")
    {
        var grid=new Grid{Margin=new Thickness(16)}; grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(290)});grid.ColumnDefinitions.Add(new ColumnDefinition());Content=grid;
        var form=new StackPanel{Margin=new Thickness(0,0,15,0)}; grid.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var venue=Combo(form,"Зал",store.Venues(),"Name");
        if(venueId.HasValue) venue.SelectedItem=venue.Items.Cast<VenueInfo>().FirstOrDefault(v=>v.Id==venueId.Value);
        var plans=Combo(form,"Существующая расценовка",Array.Empty<PlanInfo>(),"Name");
        var allVenues=new CheckBox{Content="Расценовки всех площадок",Margin=new Thickness(0,10,0,0)};form.Children.Add(allVenues);
        var oldEvent=Combo(form,"Или цены существующего показа",Array.Empty<EventInfo>(),"Caption");
        var name=Field(form,"Название новой расценовки");
        var history=new Stack<PriceState>();
        Action refreshCategory=()=>{};
        Button? undo=null;
        void Remember(){history.Push(new(new Dictionary<long,PriceValue>(prices),preview.Selected.ToArray(),name.Text));if(undo is not null)undo.IsEnabled=true;}
        void Undo()
        {
            if(!history.TryPop(out var state))return;
            prices.Clear();foreach(var pair in state.Prices)prices[pair.Key]=pair.Value;
            name.Text=state.Name;Refresh();preview.SetSelection(state.Selected);refreshCategory();if(undo is not null)undo.IsEnabled=history.Count>0;
        }
        var copied=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
        Button(form,"Загрузить выбранную расценовку",()=>Try(()=>{
            if(plans.SelectedItem is not PlanChoice choice) throw new TicketException("Выберите расценовку.");
            var p=choice.Plan;
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            var values=store.CopyPlanPrices(p.Id,v.Id);
            if(values.Count==0)throw new TicketException("В выбранной расценовке нет цен для совпадающих мест этого зала.");
            Remember();prices.Clear();foreach(var item in values)prices[item.SeatId]=item;name.Text=p.Name+" (копия)";
            copied.Text=$"Загружено цен: {values.Count} из {seats.Count} мест. Сохраните под новым названием.";Refresh();refreshCategory();
        }));
        var deletePlan=Button(form,"Удалить расценовку",()=>Try(()=>{
            if(plans.SelectedItem is not PlanChoice choice)throw new TicketException("Выберите расценовку.");
            if(MessageBox.Show(DialogOwner,$"Удалить расценовку «{choice.Name}» из справочника?\n\nОна исчезнет из списка для новых показов. Существующие показы, цены билетов и история сохранятся.","Удалить расценовку",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            store.DeletePlan(choice.Plan.Id);LoadPlans();
            copied.Text="Расценовка удалена из справочника. Текущий набор цен можно сохранить как новую расценовку.";
        }));
        deletePlan.IsEnabled=false;plans.SelectionChanged+=(_,_)=>deletePlan.IsEnabled=plans.SelectedItem is PlanChoice;
        Button(form,"Взять цены из показа",()=>Try(()=>{
            if(oldEvent.SelectedItem is not EventInfo e)throw new TicketException("Выберите показ.");
            Remember();prices.Clear();foreach(var s in store.Seats(e.Id).Where(s=>s.Amount.HasValue))prices[s.Id]=new(s.Id,s.Amount!.Value,s.Color);
            Refresh();refreshCategory();
        }));
        form.Children.Add(copied);
        var amount=Field(form,"Цена, ₽","1000");
        var color=Field(form,"Цвет категории (#RRGGBB)",PricePalette.Colors[0]);
        var colorPreview=new System.Windows.Controls.Border{Width=200,Height=20,Margin=new Thickness(0,5,0,0),HorizontalAlignment=HorizontalAlignment.Left,Background=HallView.SeatBrush(color.Text)};form.Children.Add(colorPreview);
        color.TextChanged+=(_,_)=>{if(System.Text.RegularExpressions.Regex.IsMatch(color.Text,"^#[0-9a-fA-F]{6}$"))colorPreview.Background=HallView.SeatBrush(color.Text);};
        var palette=Combo(form,"Готовый цвет",PricePalette.Colors.Concat(store.PriceColors().Select(p=>p.Color)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),"");
        palette.ItemTemplate=ColorChoices.Template();
        var rememberedColors=store.PriceColors().ToDictionary(p=>p.Amount,p=>p.Color);
        var choosingColor=false;
        void SelectCategoryColor()
        {
            if(!long.TryParse(amount.Text,out var value)||value<0)return;
            var categories=new Dictionary<long,string>(rememberedColors);
            foreach(var group in prices.Values.GroupBy(p=>p.Amount))categories[group.Key]=group.First().Color;
            var existing=categories.GetValueOrDefault(value);
            var used=categories.Where(p=>p.Key!=value).Select(p=>p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var choices=PricePalette.Colors.Concat(existing is null?Array.Empty<string>():new[]{existing})
                .Distinct(StringComparer.OrdinalIgnoreCase).Where(c=>!used.Contains(c)).ToList();
            var unavailable=used.Concat(choices).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while(choices.Count<8){var next=PricePalette.NextAvailable(unavailable);choices.Add(next);unavailable.Add(next);}
            choosingColor=true;
            try
            {
                palette.ItemsSource=choices;
                var chosen=existing??choices.FirstOrDefault();
                if(chosen is not null){color.Text=chosen;palette.SelectedItem=choices.FirstOrDefault(c=>c.Equals(chosen,StringComparison.OrdinalIgnoreCase));}
                else palette.SelectedIndex=-1;
            }
            finally{choosingColor=false;}
        }
        palette.SelectionChanged+=(_,_)=>{if(!choosingColor&&palette.SelectedItem is string chosen)color.Text=chosen;};
        refreshCategory=SelectCategoryColor;
        amount.TextChanged+=(_,_)=>SelectCategoryColor();SelectCategoryColor();
        var selected=new TextBlock{Margin=new Thickness(0,10,0,0)}; form.Children.Add(selected);
        preview.SelectionChanged+=()=>selected.Text=$"Выбрано мест: {preview.Selected.Count}";
        void Assign(bool all)=>Try(()=>{
            if(!long.TryParse(amount.Text,out var value)||value<0||!System.Text.RegularExpressions.Regex.IsMatch(color.Text,"^#[0-9a-fA-F]{6}$"))throw new TicketException("Введите неотрицательную цену и цвет вида #6EA8E0.");
            var ids=all?seats.Select(s=>s.Id).ToArray():preview.Selected.ToArray(); if(ids.Length==0)throw new TicketException("Выберите места на схеме.");
            Remember();foreach(var id in prices.Where(p=>p.Value.Amount==value).Select(p=>p.Key).ToArray())prices[id]=new(id,value,color.Text);foreach(var id in ids)prices[id]=new(id,value,color.Text);Refresh();
        });
        Button(form,"Назначить выбранным местам",()=>Assign(false)); Button(form,"Назначить всему залу",()=>Assign(true));
        Button(form,"Убрать цену у выбранных",()=>{if(preview.Selected.Any(prices.ContainsKey)){Remember();foreach(var id in preview.Selected)prices.Remove(id);Refresh();}});
        undo=Button(form,"Шаг назад",Undo);undo.IsEnabled=false;
        grid.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBox){Undo();e.Handled=true;}};
        Button(form,"Сохранить новую расценовку",()=>Try(()=>{
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            CreatedId=store.SavePlan(v.Id,name.Text,prices.Values.ToArray());Finish(true);
        }));
        form.Children.Add(new TextBlock{Text="Цены сохраняются отдельным вариантом. Уже созданные показы и выданные билеты не изменяются.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)});
        var area=new DockPanel();Grid.SetColumn(area,1);grid.Children.Add(area);var toolbar=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(toolbar,Dock.Top);area.Children.Add(toolbar);
        Button(toolbar,"Весь зал",()=>preview.Fit());Button(toolbar,"Снять выделение",()=>preview.Clear());area.Children.Add(preview);
        void LoadPlans()
        {
            if(venue.SelectedItem is not VenueInfo v)return;
            var selectedId=(plans.SelectedItem as PlanChoice)?.Plan.Id;
            var venues=store.Venues().ToDictionary(item=>item.Id,item=>item.Name);
            var items=allVenues.IsChecked==true?store.AllPlans():store.Plans(v.Id);
            plans.ItemsSource=items.Select(p=>new PlanChoice(p,allVenues.IsChecked==true?$"{p.Name} · {venues.GetValueOrDefault(p.VenueId,"Зал")}":p.Name)).ToList();
            plans.SelectedItem=plans.Items.Cast<PlanChoice>().FirstOrDefault(p=>p.Plan.Id==selectedId)??plans.Items.Cast<PlanChoice>().FirstOrDefault();
        }
        allVenues.Checked+=(_,_)=>Try(LoadPlans);allVenues.Unchecked+=(_,_)=>Try(LoadPlans);
        void LoadVenue()
        {
            if(venue.SelectedItem is not VenueInfo v)return;
            seats=store.VenueSeats(v.Id);prices.Clear();history.Clear();if(undo is not null)undo.IsEnabled=false;copied.Text="";LoadPlans();oldEvent.ItemsSource=store.Events().Where(e=>e.VenueId==v.Id).ToList();
            if(oldEvent.Items.Count>0)oldEvent.SelectedIndex=0;Refresh(true);
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
        FontSize=16.9;FontWeight=FontWeights.Bold;
        var form=new StackPanel{Margin=new Thickness(25)};Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var performances=store.Performances();
        var performance=Combo(form,"Спектакль — введите начало значимого слова",performances,"Title");
        performance.FontWeight=FontWeights.SemiBold;performance.IsEditable=true;performance.IsTextSearchEnabled=false;performance.StaysOpenOnEdit=true;
        var filtering=false;
        performance.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((_,e)=>{
            if(filtering||e.OriginalSource is not TextBox edit||!edit.IsKeyboardFocusWithin)return;
            var query=edit.Text;var caret=edit.CaretIndex;
            // A committed dropdown choice raises TextChanged too; keep its exact identity.
            if(performance.SelectedItem is PerformanceInfo chosen&&chosen.Title==query)return;
            filtering=true;
            try
            {
                var selectedId=(performance.SelectedItem as PerformanceInfo)?.Id;
                var matches=performances.Where(p=>SearchText.Matches(p.Title,query)).ToList();
                performance.ItemsSource=matches;
                performance.SelectedItem=matches.FirstOrDefault(p=>p.Id==selectedId&&p.Title==query);
                performance.Text=query;edit.Text=query;edit.CaretIndex=Math.Min(caret,query.Length);
                performance.IsDropDownOpen=true;
            }
            finally{filtering=false;}
        }));
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
            if(performance.SelectedItem is not PerformanceInfo p||performance.Text!=p.Title||venue.SelectedItem is not VenueInfo v||plan.SelectedItem is not PlanInfo r||date.SelectedDate is null)
                throw new TicketException("Выберите спектакль, зал, расценовку и дату. Если расценовок нет, сначала создайте её.");
            var t=CatalogText.Time(hours.Text,minutes.Text,seconds.Text);
            CreatedId=store.CreateEvent(p.Id,v.Id,r.Id,date.SelectedDate.Value.Date+t);Finish(true);
        }));
    }
}
