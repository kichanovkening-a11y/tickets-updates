using Tickets.Core;

internal static class LegacyHallGrid022Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("legacy cell grid",store=>{
            Run();
            var grid=new LegacyHallGrid(2,4);grid.Set(0,2,LegacyHallCell.Empty);grid.Set(1,0,LegacyHallCell.LetterRow);
            var expected=grid.Compile("Тестовый сектор");
            var id=store.CreateHall("Сетка 022 "+Guid.NewGuid(),"Адрес",expected);
            var actual=store.HallDraft(id);
            if(!actual.Select(s=>(s.Sector,s.Row,s.Number,s.X,s.Y)).OrderBy(s=>s.Y).ThenBy(s=>s.X).SequenceEqual(expected.Select(s=>(s.Sector,s.Row,s.Number,s.X,s.Y))))
                throw new Exception("Compiled grid seats did not survive hall persistence without caption seats");
        });
    }
    internal static void Run()
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var grid=new LegacyHallGrid(3,5);
        grid.Set(0,2,LegacyHallCell.Empty);grid.Set(0,4,LegacyHallCell.Empty);
        for(var c=1;c<=5;c++)grid.Set(1,c,LegacyHallCell.Empty);
        grid.Set(2,0,LegacyHallCell.LetterRow);
        grid.Set(2,1,LegacyHallCell.LetterSeat);grid.Set(2,3,LegacyHallCell.LetterSeat);
        grid.Set(2,6,LegacyHallCell.NumberSeat); // reserved edge cannot become a chair
        var seats=grid.Compile("Партер","1","1",false);
        Check(seats.Count==8,"Only actual seat cells compile; passages and both captions never become seats");
        Check(string.Join(",",seats.Take(3).Select(s=>s.Number))=="1,2,3","Two independent passages do not consume seat numbers");
        Check(seats.Skip(3).All(s=>s.Row=="А")&&string.Join(",",seats.Skip(3).Select(s=>s.Number))=="А,1,Б,2,3","Mixed seat types have independent counters");
        Check(seats.Skip(3).All(s=>s.Y==44),"Blank rows preserve geometry");
        var numeric=new LegacyHallGrid(3,1);numeric.Set(1,1,LegacyHallCell.Empty);
        Check(string.Join(",",numeric.Compile("П","4","1",false).Select(s=>s.Row))=="4,5","Blank physical rows do not consume row labels");
        var copy=grid.Clone();copy.Set(0,1,LegacyHallCell.Empty);
        Check(grid.Compile("П").Count==8&&copy.Compile("П").Count==7,"Draft snapshots do not share mutable cells");
        Check(grid.Compile("П","1","1",true).Take(3).Select(s=>s.Number).SequenceEqual(new[]{"3","2","1"}),"Reverse numbering follows real chairs across holes");
        var mixed=new LegacyHallGrid(2,4);mixed.Set(0,0,LegacyHallCell.LetterRow);
        mixed.Set(0,1,LegacyHallCell.LetterSeat);mixed.Set(0,3,LegacyHallCell.LetterSeat);
        foreach(var start in new[]{"А","А, 1"})
        {
            var typed=mixed.Compile("П",start,start);
            Check(typed.Take(4).All(s=>s.Row=="А")&&typed.Skip(4).All(s=>s.Row=="1"),"Alphabetic start preserves independent numeric and letter row types: "+start);
            Check(string.Join(",",typed.Take(4).Select(s=>s.Number))=="А,1,Б,2","Alphabetic start preserves independent numeric and letter seat types: "+start);
        }
        var shifted=mixed.Compile("П","Б, 4","Б, 4");
        Check(shifted.Take(4).All(s=>s.Row=="Б")&&shifted.Skip(4).All(s=>s.Row=="4")&&string.Join(",",shifted.Take(4).Select(s=>s.Number))=="Б,4,В,5","Mixed starts select the actual matching numeric and letter components");
        try{mixed.Compile("П","","1");throw new Exception("Empty row start accepted");}catch(TicketException){}
        try{mixed.Compile("П","1","А,");throw new Exception("Incomplete mixed seat start accepted");}catch(TicketException){}
        try{new LegacyHallGrid(200,300);throw new Exception("Oversized draft accepted");}catch(TicketException){}
        Console.WriteLine("PASS legacy grid holes, mixed labels, reserved captions, blank rows, snapshot and reverse numbering");
    }
}
