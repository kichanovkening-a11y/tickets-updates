#if CLASSIC
using System.Globalization;
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
    private readonly ListBox classicPerformances = new(){DisplayMemberPath="Title"};
    private readonly ListBox classicVenues = new(){DisplayMemberPath="Name"};
    private readonly ListBox classicDates = new(){DisplayMemberPath="Date"};
    private readonly ComboBox classicPlans = new(){DisplayMemberPath="Name",MinHeight=28};
    private readonly CheckBox classicFuture = new(){Content="Скрывать прошедшие",IsChecked=true,Margin=new Thickness(0,0,0,10)};
    private readonly List<Button> classicNavigation = [];

    private void InitializeClassic()
    {
        Title = "Распространение театральных билетов · Классическая версия";
        SeparateWindows = true;
        TopBanner043.Visibility = Visibility.Collapsed;
        WorkspaceNavigation.Background = ClassicVisual.Blue;
        WorkspaceNavigation.Padding = new Thickness(8);
        MainWorkspaceLayout.ColumnDefinitions[0].Width = new GridLength(210);
        var root = new DockPanel{Background=ClassicVisual.Gray,Margin=new Thickness(5)};
        var nav = new StackPanel{Width=190,Background=ClassicVisual.Blue};
        DockPanel.SetDock(nav,Dock.Left);
        root.Children.Add(new Border{Child=nav,Background=ClassicVisual.Blue,BorderBrush=ClassicVisual.Edge,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,5,0)});
        AddNavigation("Спектакли и\nплощадки",()=>ShowClassicCatalog(0));
        AddNavigation("Выдача билетов",()=>ShowClassicIssue());
        AddNavigation("Редактор схем",()=>Run(()=>{HallsClick(this,new RoutedEventArgs());RefreshClassicCatalog();}));
        AddNavigation("Настройки",()=>SettingsClick(this,new RoutedEventArgs()));
        var body = (UIElement)Content;
        Content = null;
        var host = new Grid();
        host.Children.Add(body);
        classicCatalog = BuildClassicCatalog();
        host.Children.Add(classicCatalog);
        root.Children.Add(host);
        Content = root;
        Loaded += (_,_)=>ShowClassicCatalog(0);

        void AddNavigation(string label,Action action)
        {
            var button = new Button{Content=label,ContentTemplate=null,Width=190,Height=56,
                FontSize=16,HorizontalContentAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,2,0,2),
                Background=ClassicVisual.Blue};
            button.Click+=(_,_)=>action();
            nav.Children.Add(button);
            classicNavigation.Add(button);
        }
    }

    private Grid BuildClassicCatalog()
    {
        var grid = new Grid{Background=ClassicVisual.Gray};
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});
        grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var shows = Group("Спектакли",classicPerformances,
            ("＋ Создать",()=>Run(()=>{new PerformanceWindow(store).ShowDialog();RefreshClassicCatalog();})),
            ("Изменить",()=>Run(()=>{PerformancesClick(this,new RoutedEventArgs());RefreshClassicCatalog();})),
            ("Удалить",()=>Run(()=>{
                if(classicPerformances.SelectedItem is PerformanceInfo p &&
                    MessageBox.Show(this,$"Удалить спектакль «{p.Title}» и его показы?","Удаление",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
                    store.DeletePerformance(p.Id);
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
        issue.Click+=(_,_)=>ShowClassicIssue();
        DockPanel.SetDock(issue,Dock.Top);lower.Children.Add(issue);lower.Children.Add(new Border());
        Grid.SetColumn(lower,1);Grid.SetRow(lower,1);grid.Children.Add(lower);
        classicPerformances.SelectionChanged+=(_,_)=>RefreshClassicDates();
        classicVenues.SelectionChanged+=(_,_)=>RefreshClassicPlans();
        classicFuture.Checked+=(_,_)=>RefreshClassicDates();classicFuture.Unchecked+=(_,_)=>RefreshClassicDates();
        classicDates.MouseDoubleClick+=(_,_)=>ShowClassicIssue();
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

    private void RefreshClassicCatalog()
    {
        var id=(classicPerformances.SelectedItem as PerformanceInfo)?.Id;
        var venueId=(classicVenues.SelectedItem as VenueInfo)?.Id;
        classicPerformances.ItemsSource=store.Performances();
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
        if(classicDates.SelectedItem is EventInfo selected)SelectEvent(selected.Id);
        else ShowHome();
    }
}
#endif
