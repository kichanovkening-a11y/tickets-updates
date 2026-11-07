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
        var performance = store.SavePerformance("Проверка карточки 0511");
        var linkedCard = new HomeCard(catalog.Posters[0].Page, performance);
        var blankCard = new HomeCard("", store.SavePerformance("Проверка пустой карточки 0511"));
        store.SaveHomeLayout(System.Text.Json.JsonSerializer.Serialize(new[] { linkedCard, blankCard }));
        var draw = typeof(MainWindow).GetMethod("DrawCards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        draw.Invoke(main, null);
        var tile = tiles.Children.OfType<Border>().Single(b => Equals(b.Tag, linkedCard));
        var blank = tiles.Children.OfType<Border>().Single(b => Equals(b.Tag, blankCard));
        if (!blank.ContextMenu.Items.OfType<MenuItem>().Any(m => Equals(m.Header, "Связать")))
            throw new Exception("Blank poster must offer site linking");
        var card = (HomeCard)tile.Tag;
        if (catalog.Photo(card.Page) == null) throw new Exception("Offline poster missing");
        var shows = store.Events().Select(e=>e.Id).ToArray();
        var before = tiles.Children.Count;
        var commands = tile.ContextMenu.Items.Cast<MenuItem>().ToArray();
        var linkCaption = card.PerformanceId != 0 && card.Page.Length > 0 ? "Заменить связку" : "Связать";
        if (!commands.Select(m => m.Header?.ToString()).SequenceEqual(new[] { "Добавить дату", linkCaption, "Удалить с главного экрана" }))
            throw new Exception("Wrong poster context menu");
        var hide = commands[2];
        hide.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (tiles.Children.Count != before - 1 || !store.Events().Select(e=>e.Id).SequenceEqual(shows))
            throw new Exception("Hiding a home card must leave the shows intact");
        var persisted = System.Text.Json.JsonSerializer.Deserialize<List<HomeCard>>(new Store(store.Path).HomeLayout)!;
        if (persisted.Any(c => c.Page == card.Page) || !store.Performances().Any(p => p.Id == card.PerformanceId))
            throw new Exception("Removing the card must release its poster and preserve its performance");
        var retired = store.SavePerformance("Проверка освобождения афиши 0511");
        store.SaveHomeLayout(System.Text.Json.JsonSerializer.Serialize(new[] { new HomeCard(card.Page, retired), new HomeCard("/keep", 0, true) }));
        store.DeletePerformance(retired);
        var remaining = System.Text.Json.JsonSerializer.Deserialize<List<HomeCard>>(new Store(store.Path).HomeLayout)!;
        if (!remaining.SequenceEqual(new[] { new HomeCard("/keep", 0, true) }))
            throw new Exception("Deleted performance still owns its poster or unrelated cards changed");
        Console.WriteLine("PASS home grid, offline artwork, repertoire parsing and safe persistent hiding");
    }
}
