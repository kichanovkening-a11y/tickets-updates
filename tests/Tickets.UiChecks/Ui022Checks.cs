using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Ui022Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var main=new MainWindow(store);main.Show();Pump();
        try
        {
            var grid=(DataGrid)main.FindName("HistoryGrid");
            Check(grid.Columns.Any(c=>Equals(c.Header,"№"))&&!grid.Columns.Any(c=>Equals(c.Header,"Накладная №")),"journal shows compact number heading");
            Check(grid.GridLinesVisibility==DataGridGridLinesVisibility.All&&grid.VerticalGridLinesBrush is SolidColorBrush,"journal retains vertical and horizontal cell boundaries");
            var recipients=(ComboBox)main.FindName("RecipientBox");
            Check(recipients.FontSize==17&&recipients.FontWeight==FontWeights.Bold&&recipients.MinHeight==34,"recipient typography is larger and bold without changing input bounds");
            Check(recipients.ItemContainerStyle.Setters.OfType<Setter>().Any(s=>s.Property==Control.HeightProperty&&Equals(s.Value,24d)),"recipient list rows stay compact");
            var venue=store.CreateHall("UI022 цены","",new[]{new DraftSeat()});var seat=store.VenueSeats(venue).Single();
            var plan=store.SavePlan(venue,"Цена022",new[]{new PriceValue(seat.Id,1000,"#123456")});
            var pricing=new PriceEditorWindow(store,venue,plan){IsInline=true};pricing.NotifyShown();
            try
            {
                var total=(TextBlock)typeof(PriceEditorWindow).GetField("hallTotals",Private)!.GetValue(pricing)!;
                Check(total.FontSize==18&&total.FontWeight==FontWeights.Bold&&total.TextWrapping==TextWrapping.NoWrap,"pricing totals remain one larger bold line");
                var red=total.Inlines.OfType<Run>().Where(r=>r.Foreground==Brushes.Red).ToArray();

                Check(red.Length==3&&red.All(r=>r.Text.Any(char.IsDigit)),"only count amount and blocked values are highlighted red");
            }
            finally{pricing.Close();}
        }
        finally{main.Close();Pump();}
        DeletedJournalCache(store);
        FirstRow(store);
    }
    private static void DeletedJournalCache(Store store)
    {
        var venue=store.CreateHall("UI022 удаление журнала","",new[]{new DraftSeat()});var seat=store.VenueSeats(venue).Single();
        var plan=store.SavePlan(venue,"План журнала022",new[]{new PriceValue(seat.Id,1000,"#123456")});
        var performance=store.SavePerformance("Удаление открытого журнала022");var showing=store.CreateEvent(performance,venue,plan,new DateTime(2096,1,1));
        var main=new MainWindow(store);main.Show();Pump();
        try
        {
            main.SelectEvent(showing);Pump();
            var save=(Action)typeof(MainWindow).GetField("saveJournalLayout",Private)!.GetValue(main)!;
            save();store.DeletePerformance(performance);save();
            var path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(store.Path)!,"journal-columns-by-event.json");
            using var json=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            Check(!json.RootElement.TryGetProperty(showing.ToString(),out _),"an open journal cannot restore deleted show layout from its cached dictionary");
        }
        finally{main.Close();Pump();}
    }
    private static void FirstRow(Store store)
    {
        var editor=new HallEditorWindow(store);editor.Show();Pump();
        try
        {
            var rows=(DataGrid)typeof(HallEditorWindow).GetField("rowTable",Private)!.GetValue(editor)!;
            var preview=(HallView)typeof(HallEditorWindow).GetField("preview",Private)!.GetValue(editor)!;
            rows.SelectedItem=rows.Items[0];rows.CurrentCell=new DataGridCellInfo(rows.Items[0],rows.Columns[1]);rows.ScrollIntoView(rows.Items[0],rows.Columns[1]);rows.Focus();
            Check(rows.BeginEdit(),"first row starts editing in an empty hall");Pump();
            var edit=(TextBox)rows.Columns[1].GetCellContent(rows.Items[0]);edit.Text="12";edit.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            // The view owns EditItem here. Calling a refresh from a focus/commit callback must not terminate the app.
            typeof(HallEditorWindow).GetMethod("RefreshRows",Private)!.Invoke(editor,null);Pump();
            Check(rows.CommitEdit(DataGridEditingUnit.Cell,true)&&rows.CommitEdit(DataGridEditingUnit.Row,true),"first-row cell and row commit after a refresh request");Pump();
            var seats=(List<SeatInfo>)typeof(HallView).GetField("seats",Private)!.GetValue(preview)!;
            Check(seats.Count==12&&seats.Select(s=>s.Number).SequenceEqual(Enumerable.Range(1,12).Select(n=>n.ToString())),"first row preview contains twelve correctly numbered places");
            Check(((System.Collections.ICollection)typeof(HallEditorWindow).GetField("seats",Private)!.GetValue(editor)!).Count==0,"editing first row does not persist seats before confirmation");
        }
        finally{editor.Close();Pump();}
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 022 "+message);}
}
