using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class PerformanceWindow : EditorWindow
{
    private static TreeViewItem? NearestItem(DependencyObject? source)
    {
        while(source is not null)
        {
            if(source is TreeViewItem item)return item;
            source=source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
    public long SelectedEventId {get;private set;}
    public PerformanceWindow(Store store):base(store,"Спектакли",800,780)
    {
        var root=new DockPanel{Margin=new Thickness(20)};Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var name=Field(header,"Название нового спектакля");
        var search= new TextBox{Margin=new Thickness(0,10,0,8)};
        var tree=new TreeView();
        Button(header,"Создать спектакль",()=>Try(()=>{store.SavePerformance(name.Text);name.Clear();Refresh();}));
        header.Children.Add(new TextBlock{Text="Поиск спектакля",Margin=new Thickness(0,10,0,0)});header.Children.Add(search);
        root.Children.Add(tree);
        void Refresh()
        {
            tree.Items.Clear();var events=store.Events();var now=DateTime.Now;
            foreach(var performance in store.Performances().Where(p=>SearchText.Matches(p.Title,search.Text)))
            {
                var node=new TreeViewItem{Header=new TextBlock{Text=performance.Title,FontSize=13,FontWeight=FontWeights.SemiBold},Tag=performance};
                tree.Items.Add(node);
                node.PreviewMouseLeftButtonDown+=(_,e)=>{
                    if(e.OriginalSource is DependencyObject source&&ReferenceEquals(NearestItem(source),node))
                    {node.IsSelected=true;node.IsExpanded=!node.IsExpanded;foreach(var child in node.Items.OfType<TreeViewItem>().Where(i=>i.Tag is null))child.IsExpanded=node.IsExpanded;e.Handled=true;}
                };
                var shows=events.Where(e=>CatalogText.Key(e.Title)==CatalogText.Key(performance.Title)).OrderBy(e=>e.SortDate).ToList();
                void Add(ItemsControl parent,IEnumerable<Tickets.Core.EventInfo> items)
                {
                    foreach(var showing in items)
                    {
                        var leaf=new TreeViewItem{Header=$"{showing.Date} · {showing.Venue}",Tag=showing};
                        leaf.PreviewMouseLeftButtonDown+=(_,e)=>{
                            if(e.OriginalSource is DependencyObject source&&ReferenceEquals(NearestItem(source),leaf))
                            {e.Handled=true;SelectedEventId=showing.Id;Finish(true);}
                        };
                        parent.Items.Add(leaf);
                    }
                }
                Add(node,shows.Where(e=>DateTime.Parse(e.SortDate)>=now));
                var past=shows.Where(e=>DateTime.Parse(e.SortDate)<now).Reverse().ToList();
                if(past.Count>0){var archive=new TreeViewItem{Header=$"Прошедшие ({past.Count})",Foreground=Brushes.Gray};node.Items.Add(archive);Add(archive,past);}
                if(shows.Count==0)node.Items.Add(new TreeViewItem{Header="Показов пока нет",IsEnabled=false});
                var menu=new ContextMenu();node.ContextMenu=menu;
                var dates=new MenuItem{Header="Добавить даты"};menu.Items.Add(dates);
                dates.Click+=(_,_)=>Try(()=>{var editor=new EventEditorWindow(store,performance.Id);if(editor.ShowDialog()==true)Refresh();});
                var delete=new MenuItem{Header="Удалить"};menu.Items.Add(delete);
                delete.Click+=(_,_)=>Try(()=>{
                    var linked=store.PerformanceDeletionSummary(performance.Id);
                    if(MessageBox.Show(DialogOwner,$"Полностью удалить спектакль «{performance.Title}»?\n\nПоказы: {linked.Shows}. Накладные: {linked.Issues}. Билеты (включая возвраты): {linked.Tickets}.\nСвязанные выдачи и документы будут удалены. Перед удалением сохранится резервная копия.","Удаление спектакля",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                    store.DeletePerformance(performance.Id);Refresh();
                });
                node.PreviewMouseRightButtonDown+=(_,e)=>{e.Handled=true;if(e.OriginalSource is DependencyObject source&&ReferenceEquals(NearestItem(source),node)){node.IsSelected=true;menu.IsOpen=true;}};
            }
        }
        tree.SelectedItemChanged+=(_,_)=>{
            if(tree.SelectedItem is not TreeViewItem item)return;
            if(item.Tag is Tickets.Core.EventInfo showing){SelectedEventId=showing.Id;Finish(true);}
            else if(item.Tag is not PerformanceInfo)item.IsExpanded=true;
        };
        search.TextChanged+=(_,_)=>Try(Refresh);Refresh();
    }
}

internal sealed class PricingDirectoryWindow : EditorWindow
{
    public PricingDirectoryWindow(Store store):base(store,"Расценовки",850,750)
    {
        var root=new DockPanel{Margin=new Thickness(20)};Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text="Выберите площадку, затем расценовку",TextWrapping=TextWrapping.Wrap});
        var search=Field(header,"Поиск площадки / расценовки");
        var tree=new TreeView{Margin=new Thickness(0,12,0,0)};
        VenueInfo? selectedVenue=null;var opening=false;
        Button? create=null;create=Button(header,"Создать расценовку",()=>Try(()=>{
            if(selectedVenue is null)return;
            new PriceEditorWindow(store,selectedVenue.Id).ShowDialog();Refresh(selectedVenue.Id);
        }));create!.IsEnabled=false;root.Children.Add(tree);
        void Refresh(long? expand=null)
        {
            tree.Items.Clear();selectedVenue=null;create!.IsEnabled=false;
            foreach(var venue in store.Venues())
            {
                var plans=store.Plans(venue.Id);
                var venueMatches=SearchText.Matches(venue.Name,search.Text);
                var matching=venueMatches?plans:plans.Where(p=>SearchText.Matches(p.Name,search.Text)).ToList();
                if(!venueMatches&&matching.Count==0)continue;
                var node=new TreeViewItem{Header=$"{venue.Name} ({plans.Count})",Tag=venue,IsExpanded=venue.Id==expand};tree.Items.Add(node);
                foreach(var plan in matching)
                {
                    var leaf=new TreeViewItem{Header=store.PricePlanCaption(plan),Tag=plan};node.Items.Add(leaf);
                    var menu=new ContextMenu();var delete=new MenuItem{Header="Удалить"};menu.Items.Add(delete);leaf.ContextMenu=menu;
                    delete.Click+=(_,_)=>Try(()=>{
                        if(MessageBox.Show(DialogOwner,$"Удалить расценовку «{plan.Name}»?\n\nЦены существующих показов и история сохранятся.","Удаление расценовки",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                        store.DeletePlan(plan.Id);Refresh(venue.Id);
                    });
                    // Right click must select the target without opening the editor.
                    leaf.PreviewMouseRightButtonDown+=(_,e)=>{opening=true;try{leaf.IsSelected=true;}finally{opening=false;}e.Handled=true;leaf.ContextMenu.IsOpen=true;};
                }
                if(matching.Count==0)node.Items.Add(new TreeViewItem{Header="Расценовок пока нет",IsEnabled=false});
            }
        }
        tree.SelectedItemChanged+=(_,_)=>Try(()=>{
            if(tree.SelectedItem is not TreeViewItem item)return;
            if(item.Tag is VenueInfo venue){selectedVenue=venue;create!.IsEnabled=true;item.IsExpanded=true;}
            else if(item.Tag is PlanInfo plan&&!opening)
            {
                opening=true;
                try{new PriceEditorWindow(store,plan.VenueId,plan.Id).ShowDialog();Refresh(plan.VenueId);}
                finally{opening=false;}
            }
        });
        search.TextChanged+=(_,_)=>Try(()=>Refresh());Refresh();
    }
}
