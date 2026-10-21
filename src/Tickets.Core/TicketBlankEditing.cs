using System.Text.Json;

namespace Tickets.Core;

/// <summary>Physical millimetre geometry shared by the editor and printing.</summary>
public static class TicketBlankEditing
{
    public static IReadOnlyList<TicketBlankField> FieldsFor(TicketBlank blank,TicketBlankPosition position)=>position.Fields??blank.Fields;
    public static TicketBlank Clone(TicketBlank source)
    {
        var copy=JsonSerializer.Deserialize<TicketBlank>(JsonSerializer.Serialize(source))!;
        copy.Id=Guid.NewGuid().ToString("N");copy.Name=source.Name+" — копия";copy.IsEditableCopy=true;
        return copy;
    }
    public static TicketBlank CreateA4()
    {
        var blank=new TicketBlank {Name="А4 — 4 билета",Positions=[]};
        blank.Fields=blank.Fields.Select((f,i)=>f with {Y=3+i*8,Width=80,Height=7}).ToList();
        for(var i=0;i<4;i++)blank.Positions.Add(new(){X=10,Y=8+i*70,Width=190,Height=70});
        return blank;
    }
    /// <summary>Copies relative first-ticket geometry, preserving each block's sheet position.</summary>
    public static void CopyFirstPlacement(TicketBlank blank)
    {
        if(blank.Legacy is not null||blank.Positions.Count==0)throw new TicketException("Выберите бланк с позициями билетов.");
        var first=blank.Positions[0];var source=FieldsFor(blank,first);
        foreach(var position in blank.Positions)
        {
            position.Fields=source.Select(f=>f with{}).ToList();
        }
    }
    public static void MovePosition(TicketBlank blank,TicketBlankPosition position,double x,double y)
    {
        position.X=Math.Clamp(x,0,Math.Max(0,blank.PageWidth-position.Width));
        position.Y=Math.Clamp(y,0,Math.Max(0,blank.PageHeight-position.Height));
    }
}
