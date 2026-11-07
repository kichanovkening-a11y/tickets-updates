using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class PosterPicker : EditorWindow
{
    internal string LocalTitle { get; private set; } = "";
    internal string Page { get; private set; } = "";

    internal PosterPicker(Store store, RepertoireCatalog catalog, List<HomeCard> cards, string? fixedTitle, string? page, string title, bool chooseExisting = false)
        : base(store, fixedTitle == null ? "Добавить спектакль" : "Афиша спектакля", 660, 600)
    {
        var form = new StackPanel { Margin = new Thickness(18) };
        Content = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var locals = store.Performances();
        var pages = catalog.Posters.ToList();
        var site = new ComboBox { DisplayMemberPath = "Title" };
        site.ItemsSource = pages.Prepend(new Poster("", "Без афиши сайта", "")).ToArray();
        var local = new ComboBox { IsEditable = !chooseExisting, ItemsSource = locals.Select(p => p.Title).ToArray(), IsReadOnly = fixedTitle != null };
        local.IsEnabled = fixedTitle == null || chooseExisting;
        form.Children.Add(new TextBlock { Text = "Спектакль на VIP-Театре" });
        form.Children.Add(site);
        var preview = new Image { Height = 200, Stretch = Stretch.Uniform, Margin = new Thickness(0,8,0,8) };
        form.Children.Add(preview);
        form.Children.Add(new TextBlock { Text = "Название в программе" });
        form.Children.Add(local);
        site.SelectionChanged += (_, _) =>
        {
            var poster = site.SelectedItem as Poster;
            preview.Source = poster == null ? null : catalog.Photo(poster.Page);
            if (chooseExisting) return;
            if (fixedTitle != null) { local.Text = fixedTitle; return; }
            if (poster == null || poster.Page.Length == 0) return;
            var matches = locals.Where(p => MainWindow.RelatedTitle(p.Title, poster.Title)).ToList();
            var linkedId = cards.FirstOrDefault(c => c.Page == poster.Page)?.PerformanceId;
            var linked = locals.FirstOrDefault(p => p.Id == linkedId);
            local.Text = linked?.Title ?? (matches.Count == 1 ? matches[0].Title : poster.Title);
        };
        var suggestion = pages.FirstOrDefault(p => p.Page == page);
        if (suggestion == null && title.Length > 0)
            suggestion = pages.FirstOrDefault(p => MainWindow.PosterKey(p.Title) == MainWindow.PosterKey(title))
                ?? pages.FirstOrDefault(p => MainWindow.RelatedTitle(p.Title, title));
        if (suggestion != null) site.SelectedItem = suggestion;
        else if (fixedTitle != null) site.SelectedIndex = 0;
        if (chooseExisting) local.SelectedItem = fixedTitle;
        else if (fixedTitle != null) local.Text = fixedTitle;
        Button(form, fixedTitle == null ? "Добавить" : "Сохранить", () => Try(() =>
        {
            var name = chooseExisting ? local.SelectedItem as string ?? "" : fixedTitle ?? local.Text.Trim();
            if (name.Length == 0) throw new TicketException("Выберите или введите название спектакля.");
            LocalTitle = name; Page = (site.SelectedItem as Poster)?.Page ?? ""; Finish(true);
        }));
    }
}
