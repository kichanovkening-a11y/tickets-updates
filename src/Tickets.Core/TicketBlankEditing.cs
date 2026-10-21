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
    public static void EnsureFourUp(TicketBlank blank)
    {
        if(blank.FourUpA4)return;
        if(blank.Legacy is null&&blank.Positions.Count==4&&Math.Abs(blank.PageWidth-210)<.001&&Math.Abs(blank.PageHeight-297)<.001){blank.FourUpA4=true;return;}
        var source=blank.Positions.ToArray();var positions=new List<TicketBlankPosition>();
        for(var index=0;index<4;index++)
        {
            var position=new TicketBlankPosition{X=0,Y=index*FourUpTicketLayouts.SlotHeight,Width=210,Height=FourUpTicketLayouts.SlotHeight};
            if(blank.Legacy is null)
            {
                var fields=source.Length>0?FieldsFor(blank,source[index%source.Length]):blank.Fields;
                var enabled=fields.Where(f=>f.Enabled).ToArray();
                var scale=enabled.Length==0?1:Math.Min(1,Math.Min(210/enabled.Max(f=>f.X+f.Width),FourUpTicketLayouts.SlotHeight/enabled.Max(f=>f.Y+f.Height)));
                if(source.Length>0&&source[index%source.Length].Fields is null&&scale==1)position.Fields=null;
                else position.Fields=fields.Select(f=>f with{X=f.X*scale,Y=f.Y*scale,Width=f.Width*scale,Height=f.Height*scale,FontSize=Math.Max(4,f.FontSize*scale)}).ToList();
            }
            positions.Add(position);
        }
        blank.PageWidth=210;blank.PageHeight=297;blank.Positions=positions;blank.FourUpA4=true;
    }
    public static TicketBlank CreateA4()
    {
        var blank=new TicketBlank {Name="А4 — 4 билета",FourUpA4=true,Positions=[]};
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
