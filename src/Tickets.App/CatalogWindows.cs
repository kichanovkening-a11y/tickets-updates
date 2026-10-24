using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal class EditorWindow : Window
{
    protected readonly Store Store;
    internal Store PrintStore=>Store;
    internal EditorWindow(Store store,string title,double width=1100,double height=760)
    {
        Store=store; Title=title; Owner=Application.Current.MainWindow; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Width=Math.Min(width,SystemParameters.WorkArea.Width); Height=Math.Min(height,SystemParameters.WorkArea.Height);
        MinWidth=Math.Min(650,Width); MinHeight=Math.Min(450,Height);
        Loaded+=(_,_)=>NotifyShown();Closed+=(_,_)=>HasClosedForHost=true;
    }
    internal bool HasClosedForHost{get;private set;}
    internal bool IsInline{get;set;}
    internal string? DocumentTab{get;set;}
    internal bool? InlineResult{get;private set;}
    private Action? shown;
    private bool wasShown;
    internal Window DialogOwnerForHost=>DialogOwner;
    protected Window DialogOwner=>IsInline?Application.Current.MainWindow:this;
    protected void OnShown(Action action)=>shown+=action;
    internal void NotifyShown(){if(wasShown)return;wasShown=true;shown?.Invoke();}
    public new bool? ShowDialog()=>Owner is EditorWindow {IsInline:false,IsVisible:true}?base.ShowDialog():Application.Current.MainWindow is MainWindow main?main.PresentEditor(this):base.ShowDialog();
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
        var horizontal=panel is WrapPanel || panel is StackPanel {Orientation:Orientation.Horizontal};
        var b=new Button{Content=label,Margin=horizontal?new Thickness(0,0,6,0):new Thickness(0,10,0,0),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
        if(horizontal){b.Width=double.NaN;b.ContentTemplate=null;b.Padding=new Thickness(10,2,10,2);b.VerticalAlignment=VerticalAlignment.Center;}
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

internal sealed class PriceEditorWindow : EditorWindow
{
    private readonly HallView preview=new(){AllowUnavailableSelection=true};
    private List<SeatInfo> seats=[];
    private readonly Dictionary<long,PriceValue> prices=[];
    private readonly HashSet<long> withdrawn=[];
    private readonly TextBlock hallTotals=new(){TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,4,0,10),FontSize=18,FontWeight=FontWeights.Bold};
    private readonly Grid priceSummary=new(){Margin=new Thickness(0,0,0,6),HorizontalAlignment=HorizontalAlignment.Left};
    private sealed record SourcePlan(long Id,PriceValue[] Prices,long[] Withdrawn);
    private sealed record PriceState(Dictionary<long,PriceValue> Prices,long[] Withdrawn,long[] Selected,string Name,SourcePlan? Source);
    private sealed record PlanChoice(PlanInfo Plan,string Name);
    public long CreatedId{get;private set;}
    internal Action? HeaderSaveAction{get;private set;}
    public PriceEditorWindow(Store store,long? venueId=null):this(store,venueId,null){}
    public PriceEditorWindow(Store store,long? venueId,long? planId):base(store,"Расценовка")
    {
        PriceSummaryLayout.Install(priceSummary);
        var grid=new Grid{Margin=new Thickness(16,5,16,16)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(290)});grid.ColumnDefinitions.Add(new ColumnDefinition());Content=grid;
        var form=new StackPanel{Margin=new Thickness(0,0,15,0)};grid.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var venue=Combo(form,"Зал",store.Venues(),"Name");
        if(venueId.HasValue)venue.SelectedItem=venue.Items.Cast<VenueInfo>().FirstOrDefault(v=>v.Id==venueId.Value);
        var plans=Combo(form,"Существующая расценовка",Array.Empty<PlanInfo>(),"Name");
        var name=Field(form,"Название новой расценовки");
        foreach(var label in form.Children.OfType<TextBlock>())
            if(label.Text is "Зал" or "Существующая расценовка" or "Название новой расценовки")
            {label.FontSize=FontSize+1;label.FontWeight=FontWeights.ExtraBold;}
        SourcePlan? sourcePlan=null;
        var history=new Stack<PriceState>();Button? undo=null;Action refreshCategory=()=>{};Action refreshSelection=()=>{};
        void Remember(){history.Push(new(new(prices),withdrawn.ToArray(),preview.Selected.ToArray(),name.Text,sourcePlan));if(undo is not null)undo.IsEnabled=true;}
        void Undo()
        {
            if(!history.TryPop(out var state))return;
            prices.Clear();foreach(var pair in state.Prices)prices[pair.Key]=pair.Value;withdrawn.Clear();withdrawn.UnionWith(state.Withdrawn);
            name.Text=state.Name;sourcePlan=state.Source;Refresh();preview.SetSelection(state.Selected);refreshCategory();refreshSelection();if(undo is not null)undo.IsEnabled=history.Count>0;
        }
        var copied=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
        void LoadSelectedPlan()
        {
            if(plans.SelectedItem is not PlanChoice choice)throw new TicketException("Выберите расценовку.");
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            var values=store.CopyPlanPrices(choice.Plan.Id,v.Id);var blocks=store.CopyPlanBlockedSeats(choice.Plan.Id,v.Id);
            Remember();prices.Clear();foreach(var item in values)prices[item.SeatId]=item;withdrawn.Clear();withdrawn.UnionWith(blocks);name.Text="";preview.FocusPrice=null;
            sourcePlan=new(choice.Plan.Id,values.ToArray(),blocks.ToArray());
            copied.Text=$"Загружено цен: {values.Count}; заблокировано: {blocks.Count}. Изменённая расценовка сохранится под новым названием.";Refresh();refreshCategory();
        }
        Button(form,"Загрузить выбранную расценовку",()=>Try(LoadSelectedPlan));form.Children.Add(copied);
        var category=new Grid();category.ColumnDefinitions.Add(new ColumnDefinition());category.ColumnDefinitions.Add(new ColumnDefinition());form.Children.Add(category);
        var priceColumn=new StackPanel{Margin=new Thickness(0,0,4,0)};category.Children.Add(priceColumn);
        var colorColumn=new StackPanel{Margin=new Thickness(4,0,0,0)};Grid.SetColumn(colorColumn,1);category.Children.Add(colorColumn);
        var amount=Field(priceColumn,"Цена, ₽","1000");amount.Height=34;
        var palette=Combo(colorColumn,"Готовый цвет",PricePalette.Colors,"");palette.ItemTemplate=ColorChoices.Template();palette.Height=34;palette.MinHeight=34;palette.Padding=new Thickness(8,2,8,2);
        var rememberedColors=store.PriceColors().ToDictionary(p=>p.Amount,p=>p.Color);var choosingColor=false;
        void SelectCategoryColor()
        {
            if(!long.TryParse(amount.Text,out var value)||value<0)return;
            var categories=new Dictionary<long,string>(rememberedColors);foreach(var p in prices.Values)categories[p.Amount]=p.Color;
            var existing=categories.GetValueOrDefault(value);var used=categories.Where(p=>p.Key!=value).Select(p=>p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var choices=PricePalette.Colors.Concat(existing is null?Array.Empty<string>():new[]{existing}).Distinct(StringComparer.OrdinalIgnoreCase).Where(c=>!used.Contains(c)).ToList();
            if(choices.Count==0)choices.Add(PricePalette.NextAvailable(used));choosingColor=true;
            try{palette.ItemsSource=choices;palette.SelectedItem=choices.FirstOrDefault(c=>c.Equals(existing,StringComparison.OrdinalIgnoreCase))??choices[0];}finally{choosingColor=false;}
        }
        palette.SelectionChanged+=(_,_)=>{if(!choosingColor)refreshSelection();};refreshCategory=SelectCategoryColor;
        var selected=new TextBlock{Margin=new Thickness(0,10,0,0),TextWrapping=TextWrapping.Wrap};form.Children.Add(selected);
        void SelectionSummary()
        {
            var ids=preview.Selected;var total=ids.Where(prices.ContainsKey).Sum(id=>(decimal)prices[id].Amount);
            selected.Text=$"Выбрано мест: {ids.Count} · Сумма: {total:N0} ₽";
            var missing=ids.Count(id=>!prices.ContainsKey(id)&&!withdrawn.Contains(id));if(missing>0)selected.Text+=$" · Без цены: {missing}";
        }
        refreshSelection=SelectionSummary;preview.SelectionChanged+=SelectionSummary;
        amount.TextChanged+=(_,_)=>{SelectCategoryColor();SelectionSummary();};SelectCategoryColor();SelectionSummary();
        string? Prompt(string title,string label,string initial,Func<string,string?> validate)
        {
            var dialog=new Window{Title=title,Owner=DialogOwner,Width=360,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            var body=new StackPanel{Margin=new Thickness(16)};dialog.Content=body;
            var input=Field(body,label,initial);var error=new TextBlock{Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};body.Children.Add(error);
            var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};body.Children.Add(buttons);
            var apply=Button(buttons,"Применить",()=>{var message=validate(input.Text);if(message is not null){error.Text=message;return;}dialog.DialogResult=true;});apply.IsDefault=true;apply.Width=140;apply.Padding=new Thickness(4,2,4,2);apply.ContentTemplate=null;
            var cancel=Button(buttons,"Отмена",()=>dialog.DialogResult=false);cancel.IsCancel=true;cancel.Width=140;cancel.Padding=new Thickness(4,2,4,2);cancel.ContentTemplate=null;cancel.Margin=new Thickness(8,0,0,0);
            void Validate(){var message=validate(input.Text);apply.IsEnabled=message is null;error.Text=message??"";}input.TextChanged+=(_,_)=>Validate();Validate();
            dialog.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
            return dialog.ShowDialog()==true?input.Text.Trim():null;
        }
        void ChangeSelectedPrice()=>Try(()=>{
            var ids=preview.Selected.ToArray();if(ids.Length==0)throw new TicketException("Выберите места на схеме.");
            var initial=prices.TryGetValue(ids[0],out var existing)?existing.Amount.ToString():amount.Text;
            var entered=Prompt("Сменить цену","Новая цена, ₽",initial,text=>long.TryParse(text,out var value)&&value>=0?null:"Введите неотрицательную целую цену.");
            if(entered is null)return;
            var value=long.Parse(entered);
            var color=prices.Values.FirstOrDefault(p=>p.Amount==value)?.Color??rememberedColors.GetValueOrDefault(value)
                ??PricePalette.NextAvailable(rememberedColors.Values.Concat(prices.Values.Select(p=>p.Color)));
            Remember();foreach(var id in ids){prices[id]=new(id,value,color);withdrawn.Remove(id);}
            amount.Text=entered;Refresh();preview.Clear();SelectionSummary();
        });
        void Assign(bool all)=>Try(()=>{
            if(!long.TryParse(amount.Text,out var value)||value<0||palette.SelectedItem is not string color)throw new TicketException("Введите неотрицательную цену и выберите готовый цвет.");
            var ids=all?seats.Select(s=>s.Id).ToArray():preview.Selected.ToArray();if(ids.Length==0)throw new TicketException("Выберите места на схеме.");
            Remember();foreach(var id in prices.Where(p=>p.Value.Amount==value).Select(p=>p.Key).ToArray())prices[id]=new(id,value,color);
            foreach(var id in ids){prices[id]=new(id,value,color);withdrawn.Remove(id);}Refresh();preview.Clear();SelectionSummary();
        });
        void Withdraw()=>Try(()=>{
            if(preview.Selected.Count==0)throw new TicketException("Выберите места на схеме.");Remember();
            foreach(var id in preview.Selected){prices.Remove(id);withdrawn.Add(id);}Refresh();preview.Clear();SelectionSummary();
        });
        var assignSelected=Button(form,"Назначить выбранным местам",()=>Assign(false));Button(form,"Назначить всему залу",()=>Assign(true));Button(form,"Заблокировать",Withdraw);
        long? lastContextSeat=null;
        preview.SeatContextRequested+=(id,point)=>{
            if(lastContextSeat==id&&preview.Selected.Contains(id))
            {
                preview.SetSelection(preview.Selected.Where(selectedId=>selectedId!=id).ToArray());lastContextSeat=null;return;
            }
            if(!preview.Selected.Contains(id))preview.SetSelection(new[]{id});
            lastContextSeat=id;
            var menu=new ContextMenu{PlacementTarget=preview,Placement=System.Windows.Controls.Primitives.PlacementMode.RelativePoint,HorizontalOffset=point.X,VerticalOffset=point.Y};
            var change=new MenuItem{Header="Сменить цену"};change.Click+=(_,_)=>ChangeSelectedPrice();menu.Items.Add(change);
            var remove=new MenuItem{Header="Заблокировать"};remove.Click+=(_,_)=>Withdraw();menu.Items.Add(remove);menu.IsOpen=true;
        };
        // Hosted input can arrive already handled by an ancestor. Still invoke the
        // same validated action as the button, before TextBox/default-button processing.
        amount.AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler((_,e)=>{
            if(e.Key!=Key.Enter)return;e.Handled=true;
            assignSelected.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }),true);
        palette.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){palette.IsDropDownOpen=false;Assign(false);e.Handled=true;}};
        undo=Button(form,"Шаг назад",Undo);undo.IsEnabled=false;
        grid.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBox){Undo();e.Handled=true;}};
        void Save()
        {
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            var missing=seats.Where(s=>!withdrawn.Contains(s.Id)&&!prices.ContainsKey(s.Id)).ToArray();
            if(missing.Length>0)throw new TicketException("Назначьте цену или заблокируйте следующие места:\n"+string.Join("\n",missing.Select(s=>s.Label)));
            if(sourcePlan is not null)
            {
                if(string.IsNullOrWhiteSpace(name.Text)&&Pricing012Presentation.SamePrices(sourcePlan.Prices,sourcePlan.Withdrawn,prices.Values,withdrawn))
                {
                    foreach(var category in prices.Values.GroupBy(p=>p.Amount))store.SetPriceColor(category.Key,category.First().Color);
                    CreatedId=sourcePlan.Id;Finish(true);return;
                }
                if(string.IsNullOrWhiteSpace(name.Text))
                {
                var existingNames=store.Plans(v.Id).Select(p=>CatalogText.Key(p.Name)).ToHashSet();
                var entered=Prompt("Сохранить новую расценовку","Новое уникальное название","",text=>
                    string.IsNullOrWhiteSpace(text)?"Введите название расценовки.":existingNames.Contains(CatalogText.Key(text))?"Такое название уже есть. Введите новое название.":null);
                if(entered is null)return;name.Text=entered;
                }
            }
            CreatedId=store.SavePlan(v.Id,name.Text,prices.Values.ToArray(),withdrawn.ToArray());Finish(true);
        }
        HeaderSaveAction=()=>Try(Save);
        var standaloneHeader=new Grid{Background=Brushes.AliceBlue,Margin=new Thickness(0)};standaloneHeader.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(306)});standaloneHeader.ColumnDefinitions.Add(new ColumnDefinition());
        var standaloneSave=new Button{Content="Сохранить",Width=140,Margin=new Thickness(0)};standaloneSave.Click+=(_,_)=>HeaderSaveAction();standaloneHeader.Children.Add(standaloneSave);
        var standaloneTitle=new TextBlock{Text="Расценовка",FontSize=21,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(standaloneTitle,1);standaloneHeader.Children.Add(standaloneTitle);
        var standaloneClose=new Button{Content="Закрыть",Width=110,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0)};standaloneClose.Click+=(_,_)=>Close();Grid.SetColumn(standaloneClose,1);standaloneHeader.Children.Add(standaloneClose);
        var shell=new DockPanel();Content=shell;DockPanel.SetDock(standaloneHeader,Dock.Top);shell.Children.Add(standaloneHeader);shell.Children.Add(grid);OnShown(()=>standaloneHeader.Visibility=IsInline?Visibility.Collapsed:Visibility.Visible);
        form.Children.Add(new TextBlock{Text="Назначьте цену каждому месту или уберите его из продажи. Цвет каждой цены общий для всех схем.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)});
        var area=new DockPanel();Grid.SetColumn(area,1);grid.Children.Add(area);
        DockPanel.SetDock(hallTotals,Dock.Top);area.Children.Add(hallTotals);
        var summaryScroll=new ScrollViewer{Content=priceSummary,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};
        DockPanel.SetDock(summaryScroll,Dock.Top);area.Children.Add(summaryScroll);area.Children.Add(preview);
        void LoadPlans()
        {
            if(venue.SelectedItem is not VenueInfo v)return;
            plans.ItemsSource=store.Plans(v.Id).Select(p=>new PlanChoice(p,store.PricePlanCaption(p))).ToList();plans.SelectedIndex=-1;
        }
        void LoadVenue()
        {
            if(venue.SelectedItem is not VenueInfo v)return;seats=store.VenueSeats(v.Id);prices.Clear();withdrawn.Clear();sourcePlan=null;name.Text="";preview.FocusPrice=null;history.Clear();if(undo is not null)undo.IsEnabled=false;copied.Text="";LoadPlans();Refresh(true);SelectCategoryColor();SelectionSummary();
        }
        preview.EmptySpaceClicked+=(_,_)=>preview.FocusPrice=null;
        venue.SelectionChanged+=(_,_)=>Try(LoadVenue);OnShown(()=>Try(()=>{LoadVenue();if(planId.HasValue){plans.SelectedItem=plans.Items.Cast<PlanChoice>().FirstOrDefault(p=>p.Plan.Id==planId.Value);LoadSelectedPlan();}}));
    }
    private void Refresh(bool fit=false)
    {
        var sellable=seats.Where(s=>!withdrawn.Contains(s.Id)).ToList();
        var assigned=sellable.Where(s=>prices.ContainsKey(s.Id)).Select(s=>prices[s.Id]).ToArray();
        hallTotals.Inlines.Clear();
        hallTotals.Inlines.Add(new Run("Всего мест: "));hallTotals.Inlines.Add(new Run(seats.Count.ToString()){Foreground=Brushes.Red});
        hallTotals.Inlines.Add(new Run(" · Общая сумма: "));hallTotals.Inlines.Add(new Run($"{assigned.Sum(p=>(decimal)p.Amount):N0}"){Foreground=Brushes.Red});
        hallTotals.Inlines.Add(new Run(" ₽ · Заблокировано: "));hallTotals.Inlines.Add(new Run(withdrawn.Count.ToString()){Foreground=Brushes.Red});
        priceSummary.Children.Clear();priceSummary.RowDefinitions.Clear();priceSummary.ColumnDefinitions.Clear();
        var summaries=Pricing012Presentation.Summaries(assigned);
        foreach(var item in summaries)
        {
            var text=new TextBlock{Text=FormattableString.Invariant($"{item.Amount}₽×{item.Count}={item.Total}₽"),Foreground=(Brush)new BrushConverter().ConvertFromString(Pricing012Presentation.Foreground(item.Color))!,TextWrapping=TextWrapping.NoWrap,FontWeight=FontWeights.SemiBold};
            var cell=new Border{Background=(Brush)new BrushConverter().ConvertFromString(item.Color)!,Child=text,Padding=new Thickness(8,4,8,4),Margin=new Thickness(0,0,5,4),CornerRadius=new CornerRadius(3),HorizontalAlignment=HorizontalAlignment.Left};
            cell.Cursor=Cursors.Hand;cell.MouseLeftButtonUp+=(_,e)=>{preview.FocusPrice=preview.FocusPrice==item.Amount?null:item.Amount;e.Handled=true;};
            priceSummary.Children.Add(cell);
        }
        PriceSummaryLayout.Arrange(priceSummary);
        var ids=preview.Selected.ToArray();
        preview.Load(seats.Select(s=>prices.TryGetValue(s.Id,out var p)?s with{Amount=p.Amount,Color=p.Color,IsBlocked=withdrawn.Contains(s.Id)}:s with{Amount=null,IsBlocked=withdrawn.Contains(s.Id)}).ToList(),
            seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(x=>x.X),g.Min(x=>x.Y)-26)).ToList(),fit);preview.SetSelection(ids);
    }
}

internal sealed class EventEditorWindow : EditorWindow
{
    public long CreatedId{get;private set;}
    public EventEditorWindow(Store store):this(store,null){}
    public EventEditorWindow(Store store,long? performanceId):base(store,"Новый показ",650,780)
    {
        FontSize=16.9;FontWeight=FontWeights.Bold;
        var form=new StackPanel{Margin=new Thickness(25)};Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var performances=store.Performances();
        var performance=Combo(form,"Спектакль — введите начало значимого слова",performances,"Title");
        performance.SelectedItem=performances.FirstOrDefault(p=>p.Id==performanceId);performance.Text=(performance.SelectedItem as PerformanceInfo)?.Title??"";
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
        var venue=Combo(form,"Площадка / зал",store.Venues(),"Name");venue.SelectedIndex=-1;
        var plan=Combo(form,"Расценовка",Array.Empty<PlanInfo>(),"");
        var planText=new FrameworkElementFactory(typeof(TextBlock));planText.SetBinding(TextBlock.TextProperty,new Binding{Converter=new PlanCaptionConverter(store)});plan.ItemTemplate=new DataTemplate{VisualTree=planText};
        form.Children.Remove(plan);
        var planRow=new Grid();planRow.ColumnDefinitions.Add(new ColumnDefinition());planRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        plan.Margin=new Thickness(0,0,10,0);plan.VerticalAlignment=VerticalAlignment.Center;planRow.Children.Add(plan);
        var createPlan=new Button{Content="Создать",Margin=new Thickness(0)};Grid.SetColumn(createPlan,1);planRow.Children.Add(createPlan);form.Children.Add(planRow);
        plan.SetBinding(FrameworkElement.HeightProperty,new Binding(nameof(FrameworkElement.ActualHeight)){Source=createPlan});
        void LoadPlans(){plan.ItemsSource=venue.SelectedItem is VenueInfo v?store.Plans(v.Id):Array.Empty<PlanInfo>();plan.SelectedIndex=-1;}
        createPlan.Click+=(_,_)=>Try(()=>{
            if(venue.SelectedItem is not VenueInfo v)throw new TicketException("Выберите зал.");
            var editor=new PriceEditorWindow(store,v.Id);
            if(editor.ShowDialog()==true){LoadPlans();plan.SelectedItem=plan.Items.Cast<PlanInfo>().FirstOrDefault(p=>p.Id==editor.CreatedId);}
        });
        venue.SelectionChanged+=(_,_)=>Try(LoadPlans);LoadPlans();
        form.Children.Add(new TextBlock{Text="Дата",Margin=new Thickness(0,12,0,5)});
        var dateRow=new Grid();dateRow.ColumnDefinitions.Add(new ColumnDefinition());dateRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});form.Children.Add(dateRow);
        var date=new DatePicker{SelectedDate=null,CalendarStyle=new Style(typeof(Calendar),(Style)Application.Current.FindResource(typeof(Calendar))){Setters={new Setter(FrameworkElement.LayoutTransformProperty,new ScaleTransform(1.3,1.3))}},FontSize=26,Height=44,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
        date.Loaded+=(_,_)=>{
            date.ApplyTemplate();if(date.Template.FindName("PART_Button",date) is UIElement button)button.Visibility=Visibility.Collapsed;
            if(date.Template.FindName("PART_TextBox",date) is System.Windows.Controls.Primitives.DatePickerTextBox input)
            {
                // Keep the DatePicker's outer field; its editable part needs neither a second frame nor a watermark.
                var content=new FrameworkElementFactory(typeof(ScrollViewer),"PART_ContentHost");
                input.Template=new ControlTemplate(typeof(System.Windows.Controls.Primitives.DatePickerTextBox)){VisualTree=content};
                input.BorderThickness=new Thickness(0);input.Background=Brushes.Transparent;
            }
        };dateRow.Children.Add(date);
        var calendarButton=new Button{Content="Календарь",Margin=new Thickness(0)};Grid.SetColumn(calendarButton,1);dateRow.Children.Add(calendarButton);calendarButton.Click+=(_,_)=>date.IsDropDownOpen=!date.IsDropDownOpen;
        form.Children.Add(new TextBlock{Text="Время",Margin=new Thickness(0,12,0,5)});
        var timeRow=new WrapPanel();form.Children.Add(timeRow);
        ComboBox Part(string title,int count)
        {
            var group=new StackPanel{Margin=new Thickness(0,0,15,0)};timeRow.Children.Add(group);
            var box=Combo(group,title,Enumerable.Range(0,count).Select(n=>n.ToString("00")),"");box.IsEditable=true;box.IsTextSearchEnabled=false;box.Width=100;box.SelectedIndex=-1;box.Text="";return box;
        }
        var hours=Part("Часы",24);var minutes=Part("Минуты",60);var seconds=Part("Секунды",60);
        var dates=new System.Collections.ObjectModel.ObservableCollection<DateTime>();
        var dateList=new ListBox{ItemsSource=dates,Height=100,Margin=new Thickness(0,10,0,0),ItemStringFormat="dd.MM.yyyy HH:mm:ss"};
        Button(form,"Добавить дату",()=>Try(()=>{
            if(date.SelectedDate is null)throw new TicketException("Выберите дату.");
            var showing=date.SelectedDate.Value.Date+CatalogText.Time(hours.Text,minutes.Text,seconds.Text);
            if(dates.Contains(showing))throw new TicketException("Такая дата и время уже добавлены.");
            dates.Add(showing);dateList.SelectedItem=showing;
            date.SelectedDate=null;date.Text="";
            foreach(var part in new[]{hours,minutes,seconds}){part.SelectedIndex=-1;part.Text="";}
        }));
        form.Children.Add(dateList);
        var removeDate=new MenuItem{Header="Удалить дату"};removeDate.Click+=(_,_)=>{if(dateList.SelectedItem is DateTime d)dates.Remove(d);};
        dateList.ContextMenu=new ContextMenu();dateList.ContextMenu.Items.Add(removeDate);
        dateList.PreviewMouseRightButtonDown+=(_,e)=>{if(e.OriginalSource is DependencyObject source&&ItemsControl.ContainerFromElement(dateList,source) is ListBoxItem item)item.IsSelected=true;};
        dateList.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Delete&&dateList.SelectedItem is DateTime d){dates.Remove(d);e.Handled=true;}};
        form.Children.Add(new TextBlock{Text="Добавьте все даты в список. Удаление даты — правой кнопкой или Delete. Если список пуст, создаётся один показ на выбранную дату.",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,6,0,0)});
        Button(form,"Создать показ",()=>Try(()=>{
            if(performance.SelectedItem is not PerformanceInfo p||performance.Text!=p.Title||venue.SelectedItem is not VenueInfo v||plan.SelectedItem is not PlanInfo r)
                throw new TicketException("Выберите спектакль, зал и расценовку. Если расценовок нет, сначала создайте её.");
            if(dates.Count==0&&date.SelectedDate is null)throw new TicketException("Выберите дату.");
            var showingDates=dates.Count>0?dates.ToArray():new[]{date.SelectedDate!.Value.Date+CatalogText.Time(hours.Text,minutes.Text,seconds.Text)};
            CreatedId=store.CreateEvents(p.Id,v.Id,r.Id,showingDates)[0];Finish(true);
        }));
    }
}

internal sealed class PlanCaptionConverter(Store store) : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>value is PlanInfo plan?store.PricePlanCaption(plan):"";
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}
