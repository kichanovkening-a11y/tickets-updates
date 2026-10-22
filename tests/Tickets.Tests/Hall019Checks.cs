using Microsoft.Data.Sqlite;
using Tickets.Core;

internal static class Hall019Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("source hall 019 exact row labels, omissions, orientation, aisles and collision clearance",_=>Layouts());
        test("source hall 019 migration is backed up, archived, isolated and runs once",s=>
        {
            Require(s.HallReplacements019.Count==2,"both imported layouts replaced");
            Require(File.Exists(s.Path+".before-v019-halls.db"),"pre-replacement database backup exists");
            var visible=s.Venues();var r=visible.Single(x=>x.Name==VenueLayouts019.RussianSong);var e=visible.Single(x=>x.Name==VenueLayouts019.Estrada);
            Require(r.Id!=25&&e.Id!=14,"new halls have independent place identities");
            Require(s.VenueSeats(25).Count==1010&&s.VenueSeats(14).Count==1310,"original seats remain archived intact");
            Require(s.VenueSeats(22).Count==989,"unrelated old Russian-song stage remains unchanged");
            Require(s.VenueSeats(r.Id).Count==1014&&s.VenueSeats(e.Id).Count==1002,"replacement seat totals");
            var count=s.Events(true).Count;var reopened=new Store(s.Path);
            Require(reopened.HallReplacements019.Count==0&&reopened.Events(true).Count==count,"reopening never clones halls or shows twice");
            using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=s.Path}.ToString());c.Open();
            using var fk=c.CreateCommand();fk.CommandText="PRAGMA foreign_key_check";using var reader=fk.ExecuteReader();Require(!reader.Read(),"all archived and replacement references valid");
        });
    }
    private static void Layouts()
    {
        var russian=VenueLayouts019.RussianSongSeats();var estrada=VenueLayouts019.EstradaSeats();
        Check(russian);Check(estrada);
        Require(russian.Count==1122&&russian.Select(s=>s.Sector).Distinct().Count()==16,"Russian song all sixteen sectors");
        int[] par=[25,26,27,28,27,28,27];int[] amph=[37,42,45,46,43,42,45,46,47,46,29,18,30,28];
        Rows(russian,"Партер",par);Rows(russian,"Амфитеатр",amph);Rows(russian,"Экстрим партер",[20,32,32,24]);
        foreach(var vip in new[]{"VIP №1","VIP №2","VIP №3"})Require(russian.Where(s=>s.Sector==vip).Select(s=>s.Row).Distinct().Order().SequenceEqual(new[]{"22","23","24"}),"VIP source rows 22–24");
        foreach(var side in new[]{"правая сторона","левая сторона"})Require(russian.Where(s=>s.Sector==$"Балкон №4 ({side})").Select(s=>s.Row).Distinct().Order().SequenceEqual(new[]{"22","23","24","25","26","27"}),"balcony4 source rows22–27");
        var rp=russian.Where(s=>s.Sector=="Партер"&&s.Row=="1").ToDictionary(s=>int.Parse(s.Number));
        Require(rp[1].X>rp[25].X&&rp[6].X-rp[7].X>18&&rp[19].X-rp[20].X>18,"Russian right-to-left labels and two compact aisles");
        Require(russian.Where(s=>s.Sector=="Экстрим партер").Max(s=>s.Y)<russian.Where(s=>s.Sector=="Партер").Min(s=>s.Y),"extra front rows stay closest to stage");
        Rows(estrada,"Партер",[20,20,29,32,38,40,42,42,40,38,36,36,32,28,22,18,40]);
        Rows(estrada,"Бельэтаж",[34,39,44,52,55,68,49,52,56]);
        foreach(var (row,missing) in new[]{(2,new[]{12,13}),(3,new[]{17,18,19}),(4,new[]{16,17,18,19})})Require(!estrada.Any(s=>s.Sector=="Партер"&&s.Row==row.ToString()&&missing.Contains(int.Parse(s.Number))),"source numbering holes must stay empty");
        var ep=estrada.Where(s=>s.Sector=="Партер"&&s.Row=="1").ToDictionary(s=>int.Parse(s.Number));Require(ep[1].X<ep[20].X&&ep[11].X-ep[10].X>18,"Estrada keeps left-to-right source order and centre aisle");
    }
    private static void Rows(IReadOnlyList<DraftSeat> seats,string sector,int[] counts)
    {for(var r=1;r<=counts.Length;r++)Require(seats.Count(s=>s.Sector==sector&&s.Row==r.ToString())==counts[r-1],$"{sector}, row{r} count");}
    private static void Check(IReadOnlyList<DraftSeat> seats)
    {
        Require(seats.Select(s=>(s.Sector,s.Row,s.Number)).Distinct().Count()==seats.Count,"unique place identities");
        Require(seats.All(s=>double.IsFinite(s.X)&&double.IsFinite(s.Y)&&s.X>=0&&s.Y>=0),"finite nonnegative coordinates");
        for(var i=0;i<seats.Count;i++)for(var j=i+1;j<seats.Count;j++)Require(Math.Abs(seats[i].X-seats[j].X)>=16||Math.Abs(seats[i].Y-seats[j].Y)>=16,$"overlapping16×16 seats {seats[i].Sector}/{seats[i].Row}/{seats[i].Number} and {seats[j].Sector}/{seats[j].Row}/{seats[j].Number}");
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
