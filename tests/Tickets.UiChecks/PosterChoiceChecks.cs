using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class PosterChoiceChecks
{
    internal static void Run(Store store, MainWindow main)
    {
        var catalog = new RepertoireCatalog(store);
        var first = store.SavePerformance("Афиша — исходный спектакль");
        const string newTitle = "Афиша — выбранный спектакль";
        var second = store.SavePerformance(newTitle);
        var page = catalog.Posters.First().Page;
        store.SaveHomeLayout(JsonSerializer.Serialize(new[] { new HomeCard(page, first), new HomeCard("", second) }));
        Exception? failure = null;
        main.Dispatcher.BeginInvoke(new Action(() =>
        {
            var picker = Application.Current.Windows.OfType<PosterPicker>().Single();
            try
            {
                var form = (StackPanel)((ScrollViewer)picker.Content).Content;
                var local = form.Children.OfType<ComboBox>().Last();
                if (!local.IsEnabled) throw new Exception("Local performance selector is disabled");
                local.IsDropDownOpen = true;
                local.SelectedItem = newTitle;
                local.IsDropDownOpen = false;
                form.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { failure = ex; picker.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        main.EditPoster("Афиша — исходный спектакль", first);
        if (failure != null) throw failure;
        var cards = JsonSerializer.Deserialize<List<HomeCard>>(new Store(store.Path).HomeLayout)!;
        if (cards.Count != 1 || cards[0] != new HomeCard(page, second))
            throw new Exception("Selected performance was not saved");
        Console.WriteLine("PASS change performance in poster picker and reopen saved link");
    }
}
