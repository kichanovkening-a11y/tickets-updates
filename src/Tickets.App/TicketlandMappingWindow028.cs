using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketlandMappingWindow028:EditorWindow
{
    private sealed record SourceChoice(TicketlandShow028 Show)
    {public string Caption=>$"{Show.Title} · {Show.StartsAt:dd.MM.yyyy HH:mm} · билетов: {Show.Tickets}";}
    private sealed record LocalChoice(Tickets.Core.EventInfo Show)
    {public string Caption=>$"{Show.Title} · {Show.Date} · {Show.Venue}";}

    internal TicketlandMappingWindow028(Store store,MainWindow main):base(store,"Сопоставление Ticketland",900,650)
    {
        var form=new StackPanel{Margin=new Thickness(20)};Content=form;
        var source=Combo(form,"Показ Ticketland",store.TicketlandShows028().Select(s=>new SourceChoice(s)).ToArray(),"Caption");
        var local=Combo(form,"Показ в программе",Array.Empty<LocalChoice>(),"Caption");
        var sourceSector=Combo(form,"Сектор Ticketland",Array.Empty<string>(),"");
        var localSector=Combo(form,"Сектор в программе",Array.Empty<string>(),"");
        var status=new TextBlock{Text="Выберите соответствующий показ. Если названия секторов отличаются, укажите их соответствие. Ряды и места сопоставляются по своим номерам.",TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,12,0,0)};form.Children.Add(status);
        void LoadLocal()
        {
            if(source.SelectedItem is not SourceChoice choice)return;
            var available=store.Events().Where(e=>DateTime.TryParse(e.SortDate,CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)&&at==choice.Show.StartsAt).Select(e=>new LocalChoice(e)).ToArray();
            localSector.ItemsSource=Array.Empty<string>();
            local.ItemsSource=available;local.SelectedItem=available.FirstOrDefault(e=>e.Show.Id==choice.Show.EventId);
            sourceSector.ItemsSource=store.TicketlandSectors028(choice.Show.Title,choice.Show.StartsAt);sourceSector.SelectedIndex=-1;
        }
        source.SelectionChanged+=(_,_)=>LoadLocal();
        local.SelectionChanged+=(_,_)=>{
            localSector.ItemsSource=local.SelectedItem is LocalChoice choice?store.Sectors(choice.Show.VenueId).Select(s=>s.Name).ToArray():Array.Empty<string>();localSector.SelectedIndex=-1;
        };
        sourceSector.SelectionChanged+=(_,_)=>{
            if(sourceSector.SelectedItem is string name)localSector.SelectedItem=localSector.Items.Cast<string>().FirstOrDefault(s=>CatalogText.Key(s)==CatalogText.Key(name));
        };
        Button(form,"Сохранить",()=>Try(()=>{
            if(main.TicketlandIsUpdating028)throw new TicketException("Дождитесь окончания обновления Ticketland.");
            if(source.SelectedItem is not SourceChoice remote||local.SelectedItem is not LocalChoice target)throw new TicketException("Выберите показ Ticketland и соответствующий показ в программе с той же датой и временем.");
            if(sourceSector.SelectedItem is not null&&localSector.SelectedItem is null)throw new TicketException("Выберите соответствующий сектор в программе.");
            store.BindTicketlandShow028(remote.Show.Title,remote.Show.StartsAt,target.Show.Id);
            if(sourceSector.SelectedItem is string from&&localSector.SelectedItem is string to)store.BindTicketlandSector028(remote.Show.Title,remote.Show.StartsAt,from,target.Show.Id,to);
            main.ReapplyTicketlandMappings028();status.Text=main.TicketlandStatus028;
        }));
        foreach(var label in form.Children.OfType<TextBlock>())label.TextAlignment=TextAlignment.Center;
        LoadLocal();
    }
}
