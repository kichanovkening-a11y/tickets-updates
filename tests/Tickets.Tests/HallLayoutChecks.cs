using Tickets.Core;

internal static class HallLayoutChecks
{
    public static void Run()
    {
        Require(HallLayout.Label("Z",1)=="AA","Latin labels continue after Z");
        Require(HallLayout.Label("Я",1)=="АА","Cyrillic labels continue after Я");
        Require(HallLayout.Label("Ё",1)=="Ж","Cyrillic alphabet includes Ё");
        Require(HallLayout.Label("a",1)=="b","Lowercase labels retain case");
        Require(HallLayout.Label("9",1)=="10","Numeric labels increment");
        var seats=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="А",SeatCount=23,AisleAfter=10},new HallRowLayout{Label="Б",SeatCount=5,AisleAfter=0}},"1",false,26).ToList();
        Require(seats.Count==28 && seats[23].Row=="Б","Each row has its configured seat count");
        Require(seats[10].X-seats[9].X==56,"Aisle adds space after configured seat");
        var survivors=seats.Where(s=>s.Row!="А"||int.Parse(s.Number)>3).ToList();
        Require(survivors[0].Number=="4"&&survivors[0].X==60&&survivors[19].Number=="23","Deleting seats preserves surviving labels and coordinates");
        var reversed=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="A",SeatCount=3}},"А",true,0);
        Require(reversed[0].Number=="В"&&reversed[2].Number=="А","Reverse numbering accepts Cyrillic letters");
        try { HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=2,AisleAfter=3}},"1",false,0); throw new Exception("Invalid aisle accepted"); }
        catch(TicketException) { }
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
