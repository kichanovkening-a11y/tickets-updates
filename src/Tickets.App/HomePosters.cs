using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private readonly RepertoireCatalog repertoire;
    private bool updatingPosters;
    private DateTime nextPosterRefresh;

    internal static string PosterKey(string title) => string.Concat(title.ToUpperInvariant().Replace('Ё','Е').Where(char.IsLetterOrDigit));

    internal static bool RelatedTitle(string left, string right)
    {
        var a = PosterKey(left);
        var b = PosterKey(right);
        return a.Length >= 3 && b.Length >= 3 && (a.Contains(b) || b.Contains(a));
    }

    private List<HomeCard> Cards()
    {
        var saved = store.HomeLayout;
        var locals = store.Performances();
        if (saved.Length > 0)
        {
            var cards = JsonSerializer.Deserialize<List<HomeCard>>(saved)!;
            var activeIds = locals.Select(p => p.Id).ToHashSet();
            if (cards.RemoveAll(c => c.Hidden || (c.PerformanceId != 0 && !activeIds.Contains(c.PerformanceId))) > 0)
                store.SaveHomeLayout(JsonSerializer.Serialize(cards));
            return cards;
        }
        return repertoire.Posters.Select(p =>
        {
            var exact = locals.Where(l => PosterKey(l.Title) == PosterKey(p.Title)).ToList();
            var matches = exact.Count > 0 ? exact : locals.Where(l => RelatedTitle(l.Title, p.Title)).ToList();
            return new HomeCard(p.Page, matches.Count == 1 ? matches[0].Id : 0);
        }).ToList();
    }

    private void SaveCards(List<HomeCard> cards)
    {
        store.SaveHomeLayout(JsonSerializer.Serialize(cards));
        DrawCards();
    }

    private async void RefreshPosters()
    {
        if (updatingPosters || DateTime.UtcNow < nextPosterRefresh) return;
        updatingPosters = true;
        try
        {
            await repertoire.RefreshAsync();
            if (IsLoaded && HomePanel.IsVisible) Run(DrawCards);
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        finally { updatingPosters = false; nextPosterRefresh = DateTime.UtcNow.AddMinutes(15); }
    }

    private void DrawCards()
    {
        HomeTiles.Children.Clear();
        var sites = repertoire.Posters.ToDictionary(p => p.Page);
        var locals = store.Performances().ToDictionary(p => p.Id);
        foreach (var card in Cards().Where(c => !c.Hidden))
        {
            locals.TryGetValue(card.PerformanceId, out var local);
            sites.TryGetValue(card.Page, out var poster);
            if ((card.PerformanceId != 0 && local == null) || (card.Page.Length > 0 && poster == null)) continue;
            var title = local?.Title ?? poster?.Title;
            if (title == null) continue;
            var photo = new Image { Source = repertoire.Photo(card.Page), Stretch = Stretch.UniformToFill };
            var picture = new Border { Background = Brushes.WhiteSmoke, Child = photo, ClipToBounds = true };
            var name = new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5) };
            var body = new DockPanel();
            var caption = new Border { Child = name, MinHeight = 44 };
            DockPanel.SetDock(caption, Dock.Bottom);
            body.Children.Add(caption);
            body.Children.Add(picture);
            var tile = new Border { Child = body, Tag = card, Margin = new Thickness(6), CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1), BorderBrush = Brushes.Gainsboro, Background = Brushes.White, AllowDrop = true, Cursor = Cursors.Hand };
            picture.SizeChanged += (_, _) => picture.Clip = new RectangleGeometry(new Rect(picture.RenderSize), 8, 8);
            tile.SizeChanged += (_, _) => picture.Height = Math.Max(1, (tile.ActualWidth - 2) * .75);
            Point? start = null;
            tile.MouseLeftButtonDown += (_, e) => { start = e.GetPosition(tile); tile.CaptureMouse(); e.Handled = true; };
            tile.LostMouseCapture += (_, _) => start = null;
            tile.MouseMove += (_, e) =>
            {
                if (start is not Point origin || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(tile) - origin;
                if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                start = null; tile.ReleaseMouseCapture();
                DragDrop.DoDragDrop(tile, new DataObject(typeof(HomeCard), card), DragDropEffects.Move);
            };
            tile.MouseLeftButtonUp += (_, e) =>
            {
                if (start == null) return;
                tile.ReleaseMouseCapture(); e.Handled = true;
                if (tile.IsMouseOver) Run(() => ShowCardDates(tile, local));
            };
            tile.Drop += (_, e) =>
            {
                if (e.Data.GetData(typeof(HomeCard)) is not HomeCard source || source == card) return;
                Run(() =>
                {
                    var cards = Cards(); var index = cards.IndexOf(card);
                    if (index < 0 || !cards.Remove(source)) return;
                    cards.Insert(index, source); SaveCards(cards);
                });
                e.Handled = true;
            };
            var menu = new ContextMenu();
            var add = new MenuItem { Header = "Добавить дату" };
            add.Click += (_, _) => Run(() =>
            {
                var id = local?.Id ?? BindCard(card, title);
                if (id == 0) return;
                var editor = new EventEditorWindow(store, id);
                if (editor.ShowDialog() == true) { ReadEventCatalog(); SearchBox.Clear(); ApplyFilter(); SelectEvent(editor.CreatedId); }
            });
            var remove = new MenuItem { Header = "Удалить с главного экрана" };
            remove.Click += (_, _) => Run(() =>
            {
                var cards = Cards();
                if (cards.Remove(card)) SaveCards(cards);
            });
            var link = new MenuItem { Header = local != null && card.Page.Length > 0 ? "Заменить связку" : "Связать" };
            link.Click += (_, _) => Run(() =>
            {
                if (local == null) BindCard(card, title);
                else EditPoster(local.Title, local.Id);
            });
            menu.Items.Add(add);
            menu.Items.Add(link);
            menu.Items.Add(remove);
            tile.ContextMenu = menu;
            HomeTiles.Children.Add(tile);
        }
    }

    private void ShowCardDates(Border tile, PerformanceInfo? local)
    {
        var menu = new ContextMenu { PlacementTarget = tile, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var shows = store.Events().Where(e => local != null && CatalogText.Key(e.Title) == CatalogText.Key(local.Title)
            && DateTime.Parse(e.SortDate) >= DateTime.Now).OrderBy(e => e.SortDate);
        foreach (var show in shows)
        {
            var row = new MenuItem { Header = show.Date + " · " + show.Venue };
            row.Click += (_, _) => Run(() => { ReadEventCatalog(); SearchBox.Clear(); ApplyFilter(); SelectEvent(show.Id); });
            menu.Items.Add(row);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "Нет активных дат", IsEnabled = false });
        menu.IsOpen = true;
    }

    private long BindCard(HomeCard card, string title)
    {
        var picker = new PosterPicker(store, repertoire, Cards(), null, card.Page, title);
        if (picker.ShowDialog() != true) return 0;
        return SavePicked(picker, card);
    }

    internal void EditPoster(string title, long id)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new TicketException("Укажите название спектакля.");
        var linked = Cards().FirstOrDefault(c => id != 0 && c.PerformanceId == id);
        var picker = new PosterPicker(store, repertoire, Cards(), title, linked?.Page, title, chooseExisting: id != 0);
        if (picker.ShowDialog() == true) SavePicked(picker, linked);
    }

    private long SavePicked(PosterPicker picker, HomeCard? origin = null)
    {
        var cards = Cards();
        var local = store.Performances().FirstOrDefault(p => CatalogText.Key(p.Title) == CatalogText.Key(picker.LocalTitle));
        var page = picker.Page;
        var existing = local == null ? -1 : cards.FindIndex(c => c.PerformanceId == local.Id);
        var original = origin == null ? -1 : cards.IndexOf(origin);
        var other = cards.FindIndex(c => page.Length > 0 && c.Page == page);
        if (other >= 0 && other != original && !cards[other].Hidden && cards[other].PerformanceId != 0 && cards[other].PerformanceId != local?.Id)
            throw new TicketException("Эта афиша уже привязана к другому спектаклю.");
        var id = local?.Id ?? store.SavePerformance(picker.LocalTitle);
        var replacement = new HomeCard(page, id);
        var index = original >= 0 ? original : existing >= 0 ? existing : other;
        if (index < 0) cards.Add(replacement);
        else
        {
            cards[index] = replacement;
            foreach (var duplicate in new[] { existing, other }.Distinct().OrderDescending())
                if (duplicate >= 0 && duplicate != index) cards.RemoveAt(duplicate);
        }
        SaveCards(cards);
        return id;
    }

    private void AddHomePoster(object sender, RoutedEventArgs e) => Run(() =>
    {
        var picker = new PosterPicker(store, repertoire, Cards(), null, null, "");
        if (picker.ShowDialog() == true) SavePicked(picker);
    });
}
