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
        var settings=new PrintDialog();
        settings.PrintTicket.PageOrientation=landscape?PageOrientation.Landscape:PageOrientation.Portrait;
        var window=new Window{Owner=owner,Title="Предварительный просмотр печати · "+title,Width=1060,Height=850,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var root=new DockPanel();window.Content=root;
        var toolbar=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8)};DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
        var printer=new Button{Content="Принтер и параметры…",Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,0)};toolbar.Children.Add(printer);
        var confirm=new Button{Content="Печатать",Padding=new Thickness(20,8,20,8),Margin=new Thickness(0,0,8,0)};toolbar.Children.Add(confirm);
        var marginLabel=new TextBlock{Text="Поля, мм",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)};toolbar.Children.Add(marginLabel);
        var margin=new TextBox{Text="8,5",Width=50,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};toolbar.Children.Add(margin);
        var apply=new Button{Content="Применить поля",Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,0,8,0)};toolbar.Children.Add(apply);
        var cancel=new Button{Content="Закрыть",Padding=new Thickness(12,8,12,8)};toolbar.Children.Add(cancel);cancel.Click+=(_,_)=>window.Close();
        var summary=new TextBlock{Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};DockPanel.SetDock(summary,Dock.Bottom);root.Children.Add(summary);
        var viewer=new DocumentViewer();root.Children.Add(viewer);FixedDocument? pages=null;
        void Rebuild()
        {
            var ticket=settings.PrintTicket;var capability=settings.PrintQueue?.GetPrintCapabilities(ticket);
            var landscapePage=ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape;
            var paper=ticket.PageMediaSize;
            var width=paper?.Width??794d;var height=paper?.Height??1123d;
            if((landscapePage&&width<height)||(!landscapePage&&width>height))(width,height)=(height,width);
            var area=capability?.PageImageableArea;
            if(!double.TryParse(margin.Text.Replace(',','.'),NumberStyles.Number,CultureInfo.InvariantCulture,out var mm)||mm<0||mm>70)throw new InvalidOperationException("Укажите поля от 0 до 70 мм.");
            var requested=mm*96/25.4;
            var left=Math.Max(requested,area?.OriginWidth??0);var top=Math.Max(requested,area?.OriginHeight??0);
            var right=Math.Max(requested,width-(area?.OriginWidth??0)-(area?.ExtentWidth??width));
            var bottom=Math.Max(requested,height-(area?.OriginHeight??0)-(area?.ExtentHeight??height));
            var document=create(width-left-right+50);document.PageWidth=width;document.PageHeight=height;
            document.PagePadding=new Thickness(left,top,right,bottom);document.ColumnWidth=width-left-right;
            var paginator=((IDocumentPaginatorSource)document).DocumentPaginator;paginator.PageSize=new Size(width,height);paginator.ComputePageCount();
            if(numbered)paginator=new SourceFormPreview.NumberedPaginator(paginator);
            pages=new FixedDocument();pages.DocumentPaginator.PageSize=new Size(width,height);
            for(var i=0;i<paginator.PageCount;i++)
            {
                var page=paginator.GetPage(i);var fixedPage=new FixedPage{Width=width,Height=height,Background=Brushes.White};
                fixedPage.Children.Add(new Rectangle{Width=width,Height=height,Fill=new VisualBrush(page.Visual){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top}});
                var content=new PageContent();((System.Windows.Markup.IAddChild)content).AddChild(fixedPage);pages.Pages.Add(content);
            }
            viewer.Document=pages;
            summary.Text=$"{settings.PrintQueue?.FullName??"Принтер по умолчанию"} · {width*25.4/96:0} × {height*25.4/96:0} мм · {(landscapePage?"Альбомная":"Книжная")} · Поля: {left*25.4/96:0}/{top*25.4/96:0}/{right*25.4/96:0}/{bottom*25.4/96:0} мм · Страниц: {pages.Pages.Count}";
        }
        apply.Click+=(_,_)=>{try{Rebuild();}catch(Exception ex){MessageBox.Show(window,ex.Message,"Параметры печати",MessageBoxButton.OK,MessageBoxImage.Warning);}};
        viewer.CommandBindings.Add(new CommandBinding(ApplicationCommands.Print,(_,e)=>{e.Handled=true;confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
        printer.Click+=(_,_)=>{try{if(settings.ShowDialog()==true)Rebuild();}catch(Exception ex){MessageBox.Show(window,ex.Message,"Параметры печати",MessageBoxButton.OK,MessageBoxImage.Warning);}};
        confirm.Click+=(_,_)=>{try{if(pages is null)return;settings.PrintDocument(pages.DocumentPaginator,title);window.Close();}catch(Exception ex){MessageBox.Show(window,ex.Message,"Печать",MessageBoxButton.OK,MessageBoxImage.Warning);}};
        Rebuild();window.ShowDialog();
    }
}
