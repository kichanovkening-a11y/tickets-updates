using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class RecipientsWindow:EditorWindow
{
    private readonly TextBox search=new();
    private readonly CheckBox showDeleted=new(){Content="Показать удалённых",Margin=new Thickness(0,5,0,5)};
    private readonly ListBox organizations=new(){DisplayMemberPath=nameof(RecipientDirectoryInfo.Caption),HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly TreeView history=new();
    private readonly TextBlock heading=new(){FontSize=21,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock summary=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,10)};
    private readonly TextBlock count=new(){Margin=new Thickness(0,5,0,10)};
    private List<RecipientDirectoryInfo> directory=[];
    private readonly Button open;
    private readonly MenuItem delete;

    public RecipientsWindow(Store store):base(store,"Получатели / организации",1100,780)
    {
        var root=new Grid{Margin=new Thickness(18)};Content=root;
        root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(330)});
        root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});
        root.ColumnDefinitions.Add(new ColumnDefinition());
        var left=new DockPanel();root.Children.Add(left);
        var filter=new StackPanel();DockPanel.SetDock(filter,Dock.Top);left.Children.Add(filter);
        filter.Children.Add(new TextBlock{Text="Поиск организации",FontWeight=FontWeights.SemiBold});
        search.Margin=new Thickness(0,6,0,0);filter.Children.Add(search);
        filter.Children.Add(new TextBlock{Text="Начните вводить значимые слова названия",TextWrapping=TextWrapping.Wrap,FontSize=15.6,Margin=new Thickness(0,5,0,0)});
        Button(filter,"Создать получателя",()=>Try(()=>{
            var editor=new RecipientEditorWindow(Store,search.Text);
            if(editor.ShowDialog()!=true)return;
            search.Clear();directory=Store.RecipientDirectory(showDeleted.IsChecked==true);Filter();
            organizations.SelectedItem=directory.FirstOrDefault(x=>x.Name==editor.CreatedName);
            organizations.ScrollIntoView(organizations.SelectedItem);
        }));
        filter.Children.Add(showDeleted);showDeleted.Checked+=(_,_)=>{directory=Store.RecipientDirectory(true);Filter();};showDeleted.Unchecked+=(_,_)=>{directory=Store.RecipientDirectory(showDeleted.IsChecked==true);Filter();};
        filter.Children.Add(count);left.Children.Add(organizations);
        var splitter=new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Stretch};Grid.SetColumn(splitter,1);root.Children.Add(splitter);
        var right=new DockPanel();Grid.SetColumn(right,2);root.Children.Add(right);
        var details=new StackPanel();DockPanel.SetDock(details,Dock.Top);right.Children.Add(details);details.Children.Add(heading);details.Children.Add(summary);
        var actions=new WrapPanel{Margin=new Thickness(0,0,0,12)};details.Children.Add(actions);
        open=Button(actions,"Открыть выдачу",()=>Try(OpenSelected));open.Margin=new Thickness(0,0,10,0);
        delete=new MenuItem{Header="Удалить"};
        delete.Click+=(_,_)=>Try(()=>{
            if(organizations.SelectedItem is not RecipientDirectoryInfo organization)return;
            if(MessageBox.Show(DialogOwner,$"Удалить получателя «{organization.Name}» из справочника?\n\nУже выданные билеты, накладные и история сохранятся.","Удалить получателя",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            Store.DeleteRecipient(organization.Name);directory=Store.RecipientDirectory(showDeleted.IsChecked==true);Filter();
        });
        organizations.ContextMenu=new ContextMenu();organizations.ContextMenu.Items.Add(delete);
        organizations.PreviewMouseRightButtonDown+=(_,e)=>{
            if(e.OriginalSource is DependencyObject source&&ItemsControl.ContainerFromElement(organizations,source) is ListBoxItem item){item.IsSelected=true;item.Focus();}
            else e.Handled=true;
        };
        organizations.ContextMenuOpening+=(_,e)=>{if(organizations.SelectedItem is not RecipientDirectoryInfo {IsDeleted:false})e.Handled=true;};
        right.Children.Add(history);
        search.TextChanged+=(_,_)=>Try(Filter);
        organizations.SelectionChanged+=(_,_)=>Try(ShowHistory);
        history.SelectedItemChanged+=(_,_)=>open.IsEnabled=history.SelectedItem is TreeViewItem {Tag:RecipientIssueInfo};
        history.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter&&history.SelectedItem is TreeViewItem {Tag:RecipientIssueInfo}){e.Handled=true;Try(OpenSelected);}};
        directory=Store.RecipientDirectory(showDeleted.IsChecked==true);Filter();
    }

    private void Filter()
    {
        var selected=(organizations.SelectedItem as RecipientDirectoryInfo)?.Name;
        var found=directory.Where(x=>SearchText.Matches(x.Name,search.Text)).ToList();
        organizations.ItemsSource=found;count.Text=$"Организаций: {found.Count} из {directory.Count}";
        organizations.SelectedItem=found.FirstOrDefault(x=>x.Name==selected)??found.FirstOrDefault();
        if(found.Count==0)ShowHistory();
    }

    private void ShowHistory()
    {
        history.Items.Clear();open.IsEnabled=false;
        var organization=organizations.SelectedItem as RecipientDirectoryInfo;delete.IsEnabled=organization is {IsDeleted:false};
        if(organization is null){heading.Text="Получатели / организации";summary.Text="Организации не найдены. Измените запрос.";return;}
        heading.Text=organization.Name;
        var issues=Store.RecipientHistory(organization.Name);
        summary.Text=issues.Count==0?"Выдач пока нет. Организация сохранена в справочнике.":
            $"Выдач: {issues.Count} · билетов: {issues.Sum(x=>x.Issue.Count)} · возвращено: {issues.Sum(x=>x.Issue.ReturnedCount)}. Нажмите выдачу, чтобы открыть накладную.";
        var now=DateTime.Now;
        AddPerformances(history,issues.Where(x=>x.StartsAt>=now),false);
        var past=issues.Where(x=>x.StartsAt<now).ToList();
        if(past.Count>0)
        {
            var archive=new TreeViewItem{Header=$"Прошедшие ({past.Select(x=>x.Issue.EventId).Distinct().Count()})",Foreground=Brushes.Gray};
            history.Items.Add(archive);AddPerformances(archive,past,true);
        }
    }

    private void AddPerformances(ItemsControl parent,IEnumerable<RecipientIssueInfo> issues,bool past)
    {
        var groups=issues.GroupBy(x=>x.Title,StringComparer.CurrentCultureIgnoreCase);
        foreach(var performance in (past?groups.OrderByDescending(g=>g.Max(x=>x.StartsAt)):groups.OrderBy(g=>g.Min(x=>x.StartsAt))))
        {
            var node=new TreeViewItem{Header=new TextBlock{Text=performance.Key,FontSize=13,FontWeight=FontWeights.SemiBold},IsExpanded=true};parent.Items.Add(node);
            var events=performance.GroupBy(x=>x.Issue.EventId);
            foreach(var showing in (past?events.OrderByDescending(g=>g.First().StartsAt):events.OrderBy(g=>g.First().StartsAt)))
            {
                var item=showing.First();var date=new TreeViewItem{Header=$"{item.EventDate} · {item.Venue}",IsExpanded=true};node.Items.Add(date);
                foreach(var issue in showing.OrderByDescending(x=>x.Issue.IssuedAt).ThenByDescending(x=>x.Issue.Id))
                {
                    var row=new TreeViewItem{Tag=issue,Header=new TextBlock{Text=$"№ {issue.Issue.Id} · {issue.Issue.Date}\nБилетов: {issue.Issue.Count} · возврат: {issue.Issue.ReturnedCount} · {InvoiceDocuments.Money(issue.Issue.Total)} · {issue.Issue.Status}",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4)}};
                    row.PreviewMouseLeftButtonUp+=(_,e)=>{e.Handled=true;row.IsSelected=true;Try(OpenSelected);};date.Items.Add(row);
                }
            }
        }
    }

    private void OpenSelected()
    {
        if(history.SelectedItem is not TreeViewItem {Tag:RecipientIssueInfo item})return;
        new InvoiceWindow(Store,item.Issue.EventId,item.Issue.Id).ShowDialog();
        directory=Store.RecipientDirectory(showDeleted.IsChecked==true);Filter();
    }
}
