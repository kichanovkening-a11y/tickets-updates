namespace Tickets.Core;

/// <summary>Seat runs transcribed from zal-ruzpesnya.png and Зал-NEW.jpg supplied for v0.19.
/// Coordinates are vector presentation coordinates; the source seat labels and omissions are authoritative.</summary>
public static class VenueLayouts019
{
    public const string RussianSong = "Театр «Русская Песня»";
    public const string Estrada = "\"Московский государственный театр Эстрады\"";

    public static IReadOnlyList<DraftSeat> RussianSongSeats(bool compact=true)
    {
        var seats=new List<DraftSeat>();
        void Run(string sector,int row,int first,int last,double x,double y,double step=15)
        {
            for(var n=first;n<=last;n++) seats.Add(new(){Sector=sector,Row=row.ToString(),Number=n.ToString(),
                X=(1150-x-(n-first)*step)*2,Y=(670-y)*2+40});
        }
        // Original picture is viewed from the stage. Reflect both axes for the stage-at-top view:
        // numbers grow right to left. Keep every gap between the three blocks.
        int[][] parter=[[6,19,25],[6,20,26],[7,20,27],[7,21,28],[7,20,27],[7,21,28],[7,20,27]];
        for(var r=1;r<=7;r++)
        {
            var p=parter[r-1]; var y=570-(r-1)*13;
            Run("Партер",r,1,p[0],r<3?320:305,y,17);
            Run("Партер",r,p[0]+1,p[1],480,y,17);
            Run("Партер",r,p[1]+1,p[2],780,y,17);
        }
        // Extra front seating: row 4 is only 1–24, rows 2–3 have 1–32, row 1 has 1–20.
        Run("Экстрим партер",1,1,20,457,654,15);
        foreach(var r in new[]{2,3}) { Run("Экстрим партер",r,1,6,334,654-(r-1)*13);Run("Экстрим партер",r,7,26,457,654-(r-1)*13);Run("Экстрим партер",r,27,32,791,654-(r-1)*13); }
        Run("Экстрим партер",4,1,2,394,615);Run("Экстрим партер",4,3,22,457,615);Run("Экстрим партер",4,23,24,791,615);
        int[][] amph=[[12,25,37],[15,28,42],[15,30,45],[15,30,46],[13,31,43],[12,31,42],[14,32,45],[13,33,46],[13,34,47],[12,34,46]];
        for(var r=1;r<=10;r++)
        {
            var p=amph[r-1]; var y=451-(r-1)*10;
            Run("Амфитеатр",r,1,p[0],245-(r-1)*5,y,14);
            Run("Амфитеатр",r,p[0]+1,p[1],505-(r-1)*5,y,14);
            Run("Амфитеатр",r,p[1]+1,p[2],790+(r-1)*3,y,14);
        }
        Run("Амфитеатр",11,1,1,202,343);Run("Амфитеатр",11,2,26,441,348,14);Run("Амфитеатр",11,27,29,998,343,14);
        Run("Амфитеатр",12,1,2,190,330);Run("Амфитеатр",12,3,8,441,337,14);Run("Амфитеатр",12,9,16,674,337,14);Run("Амфитеатр",12,17,18,998,330,14);
        Run("Амфитеатр",13,1,9,181,320);Run("Амфитеатр",13,10,15,455,326,14);Run("Амфитеатр",13,16,21,674,326,14);Run("Амфитеатр",13,22,30,890,320,14);
        Run("Амфитеатр",14,1,8,190,307);Run("Амфитеатр",14,9,13,469,315,14);Run("Амфитеатр",14,14,20,674,315,14);Run("Амфитеатр",14,21,28,898,307,14);
        void Block(string sector,int[] counts,double x,double y,int firstRow=1,double step=15)
        { for(var r=0;r<counts.Length;r++)Run(sector,firstRow+r,1,counts[r],x+(counts.Max()-counts[r])*step/2,y-r*13,step); }
        Block("VIP №1",[6,6,5],400,202,22);Block("VIP №2",[8,9,8],541,202,22);Block("VIP №3",[6,6,5],722,202,22);
        // Side boxes remain distinct from balcony sectors even where row/place labels repeat.
        for(var side=0;side<2;side++)
        {
            var label=side==0?"правая сторона":"левая сторона";
            var x=side==0?110:1030;
            // In the source the lodge rows run along the side of the parterre.
            int[] lodge=[8,7,7,7];
            for(var r=1;r<=4;r++) for(var n=1;n<=lodge[r-1];n++)
                seats.Add(new(){Sector="Ложа партера ("+label+")",Row=r.ToString(),Number=n.ToString(),X=(1150-(x+(r-1)*(side==0?12:-12)))*2,Y=(670-(615-(n-1)*15))*2+40});
        }
        Block("Балкон №1 (правая сторона)",[3,4,4],91,452);
        Block("Балкон №2 (правая сторона)",[3,6,5,4],70,373);
        Block("Балкон №3 (правая сторона)",[3,5,5,3],51,290);
        Block("Балкон №4 (правая сторона)",[7,7,8,7,5,4],208,196,22);
        Block("Балкон №1 (левая сторона)",[2,4,4],1055,452);
        Block("Балкон №2 (левая сторона)",[3,6,5,4],1070,373);
        Block("Балкон №3 (левая сторона)",[3,5,5],1100,290);
        Block("Балкон №4 (левая сторона)",[7,7,8,8,6,5],890,196,22);
        var minX=seats.Min(s=>s.X);
        foreach(var s in seats)s.X+=40-minX;
        return compact?CompactVenueLayouts020.Russian(seats):seats;
    }

    public static IReadOnlyList<DraftSeat> EstradaSeats(bool compact=true,bool legacyPerimeter=false)
    {
        var seats=new List<DraftSeat>();
        // Preserve left-to-right place numbering from this plan. The two halves have an actual
        // centre aisle; omitted numbers in rows 2–4 are intentionally NOT generated.
        (int left,int firstRight,int last)[] rows=[(10,11,20),(11,14,22),(16,20,32),(15,20,36),
            (18,19,38),(19,20,40),(20,21,42),(21,22,42),(21,22,40),(20,21,38),
            (19,20,36),(18,19,36),(16,17,32),(14,15,28),(11,12,22),(9,10,18)];
        void Half(string sector,int row,int first,int last,bool right,double y,double radius,double angularStep)
        {
            for(var n=first;n<=last;n++)
            {
                var fromCentre=right?n-first:last-n;
                var angle=(fromCentre+1.9)*angularStep;
                seats.Add(new(){Sector=sector,Row=row.ToString(),Number=n.ToString(),
                    X=1000+(right?1:-1)*radius*Math.Sin(angle),Y=y-radius*(1-Math.Cos(angle))});
            }
        }
        for(var r=1;r<=16;r++)
        {
            var p=rows[r-1];var y=200+(r-1)*38;var radius=800+r*10;
            Half("Партер",r,1,p.left,false,y,radius,0.027);
            Half("Партер",r,p.firstRight,p.last,true,y,radius,0.027);
        }
        // Row 17 follows the perimeter, not an extra straight rear row.
        for(var n=1;n<=40;n++)
        {
            var left=n<=20;var i=left?n-1:40-n;
            // Follow the actual end-of-row envelope, with a side aisle. The perimeter
            // starts beside row 4 and ends beside the rear row, rather than forming a
            // detached extra semicircle below the parterre.
            var rowPosition=4+i*12.0/19;
            (double X,double Y) Edge(int row)
            {
                var p=rows[row-1];var count=left?p.left:p.last-p.firstRight+1;
                var radius=800+row*10;var angle=(count+0.9)*0.027;
                return (radius*Math.Sin(angle)+65,200+(row-1)*38-radius*(1-Math.Cos(angle)));
            }
            var lo=(int)Math.Floor(rowPosition);var hi=Math.Min(16,lo+1);var a=Edge(lo);var b=Edge(hi);var t=rowPosition-lo;
            var y=Edge(4).Y+(Edge(16).Y-Edge(4).Y)*i/19;
            var edge=seats.Where(s=>s.Row!="17"&&Math.Abs(s.Y-y)<48&&(left?s.X<1000:s.X>1000)).Max(s=>Math.Abs(s.X-1000))+65;
            seats.Add(new(){Sector="Партер",Row="17",Number=n.ToString(),X=1000+(left?-1:1)*edge,Y=y});
        }
        (int left,int last)[] balcony=[(17,34),(20,39),(22,44),(28,52),(28,55),(34,68),(25,49),(26,52),(28,56)];
        for(var r=1;r<=9;r++)
        {
            var p=balcony[r-1];var y=1420+(r-1)*40;var radius=980+r*18;
            if(r==6)
            {
                // Row 6 alone crosses the centre, running continuously from 1 to 68.
                for(var n=1;n<=68;n++) {var a=(n-34.5)*0.026;seats.Add(new(){Sector="Бельэтаж",Row="6",Number=n.ToString(),X=1000+radius*Math.Sin(a),Y=y-radius*(1-Math.Cos(a))});}
            }
            else {Half("Бельэтаж",r,1,p.left,false,y,radius,0.026);Half("Бельэтаж",r,p.left+1,p.last,true,y,radius,0.026);}
        }
        // Row17 outermost endpoints are allowed to extend left of zero before normalization.
        var minX=seats.Min(s=>s.X);var minY=seats.Min(s=>s.Y);
        foreach(var s in seats){s.X=Math.Round(s.X-minX+40,2);s.Y=Math.Round(s.Y-minY+40,2);}
        return compact?CompactVenueLayouts020.Estrada(seats,!legacyPerimeter):seats;
    }
}
