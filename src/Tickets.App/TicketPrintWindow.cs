using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketPrintWindow:EditorWindow
{
    private readonly long eventId;
    private readonly long[] seatIds;
    private readonly string recipient="",series="";
    private readonly long firstNumber;
    private readonly bool draft;
    private InvoiceInfo? issuedSnapshot;
    private readonly StackPanel pages=new(){HorizontalAlignment=HorizontalAlignment.Center};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
    public long IssueId{get;private set;}
    public bool Printed{get;private set;}

    public TicketPrintWindow(Store store,long eventId,IReadOnlyCollection<long> seatIds,string recipient,string series,long firstNumber):base(store,"Печать билетов",1000,850)
    {
        this.eventId=eventId;this.seatIds=seatIds.ToArray();this.recipient=recipient;this.series=series;this.firstNumber=firstNumber;draft=true;
        Initialize(Store.TicketPrintPreview(eventId,this.seatIds,recipient,series,firstNumber));
    }
    public TicketPrintWindow(Store store,long eventId,IReadOnlyCollection<long> seatIds):base(store,"Повторная печать билетов",1000,850)
    {
        this.eventId=eventId;this.seatIds=seatIds.ToArray();issuedSnapshot=Store.TicketReprint(eventId,this.seatIds);Initialize(issuedSnapshot);
    }
    private void Initialize(InvoiceInfo info)
    {
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;
        DockPanel.SetDock(status,Dock.Top);root.Children.Add(status);
        var actions=new StackPanel{Margin=new Thickness(10,0,0,0)};DockPanel.SetDock(actions,Dock.Right);root.Children.Add(actions);
        InvoiceWindow.Square(actions,"Распечатать",Print,Brushes.SeaGreen);
        root.Children.Add(new ScrollViewer{Content=pages,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
        status.Text=draft?"Предпросмотр. Билеты ещё не выданы. Выдача будет сохранена только после подтверждения печати в окне принтера.":"Повторная печать выданных билетов. Серии, номера и цены сохраняются.";
        Preview(info);
    }
    private void Preview(InvoiceInfo info)
    {
        var document=BuildDocument(info,794,1123);pages.Children.Clear();
        for(var i=0;i<document.Pages.Count;i++)pages.Children.Add(new DocumentPageView{DocumentPaginator=document.DocumentPaginator,PageNumber=i,Margin=new Thickness(0,0,0,12)});
    }
    private void Print()
    {
        try
        {
            var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;
            if(draft&&IssueId==0)
            {
                // Recheck the draft after the printer dialog. Issue performs the
                // final transaction check and reads each current selected price.
                var provisional=Store.TicketPrintPreview(eventId,seatIds,recipient,series,firstNumber);
                // Reject unusable paper settings before creating the issuance.
                BuildDocument(provisional,dialog.PrintableAreaWidth,dialog.PrintableAreaHeight).DocumentPaginator.ComputePageCount();
                IssueId=Store.Issue(eventId,seatIds,recipient,series,firstNumber);
                status.Text=$"Выдача № {IssueId} сохранена. При ошибке печати повторная попытка использует те же билеты.";
            }
            issuedSnapshot??=Store.Invoice(eventId,IssueId);
            var current=Store.TicketReprint(eventId,seatIds);
            if(!current.Tickets.Select(t=>t.Id).SequenceEqual(issuedSnapshot.Tickets.Select(t=>t.Id)))
                throw new TicketException("Билеты были возвращены или заменены. Закройте окно и выберите актуальные билеты для повторной печати.");
            var info=issuedSnapshot;Preview(info);
            var document=BuildDocument(info,dialog.PrintableAreaWidth,dialog.PrintableAreaHeight);
            dialog.PrintDocument(document.DocumentPaginator,"Билеты · "+info.Title);Printed=true;
            status.Text=draft?$"Выдача № {IssueId} сохранена. Билеты отправлены на печать.":"Билеты повторно отправлены на печать.";
        }
        catch(Exception ex)
        {
            if(IssueId>0)status.Text=$"Выдача № {IssueId} сохранена. Повторная печать использует те же серии и номера.";
            MessageBox.Show(DialogOwner,ex.Message,"Печать билетов",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }

    internal static FixedDocument BuildDocument(InvoiceInfo invoice,double pageWidth=794,double pageHeight=1123)
    {
        if(!double.IsFinite(pageWidth)||!double.IsFinite(pageHeight)||pageWidth<160||pageHeight<180)
            throw new TicketException("Печатная область принтера слишком мала для билета.");
        const double margin=18,gap=12;
        var columns=pageWidth>=600?2:1;
        var rows=Math.Max(1,(int)((pageHeight-margin*2+gap)/240));
        var width=(pageWidth-margin*2-gap*(columns-1))/columns;
        var height=(pageHeight-margin*2-gap*(rows-1))/rows;
        var perPage=columns*rows;var document=new FixedDocument();
        for(var offset=0;offset<invoice.Tickets.Count;offset+=perPage)
        {
            var page=new FixedPage{Width=pageWidth,Height=pageHeight,Background=Brushes.White};
            for(var index=0;index<perPage&&offset+index<invoice.Tickets.Count;index++)
            {
                var ticket=invoice.Tickets[offset+index];
                var form=new Grid{Margin=new Thickness(10)};
                foreach(var weight in new[]{1.5,1.0,1.0,1.0,1.0,1.0})form.RowDefinitions.Add(new RowDefinition{Height=new GridLength(weight,GridUnitType.Star)});
                void Line(int row,string text,double size,bool bold=false)
                {
                    var label=new TextBlock{Text=text,FontFamily=new FontFamily("Arial"),FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,TextWrapping=TextWrapping.NoWrap};
                    var fit=new Viewbox{Child=label,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
                    Grid.SetRow(fit,row);form.Children.Add(fit);
                }
                Line(0,invoice.Title,20,true);Line(1,invoice.Venue,15);Line(2,invoice.EventDate,16,true);
                Line(3,$"Сектор: {ticket.Sector} · ряд {ticket.Row} · место {ticket.Seat}",16,true);
                Line(4,"Цена: "+InvoiceDocuments.Money(ticket.Amount),16);Line(5,"БИЛЕТ · "+ticket.Code,18,true);
                var card=new Border{Width=width,Height=height,BorderBrush=Brushes.Black,BorderThickness=new Thickness(1),Background=Brushes.White,Child=form};
                FixedPage.SetLeft(card,margin+index%columns*(width+gap));FixedPage.SetTop(card,margin+index/columns*(height+gap));page.Children.Add(card);
            }
            var content=new PageContent{Child=page};document.Pages.Add(content);
        }
        return document;
    }
}
