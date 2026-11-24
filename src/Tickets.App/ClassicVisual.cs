#if CLASSIC
using System.Globalization;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal static class ClassicVisual
{
    internal static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(214,230,245));
    internal static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(240,240,240));
    internal static readonly Brush Edge = new SolidColorBrush(Color.FromRgb(136,160,183));

    internal static void Install()
    {
        var resources = Application.Current.Resources;
        var window = new Style(typeof(Window));
        window.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("Tahoma")));
        window.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
        window.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
        window.Setters.Add(new Setter(Control.BackgroundProperty, Gray));
        resources[typeof(Window)] = window;
        var button = new Style(typeof(Button), (Style)resources[typeof(Button)]);
        button.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
        button.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
        button.Setters.Add(new Setter(Control.BackgroundProperty, Gray));
        button.Setters.Add(new Setter(Control.BorderBrushProperty, Edge));
        button.Setters.Add(new Setter(Control.HeightProperty, 30d));
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,2,8,2)));
        resources[typeof(Button)] = button;
        var text = new Style(typeof(TextBlock));
        text.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));
        text.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        resources[typeof(TextBlock)] = text;
        var group = new Style(typeof(GroupBox));
        group.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8)));
        group.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.DarkGray));
        group.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(5)));
        resources[typeof(GroupBox)] = group;
    }
}

public partial class MainWindow
{
    private Grid? classicCatalog;
    private Border? classicSidebar;
    private Grid? classicRoot;
    private GridSplitter? classicSplitter;
    private double classicSidebarWidth;
    private string ClassicSidebarPath=>Path.Combine(Path.GetDirectoryName(store.Path)!,"classic-sidebar-width.txt");
    private bool classicNavigationBusy;
    private readonly ListBox classicPerformances = new(){DisplayMemberPath="Title"};
    private readonly ListBox classicVenues = new(){DisplayMemberPath="Name"};
    private readonly ListBox classicDates = new(){DisplayMemberPath="Date"};
    private readonly ComboBox classicPlans = new(){DisplayMemberPath="Name",MinHeight=28};
    private readonly CheckBox classicFuture = new(){Content="Скрывать прошедшие",IsChecked=true,Margin=new Thickness(0,0,0,10)};
    private readonly List<Button> classicNavigation = [];

    private void InitializeClassic()
    {
        Title = "Распространение театральных билетов · Классическая версия";
        TopBanner043.Visibility = Visibility.Collapsed;
        WorkspaceNavigation.Background = ClassicVisual.Blue;
        WorkspaceNavigation.Padding = new Thickness(8);
        MainWorkspaceLayout.ColumnDefinitions[0].Width = new GridLength(0);
        MainWorkspaceLayout.ColumnDefinitions[1].Width = new GridLength(0);
        WorkspaceNavigation.Visibility=Visibility.Collapsed;
        var root = new Grid{Background=ClassicVisual.Gray,Margin=new Thickness(5)};classicRoot=root;
        root.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        root.ColumnDefinitions.Add(new(){Width=new GridLength(6)});
        root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star),MinWidth=220});
        var nav = new DockPanel{Background=ClassicVisual.Blue};
        var buttons=new StackPanel();DockPanel.SetDock(buttons,Dock.Top);nav.Children.Add(buttons);
        classicSidebar=new Border{Child=nav,Background=ClassicVisual.Blue,BorderBrush=ClassicVisual.Edge,BorderThickness=new Thickness(1),Margin=new Thickness(0)};
        root.Children.Add(classicSidebar);
        classicSplitter=new GridSplitter{Width=6,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,ResizeDirection=GridResizeDirection.Columns,ResizeBehavior=GridResizeBehavior.PreviousAndNext,Background=ClassicVisual.Edge};
        Grid.SetColumn(classicSplitter,1);root.Children.Add(classicSplitter);
        AddNavigation("Спектакли и\nплощадки",()=>ShowClassicCatalog(0));
        AddNavigation("Выдача\nбилетов",()=>ShowClassicIssue());
        AddNavigation("Редактор\nсхем",()=>Run(()=>{HallsClick(this,new RoutedEventArgs());RefreshClassicCatalog();}));
        AddNavigation("Настройки",()=>SettingsClick(this,new RoutedEventArgs()));
        AddNavigation("Расценовки",()=>PricesClick(this,new RoutedEventArgs()));
        AddNavigation("Получатели",()=>RecipientsClick(this,new RoutedEventArgs()));
        var compactWidth=Math.Ceiling(classicNavigation.SelectMany(b=>b.Content.ToString()!.Split(new[]{' ','\n'},StringSplitOptions.RemoveEmptyEntries))
            .Max(word=>new FormattedText(word,CultureInfo.GetCultureInfo("ru-RU"),FlowDirection.LeftToRight,new Typeface("Tahoma"),16,Brushes.Black,1).WidthIncludingTrailingWhitespace)+16);
        buttons.Width=compactWidth;buttons.HorizontalAlignment=HorizontalAlignment.Left;
        foreach(var button in classicNavigation){button.Width=compactWidth;button.HorizontalAlignment=HorizontalAlignment.Stretch;}
        root.ColumnDefinitions[0].MinWidth=compactWidth+4;root.ColumnDefinitions[0].MaxWidth=480;
        classicSidebarWidth=compactWidth+4;
        try{if(File.Exists(ClassicSidebarPath)&&double.TryParse(File.ReadAllText(ClassicSidebarPath),NumberStyles.Float,CultureInfo.InvariantCulture,out var saved)&&double.IsFinite(saved))classicSidebarWidth=Math.Clamp(saved,compactWidth+4,480);}catch(IOException){}catch(UnauthorizedAccessException){}
        root.ColumnDefinitions[0].Width=new GridLength(classicSidebarWidth);
        classicSplitter.DragCompleted+=(_,e)=>{if(e.Canceled)return;classicSidebarWidth=root.ColumnDefinitions[0].ActualWidth;Run(()=>File.WriteAllText(ClassicSidebarPath,classicSidebarWidth.ToString("R",CultureInfo.InvariantCulture)));};
        ((Panel)SearchBox.Parent).Children.Remove(SearchBox);
        SearchBox.Width=double.NaN;SearchBox.Visibility=Visibility.Visible;SearchBox.Margin=new Thickness(2,4,2,6);
        buttons.Children.Add(new TextBlock{Text="Поиск",Margin=new Thickness(4,8,0,0)});
        buttons.Children.Add(SearchBox);
        ((Panel)EventsList.Parent).Children.Remove(EventsList);nav.Children.Add(EventsList);
        EventsList.Background=Brushes.White;
        EventsList.SizeChanged+=(_,_)=>WrapClassicEvents();
        SearchBox.TextChanged+=(_,_)=>{if(classicCatalog?.Visibility==Visibility.Visible)RefreshClassicCatalog();};
        var body = (UIElement)Content;
        Content = null;
        var host = new Grid();
        host.Children.Add(body);
        classicCatalog = BuildClassicCatalog();
        host.Children.Add(classicCatalog);
        Grid.SetColumn(host,2);root.Children.Add(host);
        // Keep embedded dialogs above both the catalog and the classic navigation.
        ((Panel)DialogLayer.Parent).Children.Remove(DialogLayer);
        var outer=new Grid();outer.Children.Add(root);outer.Children.Add(DialogLayer);
        DialogLayer.IsVisibleChanged+=(_,_)=>root.IsEnabled=!DialogLayer.IsVisible;
        Content = outer;
        Loaded += (_,_)=>ShowClassicCatalog(0);

        void AddNavigation(string label,Action action)
        {
            var index=classicNavigation.Count;
            var button = new Button{Content=label,ContentTemplate=null,Width=double.NaN,Height=double.NaN,MinHeight=38,Padding=new Thickness(5,6,5,6),
                FontSize=16,HorizontalContentAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,2,0,2),
                Background=ClassicVisual.Blue};
            button.Click+=(_,_)=>{
                if(classicNavigationBusy)return;
                classicNavigationBusy=true;SetClassicNavigation(index);
                try{action();}finally{classicNavigationBusy=false;}
            };
            buttons.Children.Add(button);
            classicNavigation.Add(button);
        }
    }

    private void WrapClassicEvents()
    {
        void Wrap(ItemsControl parent,int depth)
        {
            foreach(var item in parent.Items.OfType<TreeViewItem>())
            {
                if(item.Header is string title)item.Header=new TextBlock{Text=title};
                if(item.Header is TextBlock text){text.MaxWidth=Math.Max(24,EventsList.ActualWidth-30-depth*16);text.TextWrapping=TextWrapping.Wrap;text.ToolTip=new System.Windows.Documents.TextRange(text.ContentStart,text.ContentEnd).Text;}
                Wrap(item,depth+1);
            }
        }
        Wrap(EventsList,0);
    }
    private void SetClassicExpanded(bool expanded)
    {
        if(classicRoot is null||classicSidebar is null||classicSplitter is null)return;
        if(expanded&&classicSidebar.IsVisible)classicSidebarWidth=classicRoot.ColumnDefinitions[0].ActualWidth;
        classicSidebar.Visibility=classicSplitter.Visibility=expanded?Visibility.Collapsed:Visibility.Visible;
        classicRoot.ColumnDefinitions[0].MinWidth=expanded?0:classicNavigation[0].Width+4;
        classicRoot.ColumnDefinitions[0].Width=new GridLength(expanded?0:classicSidebarWidth);
        classicRoot.ColumnDefinitions[1].Width=new GridLength(expanded?0:6);
    }
    private Grid BuildClassicCatalog()
    {
        var grid = new Grid{Background=ClassicVisual.Gray};
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});
        grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var shows = Group("Спектакли",classicPerformances,
            ("＋ Создать",()=>Run(()=>{var picker=new PosterPicker(store,repertoire,Cards(),null,null,"");if(picker.ShowDialog()==true)SavePicked(picker);RefreshClassicCatalog();})),
            ("Изменить",()=>Run(()=>{if(classicPerformances.SelectedItem is PerformanceInfo p)new ClassicPerformanceEditor(store,p,()=>EditPoster(p.Title,p.Id)).ShowDialog();RefreshClassicCatalog();RefreshCatalogNavigation();ShowClassicCatalog(0);})),
            ("Удалить",()=>Run(()=>{
                if(classicPerformances.SelectedItem is not PerformanceInfo p)return;
                var linked=store.PerformanceDeletionSummary(p.Id);
                var text=$"Полностью удалить спектакль «{p.Title}»?\n\nПоказы, включая прошедшие: {linked.Shows}. Накладные: {linked.Issues}. Билеты: {linked.Tickets}.\nПеред удалением сохранится резервная копия.";
                if(MessageBox.Show(this,text,"Удаление спектакля",MessageBoxButton.YesNo,
                    MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes)
                    store.DeletePerformance(p.Id,includeArchived:true);
                RefreshClassicCatalog();
            })));
        grid.Children.Add(shows);
        var schedule = new DockPanel();
        DockPanel.SetDock(classicFuture,Dock.Top);schedule.Children.Add(classicFuture);schedule.Children.Add(classicDates);
        var dates = Group("Расписание",schedule,
            ("＋ Создать",()=>Run(()=>{
                var id=(classicPerformances.SelectedItem as PerformanceInfo)?.Id;
                new EventEditorWindow(store,id).ShowDialog();RefreshClassicCatalog();
            })),
            ("Изменить",()=>Run(()=>{if(classicDates.SelectedItem is EventInfo e){EditShow050(e);RefreshClassicCatalog();}})),
            ("Удалить",()=>Run(()=>{
                if(classicDates.SelectedItem is EventInfo e && CatalogCleanup023.Confirm(this,1))store.DeleteEventsPermanently([e.Id]);
                RefreshClassicCatalog();
            })));
        Grid.SetColumn(dates,1);grid.Children.Add(dates);
        var venues = Group("Площадки",classicVenues,
            ("＋ Создать",()=>Run(()=>{new HallEditorWindow(store).ShowDialog();RefreshClassicCatalog();})),
            ("Изменить",()=>Run(()=>{
                if(classicVenues.SelectedItem is VenueInfo v)new HallEditorWindow(store,v,true).ShowDialog();
                RefreshClassicCatalog();
            })),
            ("Удалить",()=>Run(()=>{
                if(classicVenues.SelectedItem is VenueInfo v&&MessageBox.Show(this,$"Удалить площадку «{v.Name}»?","Удаление",MessageBoxButton.YesNo)==MessageBoxResult.Yes)store.DeleteVenue(v.Id);
                RefreshClassicCatalog();
            })));
        Grid.SetRow(venues,1);grid.Children.Add(venues);
        var lower = new DockPanel();
        var plans = Group("Ценовая схема",classicPlans,
            ("Список",()=>Run(()=>{PricesClick(this,new RoutedEventArgs());RefreshClassicPlans();})),
            ("＋ Создать",()=>Run(()=>{
                new PriceEditorWindow(store,(classicVenues.SelectedItem as VenueInfo)?.Id).ShowDialog();RefreshClassicPlans();
            })),
            ("Изменить",()=>Run(()=>{
                if(classicPlans.SelectedItem is PlanInfo p)new PriceEditorWindow(store,p.VenueId,p.Id).ShowDialog();
                RefreshClassicPlans();
            })));
        DockPanel.SetDock(plans,Dock.Top);lower.Children.Add(plans);
        var issue = new Button{Content="Перейти к выдаче билетов",ContentTemplate=null,Height=78,
            Width=double.NaN,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(8),Background=Brushes.White};
        issue.Click+=(_,_)=>OpenClassicSelectedShow();
        DockPanel.SetDock(issue,Dock.Top);lower.Children.Add(issue);lower.Children.Add(new Border());
        Grid.SetColumn(lower,1);Grid.SetRow(lower,1);grid.Children.Add(lower);
        classicPerformances.SelectionChanged+=(_,_)=>RefreshClassicDates();
        classicVenues.SelectionChanged+=(_,_)=>RefreshClassicPlans();
        classicFuture.Checked+=(_,_)=>RefreshClassicDates();classicFuture.Unchecked+=(_,_)=>RefreshClassicDates();
        classicDates.MouseDoubleClick+=(_,_)=>OpenClassicSelectedShow();
        classicDates.SelectionChanged+=(_,_)=>{
            if(classicDates.SelectedItem is EventInfo e)
                classicVenues.SelectedItem=classicVenues.Items.Cast<VenueInfo>().FirstOrDefault(v=>v.Id==e.VenueId);
        };
        return grid;
    }

    private static GroupBox Group(string title,UIElement content,params (string Text,Action Run)[] actions)
    {
        var panel = new DockPanel();
        var buttons = new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};
        DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
        foreach(var action in actions)
        {
            var button=new Button{Content=action.Text,ContentTemplate=null,Width=double.NaN,MinWidth=92,Margin=new Thickness(3,0,0,0)};
            button.Click+=(_,_)=>action.Run();buttons.Children.Add(button);
        }
        panel.Children.Add(content);
        return new GroupBox{Header=title,Content=panel};
    }

    internal void VerifyClassicNavigation()
    {
        classicFuture.IsChecked=false;
        classicPerformances.SelectedItem=classicPerformances.Items.Cast<PerformanceInfo>()
            .FirstOrDefault(p=>store.Events().Any(e=>CatalogText.Key(e.Title)==CatalogText.Key(p.Title)));
        if(classicDates.Items.Count==0)throw new InvalidOperationException("Не загрузилось расписание спектакля.");
        classicDates.SelectedIndex=0;
        var selected=(EventInfo)classicDates.SelectedItem;
        OpenClassicSelectedShow();
        if(Workspace.Visibility!=Visibility.Visible||current?.Id!=selected.Id)
            throw new InvalidOperationException("Не открылся выбранный сеанс.");
        ShowClassicCatalog(0);
        classicFuture.IsChecked=true;
    }

    private void RefreshClassicCatalog()
    {
        var id=(classicPerformances.SelectedItem as PerformanceInfo)?.Id;
        var venueId=(classicVenues.SelectedItem as VenueInfo)?.Id;
        var matching=store.Events().Where(e=>SearchText.Matches(e.Title+" "+e.Venue+" "+e.Date,SearchBox.Text))
            .Select(e=>CatalogText.Key(e.Title)).ToHashSet();
        classicPerformances.ItemsSource=store.Performances()
            .Where(p=>SearchText.Matches(p.Title,SearchBox.Text)||matching.Contains(CatalogText.Key(p.Title))).ToArray();
        classicVenues.ItemsSource=store.Venues();
        classicPerformances.SelectedItem=classicPerformances.Items.Cast<PerformanceInfo>().FirstOrDefault(p=>p.Id==id);
        classicVenues.SelectedItem=classicVenues.Items.Cast<VenueInfo>().FirstOrDefault(v=>v.Id==venueId);
        RefreshClassicDates();
    }

    private void RefreshClassicDates()
    {
        var selected=(classicDates.SelectedItem as EventInfo)?.Id;
        var performance=classicPerformances.SelectedItem as PerformanceInfo;
        classicDates.ItemsSource=performance is null ? [] : store.Events()
            .Where(e=>CatalogText.Key(e.Title)==CatalogText.Key(performance.Title))
            .Where(e=>classicFuture.IsChecked!=true||DateTime.Parse(e.SortDate,CultureInfo.InvariantCulture)>=DateTime.Now)
            .OrderBy(e=>e.SortDate).ToArray();
        classicDates.SelectedItem=classicDates.Items.Cast<EventInfo>().FirstOrDefault(e=>e.Id==selected);
    }

    private void RefreshClassicPlans()
    {
        classicPlans.ItemsSource=classicVenues.SelectedItem is VenueInfo v ? store.Plans(v.Id) : [];
        if(classicPlans.Items.Count>0)classicPlans.SelectedIndex=0;
    }

    private void ShowClassicCatalog(int active)
    {
        if(classicCatalog is null)return;
        classicCatalog.Visibility=Visibility.Visible;
        SetClassicNavigation(active);RefreshClassicCatalog();
    }

    private void SetClassicNavigation(int active)
    {
        for(var i=0;i<classicNavigation.Count;i++)
            classicNavigation[i].Background=i==active?new SolidColorBrush(Color.FromRgb(255,241,197)):ClassicVisual.Blue;
    }

    private void ShowClassicIssue()
    {
        if(classicCatalog is null)return;
        classicCatalog.Visibility=Visibility.Collapsed;
        SetClassicNavigation(1);
        ReadEventCatalog();ApplyFilter();
        current=null;foreach(var row in EventRows())row.IsSelected=false;
        ShowHome();
    }
    private void OpenClassicSelectedShow()
    {
        var selected=classicDates.SelectedItem as EventInfo;
        ShowClassicIssue();
        if(selected is not null)SelectEvent(selected.Id);
    }
}
internal sealed class ClassicPerformanceEditor:EditorWindow
{
    internal ClassicPerformanceEditor(Store store,PerformanceInfo performance,Action poster):base(store,"Изменить спектакль",560,300)
    {
        var form=new StackPanel{Margin=new Thickness(16)};Content=form;
        var name=Field(form,"Название спектакля",performance.Title);
        Button(form,"Афиша",()=>Try(poster));
        Button(form,"Сохранить",()=>Try(()=>{store.RenamePerformance(performance.Id,name.Text);Finish(true);}));
    }
}
#endif
