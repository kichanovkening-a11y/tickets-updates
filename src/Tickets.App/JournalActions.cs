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
        Closing+=(_,_)=>{
            try
            {
                var columns=HistoryGrid.Columns.Select(c=>new JournalColumn(c.Header?.ToString()??"",c.DisplayIndex,c.Width.Value,c.Width.UnitType,c.Visibility)).ToArray();
                System.IO.File.WriteAllText(path+".tmp",JsonSerializer.Serialize(columns));System.IO.File.Move(path+".tmp",path,true);
            }
            catch(System.IO.IOException){}catch(UnauthorizedAccessException){}
        };
    }
    private void MergeInvoicesClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;
        var window=new InvoiceMergeWindow(store,current.Id);
        window.ShowDialog();
        if(window.MergedIssueId>0){LoadEvent(false);OpenInvoice(window.MergedIssueId);}
    });
}
