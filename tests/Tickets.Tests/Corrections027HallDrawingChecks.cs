using Tickets.Core;
internal static class Corrections027HallDrawingChecks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("027 row numbers appear in every aisle",_=>{
            var row=new[]{0d,20,60,80,130,150}.Select((x,i)=>new SeatInfo(i+1,"Партер","7",(i+1).ToString(),x,40,null,"",null,"","",null)).ToArray();
            var anchors=HallEditing025.RowAnchors(row);if(anchors.Count!=4||anchors.Count(a=>a.Centre)!=2)throw new Exception("Every aisle must carry a row number");
            if(anchors.Any(a=>a.Y!=48))throw new Exception("Row captions lost vertical attachment");
        });
    }
}
