using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Tickets.Core;

internal static class TicketlandXml0281
{
    internal static IReadOnlyList<TicketlandSale028> Parse(string xml,DateTime from,DateTime to)
    {
        if(TicketlandHtml028.IsLogin(xml))throw new TicketException("Ticketland запросил повторный вход. Продажи не изменены.");
        try
        {
            using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20*1024*1024});
            var root=XDocument.Load(reader).Root??throw Invalid();
            string Attr(XElement e,string name)=>e.Attribute(name)?.Value??throw Invalid();
            static IEnumerable<XElement> Children(XContainer e,string name)=>e.Descendants().Where(x=>x.Name.LocalName==name);
            int Count(XElement e,string name)=>int.TryParse(Attr(e,name),NumberStyles.None,CultureInfo.InvariantCulture,out var n)?n:throw Invalid();
            if(root.Name.LocalName!="Report"||Attr(root,"Name")!="(C4at) По вознаграждению со списком билетов")throw Invalid();
            var heading=Attr(root,"ReportTitle3");
            var dates=Regex.Matches(heading,@"\d{2}\.\d{2}\.\d{4}",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
            if(!heading.Contains("за период мероприятий",StringComparison.OrdinalIgnoreCase)||dates.Count!=2
                ||DateTime.ParseExact(dates[0].Value,"dd.MM.yyyy",CultureInfo.InvariantCulture)!=from
                ||DateTime.ParseExact(dates[1].Value,"dd.MM.yyyy",CultureInfo.InvariantCulture)!=to)throw Invalid();
            var totals=Children(root,"Tablix1").ToArray();if(totals.Length!=1)throw Invalid();
            var sold=new Dictionary<string,TicketlandSale028>(StringComparer.Ordinal);
            var returned=new Dictionary<string,TicketlandSale028>(StringComparer.Ordinal);
            var saleCount=0;var returnCount=0;var detailCount=0;
            foreach(var group in Children(root,"idPerformance"))
            {
                var show=Regex.Match(Attr(group,"PerfName"),@"^(?<title>.+?)\s+(?<date>\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}:\d{2})$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
                if(!show.Success||!DateTime.TryParseExact(show.Groups["date"].Value,"dd.MM.yyyy HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var starts)||starts.Date<from||starts.Date>to)throw Invalid();
                var gs=0;var gr=0;
                foreach(var row in Children(group,"Подробности"))
                {
                    detailCount++;
                    // These counterintuitive XML names are verified against the actual C4at
                    // export: SectionName contains the barcode; Barcode contains the sector.
                    var barcode=Attr(row,"SectionName");var sector=Attr(row,"Barcode");var placeRow=Attr(row,"PlaceRow");var place=Attr(row,"PlacePlace");
                    if(barcode.Length is 0 or >512||!barcode.All(char.IsAsciiDigit)||string.IsNullOrWhiteSpace(sector)||string.IsNullOrWhiteSpace(placeRow)||string.IsNullOrWhiteSpace(place))throw Invalid();
                    if(!decimal.TryParse(Attr(row,"Price1"),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var price)||price<0)throw Invalid();
                    var sale=new TicketlandSale028(show.Groups["title"].Value.Trim(),starts,sector,placeRow,place,barcode,price);
                    switch(Attr(row,"Oper1").Trim().ToLowerInvariant())
                    {
                        case "продажа":if(!sold.TryAdd(barcode,sale))throw Invalid();gs++;break;
                        case "возврат":if(!returned.TryAdd(barcode,sale))throw Invalid();gr++;break;
                        default:throw Invalid();
                    }
                }
                if(gs!=Count(group,"Price")||gr!=Count(group,"Price2")||gs-gr!=Count(group,"RetSum"))throw Invalid();
                saleCount+=gs;returnCount+=gr;
            }
            if(detailCount!=Children(root,"Подробности").Count()||saleCount!=Count(totals[0],"SalesSumm4")||returnCount!=Count(totals[0],"SalesSumm6")||saleCount-returnCount!=Count(totals[0],"Oper"))throw Invalid();
            foreach(var (barcode,ret) in returned)
            {
                if(!sold.TryGetValue(barcode,out var sale)||sale.Title!=ret.Title||sale.StartsAt!=ret.StartsAt||sale.Sector!=ret.Sector||sale.Row!=ret.Row||sale.Seat!=ret.Seat||ret.Nominal>sale.Nominal)throw Invalid();
                sold.Remove(barcode);
            }
            return sold.Values.ToArray();
        }
        catch(TicketException){throw;}
        catch{throw Invalid();}
    }
    private static TicketException Invalid()=>new("Ticketland вернул неполный или нераспознанный отчёт текущих сеансов. Продажи не изменены.");
}

public sealed partial class Store
{
    public (DateTime From,DateTime To)? TicketlandPeriod0281(DateTime today)
    {
        var dates=Events().Select(e=>DateTime.TryParse(e.SortDate,CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)?at.Date:DateTime.MinValue).Where(d=>d>=today.Date).ToArray();
        return dates.Length==0?null:(today.Date,dates.Max());
    }
}
