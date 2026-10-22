using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;
using Tickets.Core;

namespace Tickets.App;

/// <summary>Uses the same filled source Word grid for preview and printing.</summary>
internal static class SourceFormPreview
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    internal static FlowDocument Document(IssueDocumentInfo info,double? pageWidth=null,Func<XElement,Paragraph>? paragraphFactory=null)
    {
        var xml=InvoiceDocuments.IssueDocumentXml(info);var body=xml.Root!.Element(W+"body")!;
        var width=pageWidth??(info.Kind==IssueDocumentKinds.Waybill?1123:794);var usable=Math.Max(100,width-50);
        var document=new FlowDocument{PageWidth=width,PagePadding=new Thickness(25),ColumnWidth=usable,FontFamily=new FontFamily(info.Kind==IssueDocumentKinds.Contract?"Times New Roman":"Arial"),FontSize=13};
        foreach(var block in body.Elements())
        {
            if(block.Name==W+"p")document.Blocks.Add(paragraphFactory?.Invoke(block)??Paragraph(block,document.FontFamily.Source));
            else if(block.Name==W+"tbl")document.Blocks.Add(Table(block,usable,document.FontFamily.Source,paragraphFactory));
        }
        return document;
    }
    internal sealed class NumberedPaginator(DocumentPaginator source):DocumentPaginator
    {
        public override bool IsPageCountValid=>source.IsPageCountValid;
        public override int PageCount=>source.PageCount;
        public override Size PageSize{get=>source.PageSize;set=>source.PageSize=value;}
        public override IDocumentPaginatorSource Source=>source.Source;
        public override DocumentPage GetPage(int pageNumber)
        {
            var page=source.GetPage(pageNumber);if(page==DocumentPage.Missing)return page;
            var container=new ContainerVisual();container.Children.Add(page.Visual);var overlay=new DrawingVisual();
            using(var drawing=overlay.RenderOpen())drawing.DrawText(new FormattedText((pageNumber+1).ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Times New Roman"),11,Brushes.Black,1),new Point(32,Math.Max(0,page.Size.Height-23)));
            container.Children.Add(overlay);return new DocumentPage(container,page.Size,page.BleedBox,page.ContentBox);
        }
    }
    private static int Number(XElement? element,string attribute,int fallback=0)=>int.TryParse((string?)element?.Attribute(W+attribute),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)?value:fallback;
    internal static Paragraph Paragraph(XElement source,string defaultFont)
    {
        var p=new Paragraph{Margin=new Thickness(0,0,0,Number(source.Element(W+"pPr")?.Element(W+"spacing"),"after",0)/15d)};
        p.TextAlignment=(string?)source.Element(W+"pPr")?.Element(W+"jc")?.Attribute(W+"val") switch{"right"=>TextAlignment.Right,"center"=>TextAlignment.Center,"both"=>TextAlignment.Justify,_=>TextAlignment.Left};
        foreach(var r in source.Descendants(W+"r").Where(r=>r.Ancestors(W+"p").FirstOrDefault()==source))
        {
            var props=r.Element(W+"rPr");var size=Number(props?.Element(W+"sz"),"val",defaultFont=="Times New Roman"?24:20)*2d/3;
            foreach(var node in r.Elements())
            {
                if(node.Name==W+"br")p.Inlines.Add(new LineBreak());
                else if(node.Name==W+"tab")p.Inlines.Add(new Run("    "){Tag="DocxTab"});
                else if(node.Name==W+"t")p.Inlines.Add(new Run(node.Value){FontSize=Math.Max(8,size),FontWeight=props?.Element(W+"b") is not null?FontWeights.Bold:FontWeights.Normal,FontFamily=new FontFamily((string?)props?.Element(W+"rFonts")?.Attribute(W+"ascii")??defaultFont)});
            }
        }
        return p;
    }
    private static Table Table(XElement source,double width,string defaultFont,Func<XElement,Paragraph>? paragraphFactory)
    {
        var grid=source.Element(W+"tblGrid")?.Elements(W+"gridCol").Select(c=>Number(c,"w",1)).ToArray()??[1,1];
        var table=new Table{CellSpacing=0,Margin=new Thickness(0)};
        foreach(var col in grid)table.Columns.Add(new TableColumn{Width=new GridLength(width*col/grid.Sum())});
        var group=new TableRowGroup();table.RowGroups.Add(group);
        var rows=source.Elements(W+"tr").ToArray();
        for(var index=0;index<rows.Length;index++)
        {
            var row=new TableRow();group.Rows.Add(row);var col=0;
            foreach(var cell in rows[index].Elements(W+"tc"))
            {
                var props=cell.Element(W+"tcPr");var span=Number(props?.Element(W+"gridSpan"),"val",1);var merge=props?.Element(W+"vMerge");
                if(merge is not null&&(string?)merge.Attribute(W+"val")!="restart"){col+=span;continue;}
                var rowSpan=1;
                if(merge is not null)
                {
                    for(var next=index+1;next<rows.Length;next++)
                    {
                        var offset=0;XElement? below=null;
                        foreach(var candidate in rows[next].Elements(W+"tc")){if(offset==col){below=candidate;break;}offset+=Number(candidate.Element(W+"tcPr")?.Element(W+"gridSpan"),"val",1);}
                        var continuation=below?.Element(W+"tcPr")?.Element(W+"vMerge");
                        if(continuation is null||(string?)continuation.Attribute(W+"val")=="restart")break;rowSpan++;
                    }
                }
                var result=new TableCell{ColumnSpan=span,RowSpan=rowSpan,Padding=new Thickness(1,0,1,0)};
                var borders=props?.Element(W+"tcBorders");
                if(borders is not null&&borders.Elements().Any(b=>(string?)b.Attribute(W+"val") is not ("nil" or "none"))){result.BorderBrush=Brushes.Black;result.BorderThickness=new Thickness(.4);}
                // A native Word cell can contain paragraphs and nested tables in
                // any order. Keep that order and the same editable factory at
                // every depth, rather than losing nested ordinary text.
                var cellWidth=Math.Max(30,width*grid.Skip(col).Take(span).Sum()/Math.Max(1,grid.Sum()));
                foreach(var block in cell.Elements())
                {
                    if(block.Name==W+"p")result.Blocks.Add(paragraphFactory?.Invoke(block)??Paragraph(block,defaultFont));
                    else if(block.Name==W+"tbl")result.Blocks.Add(Table(block,cellWidth,defaultFont,paragraphFactory));
                }
                if(result.Blocks.Count==0)result.Blocks.Add(new Paragraph());
                row.Cells.Add(result);col+=span;
            }
        }
        return table;
    }
}
