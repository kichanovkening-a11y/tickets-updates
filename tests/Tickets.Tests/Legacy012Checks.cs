using System.Security.Cryptography;
using System.Text.Json;
using Tickets.Core;

internal static class Legacy012Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("all eleven original Crystal templates retain their recorded source hashes",_=>
        {
            DirectoryInfo? directory=null;
            foreach(var start in new[]{AppContext.BaseDirectory,Directory.GetCurrentDirectory()})
            {
                var candidate=new DirectoryInfo(start);
                while(candidate is not null&&!File.Exists(System.IO.Path.Combine(candidate.FullName,"legacy-ticket-templates","manifest.json")))candidate=candidate.Parent;
                if(candidate is not null){directory=candidate;break;}
            }
            if(directory is null)throw new Exception("Original template catalog not found above test output or current repository directory.");
            var root=System.IO.Path.Combine(directory.FullName,"legacy-ticket-templates");
            using var manifest=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(root,"manifest.json")));
            var templates=manifest.RootElement.GetProperty("templates").EnumerateArray().ToArray();
            if(templates.Length!=11||Directory.GetFiles(System.IO.Path.Combine(root,"Template"),"*.rpt").Length!=11)throw new Exception("Expected eleven original RPTs.");
            foreach(var template in templates)
            {
                var name=template.GetProperty("file").GetString()!;
                var bytes=File.ReadAllBytes(System.IO.Path.Combine(root,"Template",name));
                if(bytes.Length!=template.GetProperty("bytes").GetInt32())throw new Exception(name+": length changed.");
                if(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=template.GetProperty("sha256").GetString())throw new Exception(name+": source hash changed.");
                if(!bytes.AsSpan(0,8).SequenceEqual(new byte[]{0xd0,0xcf,0x11,0xe0,0xa1,0xb1,0x1a,0xe1}))throw new Exception(name+": expected original OLE report.");
                if(template.GetProperty("status").GetString()!="portable-layout-adapted")throw new Exception(name+": expected SDK-backed portable adaptation.");
            }
        });
        test("portable old ticket layouts preserve SDK coordinates fonts rotations and labels",_=>
        {
            var blanks=LegacyTicketTemplates.All();
            if(blanks.Count!=11)throw new Exception("Eleven embedded source layouts required.");
            foreach(var blank in blanks){blank.Validate();if(blank.Legacy is null||blank.Fields.Count!=0||blank.Positions.Count!=0)throw new Exception("Legacy layout must not be forced into eight generic fields.");}
            var standard=blanks.Single(b=>b.Name=="Стандарт1");var band=standard.Legacy!.Bands.Single(b=>b.Kind=="Detail");
            var title=band.Elements.Single(e=>e.Binding=="perf_name");
            static void Near(double actual,double expected){if(Math.Abs(actual-expected)>0.000001)throw new Exception($"Geometry mismatch {actual} vs {expected}");}
            Near(title.X,3000*25.4/1440);Near(title.Y,1200*25.4/1440);Near(title.Width,7887*25.4/1440);Near(title.Height,765*25.4/1440);
            if(title.FontFamily!="Times New Roman"||title.FontSize!=15||!title.Bold)throw new Exception("Source font not preserved.");
            Near(standard.Legacy.LeftMargin,113*25.4/1440);
            if(band.Elements.Count!=11||band.Elements.Count(e=>e.Rotation==90)!=3)throw new Exception("Tear-off strip fields and rotations lost.");
            var gift=blanks.Single(b=>b.Name=="Gift").Legacy!;
            if(gift.Bands.SelectMany(b=>b.Elements).Count(e=>e.Rotation==270)!=7||!gift.Bands.SelectMany(b=>b.Elements).Any(e=>e.Text=="Приглашаем Вас "))throw new Exception("Gift orientation or original invitation text lost.");
            var child=blanks.Single(b=>b.Name=="podarok2").Legacy!.Bands.SelectMany(b=>b.Elements).ToArray();
            foreach(var text in new[]{"Для детей от 3 до 8 лет","(не для продажи)","подарок"})if(!child.Any(e=>e.Text==text))throw new Exception("Source gift text lost: "+text);
            if(child.Single(e=>e.Text=="подарок").Rotation!=90)throw new Exception("Gift label rotation lost.");
            var special=blanks.Single(b=>b.Name=="250416").Legacy!;
            if(special.Bands.SelectMany(b=>b.Elements).Count()!=5||special.Bands.SelectMany(b=>b.Elements).Count(e=>e.Binding=="PrintDate")!=3||!special.Bands.SelectMany(b=>b.Elements).Any(e=>e.Binding=="PageNumber"))throw new Exception("Special report replaced with generic ticket.");
            if(special.Bands.SelectMany(b=>b.Elements).Single(e=>e.Binding=="PageNumber").Alignment!="RightAlign")throw new Exception("Numeric special-field default alignment must be right.");
            Near(blanks.Single(b=>b.Name=="030879").Legacy!.SourceContentHeight,102060*25.4/1440);
            Near(blanks.Single(b=>b.Name=="030879").PageHeight,279.4);
            Near(blanks.Single(b=>b.Name=="Стандарт1 - копия").PageWidth,297);
            var pages=LegacyTicketTemplates.Paginate(standard.Legacy,7);
            if(pages.Count!=3||pages.SelectMany(p=>p.Elements).Count()!=77||pages[1].Elements[0].TicketIndex!=3)throw new Exception("Source detail-band pagination lost tickets.");
            var a4=standard.Legacy with{ContentHeight=297};if(LegacyTicketTemplates.Paginate(a4,7).Count!=2)throw new Exception("Chosen paper must affect pagination without scaling fields.");
        });
        test("old ticket XML mappings preserve series amount row date and tear-off values",_=>
        {
            var ticket=new TicketInfo(1,1,"Партер","5","12","Получатель","АБ",7,1500,"2026-10-21T12:00:00Z",null);
            var invoice=new InvoiceInfo(1,1,"Снежная королева","Театр","21.10.2026 19:05","Получатель","2026-10-21T12:00:00Z",[ticket]);
            string V(string binding)=>LegacyTicketTemplates.Value(binding,invoice,ticket,"Адрес театра",new DateTime(2026,10,21),2);
            var expected=new Dictionary<string,string>{{"theatre_adress","Адрес театра"},{"ticket_number","АБ № 000007"},{"perf_name","СНЕЖНАЯ КОРОЛЕВА"},{"ticket_place","Ряд: 5   Место: 12"},{"ticket_price","Цена: 1500р."},{"small_info1","АБ № 000007 Цена: 1500р. 21.10.2026 19:05"},{"small_info2","Партер Ряд: 5 Место: 12"},{"small_info3","Снежная королева"},{"PrintDate","21.10.2026"},{"PageNumber","2"}};
            foreach(var pair in expected)if(V(pair.Key)!=pair.Value)throw new Exception("Original XML binding changed: "+pair.Key);
            if(!V("perf_date").EndsWith("19 ч. 05 мин.",StringComparison.Ordinal))throw new Exception("Long source event date format lost.");
        });
        test("old print templates seed once and right-click deletion persists",store=>
        {
            var originals=store.TicketBlanks().Where(b=>b.Legacy is not null).ToArray();if(originals.Length!=11)throw new Exception("Original templates not seeded.");
            store.DeleteTicketBlank(originals[0].Id);
            if(store.TicketBlanks().Any(b=>b.Id==originals[0].Id)||store.TicketBlanks().Count(b=>b.Legacy is not null)!=10)throw new Exception("Deleted original template was recreated.");
        });
    }
}
