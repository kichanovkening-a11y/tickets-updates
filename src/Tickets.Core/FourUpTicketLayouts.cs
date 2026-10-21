namespace Tickets.Core;

/// <summary>One physical A4 sheet always has four ticket positions.</summary>
public static class FourUpTicketLayouts
{
    public const double PageWidth=210,PageHeight=297,SlotHeight=PageHeight/4;
    public static IReadOnlyList<LegacyTicketPage> Paginate(TicketBlank blank,int ticketCount)
    {
        if(ticketCount<=0)return [];
        if(blank.Legacy is not {} layout)throw new TicketException("Выберите исходный макет билета.");
        blank.Validate();
        var source=new List<(LegacyTicketElement Element,double X,double Y)>();var top=layout.TopMargin;
        foreach(var kind in new[]{"ReportHeader","PageHeader","Detail","ReportFooter","PageFooter"})
        foreach(var band in layout.Bands.Where(b=>!b.Suppressed&&b.Kind==kind))
        {
            var fields=band.Elements.Where(e=>!e.Suppressed).ToArray();if(fields.Length==0)continue;
            foreach(var field in fields)source.Add((field,layout.LeftMargin+field.X,top+field.Y));
            top+=Math.Max(band.Height,fields.Max(f=>f.Y+f.Height));
        }
        if(source.Count==0)throw new TicketException("В макете нет печатаемых полей.");
        var sourceWidth=source.Max(f=>f.X+f.Element.Width);var sourceHeight=source.Max(f=>f.Y+f.Element.Height);
        var pages=new List<LegacyTicketPage>();
        for(var offset=0;offset<ticketCount;offset+=4)
        {
            var elements=new List<LegacyTicketPlacement>();
            for(var index=0;index<4&&offset+index<ticketCount;index++)
            {
                var position=blank.Positions[index];
                var normal=Math.Min(1,Math.Min(position.Width/sourceWidth,position.Height/sourceHeight));
                var rotated=Math.Min(1,Math.Min(position.Width/sourceHeight,position.Height/sourceWidth));
                var quarterTurn=rotated>normal+.000001;var scale=quarterTurn?rotated:normal;
                foreach(var item in source)
                {
                    var field=item.Element;var x=quarterTurn?item.Y:item.X;
                    var y=quarterTurn?sourceWidth-item.X-field.Width:item.Y;
                    var rendered=field with{Width=(quarterTurn?field.Height:field.Width)*scale,Height=(quarterTurn?field.Width:field.Height)*scale,
                        FontSize=field.FontSize*scale,Rotation=(field.Rotation+(quarterTurn?90:0))%360};
                    elements.Add(new(rendered,position.X+x*scale,position.Y+y*scale,offset+index,pages.Count+1,field,scale,quarterTurn));
                }
            }
            pages.Add(new(elements));
        }
        return pages;
    }
}
