using System.Globalization;

namespace Tickets.Core;

public static class DocumentTotals
{
    public static string Amount(decimal amount,string groupSeparator=" ")
    {
        var format=(NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberGroupSeparator=groupSeparator;format.NumberDecimalSeparator=",";
        return amount.ToString(amount==decimal.Truncate(amount)?"N0":"N2",format);
    }
    public static string TicketNoun(long count)
    {
        var n=decimal.Abs((decimal)count);
        return n%100 is >=11 and <=14?"билетов":n%10==1?"билет":n%10 is >=2 and <=4?"билета":"билетов";
    }
    public static string Summary(long count,decimal total)=>$"Всего билетов: {count}. Сумма выдачи: {Amount(total)} ₽";
    public static string Total(long count,decimal total)=>$"Итого: {count} {TicketNoun(count)} ({RussianWords.Quantity(count)}) на сумму {Amount(total,".")} ({RussianWords.Money(total)})";
}
