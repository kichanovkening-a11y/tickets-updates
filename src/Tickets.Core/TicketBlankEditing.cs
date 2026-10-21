using System.Text.Json;

namespace Tickets.Core;

/// <summary>Physical millimetre geometry shared by the editor and printing.</summary>
public static class TicketBlankEditing
{
    public static IReadOnlyList<TicketBlankField> FieldsFor(TicketBlank blank,TicketBlankPosition position)=>position.Fields??blank.Fields;
    public static LegacyTicketLayout LayoutFor(TicketBlank blank,TicketBlankPosition position)=>position.Legacy??blank.Legacy??throw new TicketException("Выберите исходный макет билета.");
    public static LegacyTicketLayout CloneLayout(LegacyTicketLayout layout)=>JsonSerializer.Deserialize<LegacyTicketLayout>(JsonSerializer.Serialize(layout))!;
    public static void EnsureIndependentLayouts(TicketBlank blank)
    {
        if(blank.Legacy is null)return;
        foreach(var position in blank.Positions)position.Legacy??=CloneLayout(blank.Legacy);
    }
    public static string BindingName(string binding)=>binding switch
    {
        "theatre_name"=>"Площадка","theatre_adress"=>"Адрес","ticket_number"=>"Серия и номер",
        "ticket_series"=>"Серия","ticket_serial"=>"Номер","perf_name"=>"Мероприятие",
        "perf_date"=>"Дата и время","perf_small_date"=>"Краткая дата и время","event_date"=>"Дата","event_time"=>"Время",
        "ticket_zone"=>"Зона / сектор","ticket_place"=>"Ряд и место","ticket_row"=>"Ряд","ticket_seat"=>"Место",
        "ticket_price"=>"Цена с подписью","ticket_amount"=>"Цена","small_info1"=>"Отрывная часть: номер, цена, дата",
        "small_info2"=>"Отрывная часть: сектор, ряд, место","small_info3"=>"Отрывная часть: мероприятие",
        "PrintDate"=>"Дата печати","PageNumber"=>"Номер страницы",""=>"Постоянный текст",_=>binding
    };
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
    /// <summary>Editable stock ticket with a vertical tear-off and separate constant/data elements.</summary>
    public static TicketBlank CreateA4From030879()
    {
        var blank=Clone(LegacyTicketTemplates.All().Single(b=>System.IO.Path.GetFileNameWithoutExtension(b.Legacy!.SourceFile)=="030879"));
        blank.Name="А4 — 4 билета с отрывной частью";
        var layout=blank.Legacy!;
        foreach(var band in layout.Bands)
        {
            var fields=new List<LegacyTicketElement>();
            foreach(var field in band.Elements)
            {
                (string Binding,string Text,double Share)[]? parts=field.Binding switch
                {
                    "ticket_number"=>[("ticket_series","",.25),(""," № ",.15),("ticket_serial","",.6)],
                    "perf_date"=>[("","Дата: ",.18),("event_date","",.37),(""," Время: ",.22),("event_time","",.23)],
                    "ticket_place"=>[("","Ряд: ",.18),("ticket_row","",.22),(""," Место: ",.25),("ticket_seat","",.35)],
                    "ticket_price"=>[("","Цена: ",.28),("ticket_amount","",.55),(""," руб.",.17)],
                    "small_info1"=>[("ticket_series","",.12),(""," № ",.07),("ticket_serial","",.17),(""," Цена: ",.12),("ticket_amount","",.12),("","р. ",.09),("event_date","",.18),(""," ",.01),("event_time","",.12)],
                    "small_info2"=>[("ticket_zone","",.32),(""," Ряд: ",.18),("ticket_row","",.12),(""," Место: ",.22),("ticket_seat","",.16)],
                    _=>null
                };
                if(parts is null){fields.Add(field);continue;}
                var offset=0.0;var partIndex=0;
                foreach(var part in parts)
                {
                    var vertical=field.Rotation is 90 or 270;
                    // A 90-degree text line reads bottom-to-top after Rotate(-90).
                    var verticalOffset=field.Rotation==90?1-offset-part.Share:offset;
                    fields.Add(field with{Name=field.Name+"_"+(++partIndex),Binding=part.Binding,Text=part.Text,
                        X=field.X+(vertical?0:field.Width*offset),Y=field.Y+(vertical?field.Height*Math.Max(0,verticalOffset):0),
                        Width=vertical?field.Width:field.Width*part.Share,Height=vertical?field.Height*part.Share:field.Height});
                    offset+=part.Share;
                }
            }
            band.Elements=fields;
        }
        EnsureFourUp(blank);
        for(var index=0;index<blank.Positions.Count;index++)
        {
            var position=blank.Positions[index];position.X=5;position.Y=4+index*FourUpTicketLayouts.SlotHeight;position.Width=200;position.Height=FourUpTicketLayouts.SlotHeight-8;
        }
        EnsureIndependentLayouts(blank);blank.Validate();return blank;
    }
    /// <summary>Copies relative first-ticket geometry, preserving each block's sheet position.</summary>
    public static void CopyFirstPlacement(TicketBlank blank)
    {
        if(blank.Positions.Count==0)throw new TicketException("Выберите бланк с позициями билетов.");
        var first=blank.Positions[0];
        if(blank.Legacy is not null)
        {
            var layout=LayoutFor(blank,first);
            foreach(var position in blank.Positions)position.Legacy=CloneLayout(layout);
            return;
        }
        var source=FieldsFor(blank,first);
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
