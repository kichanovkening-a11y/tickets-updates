using System.Xml.Linq;
namespace Tickets.Core;

/// <summary>WordprocessingML property groups are ordered sequences, not unordered bags.</summary>
internal static class WordDocumentStructure
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly IReadOnlyDictionary<string,string[]> Orders=new Dictionary<string,string[]>
    {
        ["tcPr"]="cnfStyle tcW gridSpan hMerge vMerge tcBorders shd noWrap tcMar textDirection tcFitText vAlign hideMark headers cellIns cellDel cellMerge tcPrChange".Split(' '),
        ["trPr"]="cnfStyle divId gridBefore gridAfter wBefore wAfter cantSplit trHeight tblHeader tblCellSpacing jc hidden ins del trPrChange".Split(' '),
        ["tblPr"]="tblStyle tblpPr tblOverlap bidiVisual tblStyleRowBandSize tblStyleColBandSize tblW jc tblCellSpacing tblInd tblBorders shd tblLayout tblCellMar tblLook tblCaption tblDescription tblPrChange".Split(' '),
        ["pPr"]="pStyle keepNext keepLines pageBreakBefore framePr widowControl numPr suppressLineNumbers pBdr shd tabs suppressAutoHyphens kinsoku wordWrap overflowPunct topLinePunct autoSpaceDE autoSpaceDN bidi adjustRightInd snapToGrid spacing ind contextualSpacing mirrorIndents suppressOverlap jc textDirection textAlignment textboxTightWrap outlineLvl divId cnfStyle rPr sectPr pPrChange".Split(' '),
        ["rPr"]="rStyle rFonts b bCs i iCs caps smallCaps strike dstrike outline shadow emboss imprint noProof snapToGrid vanish webHidden color spacing w kern position sz szCs highlight u effect bdr shd fitText vertAlign rtl cs em lang eastAsianLayout specVanish oMath rPrChange".Split(' ')
    };
    public static void Normalize(XDocument document)
    {
        foreach(var group in document.Descendants().Where(e=>e.Name.Namespace==W&&Orders.ContainsKey(e.Name.LocalName)).ToArray())
        {
            var order=Orders[group.Name.LocalName];
            var children=group.Elements().Select((element,index)=>(element,index)).OrderBy(pair=>{var index=Array.IndexOf(order,pair.element.Name.LocalName);return index<0?int.MaxValue:index;}).ThenBy(pair=>pair.index).Select(pair=>pair.element).ToArray();
            group.ReplaceNodes(children);
        }
        foreach(var cell in document.Descendants(W+"tc"))if(cell.Elements().LastOrDefault()?.Name!=W+"p")cell.Add(new XElement(W+"p"));
    }
    public static void Validate(XDocument document)
    {
        foreach(var table in document.Descendants(W+"tbl"))
            if(table.Elements().Any(e=>e.Name.Namespace==W&&e.Name.LocalName is not ("tblPr" or "tblGrid" or "tr" or "customXml" or "sdt" or "bookmarkStart" or "bookmarkEnd")))throw new TicketException("Некорректная структура таблицы Word.");
        foreach(var cell in document.Descendants(W+"tc"))if(cell.Elements().LastOrDefault()?.Name!=W+"p")throw new TicketException("Некорректная структура ячейки Word.");
        foreach(var group in document.Descendants().Where(e=>e.Name.Namespace==W&&Orders.ContainsKey(e.Name.LocalName)))
        {
            var order=Orders[group.Name.LocalName];var previous=-1;
            foreach(var child in group.Elements()){var index=Array.IndexOf(order,child.Name.LocalName);if(index<0)continue;if(index<previous)throw new TicketException("Неверный порядок свойств Word: "+group.Name.LocalName);previous=index;}
        }
    }
}
