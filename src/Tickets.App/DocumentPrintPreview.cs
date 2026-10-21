using System.Printing;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Tickets.App;

/// <summary>Printer settings select the real paper; only confirmation submits the previewed pages.</summary>
internal static class DocumentPrintPreview
{
    internal static void Show(Window owner,string title,Func<double,FlowDocument> create,bool landscape=false,bool numbered=false)
    {
        var margin=new TextBox{Text="8,5",Width=60,Margin=new Thickness(6)};
        var options=new StackPanel{Orientation=Orientation.Horizontal};
        options.Children.Add(new TextBlock{Text="Поля, мм (после изменения нажмите «Обновить просмотр»)",VerticalAlignment=VerticalAlignment.Center});options.Children.Add(margin);
        UnifiedPrintPreview.Show(owner,title,(settings,allowOverflow)=>
        {
            var size=UnifiedPrintPreview.PageSize(settings);var width=size.Width;var height=size.Height;
            var area=settings.PrintQueue?.GetPrintCapabilities(settings.PrintTicket).PageImageableArea;
            if(!double.TryParse(margin.Text.Replace(',','.'),NumberStyles.Number,CultureInfo.InvariantCulture,out var mm)||mm<0||mm>70)throw new InvalidOperationException("Укажите поля от 0 до 70 мм.");
            var requested=mm*96/25.4;
            var left=Math.Max(requested,area?.OriginWidth??0);var top=Math.Max(requested,area?.OriginHeight??0);
            var right=Math.Max(requested,width-(area?.OriginWidth??0)-(area?.ExtentWidth??width));
            var bottom=Math.Max(requested,height-(area?.OriginHeight??0)-(area?.ExtentHeight??height));
            if(width-left-right<80||height-top-bottom<80)throw new InvalidOperationException("Печатная область слишком мала. Уменьшите поля или выберите другую бумагу.");
            var document=create(width-left-right+50);document.PageWidth=width;document.PageHeight=height;
            document.PagePadding=new Thickness(left,top,right,bottom);document.ColumnWidth=width-left-right;
            var paginator=((IDocumentPaginatorSource)document).DocumentPaginator;paginator.PageSize=size;paginator.ComputePageCount();
            if(numbered)paginator=new SourceFormPreview.NumberedPaginator(paginator);
            var pages=new FixedDocument();pages.DocumentPaginator.PageSize=size;
            for(var i=0;i<paginator.PageCount;i++)
            {
                var page=paginator.GetPage(i);var fixedPage=new FixedPage{Width=width,Height=height,Background=Brushes.White};
                fixedPage.Children.Add(new Rectangle{Width=width,Height=height,Fill=new VisualBrush(page.Visual){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top}});
                pages.Pages.Add(new PageContent{Child=fixedPage});
            }
            return pages;
        },(settings,pages)=>settings.PrintDocument(pages.DocumentPaginator,title),landscape,options);
    }
}
