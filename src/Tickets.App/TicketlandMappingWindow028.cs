using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketlandMappingWindow028:EditorWindow
{
    internal TicketlandMappingWindow028(Store store,MainWindow main):base(store,"Расхождения Ticketland",900,650)
    {
        var form=new DockPanel{Margin=new Thickness(20)};Content=form;
        var instructions=new TextBlock{Text="Проверьте отличия и подтвердите предложенное соответствие или откажитесь от него.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(instructions,Dock.Top);form.Children.Add(instructions);
        var commands=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        DockPanel.SetDock(commands,Dock.Bottom);form.Children.Add(commands);
        var agree=Button(commands,"Согласиться",()=>{});var reject=Button(commands,"Отказаться",()=>{});
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
        DockPanel.SetDock(status,Dock.Bottom);form.Children.Add(status);
        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3,GridUnitType.Star)});form.Children.Add(body);
        var issues=new ListBox{Name="TicketlandIssues029",DisplayMemberPath="Source",Margin=new Thickness(0,0,12,0),HorizontalContentAlignment=HorizontalAlignment.Stretch};body.Children.Add(issues);
        var detail=new DockPanel();Grid.SetColumn(detail,1);body.Children.Add(detail);
        var reason=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};DockPanel.SetDock(reason,Dock.Top);detail.Children.Add(reason);
        var differences=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(differences,Dock.Bottom);detail.Children.Add(differences);
        var candidates=new ListBox{Name="TicketlandCandidates029",DisplayMemberPath="Caption",HorizontalContentAlignment=HorizontalAlignment.Stretch};detail.Children.Add(candidates);
        void UpdateCommands(){agree.IsEnabled=!main.TicketlandIsUpdating028&&issues.SelectedItem is TicketlandIssue029&&candidates.SelectedItem is TicketlandCandidate029;reject.IsEnabled=!main.TicketlandIsUpdating028&&issues.SelectedItem is TicketlandIssue029;}
        void Reload()
        {
            var selected=issues.SelectedIndex;var pending=store.TicketlandIssues029();issues.ItemsSource=pending;
            if(pending.Count>0)issues.SelectedIndex=Math.Clamp(selected,0,pending.Count-1);
            else{reason.Text="Расхождений нет.";candidates.ItemsSource=null;differences.Text="";}
            status.Text=pending.Count>0?$"Расхождений: {pending.Count}":"Все расхождения рассмотрены.";UpdateCommands();
        }
        issues.SelectionChanged+=(_,_)=>{
            if(issues.SelectedItem is TicketlandIssue029 issue){reason.Text=issue.Source+"\n"+issue.Reason;candidates.ItemsSource=issue.Candidates;candidates.SelectedIndex=issue.Candidates.Count==1?0:-1;}
            else{reason.Text="";candidates.ItemsSource=null;}
            differences.Text=(candidates.SelectedItem as TicketlandCandidate029)?.Differences??"";UpdateCommands();
        };
        candidates.SelectionChanged+=(_,_)=>{differences.Text=(candidates.SelectedItem as TicketlandCandidate029)?.Differences??"";UpdateCommands();};
        agree.Click+=(_,_)=>Try(()=>{
            if(main.TicketlandIsUpdating028)throw new TicketException("Дождитесь окончания обновления Ticketland.");
            if(issues.SelectedItem is not TicketlandIssue029 issue||candidates.SelectedItem is not TicketlandCandidate029 candidate)return;
            store.AcceptTicketlandIssue029(issue.Key,candidate.EventId,candidate.SeatId);main.ReapplyTicketlandMappings028();Reload();
        });
        reject.Click+=(_,_)=>Try(()=>{
            if(main.TicketlandIsUpdating028)throw new TicketException("Дождитесь окончания обновления Ticketland.");
            if(issues.SelectedItem is not TicketlandIssue029 issue)return;
            store.RejectTicketlandIssue029(issue.Key);Reload();
        });
        void StateChanged(){if(main.TicketlandIsUpdating028)UpdateCommands();else Reload();}
        main.TicketlandStatusChanged028+=StateChanged;Closed+=(_,_)=>main.TicketlandStatusChanged028-=StateChanged;
        Reload();
    }
}
