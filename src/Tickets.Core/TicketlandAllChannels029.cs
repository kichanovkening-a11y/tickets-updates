using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Tickets.Core;

/// <summary>Sub1T exports confirmed ticket operations for an event period across all agent categories.</summary>
internal static class TicketlandAllChannels029
{
    internal const string ReportPath="/Театры/(Sub1T) Актуальные продажи по распространителям и МДТЗК с субагентами";
    internal static IReadOnlyList<TicketlandSale028> Parse(string xml,DateTime from,DateTime to)
    {
        try
        {
            using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20*1024*1024});
            var root=XDocument.Load(reader).Root??throw Invalid();
            static string A(XElement e,string name)=>e.Attribute(name)?.Value??throw Invalid();
            static IEnumerable<XElement> D(XContainer e,string name)=>e.Descendants().Where(x=>x.Name.LocalName==name);
            static int N(XElement e,string name)=>int.TryParse(A(e,name),NumberStyles.None,CultureInfo.InvariantCulture,out var n)?n:throw Invalid();
            if(root.Name.LocalName!="Report"||A(root,"Name")!=ReportPath.Split('/').Last())throw Invalid();
            var heading=A(root,"ReportTitle3");var dates=Regex.Matches(heading,@"\d{2}\.\d{2}\.\d{4}",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
            if(!heading.Contains("за период мероприятий",StringComparison.OrdinalIgnoreCase)||!heading.Contains("По фактической цене",StringComparison.Ordinal)
                ||!heading.Contains("<Все субагенты>",StringComparison.Ordinal)||!heading.Contains("Все зрелища",StringComparison.Ordinal)||dates.Count!=2
                ||DateTime.ParseExact(dates[0].Value,"dd.MM.yyyy",CultureInfo.InvariantCulture)!=from.Date||DateTime.ParseExact(dates[1].Value,"dd.MM.yyyy",CultureInfo.InvariantCulture)!=to.Date)throw Invalid();
            var totals=D(root,"Tablix1").ToArray();if(totals.Length!=1)throw Invalid();
            var sold=new Dictionary<string,TicketlandSale028>(StringComparer.Ordinal);var returned=new Dictionary<string,TicketlandSale028>(StringComparer.Ordinal);
            var saleCount=0;var returnCount=0;var detailCount=0;
            foreach(var group in D(root,"ShowName"))
            {
                var title=A(group,"ShowName").Trim();var venue=A(group,"HallBuildingName").Trim();var channel=A(group,"AgentName").Trim();
                if(title.Length==0||venue.Length==0||channel.Length==0||!DateTime.TryParseExact(A(group,"PerformanceTime"),"yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var starts)||starts.Date<from.Date||starts.Date>to.Date)throw Invalid();
                var gs=0;var gr=0;
                foreach(var row in D(group,"Details"))
                {
                    detailCount++;var barcode=A(row,"Barcode2");var sector=A(row,"SectionName4").Trim();var placeRow=A(row,"PlaceRow2").Trim();var seat=A(row,"PlacePlace2").Trim();
                    if(barcode.Length is 0 or >512||!barcode.All(char.IsAsciiDigit)||sector.Length==0||placeRow.Length==0||seat.Length==0||!decimal.TryParse(A(row,"NonPlaceSumm2"),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var price)||price<0)throw Invalid();
                    var sale=new TicketlandSale028(title,starts,sector,placeRow,seat,barcode,price){Venue=venue,SourceChannel=channel};
                    switch(A(row,"Oper1").Trim().ToLowerInvariant())
                    {
                        case "продажа":if(!sold.TryAdd(barcode,sale))throw Invalid();gs++;break;
                        case "возврат":if(!returned.TryAdd(barcode,sale))throw Invalid();gr++;break;
                        default:throw Invalid();
                    }
                }
                if(gs!=N(group,"ETicketCnt3")||gr!=N(group,"RetAnotherCnt3")||gs-gr!=N(group,"ETicketCnt6"))throw Invalid();saleCount+=gs;returnCount+=gr;
            }
            if(detailCount!=D(root,"Details").Count()||saleCount!=N(totals[0],"ETicketCnt4")||returnCount!=N(totals[0],"RetAnotherCnt4"))throw Invalid();
            foreach(var (barcode,ret) in returned)
            {
                if(!sold.TryGetValue(barcode,out var sale)||sale.Title!=ret.Title||sale.StartsAt!=ret.StartsAt||sale.Venue!=ret.Venue||sale.Sector!=ret.Sector||sale.Row!=ret.Row||sale.Seat!=ret.Seat||ret.Nominal>sale.Nominal)throw Invalid();sold.Remove(barcode);
            }
            static string K(string x)=>CatalogText.Key(x);
            // General-admission gift tickets explicitly carry --/--; barcodes remain unique.
            if(sold.Values.Where(x=>x.Row!="--"||x.Seat!="--").GroupBy(x=>(K(x.Title),x.StartsAt,K(x.Venue),K(x.Sector),K(x.Row),K(x.Seat))).Any(g=>g.Count()>1))throw Invalid();
            return sold.Values.ToArray();
        }
        catch(TicketException){throw;}
        catch{throw Invalid();}
    }
    private static TicketException Invalid()=>new("Ticketland вернул неполный или нераспознанный отчёт продаж по всем каналам. Продажи не изменены.");
}
