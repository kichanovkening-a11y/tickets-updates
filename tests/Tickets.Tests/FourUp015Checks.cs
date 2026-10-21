using Tickets.Core;
using System.Text.Json;

internal static class FourUp015Checks
{
    internal static void Register(Action<string,Action<Store>> test)
    {
        test("all saved ticket blanks migrate to four positions on A4",store=>
        {
            var originals=LegacyTicketTemplates.All();var snapshot=JsonSerializer.Serialize(originals);
            var blanks=store.TicketBlanks();Check(blanks.Count(b=>b.Legacy is not null)==11,"eleven original templates");
            foreach(var blank in blanks)
            {
                Check(blank.FourUpA4&&blank.Positions.Count==4&&blank.PageWidth==210&&blank.PageHeight==297,blank.Name+" A4 four-up");blank.Validate();
                if(blank.Legacy is null)continue;
                foreach(var count in new[]{1,4,5,9})
                {
                    var pages=FourUpTicketLayouts.Paginate(blank,count);Check(pages.Count==(count+3)/4,blank.Name+" pages");
                    Check(pages.SelectMany(p=>p.Elements).Select(p=>p.TicketIndex).Distinct().Order().SequenceEqual(Enumerable.Range(0,count)),blank.Name+" ticket identity");
                    foreach(var p in pages.SelectMany(p=>p.Elements))
                    {
                        var slot=blank.Positions[p.TicketIndex%4];
                        Check(p.SourceElement is not null&&!ReferenceEquals(p.Element,p.SourceElement),blank.Name+" detached rendered geometry");
                        Check(p.X>=slot.X-.001&&p.Y>=slot.Y-.001&&p.X+p.Element.Width<=slot.X+slot.Width+.001&&p.Y+p.Element.Height<=slot.Y+slot.Height+.001,blank.Name+" ticket fits its quarter");
                    }
                }
            }
            Check(JsonSerializer.Serialize(originals)==snapshot,"original source layouts unchanged");
            var reopened=new Store(store.Path).TicketBlanks();Check(reopened.Select(b=>b.Id).SequenceEqual(blanks.Select(b=>b.Id)),"migration retains IDs");
        });
        test("new and calibrated saved blanks retain four printable positions",store=>
        {
            var single=new TicketBlank{Name="single converted"};store.SaveTicketBlank(single);Check(single.FourUpA4&&single.Positions.Count==4,"single converted to four");single.Validate();
            var calibrated=TicketBlankEditing.CreateA4();calibrated.Name="calibrated four";var before=JsonSerializer.Serialize(calibrated.Positions);store.SaveTicketBlank(calibrated);Check(JsonSerializer.Serialize(calibrated.Positions)==before,"calibrated four positions preserved");
            store.DeleteTicketBlank(single.Id);Check(new Store(store.Path).TicketBlanks().All(b=>b.Id!=single.Id),"deleted templates stay deleted");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
