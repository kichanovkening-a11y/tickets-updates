using Tickets.Core;

internal static class HallEditorRefinementChecks
{
    internal static void Run()
    {
        void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        var row=new HallRowLayout{Label="А",SeatCount=23,AisleAfter=10};
        var choices=HallLayout.AisleChoices(row,"1",false);
        Require(choices.Single(c=>c.After==10).Caption=="Между 10 и 11","Numeric aisle displays its actual seat pair");
        Require(choices.Count==23&&choices[0].After==0,"No aisle and each interior gap are available");
        var alpha=HallLayout.AisleChoices(new HallRowLayout{SeatCount=4,FirstSeat="А"},"1",false);
        Require(alpha[2].Caption=="Между Б и В","Alphabetic seat labels survive aisle choice");
        var reversed=HallLayout.AisleChoices(new HallRowLayout{SeatCount=3,FirstSeat="А, 1"},"1",true);
        Require(reversed[1].Caption=="Между 2 и 1"&&reversed[2].Caption=="Между 1 и А","Reversed mixed labels use physical neighbors");
        Require(row.AisleAfter==10&&row.SeatCount==23,"Displaying aisle options does not mutate existing data");
        var end=HallLayout.AisleChoices(new HallRowLayout{SeatCount=2,AisleAfter=2},"1",false);
        Require(end.Any(c=>c.After==2),"Existing edge aisle is preserved");
        var many=HallLayout.Generate("Партер",Enumerable.Range(1,37).Select(n=>new HallRowLayout{Label=n.ToString(),SeatCount=1}).ToArray(),"А",false,0);
        Require(many.Count==37&&many[^1].Row=="37","Row count is arbitrary above ten");
        Require(PricePlanPresentation.ImportedName("Спектакль — 21.10.2026 19:00 (показ №12)","Спектакль","21.10.2026 19:00",12)=="Спектакль","Imported generated metadata is hidden");
        Require(PricePlanPresentation.ImportedName("Авторское название 2026 — 19:00","Спектакль","21.10.2026 19:00",12)=="Авторское название 2026 — 19:00","Custom pricing names are preserved");
    }
}
