using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketlandMappingWindow028:EditorWindow
{
    internal TicketlandMappingWindow028(Store store,MainWindow main):base(store,"Расхождения Ticketland",900,650)
    {
        var form=new DockPanel{Margin=new Thickness(20)};Content=form;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);form.Children.Add(header);
        header.Children.Add(new TextBlock{Text="Название, дата, время и площадка определяются автоматически. Подтвердите только написание сектора: решение применяется ко всем билетам группы.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        header.Children.Add(new TextBlock{Text="Поиск по спектаклю",Margin=new Thickness(0,0,0,4)});
        var search=new TextBox{Name="TicketlandSearch030",Margin=new Thickness(0,0,0,12)};header.Children.Add(search);
        var commands=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        DockPanel.SetDock(commands,Dock.Bottom);form.Children.Add(commands);
        var agree=Button(commands,"Согласиться",()=>{});var reject=Button(commands,"Отказаться",()=>{});
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
        DockPanel.SetDock(status,Dock.Bottom);form.Children.Add(status);
        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3,GridUnitType.Star)});form.Children.Add(body);
        var issues=new ListBox{Name="TicketlandIssues029",Margin=new Thickness(0,0,12,0),HorizontalContentAlignment=HorizontalAlignment.Stretch};body.Children.Add(issues);
        var row=new FrameworkElementFactory(typeof(TextBlock));row.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);row.SetValue(FrameworkElement.MarginProperty,new Thickness(0,4,0,8));
        var caption=new MultiBinding{StringFormat="{0}\n{1:dd.MM.yyyy HH:mm:ss}; {2}\nСектор Ticketland: {3}\nБилетов: {4}"};
        foreach(var field in new[]{"Title","StartsAt","Venue","SourceSector","TicketCount"})caption.Bindings.Add(new Binding(field));
        row.SetBinding(TextBlock.TextProperty,caption);issues.ItemTemplate=new DataTemplate{VisualTree=row};
        var detail=new DockPanel();Grid.SetColumn(detail,1);body.Children.Add(detail);
        var reason=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};DockPanel.SetDock(reason,Dock.Top);detail.Children.Add(reason);
        var differences=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(differences,Dock.Bottom);detail.Children.Add(differences);
        var candidates=new ListBox{Name="TicketlandCandidates029",DisplayMemberPath="Caption",HorizontalContentAlignment=HorizontalAlignment.Stretch};detail.Children.Add(candidates);
        IReadOnlyList<TicketlandSectorReview030> pending=[];
        void UpdateCommands(){agree.IsEnabled=!main.TicketlandIsUpdating028&&issues.SelectedItem is TicketlandSectorReview030&&candidates.SelectedItem is TicketlandSectorCandidate030;reject.IsEnabled=!main.TicketlandIsUpdating028&&issues.SelectedItem is TicketlandSectorReview030;}
        void Filter()
        {
            var key=(issues.SelectedItem as TicketlandSectorReview030)?.Key;
            var query=CatalogText.Key(search.Text);var visible=pending.Where(i=>CatalogText.Key(i.Title).Contains(query,StringComparison.Ordinal)).ToArray();issues.ItemsSource=visible;
            if(visible.Length>0)issues.SelectedItem=visible.FirstOrDefault(i=>i.Key==key)??visible[0];
            else{reason.Text=pending.Count==0?"Расхождений нет.":"По этому запросу расхождений нет.";candidates.ItemsSource=null;differences.Text="";}
            status.Text=pending.Count>0?$"Групп: {visible.Length} из {pending.Count}. Билетов: {visible.Sum(i=>i.TicketCount)}.":"Все расхождения рассмотрены.";UpdateCommands();
        }
        void Reload(){pending=store.TicketlandSectorReviews030();Filter();}
        issues.SelectionChanged+=(_,_)=>{
            if(issues.SelectedItem is TicketlandSectorReview030 issue)
            {
                reason.Text=$"{issue.Title}\n{issue.StartsAt:dd.MM.yyyy HH:mm:ss}; {issue.Venue}\nСектор Ticketland: {issue.SourceSector}\nБилетов: {issue.TicketCount}\n{issue.Reason}";
                candidates.ItemsSource=issue.Candidates;candidates.SelectedIndex=issue.Candidates.Count==1?0:-1;
                differences.Text=issue.MissingSeats.Count>0?"Не определены места: "+string.Join("; ",issue.MissingSeats):"Выберите местный сектор этого показа. Ряд и место каждого билета проверяются автоматически.";
            }
            else{reason.Text="";candidates.ItemsSource=null;differences.Text="";}
            UpdateCommands();
        };
        search.TextChanged+=(_,_)=>Filter();candidates.SelectionChanged+=(_,_)=>UpdateCommands();
        agree.Click+=(_,_)=>Try(()=>{
            if(main.TicketlandIsUpdating028)throw new TicketException("Дождитесь окончания обновления Ticketland.");
            if(issues.SelectedItem is not TicketlandSectorReview030 issue||candidates.SelectedItem is not TicketlandSectorCandidate030 candidate)return;
            store.AcceptTicketlandSector030(issue.Key,candidate.Sector);main.ReapplyTicketlandMappings028();Reload();
        });
        reject.Click+=(_,_)=>Try(()=>{
            if(main.TicketlandIsUpdating028)throw new TicketException("Дождитесь окончания обновления Ticketland.");
            if(issues.SelectedItem is not TicketlandSectorReview030 issue)return;
            store.RejectTicketlandSector030(issue.Key);Reload();
        });
        void StateChanged(){if(main.TicketlandIsUpdating028)UpdateCommands();else Reload();}
        main.TicketlandStatusChanged028+=StateChanged;Closed+=(_,_)=>main.TicketlandStatusChanged028-=StateChanged;
        Reload();
    }
}
