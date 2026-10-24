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
            var preview=(HallView)typeof(HallEditorWindow).GetField("preview",Private)!.GetValue(editor)!;
            var draft=(IEnumerable<DraftSeat>)typeof(HallEditorWindow).GetField("seats",Private)!.GetValue(editor)!;
            var initialHallCount=store.Venues().Count;Exception? error=null;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var popup=Application.Current.Windows.OfType<HallSectorWindow023>().Single();
                try
                {
                    var inputs=Logical(popup).OfType<TextBox>().ToArray();inputs[0].Text="Партер022";inputs[1].Text="1";inputs[2].Text="12";
                    Check(!draft.Any(),"first sector remains unmaterialized until explicit acceptance");
                    Logical(popup).OfType<Button>().Single(b=>Equals(b.Content,"Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch(Exception ex){error=ex;popup.Close();}
            }));
            Logical(editor).OfType<Button>().Single(b=>Equals(b.Content,"Добавить сектор")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();if(error is not null)throw error;
            var seats=(List<SeatInfo>)typeof(HallView).GetField("seats",Private)!.GetValue(preview)!;
            Check(seats.Count==12&&seats.Select(s=>s.Number).SequenceEqual(Enumerable.Range(1,12).Select(n=>n.ToString())),"accepted first sector contains twelve correctly numbered places");
            Check(store.Venues().Count==initialHallCount,"draft sector acceptance does not persist a hall before Save");
        }
        finally{editor.Close();Pump();}
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 022 "+message);}
}
