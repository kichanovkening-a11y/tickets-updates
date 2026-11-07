using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tickets.App;
using Tickets.Core;

internal static class Home051Checks
{
    internal static void Run(Store store, MainWindow main)
    {
        var catalog = new RepertoireCatalog(store);
        var sample = """<div class="repertuar vip-repertuar-tiles"><a href="repertuar/show.html" class="item"><div class="foto"><img src="/assets/cover.jpg" alt="Ёлка &amp; друзья"></div></a></div><style></style><a href="repertuar/archive.html"><img src="/assets/old.jpg" alt="Архив"></a>""";
        var item = RepertoireCatalog.Parse(sample).Single();
        if (item.Page != "/repertuar/show.html" || item.Title != "Ёлка & друзья")
            throw new Exception("Active repertoire parsing failed");
        var tiles = (UniformGrid)main.FindName("HomeTiles");
        if (tiles.Columns != 5 || tiles.Children.Count == 0) throw new Exception("Home posters missing");
        var tile = (Border)tiles.Children[0];
        var card = (HomeCard)tile.Tag;
        if (catalog.Photo(card.Page) == null) throw new Exception("Offline poster missing");
        var shows = store.Events().Select(e=>e.Id).ToArray();
        var before = tiles.Children.Count;
        var hide = (MenuItem)tile.ContextMenu.Items[1];
        hide.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (tiles.Children.Count != before - 1 || !store.Events().Select(e=>e.Id).SequenceEqual(shows))
            throw new Exception("Hiding a home card must leave the shows intact");
        var persisted = System.Text.Json.JsonSerializer.Deserialize<List<HomeCard>>(new Store(store.Path).HomeLayout)!;
        if (!persisted.Single(c => c.Page == card.Page).Hidden) throw new Exception("Card visibility was not persisted");
        var retired = store.SavePerformance("Проверка освобождения афиши 0511");
        store.SaveHomeLayout(System.Text.Json.JsonSerializer.Serialize(new[] { new HomeCard(card.Page, retired), new HomeCard("/keep", 0, true) }));
        store.DeletePerformance(retired);
        var remaining = System.Text.Json.JsonSerializer.Deserialize<List<HomeCard>>(new Store(store.Path).HomeLayout)!;
        if (!remaining.SequenceEqual(new[] { new HomeCard("/keep", 0, true) }))
            throw new Exception("Deleted performance still owns its poster or unrelated cards changed");
        Console.WriteLine("PASS home grid, offline artwork, repertoire parsing and safe persistent hiding");
    }
}
