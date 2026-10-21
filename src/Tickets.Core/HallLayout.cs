using System.Globalization;

namespace Tickets.Core;

public sealed class HallRowLayout
{
    public string Label {get;set;}="1";
    public int SeatCount {get;set;}=20;
    public int AisleAfter {get;set;}
    public string FirstSeat {get;set;}="";
}

/// <summary>Builds draft seats once. Subsequent editing and deletion never regenerate their labels or positions.</summary>
public static class HallLayout
{
    public static string Label(string first,int offset)
    {
        first=first.Trim();
        if(first.Contains(','))
        {
            var parts=first.Split(',').Select(x=>x.Trim()).ToArray();
            if(parts.Any(string.IsNullOrWhiteSpace))throw new TicketException("Перечислите подписи через запятую, например А, 1.");
            if(offset<0)throw new TicketException("Смещение номера не может быть отрицательным.");
            return offset<parts.Length-1?parts[offset]:Label(parts[^1],offset-parts.Length+1);
        }
        if(offset<0)throw new TicketException("Смещение номера не может быть отрицательным.");
        if(long.TryParse(first,NumberStyles.None,CultureInfo.InvariantCulture,out var number))
        {
            if(number>long.MaxValue-offset)throw new TicketException("Слишком большой номер.");
            return (number+offset).ToString(CultureInfo.InvariantCulture);
        }
        var upper=first.ToUpperInvariant();
        var alphabet=upper.All(c=>c>='A'&&c<='Z')?"ABCDEFGHIJKLMNOPQRSTUVWXYZ":"АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        if(first.Length==0||upper.Length>8||!upper.All(alphabet.Contains))
            throw new TicketException("Первый номер: число или буквы одного алфавита (например 1, A, А). Произвольные подписи задаются в таблице мест.");
        long value=0;
        foreach(var c in upper)value=value*alphabet.Length+alphabet.IndexOf(c)+1;
        value+=offset;
        var result="";
        while(value>0){value--;result=alphabet[(int)(value%alphabet.Length)]+result;value/=alphabet.Length;}
        return first==first.ToLowerInvariant()?result.ToLowerInvariant():result;
    }

    public sealed record AisleChoice(int After,string Caption);
    public static IReadOnlyList<AisleChoice> AisleChoices(HallRowLayout row,string firstSeat,bool reverse)
    {
        if(row.SeatCount is <1 or >300)throw new TicketException("Количество мест: от 1 до 300.");
        var start=string.IsNullOrWhiteSpace(row.FirstSeat)?firstSeat:row.FirstSeat;
        var labels=Enumerable.Range(0,row.SeatCount).Select(i=>Label(start,i)).ToArray();
        if(reverse)Array.Reverse(labels);
        var choices=new List<AisleChoice>{new(0,"Нет прохода")};
        for(var i=1;i<labels.Length;i++)choices.Add(new(i,$"Между {labels[i-1]} и {labels[i]}"));
        // Keep previously saved edge gaps selectable without changing existing layouts.
        if(row.AisleAfter==row.SeatCount)choices.Add(new(row.SeatCount,$"После {labels[^1]} (край ряда)"));
        return choices;
    }

    public static IReadOnlyList<DraftSeat> Generate(string sector,IReadOnlyList<HallRowLayout> rows,string firstSeat,bool reverse,double y)
    {
        sector=sector.Trim();
        if(sector.Length==0)throw new TicketException("Введите название сектора.");
        if(rows.Count is <1 or >200)throw new TicketException("Количество рядов: от 1 до 200.");
        if(!double.IsFinite(y))throw new TicketException("Некорректная координата ряда.");
        if(rows.Any(r=>string.IsNullOrWhiteSpace(r.Label)||r.SeatCount is <1 or >300||r.AisleAfter<0||r.AisleAfter>r.SeatCount))
            throw new TicketException("Укажите подпись каждого ряда, от 1 до 300 мест и проход от 0 до количества мест.");
        if(rows.Select(r=>r.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=rows.Count)
            throw new TicketException("Подписи рядов в секторе должны различаться.");
        if(rows.Sum(r=>r.SeatCount)>50000)throw new TicketException("Максимум 50 000 мест в одном зале.");
        var result=new List<DraftSeat>();
        for(var r=0;r<rows.Count;r++)
        {
            var row=rows[r];
            var start=string.IsNullOrWhiteSpace(row.FirstSeat)?firstSeat:row.FirstSeat;
            var labels=Enumerable.Range(0,row.SeatCount).Select(i=>Label(start,i)).ToArray();
            if(labels.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=labels.Length)throw new TicketException($"В ряду {row.Label} повторяются подписи мест.");
            for(var c=0;c<row.SeatCount;c++)result.Add(new DraftSeat{Sector=sector,Row=row.Label.Trim(),
                Number=labels[reverse?row.SeatCount-1-c:c],X=c*20+(row.AisleAfter>0&&c>=row.AisleAfter?36:0),Y=y+r*22});
        }
        return result;
    }
}
