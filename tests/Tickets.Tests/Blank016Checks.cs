using System.Text.Json;
using Tickets.Core;

internal static class Blank016Checks
{
    internal static void Run(Store store)
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        var blank=TicketBlankEditing.CreateA4From030879();blank.Validate();
        var source=LegacyTicketTemplates.All().Single(b=>b.Name=="030879");
        var sourceJson=JsonSerializer.Serialize(source);
        var fields=blank.Legacy!.Bands.SelectMany(b=>b.Elements).ToArray();
        foreach(var key in new[]{"ticket_series","ticket_serial","event_date","event_time","ticket_row","ticket_seat","ticket_zone","perf_name","theatre_name","theatre_adress"})
            Check(fields.Any(f=>f.Binding==key),"New tear-off blank missing dynamic field: "+key);
        Check(fields.Count(f=>f.Binding=="ticket_series")==2&&fields.Count(f=>f.Binding=="ticket_serial")==2,"Main ticket and tear-off must each bind series and number separately");
        Check(fields.Any(f=>f.Binding.Length==0&&f.Text.Contains("Ряд"))&&fields.Any(f=>f.Binding.Length==0&&f.Text.Contains("Место")),"Static labels must remain separately editable");
        Check(fields.Any(f=>f.Rotation==90&&f.Binding=="ticket_serial"),"Tear-off number retains vertical orientation");
        var hidden=TicketBlankEditing.CloneLayout(blank.Legacy);
        foreach(var field in hidden.Bands.SelectMany(b=>b.Elements))field.Suppressed=true;
        var rejected=false;try{hidden.Validate();}catch(TicketException){rejected=true;}
        Check(rejected,"Legacy layouts must reject hiding every printable field");
        var hiddenPosition=TicketBlankEditing.Clone(blank);hiddenPosition.Positions[0].Legacy=hidden;
        rejected=false;try{hiddenPosition.Validate();}catch(TicketException){rejected=true;}
        Check(rejected,"Blank validation must reject an empty individual ticket layout before rendering");
        var second=TicketBlankEditing.LayoutFor(blank,blank.Positions[1]);
        var secondField=second.Bands.SelectMany(b=>b.Elements).First(f=>f.Binding=="theatre_name");
        var before=secondField.X;secondField.X+=2;secondField.FontFamily="Courier New";secondField.FontSize=11;secondField.Rotation=180;
        Check(TicketBlankEditing.LayoutFor(blank,blank.Positions[0]).Bands.SelectMany(b=>b.Elements).First(f=>f.Binding=="theatre_name").X==before,"Editing one position must not change another");
        var printed=FourUpTicketLayouts.Paginate(blank,5);
        Check(printed.Count==2&&printed[1].Elements.All(p=>p.TicketIndex==4),"New blank prints only real tickets on final sheet");
        Check(printed[0].Elements.Any(p=>p.TicketIndex==1&&p.SourceElement==secondField),"Print renderer must use edited position layout");
        TicketBlankEditing.CopyFirstPlacement(blank);
        Check(TicketBlankEditing.LayoutFor(blank,blank.Positions[1]).Bands.SelectMany(b=>b.Elements).First(f=>f.Binding=="theatre_name").X==before,"Copy first must include legacy geometry");
        TicketBlankEditing.LayoutFor(blank,blank.Positions[2]).Bands.SelectMany(b=>b.Elements).First().Text="Отдельная подпись";
        Check(TicketBlankEditing.LayoutFor(blank,blank.Positions[0]).Bands.SelectMany(b=>b.Elements).First().Text!="Отдельная подпись","Copied position layouts must remain detached");
        var show=store.Events().First(e=>store.Seats(e.Id).Any(seat=>seat.Available));
        var seat=store.Seats(show.Id).First(seat=>seat.Available);
        var invoice=store.TicketPrintPreview(show.Id,[seat.Id],"Blank016 test","БЛ",123);
        var ticket=invoice.Tickets.Single();
        foreach(var binding in new[]{"small_info1","small_info2"})
        {
            var originalField=source.Legacy!.Bands.SelectMany(b=>b.Elements).Single(f=>f.Binding==binding);
            var fragments=fields.Where(f=>f.Name.StartsWith(originalField.Name+"_",StringComparison.Ordinal)).ToArray();
            var readingOrder=(originalField.Rotation==90?fragments.OrderByDescending(f=>f.Y):fragments.OrderBy(f=>f.Y)).ToArray();
            Check(readingOrder.Select(f=>f.Name).SequenceEqual(fragments.Select(f=>f.Name)),"Vertical fragments must retain original reading order: "+binding);
            var actual=string.Concat(readingOrder.Select(f=>f.Binding.Length==0?f.Text:LegacyTicketTemplates.Value(f.Binding,invoice,ticket,"Адрес",DateTime.Today,1)));
            var expected=LegacyTicketTemplates.Value(binding,invoice,ticket,"Адрес",DateTime.Today,1);
            Check(actual==expected,"Physically ordered tear-off text must match original dynamic value: "+binding);
            for(var index=1;index<readingOrder.Length;index++)
                Check(readingOrder[index].Y+readingOrder[index].Height<=readingOrder[index-1].Y+.000001,"Vertical fragments must not overlap: "+binding);
        }
        var serialFragments=fields.Where(f=>f.Rotation==90&&f.Name.StartsWith("smallinfo11_",StringComparison.Ordinal)).OrderByDescending(f=>f.Y).ToArray();
        Check(serialFragments[0].Binding=="ticket_series"&&serialFragments[2].Binding=="ticket_serial","Vertical serial follows series and number sign in physical reading order");
        Check(LegacyTicketTemplates.Value("ticket_series",invoice,ticket,"Адрес",DateTime.Today,1)==ticket.Series,"Series binding uses ticket identity");
        Check(LegacyTicketTemplates.Value("ticket_serial",invoice,ticket,"Адрес",DateTime.Today,1)==ticket.Number.ToString("D6"),"Number binding uses ticket identity");
        Check(LegacyTicketTemplates.Value("ticket_row",invoice,ticket,"Адрес",DateTime.Today,1)==ticket.Row&&LegacyTicketTemplates.Value("ticket_seat",invoice,ticket,"Адрес",DateTime.Today,1)==ticket.Seat,"Row and seat remain separate dynamic values");
        store.SaveTicketBlank(blank);
        var persisted=store.TicketBlanks().Single(b=>b.Id==blank.Id);
        Check(persisted.Positions.All(p=>p.Legacy is not null),"Independent legacy positions must survive persistence");
        store.DeleteTicketBlank(blank.Id);
        Check(JsonSerializer.Serialize(LegacyTicketTemplates.All().Single(b=>b.Name=="030879"))==sourceJson,"New blank must preserve embedded original resource");
        var savedOriginal=store.TicketBlanks().First(b=>b.Legacy is not null);
        var editableOriginal=JsonSerializer.Deserialize<TicketBlank>(JsonSerializer.Serialize(savedOriginal))!;
        TicketBlankEditing.EnsureIndependentLayouts(editableOriginal);
        TicketBlankEditing.LayoutFor(editableOriginal,editableOriginal.Positions[0]).Bands.SelectMany(b=>b.Elements).First().FontFamily="Courier New";
        try
        {
            store.SaveTicketBlank(editableOriginal);
            Check(store.TicketBlanks().Single(b=>b.Id==savedOriginal.Id).Positions[0].Legacy!.Bands.SelectMany(b=>b.Elements).First().FontFamily=="Courier New","Original identity may save customized geometry");
        }
        finally{store.SaveTicketBlank(savedOriginal);}
        Check(TicketBlankEditing.BindingName("theatre_adress")=="Адрес"&&TicketBlankEditing.BindingName("theatre_name")=="Площадка","User-facing binding labels are localized");
    }
}
