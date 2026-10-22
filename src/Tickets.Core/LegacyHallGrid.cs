namespace Tickets.Core;

public enum LegacyHallCell { Empty, NumberSeat, LetterSeat, NumberRow, LetterRow }

/// <summary>A temporary typed grid. Edge captions and passages are never sellable seats.</summary>
public sealed class LegacyHallGrid
{
    private readonly LegacyHallCell[,] cells;
    public int Rows{get;}
    public int Places{get;}
    public int Columns=>Places+2;
    public LegacyHallGrid(int rows,int places)
    {
        if(rows is <1 or >200||places is <1 or >300||(long)rows*places>50000)
            throw new TicketException("Сетка: 1–200 рядов, 1–300 мест; максимум 50 000 ячеек мест.");
        Rows=rows;Places=places;cells=new LegacyHallCell[rows,places+2];
        for(var r=0;r<rows;r++){cells[r,0]=LegacyHallCell.NumberRow;for(var c=1;c<=places;c++)cells[r,c]=LegacyHallCell.NumberSeat;}
    }
    public LegacyHallCell Get(int row,int column)=>cells[row,column];
    public void Set(int row,int column,LegacyHallCell kind)
    {
        if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));
        var edge=column==0||column==Columns-1;
        if(edge&&kind is LegacyHallCell.NumberSeat or LegacyHallCell.LetterSeat)return;
        if(!edge&&kind is LegacyHallCell.NumberRow or LegacyHallCell.LetterRow)return;
        cells[row,column]=kind;
        if(edge&&kind is LegacyHallCell.NumberRow or LegacyHallCell.LetterRow)
        {cells[row,0]=kind;cells[row,Columns-1]=kind;}
    }
    public LegacyHallGrid Clone()
    {
        var copy=new LegacyHallGrid(Rows,Places);Array.Copy(cells,copy.cells,cells.Length);return copy;
    }
    public List<DraftSeat> Compile(string sector,string firstRow="1",string firstSeat="1",bool reverse=false)
    {
        if(string.IsNullOrWhiteSpace(sector))throw new TicketException("Введите название сектора.");
        var numberRowStart=NumberStart(firstRow);var letterRowStart=LetterStart(firstRow);
        var numberSeatStart=NumberStart(firstSeat);var letterSeatStart=LetterStart(firstSeat);
        var result=new List<DraftSeat>();var numericRow=0;var letterRow=0;
        for(var r=0;r<Rows;r++)
        {
            var columns=Enumerable.Range(1,Places).Where(c=>cells[r,c] is LegacyHallCell.NumberSeat or LegacyHallCell.LetterSeat).ToArray();
            if(columns.Length==0)continue;
            var rowKind=cells[r,0]==LegacyHallCell.LetterRow||cells[r,Columns-1]==LegacyHallCell.LetterRow?LegacyHallCell.LetterRow:LegacyHallCell.NumberRow;
            var row=HallLayout.Label(rowKind==LegacyHallCell.LetterRow?letterRowStart:numberRowStart,rowKind==LegacyHallCell.LetterRow?letterRow++:numericRow++);
            var number=0;var letter=0;
            foreach(var c in reverse?columns.Reverse():columns)
            {
                var isLetter=cells[r,c]==LegacyHallCell.LetterSeat;
                result.Add(new(){Sector=sector.Trim(),Row=row,Number=HallLayout.Label(isLetter?letterSeatStart:numberSeatStart,isLetter?letter++:number++),X=(c-1)*20,Y=r*22});
            }
        }
        if(result.Count==0)throw new TicketException("В сетке нет мест. Выделите ячейки и задайте места.");
        if(result.GroupBy(s=>(s.Row,s.Number)).Any(g=>g.Count()>1))throw new TicketException("Повторяются подписи рядов или мест. Уточните начало нумерации и типы ячеек.");
        return result.OrderBy(s=>s.Y).ThenBy(s=>s.X).ToList();
    }
    private static string NumberStart(string start)=>TypedStart(start,true);
    private static string LetterStart(string start)=>TypedStart(start,false);
    private static string TypedStart(string start,bool numeric)
    {
        var parts=start.Split(',').Select(s=>s.Trim()).ToArray();
        // Validate every component, including unused/unfinished components, before choosing a type.
        foreach(var part in parts)HallLayout.Label(part,0);
        return parts.FirstOrDefault(part=>long.TryParse(part,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out _)==numeric)
            ??(numeric?"1":"А");
    }
}
