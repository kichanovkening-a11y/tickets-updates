using Tickets.Core;

internal static class HallGeometry053Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("053 performance editing preserves show identity",store=>{
            var title="053 original "+Guid.NewGuid();var id=store.SavePerformance(title);
            var before=store.Events().Select(e=>e.Id).ToArray();store.RenamePerformance(id,title+" edited");
            Check(store.Performances().Single(p=>p.Id==id).Title==title+" edited","Name not saved");
            Check(store.Events().Select(e=>e.Id).SequenceEqual(before),"Show identities changed");
        });
        run("053 bend preserves labels, aisle and concentric row distances",_=>{
            var seats=HallLayout.Generate("S",[new(){Label="1",SeatCount=11,AisleAfter=5},new(){Label="2",SeatCount=11,AisleAfter=5}],"1",false,0).ToArray();
            var labels=seats.Select(s=>(s.Sector,s.Row,s.Number)).ToArray();
            HallCurving053.Bend(seats,60);
            Check(labels.SequenceEqual(seats.Select(s=>(s.Sector,s.Row,s.Number))),"Seat identity changed");
            foreach(var row in seats.GroupBy(s=>s.Row))
            {
                var points=row.ToArray();var gaps=points.Zip(points.Skip(1),(a,b)=>Distance(a,b)).ToArray();
                Check(gaps[4]>50&&gaps.Where((g,i)=>i!=4).All(g=>g>17&&g<18.1),"Aisle or chair pitch changed");
            }
            Check(seats.All(s=>double.IsFinite(s.X)&&double.IsFinite(s.Y)),"Nonfinite coordinates");
        });
        run("053 rotated rows bend and straighten without renumbering",_=>{
            var seats=HallLayout.Generate("S",[new(){Label="1",SeatCount=15,AisleAfter=7}],"1",true,0).ToArray();
            HallEditing023.Rotate(seats,67);HallCurving053.Bend(seats,-50);HallCurving053.Straighten(seats);
            var (cx,cy,dx,dy)=HallCurving053.Frame(seats);
            Check(seats.All(s=>Math.Abs(-(s.X-cx)*dy+(s.Y-cy)*dx)<.001),"Row is not straight");
            Check(seats[0].Number=="15"&&seats[^1].Number=="1","Reverse numbering changed");
        });
        run("053 selected seats create individual aisles across rows",_=>{
            var seats=HallLayout.Generate("S",[new(){Label="1",SeatCount=20},new(){Label="2",SeatCount=20}],"1",false,0).ToArray();
            var before=seats.Select(s=>(s.X,s.Y,s.Number)).ToArray();
            HallEditing023.MakeAisle(seats,[seats[9],seats[10],seats[29]],eachSelected:true);
            for(var i=0;i<seats.Length;i++)
            {
                var shift=i<9?0:i<10?20:i<20?40:i<29?0:20;
                Check(seats[i].X==before[i].X+shift&&seats[i].Y==before[i].Y&&seats[i].Number==before[i].Number,"Wrong selected-seat aisle or numbering");
            }
        });
        run("053 legacy selected-run aisle behavior remains unchanged",_=>{
            var seats=HallLayout.Generate("S",[new(){Label="1",SeatCount=20}],"1",false,0).ToArray();
            var x=seats[^1].X;HallEditing023.MakeAisle(seats,[seats[9],seats[10]]);
            Check(seats[^1].X==x+20,"Legacy run now creates duplicate aisles");
        });
        run("053 aisle before seat shifts following seats without removing seat",_=>{
            var seats=HallLayout.Generate("S",[new(){Label="1",SeatCount=20}],"1",false,0).ToArray();
            var before=seats.Select(s=>s.X).ToArray();HallEditing023.MakeAisle(seats,[seats[10]]);
            Check(seats.Length==20&&seats[10].Number=="11","Seat lost or renumbered");
            Check(seats.Take(10).Select(s=>s.X).SequenceEqual(before.Take(10)),"Previous seats moved");
            Check(seats.Skip(10).Select((s,i)=>s.X-before[i+10]).All(d=>d==20),"Following seats not shifted together");
        });
    }
    private static double Distance(DraftSeat a,DraftSeat b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
