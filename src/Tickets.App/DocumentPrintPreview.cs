using System.Printing;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Tickets.Core;

namespace Tickets.App;

/// <summary>Printer settings select the real paper; only confirmation submits the previewed pages.</summary>
internal static class DocumentPrintPreview
{
    internal static void Show(Window owner,string title,IssueDocumentInfo info)=>Show(owner,title,DocumentPagePreview.Bytes(info),width=>IssueDocumentWindow.Document(info,width),info.Kind==IssueDocumentKinds.Waybill,info.Kind==IssueDocumentKinds.Contract);
    internal static void Show(Window owner,string title,byte[] source,Func<double,FlowDocument> fallback,bool landscape=false,bool numbered=false)
    {
        if(ExcelTtnData.IsRequest(source))
        {
            if(!WordDocumentPages.IsAvailable(source))throw new TicketException("Для печати ТТН по исходной форме установите Microsoft Excel.");
            UnifiedPrintPreview.Show(owner,title,(_,_)=>new FixedDocument(),(settings,pages)=>settings.PrintDocument(pages.DocumentPaginator,title),true,
                preferredMedia:new Size(210*96/25.4,297*96/25.4),asyncBuild:async settings=>
                {
                    if(settings.PrintTicket.PageOrientation!=PageOrientation.Landscape)throw new TicketException("Исходная форма ТТН печатается в альбомной ориентации.");
                    var paper=UnifiedPrintPreview.PageSize(settings);
                    if(Math.Abs(paper.Width-297*96/25.4)>2||Math.Abs(paper.Height-210*96/25.4)>2)throw new TicketException("Для исходной формы ТТН выберите бумагу A4 (297 × 210 мм).");
                    var path=await WordDocumentPages.RenderAsync(source);return new WordPageLease(path).Pages;
                });
            return;
        }
        if(!WordDocumentPages.Available){Show(owner,title+" · просмотр без Word",fallback,landscape,numbered);return;}
        var layout=DocxPageLayout.Read(source);
        var options=new WrapPanel();options.Children.Add(new TextBlock{Text="Поля, мм:",VerticalAlignment=VerticalAlignment.Center});
        TextBox Margin(string caption,double value)
        {
            options.Children.Add(new TextBlock{Text=caption,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,0,0)});
            var box=new TextBox{Text=(value*25.4/96).ToString("0.##",CultureInfo.GetCultureInfo("ru-RU")),Width=58,Margin=new Thickness(4)};options.Children.Add(box);return box;
        }
        var left=Margin("слева",layout.Margins.Left);var top=Margin("сверху",layout.Margins.Top);var right=Margin("справа",layout.Margins.Right);var bottom=Margin("снизу",layout.Margins.Bottom);
        double Read(TextBox box)
        {
            if(!double.TryParse(box.Text.Replace(',','.'),NumberStyles.Number,CultureInfo.InvariantCulture,out var mm)||mm<0||mm>70)throw new TicketException("Укажите поля от 0 до 70 мм.");
            return mm*96/25.4;
        }
        var debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};Action? refresh=null;
        debounce.Tick+=(_,_)=>{debounce.Stop();refresh?.Invoke();};
        foreach(var box in new[]{left,top,right,bottom})box.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
        try
        {
            UnifiedPrintPreview.Show(owner,title,(_,_)=>new FixedDocument(),(settings,pages)=>settings.PrintDocument(pages.DocumentPaginator,title),layout.Landscape,options,
                new Size(Math.Min(layout.Paper.Width,layout.Paper.Height),Math.Max(layout.Paper.Width,layout.Paper.Height)),action=>refresh=action,async settings=>
                {
                    var margins=new Thickness(Read(left),Read(top),Read(right),Read(bottom));var size=UnifiedPrintPreview.PageSize(settings);
                    if(size.Width-margins.Left-margins.Right<80||size.Height-margins.Top-margins.Bottom<80)throw new TicketException("Печатная область слишком мала. Уменьшите поля или выберите другую бумагу.");
                    var bytes=layout.Apply(source,size,margins);var path=await WordDocumentPages.RenderAsync(bytes);return new WordPageLease(path).Pages;
                });
        }
        finally{debounce.Stop();}
    }
    internal static void Route(UIElement target,Action print)
    {
        // FlowDocumentScrollViewer handles Ctrl+P with its own native print dialog.
        // Intercept the routed command before that class handler can run.
        CommandManager.AddPreviewExecutedHandler(target,(_,e)=>{
            if(e.Command!=ApplicationCommands.Print)return;e.Handled=true;print();
        });
        target.PreviewKeyDown+=(_,e)=>{
            if(e.Key!=Key.P||Keyboard.Modifiers!=ModifierKeys.Control)return;
            e.Handled=true;print();
        };
    }
    internal static void Show(Window owner,string title,Func<double,FlowDocument> create,bool landscape=false,bool numbered=false)
    {
        var margin=new TextBox{Text="8,5",Width=60,Margin=new Thickness(6)};
        var options=new StackPanel{Orientation=Orientation.Horizontal};
        options.Children.Add(new TextBlock{Text="Поля, мм",VerticalAlignment=VerticalAlignment.Center});options.Children.Add(margin);
        var debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};
        Action? refresh=null;
        debounce.Tick+=(_,_)=>{debounce.Stop();if(double.TryParse(margin.Text.Replace(',','.'),NumberStyles.Number,CultureInfo.InvariantCulture,out var value)&&value is >=0 and <=70)refresh?.Invoke();};
        margin.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
        try{UnifiedPrintPreview.Show(owner,title,(settings,allowOverflow)=>
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
        },(settings,pages)=>settings.PrintDocument(pages.DocumentPaginator,title),landscape,options,refreshReady:action=>refresh=action);}
        finally{debounce.Stop();}
    }
}
