namespace Tickets.Core;

/// <summary>Russian cardinal numbers and agreement for document totals.</summary>
public static class RussianWords
{
    private static readonly string[] Male=["", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"];
    private static readonly string[] Female=["", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"];
    private static readonly string[] Teens=["десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"];
    private static readonly string[] Tens=["", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"];
    private static readonly string[] Hundreds=["", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"];
    private static readonly string[][] Scales=[[], ["тысяча", "тысячи", "тысяч"], ["миллион", "миллиона", "миллионов"], ["миллиард", "миллиарда", "миллиардов"], ["триллион", "триллиона", "триллионов"], ["квадриллион", "квадриллиона", "квадриллионов"], ["квинтиллион", "квинтиллиона", "квинтиллионов"], ["секстиллион", "секстиллиона", "секстиллионов"], ["септиллион", "септиллиона", "септиллионов"], ["октиллион", "октиллиона", "октиллионов"]];

    public static string Number(long value)=>Number((decimal)value,false);
    public static string Number(decimal value)=>Number(value,false);
    public static string Quantity(long value)=>Number(value)+" "+Form(Magnitude(value),"билет","билета","билетов");
    public static string Money(decimal value)
    {
        var rounded=decimal.Round(value,2,MidpointRounding.AwayFromZero);
        var absolute=decimal.Abs(rounded);
        var rubles=decimal.Truncate(absolute);
        var kopecks=(long)((absolute-rubles)*100);
        return (rounded<0?"минус ":"")+Number(rubles)+" "+Form(rubles,"рубль","рубля","рублей")+" "+Number(kopecks,true)+" "+Form(kopecks,"копейка","копейки","копеек");
    }
    private static ulong Magnitude(long value)=>value<0?(ulong)(-(value+1))+1:(ulong)value;
    private static string Number(decimal value,bool feminine)
    {
        value=decimal.Truncate(value);
        if(value==0)return "ноль";
        var number=decimal.Abs(value);
        var groups=new List<string>();
        for(var scale=0;number>0;scale++,number=decimal.Truncate(number/1000))
        {
            var part=(int)(number%1000);
            if(part==0)continue;
            var words=new List<string>();
            if(part>=100)words.Add(Hundreds[part/100]);
            var remainder=part%100;
            if(remainder is >=10 and <20)words.Add(Teens[remainder-10]);
            else
            {
                if(remainder>=20)words.Add(Tens[remainder/10]);
                if(remainder%10>0)words.Add((scale==1||scale==0&&feminine?Female:Male)[remainder%10]);
            }
            if(scale>0)words.Add(Form((ulong)part,Scales[scale][0],Scales[scale][1],Scales[scale][2]));
            groups.Insert(0,string.Join(" ",words));
        }
        return (value<0?"минус ":"")+string.Join(" ",groups);
    }
    private static string Form(decimal value,string one,string few,string many)=>value%100 is >=11 and <=14?many:value%10==1?one:value%10 is >=2 and <=4?few:many;
}
