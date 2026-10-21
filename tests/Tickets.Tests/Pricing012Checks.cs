using Tickets.Core;

internal static class Pricing012Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("pricing 012 summaries fill three rows per column in descending price order",s=>{
            var prices=Enumerable.Range(1,7).Select(i=>new PriceValue(i,i*100,PricePalette.Colors[i])).ToList();
            prices.Add(new PriceValue(8,700,PricePalette.Colors[7]));
            var rows=Pricing012Presentation.Summaries(prices);
            Require(rows.Select(r=>r.Amount).SequenceEqual(new long[]{700,600,500,400,300,200,100}),"price order changed");
            Require(rows.Select(r=>(r.Row,r.Column)).SequenceEqual(new[]{(0,0),(1,0),(2,0),(0,1),(1,1),(2,1),(0,2)}),"columns do not contain three descending prices");
            Require(rows[0].Count==2&&rows[0].Total==1400&&rows.Sum(r=>r.Count)==8&&rows.Sum(r=>r.Total)==3500,"summary totals changed");
        });
        test("pricing 012 summary foreground remains readable for palette colors",s=>{
            static double Linear(byte value){var n=value/255d;return n<=0.04045?n/12.92:Math.Pow((n+0.055)/1.055,2.4);}
            foreach(var color in PricePalette.Colors)
            {
                var l=0.2126*Linear(Convert.ToByte(color.Substring(1,2),16))+0.7152*Linear(Convert.ToByte(color.Substring(3,2),16))+0.0722*Linear(Convert.ToByte(color.Substring(5,2),16));
                var contrast=Pricing012Presentation.Foreground(color)=="#FFFFFF"?1.05/(l+0.05):(l+0.05)/0.05;
                Require(contrast>=4.5,$"unreadable summary color {color}");
            }
            Require(Pricing012Presentation.Foreground("#000000")=="#FFFFFF"&&Pricing012Presentation.Foreground("#FFD600")=="#000000","extreme color contrast changed");
        });
        test("pricing 012 compares actual seat prices including reversal and withdrawals",s=>{
            var original=new[]{new PriceValue(1,100,"#FF5252"),new PriceValue(2,200,"#FFD600")};
            var edited=new[]{new PriceValue(1,300,"#00E676"),original[1]};
            Require(!Pricing012Presentation.SamePrices(original,new long[]{3},edited,new long[]{3}),"price edit ignored");
            Require(Pricing012Presentation.SamePrices(original,new long[]{3},original.Reverse().Select(p=>p with{Color="#000000"}),new long[]{3}),"restored prices still marked changed");
            Require(!Pricing012Presentation.SamePrices(original,new long[]{3},original,Array.Empty<long>()),"withdrawal edit ignored");
            Require(!Pricing012Presentation.SamePrices(original,Array.Empty<long>(),new[]{new PriceValue(2,100,"#FF5252"),new PriceValue(1,200,"#FFD600")},Array.Empty<long>()),"equal totals hid different seat prices");
            Require(!Pricing012Presentation.SamePrices(original,new long[]{3},original.Select(p=>p with{Amount=400}),new long[]{3}),"assign all lost loaded source comparison");
        });
        test("pricing 012 color-only save preserves source mappings and updates shared colors",s=>{
            var venue=s.CreateHall("Общие цвета 012","",new[]{new DraftSeat{Number="1"}});var id=s.VenueSeats(venue).Single().Id;
            var color=PricePalette.NextAvailable(s.PriceColors().Select(p=>p.Color));
            var original=s.SavePlan(venue,"Общий источник 012",new[]{new PriceValue(id,76503,color)},Array.Empty<long>());
            var copy=s.SavePlan(venue,"Общая копия 012",new[]{new PriceValue(id,76503,color)},Array.Empty<long>());
            var next=PricePalette.NextAvailable(s.PriceColors().Select(p=>p.Color));var edited=new[]{new PriceValue(id,76503,next)};
            Require(Pricing012Presentation.SamePrices(s.PlanPrices(original),Array.Empty<long>(),edited,Array.Empty<long>()),"color-only edit required new name");
            s.SetPriceColor(76503,next);
            Require(s.PlanPrices(original).Single().Amount==76503&&s.PlanPrices(original).Single().Color==next&&s.PlanPrices(copy).Single().Color==next,"shared color did not persist or source amounts changed");
        });
        test("pricing 012 saving a changed copy preserves original seat amounts and blocks",s=>{
            var venue=s.CreateHall("Расценовки 012","",new[]{new DraftSeat{Number="1"},new DraftSeat{Number="2",X=20}});
            var seats=s.VenueSeats(venue);var first=new[]{new PriceValue(seats[0].Id,76501,PricePalette.NextAvailable(s.PriceColors().Select(p=>p.Color)))};
            var original=s.SavePlan(venue,"Исходная 012",first,new[]{seats[1].Id});
            var changed=new[]{new PriceValue(seats[0].Id,76502,PricePalette.NextAvailable(s.PriceColors().Select(p=>p.Color)))};
            var revised=s.SavePlan(venue,"Изменённая 012",changed,new[]{seats[1].Id});
            Require(revised!=original&&s.PlanPrices(original).Single().Amount==76501&&s.PlanBlockedSeats(original).SequenceEqual(new[]{seats[1].Id}),"source plan history overwritten");
            try{s.SavePlan(venue," Исходная 012 ",changed,new[]{seats[1].Id});throw new Exception("duplicate name accepted");}catch(TicketException){}
        });
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
