using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Threading;
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
        catch(System.IO.IOException){}catch(UnauthorizedAccessException){}catch(JsonException){}catch(ArgumentException){}
        foreach(var column in HistoryGrid.Columns.Where(c=>c.Header?.ToString() is "Возвращено" or "Состояние"))column.Visibility=Visibility.Collapsed;
        var selector=HistoryGrid.Columns.OfType<DataGridTemplateColumn>().FirstOrDefault();
        if(selector is not null){selector.DisplayIndex=0;selector.Visibility=Visibility.Visible;selector.Width=new DataGridLength(38);selector.MinWidth=38;selector.CanUserReorder=false;}
        // Column sizes belong to the workstation, not to the currently displayed show.
        // Persist actual user changes during the session, so reopening a window does
        // not restore stale widths from before the last application shutdown.
        var pendingSave=new DispatcherTimer(DispatcherPriority.Background,Dispatcher){Interval=TimeSpan.FromMilliseconds(250)};
        void Save()
        {
            pendingSave.Stop();
            try
            {
                var columns=HistoryGrid.Columns.Select(c=>new JournalColumn(c.Header?.ToString()??"",c.DisplayIndex,c.Width.Value,c.Width.UnitType,c.Visibility)).ToArray();
                System.IO.File.WriteAllText(path+".tmp",JsonSerializer.Serialize(columns));System.IO.File.Move(path+".tmp",path,true);
            }
            catch(System.IO.IOException){}catch(UnauthorizedAccessException){}
        }
        void Changed(object? sender,EventArgs args){pendingSave.Stop();pendingSave.Start();}
        pendingSave.Tick+=(_,_)=>Save();
        var width=DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty,typeof(DataGridColumn));
        var visibility=DependencyPropertyDescriptor.FromProperty(DataGridColumn.VisibilityProperty,typeof(DataGridColumn));
        foreach(var column in HistoryGrid.Columns){width.AddValueChanged(column,Changed);visibility.AddValueChanged(column,Changed);}
        HistoryGrid.ColumnDisplayIndexChanged+=(_,_)=>Changed(null,EventArgs.Empty);
        Closing+=(_,_)=>Save();
        Closed+=(_,_)=>{
            pendingSave.Stop();
            foreach(var column in HistoryGrid.Columns){width.RemoveValueChanged(column,Changed);visibility.RemoveValueChanged(column,Changed);}
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
        public string SenderKind=>Regex.Match(CatalogText.Key(Issue.Sender),@"^(ООО|ИП)(?=$|[^\p{L}\p{Nd}])").Value;
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
    private void ClearJournalSelection()
    {
        journalSelection.Clear();
        foreach(var row in HistoryGrid.Items.OfType<JournalIssueRow>())row.IsSelected=false;
        HistoryGrid.Items.Refresh();UpdateJournalSelection();
    }
    private void JournalChecked(object sender,RoutedEventArgs e)
    {
        if(sender is not CheckBox box||box.Tag is not long id)return;
        if(box.IsChecked==true)journalSelection.Add(id);else journalSelection.Remove(id);
        UpdateJournalSelection();
    }
    private void UpdateJournalSelection()
    {
        JournalMergeActions.Visibility=journalSelection.Count>=2?Visibility.Visible:Visibility.Collapsed;
        MergeInvoicesButton.Visibility=JournalMergeActions.Visibility;
        JournalInstructions.Visibility=journalSelection.Count>=2?Visibility.Collapsed:Visibility.Visible;
        var selected=SelectedJournalInvoices(out var error);
        MergeInvoicesButton.IsEnabled=journalSelection.Count>=2&&error.Length==0;
        MergeInvoicesButton.Content="Объединить накладные";
        JournalSelectionStatus.Text=journalSelection.Count<2?"":error.Length>0?error:$"Выбрано: {selected.Length}. Будет создана новая накладная со следующим общим номером.";
    }
    private IssueInfo[] SelectedJournalInvoices(out string error)
    {
        error="";
        if(current is null||journalSelection.Count<2)return [];
        // Keep the source header choice stable across checkbox and sort order.
        var selected=store.Issues(current.Id).Where(issue=>journalSelection.Contains(issue.Id)).OrderBy(issue=>issue.Id).ToArray();
        if(selected.Length!=journalSelection.Count)error="Выберите накладные одного мероприятия. Обновите журнал.";
        else if(selected.Select(issue=>CatalogText.Key(issue.Recipient)).Distinct().Count()>1)error="Объединение: выберите одного получателя.";
        else if(selected.Select(issue=>CatalogText.Key(issue.Sender)).Distinct().Count()>1)error="Объединение: выберите одного отправителя.";
        else if(selected.Any(issue=>issue.ReservedCount>0))error="Сначала подтвердите брони в выбранных накладных.";
        else if(selected.Any(issue=>issue.Count<=issue.ReturnedCount))error="Выберите накладные с билетами на руках.";
        return selected;
    }
    private void MergeInvoicesClick(object sender,RoutedEventArgs e)
    {
        if(current is null||journalSelection.Count<2)return;
        try
        {
            var selected=SelectedJournalInvoices(out var error);
            if(error.Length>0){UpdateJournalSelection();return;}
            var target=selected[0];
            var message=$"Объединить накладные {string.Join(", ",selected.Select(issue=>"№ "+issue.Id))} в новую накладную?\nПолучатель: {target.Recipient}\nОтправитель: {target.Sender}\nБудет присвоен следующий общий номер. Дата и реквизиты взяты из № {target.Id}. Исходные накладные и возвраты останутся в истории.";
            if(MessageBox.Show(this,message,"Объединение накладных",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var eventId=current.Id;
            var mergedId=store.MergeInvoices(eventId,selected.Select(issue=>issue.Id).ToArray());
            string? documentError=null;
            try{store.SaveAllDocuments(eventId,mergedId);}catch(Exception ex){documentError=ex.Message;}
            journalSelection.Clear();LoadEvent(false);
            JournalSelectionStatus.Text=documentError is null?$"Накладные объединены в № {mergedId}. Документы обновлены.":$"Накладные объединены в № {mergedId}. Не удалось обновить документы: {documentError}";
        }
        catch(Exception ex){JournalSelectionStatus.Text=ex.Message;}
    }
}
