using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private sealed record JournalColumn(string Header,int Order,double Width,DataGridLengthUnitType Unit,Visibility Visibility);
    private void InitializeJournalLayout()
    {
        var path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(store.Path)!,"journal-columns.json");
        try
        {
            if(System.IO.File.Exists(path))
                foreach(var saved in (JsonSerializer.Deserialize<JournalColumn[]>(System.IO.File.ReadAllText(path))??[]).OrderBy(x=>x.Order))
                {
                    var column=HistoryGrid.Columns.FirstOrDefault(c=>Equals(c.Header?.ToString(),saved.Header));
                    if(column is null||saved.Order<0||saved.Order>=HistoryGrid.Columns.Count||!double.IsFinite(saved.Width)||saved.Width<0||!Enum.IsDefined(saved.Unit)||!Enum.IsDefined(saved.Visibility))continue;
                    column.Width=new DataGridLength(saved.Width,saved.Unit);column.DisplayIndex=saved.Order;column.Visibility=saved.Visibility;
                }
        }
        catch(System.IO.IOException){}catch(JsonException){}catch(ArgumentException){}
        var selector=HistoryGrid.Columns.OfType<DataGridTemplateColumn>().FirstOrDefault();
        if(selector is not null){selector.DisplayIndex=0;selector.Visibility=Visibility.Visible;selector.Width=new DataGridLength(38);selector.MinWidth=38;selector.CanUserReorder=false;}
        Closing+=(_,_)=>{
            try
            {
                var columns=HistoryGrid.Columns.Select(c=>new JournalColumn(c.Header?.ToString()??"",c.DisplayIndex,c.Width.Value,c.Width.UnitType,c.Visibility)).ToArray();
                System.IO.File.WriteAllText(path+".tmp",JsonSerializer.Serialize(columns));System.IO.File.Move(path+".tmp",path,true);
            }
            catch(System.IO.IOException){}catch(UnauthorizedAccessException){}
        };
    }
    private readonly HashSet<long> journalSelection=[];
    private long journalEventId;
    private sealed class JournalIssueRow(IssueInfo issue,bool selected)
    {
        public IssueInfo Issue{get;}=issue;
        public long Id=>Issue.Id;
        public string Date=>Issue.Date;
        public string Recipient=>Issue.Recipient;
        public int Count=>Issue.Count;
        public int ReturnedCount=>Issue.ReturnedCount;
        public decimal Total=>Issue.Total;
        public string Status=>Issue.Status;
        public bool IsSelected{get;set;}=selected;
    }
    private void LoadJournal()
    {
        var eventId=current?.Id??0;
        if(journalEventId!=eventId){journalSelection.Clear();journalEventId=eventId;}
        var issues=eventId==0?[]:store.Issues(eventId);
        journalSelection.IntersectWith(issues.Select(issue=>issue.Id));
        HistoryGrid.ItemsSource=issues.Select(issue=>new JournalIssueRow(issue,journalSelection.Contains(issue.Id))).ToArray();
        UpdateJournalSelection();
    }
    private void JournalChecked(object sender,RoutedEventArgs e)
    {
        if(sender is not CheckBox box||box.Tag is not long id)return;
        if(box.IsChecked==true)journalSelection.Add(id);else journalSelection.Remove(id);
        UpdateJournalSelection();
    }
    private void UpdateJournalSelection()
    {
        MergeInvoicesButton.Visibility=journalSelection.Count>=2?Visibility.Visible:Visibility.Collapsed;
    }
    private void MergeInvoicesClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null||journalSelection.Count<2)return;
        var window=new InvoiceMergeWindow(store,current.Id,journalSelection.ToArray());
        window.ShowDialog();
        if(window.MergedIssueId>0){journalSelection.Clear();LoadEvent(false);OpenInvoice(window.MergedIssueId);}
    });
}
