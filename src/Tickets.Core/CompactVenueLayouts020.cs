namespace Tickets.Core;

// Presentation spacing matches Taganka: 16-unit seats on an 18-unit pitch.
// Seat identities, source ordering, distinct sectors and real aisles stay intact.
internal static class CompactVenueLayouts020
{
    private const double Pitch=18,Gap=36;
    private static double Width(IEnumerable<DraftSeat> seats)=>seats.Max(s=>s.X)-seats.Min(s=>s.X)+16;
    private static double Bottom(IEnumerable<DraftSeat> seats)=>seats.Max(s=>s.Y)+16;
    private static void Move(IEnumerable<DraftSeat> seats,double x,double y)
    {
        var group=seats.ToArray();var dx=x-group.Min(s=>s.X);var dy=y-group.Min(s=>s.Y);
        foreach(var s in group){s.X+=dx;s.Y+=dy;}
    }
    private static void Rows(DraftSeat[] seats,bool aisles)
    {
        var rowGroups=seats.GroupBy(s=>s.Row).OrderBy(g=>int.Parse(g.Key)).ToArray();
        foreach(var (row,index) in rowGroups.Select((row,index)=>(row,index)))
        {
            var sorted=row.OrderBy(s=>s.X).ToArray();var original=sorted.Select(s=>s.X).ToArray();
            var ordinary=original.Zip(original.Skip(1),(a,b)=>b-a).Where(d=>d>0).DefaultIfEmpty(Pitch).Min();
            double x=0;
            for(var i=0;i<sorted.Length;i++)
            {
                if(i>0)x+=Pitch+(aisles&&original[i]-original[i-1]>ordinary*1.7?Pitch:0);
                sorted[i].X=x;sorted[i].Y=index*Pitch;
            }
        }
        var max=Width(seats);
        foreach(var row in rowGroups){var offset=Math.Round((max-Width(row))/2/Pitch)*Pitch;foreach(var s in row)s.X+=offset;}
    }
    internal static IReadOnlyList<DraftSeat> Russian(List<DraftSeat> seats)
    {
        var groups=seats.GroupBy(s=>s.Sector).ToDictionary(g=>g.Key,g=>g.ToArray());
        foreach(var pair in groups.Where(g=>!g.Key.StartsWith("Ложа партера")))Rows(pair.Value,true);
        var mainWidth=Width(groups["Амфитеатр"]);var center=mainWidth/2;
        double y=0;
        foreach(var name in new[]{"Экстрим партер","Партер","Амфитеатр"})
        {var group=groups[name];Move(group,center-Width(group)/2,y);y=Bottom(group)+Gap;}
        var parter=groups["Партер"];var amph=groups["Амфитеатр"];
        foreach(var side in new[]{"левая сторона","правая сторона"})
        {
            var left=side=="левая сторона";var lodge=groups[$"Ложа партера ({side})"];
            foreach(var s in lodge){s.X=(left?int.Parse(s.Row)-1:4-int.Parse(s.Row))*Pitch;s.Y=(int.Parse(s.Number)-1)*Pitch;}
            var lodgeX=left?parter.Min(s=>s.X)-Gap-Width(lodge):parter.Max(s=>s.X)+16+Gap;
            Move(lodge,lodgeX,Math.Max(0,Bottom(parter)-Bottom(lodge))+Pitch);
            var sideY=amph.Min(s=>s.Y);
            foreach(var number in new[]{1,2,3})
            {
                var group=groups[$"Балкон №{number} ({side})"];
                var height=Bottom(group)-group.Min(s=>s.Y);
                var adjacent=amph.Where(s=>s.Y+16>=sideY-18&&s.Y<=sideY+height+18).ToArray();
                if(adjacent.Length==0)adjacent=amph;
                var x=left?adjacent.Min(s=>s.X)-Gap-Width(group):adjacent.Max(s=>s.X)+16+Gap;
                Move(group,x,sideY);sideY=Bottom(group)+44;
            }
        }
        var floorY=seats.Where(s=>!s.Sector.StartsWith("VIP")&&!s.Sector.StartsWith("Балкон №4")).Max(s=>s.Y)+16+44;
        var names=new[]{"Балкон №4 (левая сторона)","VIP №3","VIP №2","VIP №1","Балкон №4 (правая сторона)"};
        var floorWidth=names.Sum(n=>Width(groups[n]))+Gap*(names.Length-1);var floorX=center-floorWidth/2;
        foreach(var name in names){Move(groups[name],floorX,floorY);floorX+=Width(groups[name])+Gap;}
        Normalize(seats);return seats;
    }
    internal static IReadOnlyList<DraftSeat> Estrada(List<DraftSeat> seats,bool contiguousRow17=true)
    {
        static double Curve(double x)=>0.00015*x*x;
        foreach(var name in new[]{"Партер","Бельэтаж"})
        {
            foreach(var row in seats.Where(s=>s.Sector==name&&!(name=="Партер"&&s.Row=="17"&&!contiguousRow17)).GroupBy(s=>s.Row))
            {
                var sorted=(name=="Партер"&&row.Key=="17"&&contiguousRow17?row.OrderBy(s=>int.Parse(s.Number)):row.OrderBy(s=>s.X)).ToArray();
                // The source gaps distinguish halves reliably even in rows with omitted labels.
                var gaps=sorted.Zip(sorted.Skip(1),(a,b)=>b.X-a.X).ToArray();
                var middle=name=="Партер"&&row.Key=="17"&&contiguousRow17?20:Array.IndexOf(gaps,gaps.Max())+1;
                var continuous=name=="Бельэтаж"&&row.Key=="6";
                for(var i=0;i<sorted.Length;i++)
                {
                    var x=continuous?(i-33.5)*Pitch:i<middle?-(27+(middle-1-i)*Pitch):27+(i-middle)*Pitch;
                    sorted[i].X=x;sorted[i].Y=(int.Parse(row.Key)-1)*Pitch-Curve(x);
                }
            }
        }
        var parter=seats.Where(s=>s.Sector=="Партер"&&s.Row!="17").ToArray();
        foreach(var s in seats.Where(s=>!contiguousRow17&&s.Sector=="Партер"&&s.Row=="17"))
        {
            var number=int.Parse(s.Number);var left=number<=20;var i=left?number-1:40-number;
            var row=parter.Where(p=>p.Row==(Math.Min(i,15)+1).ToString()).ToArray();
            var edge=(left?-row.Min(p=>p.X):row.Max(p=>p.X))+Gap;
            if(i>=16){var last=parter.Where(p=>p.Row=="16").ToArray();edge=(left?-last.Min(p=>p.X):last.Max(p=>p.X))+Gap-(i-16)*Pitch;}
            s.X=left?-edge:edge;s.Y=Math.Min(i,16)*Pitch-Curve(s.X);
        }
        var allParter=seats.Where(s=>s.Sector=="Партер").ToArray();var balcony=seats.Where(s=>s.Sector=="Бельэтаж").ToArray();
        var delta=Bottom(allParter)+Gap-balcony.Min(s=>s.Y);foreach(var s in balcony)s.Y+=delta;
        Normalize(seats);return seats;
    }
    private static void Normalize(List<DraftSeat> seats)
    {
        var x=seats.Min(s=>s.X);var y=seats.Min(s=>s.Y);
        foreach(var s in seats){s.X=Math.Round(s.X-x+40,4);s.Y=Math.Round(s.Y-y+48,4);}
    }
}
