using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketPrintWindow:EditorWindow
{
    private readonly long eventId;
    private readonly long[] seatIds;
    private readonly long[] draftSeatIds=[];
    private InvoiceInfo? originalOccupied;
    private readonly string recipient="",series="";
    private readonly long firstNumber;
    private readonly bool draft;
    private InvoiceInfo? issuedSnapshot;
    private readonly StackPanel pages=new(){HorizontalAlignment=HorizontalAlignment.Center};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
    private readonly ComboBox blankChoice=new(){MinWidth=260,DisplayMemberPath="Name",Margin=new Thickness(8,0,8,0)};
    private InvoiceInfo? previewInfo;
    private TicketBlank? SelectedBlank=>blankChoice.SelectedItem as TicketBlank;
    public long IssueId{get;private set;}
    public bool Printed{get;private set;}

    public TicketPrintWindow(Store store,long eventId,IReadOnlyCollection<long> seatIds,string recipient,string series,long firstNumber):base(store,"Печать билетов",1000,850)
    {
        this.eventId=eventId;this.seatIds=seatIds.ToArray();this.recipient=recipient;this.series=series;this.firstNumber=firstNumber;draft=true;
        var selected=Store.Seats(eventId).Where(s=>this.seatIds.Contains(s.Id)).ToArray();
        if(selected.Length!=this.seatIds.Length)throw new TicketException("Место не найдено в этом показе.");
        draftSeatIds=selected.Where(s=>!s.TicketId.HasValue).Select(s=>s.Id).ToArray();
        var occupied=selected.Where(s=>s.TicketId.HasValue).Select(s=>s.Id).ToArray();
        if(occupied.Length>0)originalOccupied=Store.TicketReprint(eventId,occupied);
        if(draftSeatIds.Length==0)throw new TicketException("Для ранее выданных мест откройте печать существующих билетов.");
        Initialize(Combine(Store.TicketPrintPreview(eventId,draftSeatIds,recipient,series,firstNumber),originalOccupied));
    }
    public TicketPrintWindow(Store store,long eventId,IReadOnlyCollection<long> seatIds):base(store,"Повторная печать билетов",1000,850)
    {
        this.eventId=eventId;this.seatIds=seatIds.ToArray();issuedSnapshot=Store.TicketReprint(eventId,this.seatIds);Initialize(issuedSnapshot);
    }
    private void Initialize(InvoiceInfo info)
    {
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;
        var templateBar=new WrapPanel{Margin=new Thickness(0,0,0,10)};
        templateBar.Children.Add(new TextBlock{Text="На каком бланке печатать:",VerticalAlignment=VerticalAlignment.Center});
        blankChoice.Items.Add(new {Name="Стандартный"});foreach(var blank in Store.TicketBlanks())blankChoice.Items.Add(blank);blankChoice.SelectedIndex=0;
        templateBar.Children.Add(blankChoice);var manage=new Button{Content="Бланки печати"};templateBar.Children.Add(manage);
        manage.Click+=(_,_)=>Try(()=>{new TicketBlankWindow(Store).ShowDialog();var id=SelectedBlank?.Id;while(blankChoice.Items.Count>1)blankChoice.Items.RemoveAt(1);foreach(var blank in Store.TicketBlanks())blankChoice.Items.Add(blank);blankChoice.SelectedItem=blankChoice.Items.OfType<TicketBlank>().FirstOrDefault(b=>b.Id==id)??blankChoice.Items[0];if(previewInfo is not null)Preview(previewInfo);});
        blankChoice.SelectionChanged+=(_,_)=>Try(()=>{if(previewInfo is not null)Preview(previewInfo);});
        DockPanel.SetDock(templateBar,Dock.Top);root.Children.Add(templateBar);
        DockPanel.SetDock(status,Dock.Top);root.Children.Add(status);
        var actions=new StackPanel{Margin=new Thickness(10,0,0,0)};DockPanel.SetDock(actions,Dock.Right);root.Children.Add(actions);
        InvoiceWindow.Square(actions,"Распечатать",Print,Brushes.SeaGreen);
        root.Children.Add(new ScrollViewer{Content=pages,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
        status.Text=draft?"Предпросмотр. Новые билеты будут выданы после подтверждения печати. Ранее выданные билеты останутся в исходных накладных.":"Печать билетов исходной накладной. Серии, номера, цены и привязка к накладной сохраняются.";
        Preview(info);
    }
    private void Preview(InvoiceInfo info)
    {
        previewInfo=info;
        var document=BuildWithSources(info,SelectedBlank is {} b?Mm(b.PageWidth):794,SelectedBlank is {} b2?Mm(b2.PageHeight):1123);pages.Children.Clear();
        for(var i=0;i<document.Pages.Count;i++)pages.Children.Add(new DocumentPageView{DocumentPaginator=document.DocumentPaginator,PageNumber=i,Margin=new Thickness(0,0,0,12)});
    }
    private void Print()
    {
        var submitted=false;
        try
        {
            var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;
            if(draft&&IssueId==0)
            {
                // Recheck the draft after the printer dialog. Issue performs the
                // final transaction check and reads each current selected price.
                if(originalOccupied is not null)ValidateSnapshot(originalOccupied);
                var provisional=Combine(Store.TicketPrintPreview(eventId,draftSeatIds,recipient,series,firstNumber),originalOccupied);
                // Reject unusable paper settings before creating the issuance.
                BuildWithSources(provisional,dialog.PrintableAreaWidth,dialog.PrintableAreaHeight,dialog).DocumentPaginator.ComputePageCount();
                IssueId=Store.Issue(eventId,draftSeatIds,recipient,series,firstNumber);
                issuedSnapshot=Combine(Store.Invoice(eventId,IssueId),originalOccupied);
                status.Text=$"Выдача № {IssueId} сохранена. При ошибке печати повторная попытка использует те же билеты.";
            }
            issuedSnapshot??=Combine(Store.Invoice(eventId,IssueId),originalOccupied);
            var current=Store.TicketReprint(eventId,seatIds);
            if(!current.Tickets.OrderBy(t=>t.SeatId).Select(t=>t.Id).SequenceEqual(issuedSnapshot.Tickets.OrderBy(t=>t.SeatId).Select(t=>t.Id)))
                throw new TicketException("Билеты были возвращены или заменены. Закройте окно и выберите актуальные билеты для повторной печати.");
            var info=issuedSnapshot;Preview(info);
            var document=BuildWithSources(info,dialog.PrintableAreaWidth,dialog.PrintableAreaHeight,dialog);
            dialog.PrintDocument(document.DocumentPaginator,"Билеты · "+info.Title);submitted=true;Printed=true;
            Store.MarkTicketsPrinted(eventId,info.Tickets.Select(t=>t.Id).ToArray());Printed=true;
            status.Text=draft?$"Выдача № {IssueId} сохранена. Билеты отправлены на печать.":"Билеты повторно отправлены на печать.";
        }
        catch(Exception ex)
        {
            if(submitted){status.Text="Задание уже отправлено принтеру. Не удалось отметить печать в базе: "+ex.Message;MessageBox.Show(DialogOwner,status.Text+" Проверьте очередь принтера перед повторной печатью.","Состояние печати",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
            if(IssueId>0)status.Text=$"Выдача № {IssueId} сохранена. Повторная печать использует те же серии и номера.";
            MessageBox.Show(DialogOwner,ex.Message,"Печать билетов",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }

    private static InvoiceInfo Combine(InvoiceInfo fresh,InvoiceInfo? occupied)
        =>occupied is null?fresh:fresh with{Tickets=fresh.Tickets.Concat(occupied.Tickets).ToArray()};
    private void ValidateSnapshot(InvoiceInfo snapshot)
    {
        var actual=Store.TicketReprint(eventId,snapshot.Tickets.Select(t=>t.SeatId).ToArray());
        if(!actual.Tickets.Select(t=>t.Id).SequenceEqual(snapshot.Tickets.Select(t=>t.Id)))
            throw new TicketException("Ранее выбранные билеты возвращены или заменены. Откройте актуальные билеты заново.");
    }

    private FixedDocument BuildWithSources(InvoiceInfo invoice,double pageWidth,double pageHeight,PrintDialog? printer=null)
    {
        var headers=new Dictionary<long,InvoiceInfo>();
        foreach(var issue in invoice.Tickets.Where(t=>t.IssueId>0).GroupBy(t=>t.IssueId))
        {var original=Store.Invoice(eventId,issue.Key);foreach(var ticket in issue)headers[ticket.Id]=original;}
        return SelectedBlank is {} blank?BuildBlankDocument(invoice,blank,pageWidth,pageHeight,headers,printer):BuildDocumentCore(invoice,pageWidth,pageHeight,headers);
    }
    private const double DipsPerMm=96.0/25.4;
    private static double Mm(double value)=>value*DipsPerMm;
    internal static string BlankSample(string key)=>key switch {"event"=>"Название мероприятия","venue"=>"Название площадки","date"=>"21.10.2026 19:00","sector"=>"Партер","row"=>"Ряд 5","seat"=>"Место 12","price"=>"1 500 ₽","number"=>"АБ 000001",_=>key};
    private FixedDocument BuildBlankDocument(InvoiceInfo invoice,TicketBlank blank,double printableWidth,double printableHeight,IReadOnlyDictionary<long,InvoiceInfo> headers,PrintDialog? printer)
    {
        blank.Validate();var width=Mm(blank.PageWidth);var height=Mm(blank.PageHeight);
        var originX=0.0;var originY=0.0;var mediaWidth=printableWidth;var mediaHeight=printableHeight;
        if(printer is not null)
        {
            var capabilities=printer.PrintQueue.GetPrintCapabilities(printer.PrintTicket);
            var area=capabilities.PageImageableArea;
            if(area is not null){originX=area.OriginWidth;originY=area.OriginHeight;printableWidth=area.ExtentWidth;printableHeight=area.ExtentHeight;}
            mediaWidth=capabilities.OrientedPageMediaWidth??mediaWidth;
            mediaHeight=capabilities.OrientedPageMediaHeight??mediaHeight;
        }
        if(!double.IsFinite(printableWidth)||!double.IsFinite(printableHeight)||!double.IsFinite(mediaWidth)||!double.IsFinite(mediaHeight)||(blank.Legacy is null&&(width>mediaWidth+0.5||height>mediaHeight+0.5)))
            throw new TicketException($"Бланк «{blank.Name}» ({blank.PageWidth} × {blank.PageHeight} мм) не помещается на бумагу принтера ({mediaWidth/DipsPerMm:0.#} × {mediaHeight/DipsPerMm:0.#} мм). Выберите подходящую бумагу и ориентацию в настройках принтера.");
        if(printer is not null)
            foreach(var position in blank.Positions)foreach(var field in blank.Fields.Where(f=>f.Enabled))
            {
                var x=Mm(position.X+field.X);var y=Mm(position.Y+field.Y);
                if(x<originX-0.5||y<originY-0.5||x+Mm(field.Width)>originX+printableWidth+0.5||y+Mm(field.Height)>originY+printableHeight+0.5)
                    throw new TicketException($"Поле «{field.DisplayName}» выходит за печатную область принтера. Переместите поле или позицию билета внутрь листа в разделе «Бланки печати». Непечатаемые поля слева и сверху: {originX/DipsPerMm:0.#} и {originY/DipsPerMm:0.#} мм.");
            }
        if(blank.Legacy is not null)return BuildLegacyDocument(invoice,blank,headers,printer is null?width:mediaWidth,printer is null?height:mediaHeight,printer is null?null:new Rect(originX,originY,printableWidth,printableHeight));
        BitmapImage? background=null;
        if(blank.PrintBackground){var path=Store.TicketBlankScanPath(blank.ScanPath);if(!System.IO.File.Exists(path))throw new TicketException("Скан бланка не найден. Загрузите его заново в разделе «Бланки печати». ");background=new BitmapImage(new Uri(path));}
        var document=new FixedDocument();
        for(var offset=0;offset<invoice.Tickets.Count;offset+=blank.Positions.Count)
        {
            // FixedPage/XPS coordinates start at the physical sheet edge. Hardware
            // margins constrain fields but must never shift calibrated coordinates.
            var page=new FixedPage{Width=printer is null?width:mediaWidth,Height=printer is null?height:mediaHeight,Background=Brushes.White};
            if(background is not null){var image=new Image{Source=background,Width=width,Height=height,Stretch=Stretch.Fill};FixedPage.SetLeft(image,0);FixedPage.SetTop(image,0);page.Children.Add(image);}
            for(var i=0;i<blank.Positions.Count&&offset+i<invoice.Tickets.Count;i++)
            {
                var ticket=invoice.Tickets[offset+i];var source=headers.TryGetValue(ticket.Id,out var original)?original:invoice;var position=blank.Positions[i];
                foreach(var field in blank.Fields.Where(f=>f.Enabled))
                {
                    var text=field.Key switch {"event"=>source.Title,"venue"=>source.Venue,"date"=>source.EventDate,"sector"=>ticket.Sector,"row"=>ticket.Row,"seat"=>ticket.Seat,"price"=>InvoiceDocuments.Money(ticket.Amount),"number"=>ticket.Code,_=>""};
                    var label=new TextBlock{Text=text,FontFamily=new FontFamily("Arial"),FontSize=field.FontSize*96/72,TextWrapping=TextWrapping.NoWrap};
                    var fit=new Viewbox{Child=label,Width=Mm(field.Width),Height=Mm(field.Height),Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left};
                    FixedPage.SetLeft(fit,Mm(position.X+field.X));FixedPage.SetTop(fit,Mm(position.Y+field.Y));page.Children.Add(fit);
                }
            }
            document.Pages.Add(new PageContent{Child=page});
        }
        return document;
    }
    internal static string LegacySample(string binding)
    {
        var ticket=new TicketInfo(1,1,"Партер","5","12","Получатель","АБ",1,1500,"2026-10-21T12:00:00Z",null);
        var invoice=new InvoiceInfo(1,1,"Название мероприятия","Название площадки","21.10.2026 19:00","Получатель","2026-10-21T12:00:00Z",[ticket]);
        return LegacyTicketTemplates.Value(binding,invoice,ticket,"Адрес площадки",new DateTime(2026,10,21),1);
    }
    internal static FrameworkElement LegacyElementVisual(LegacyTicketElement field,string text,double scale)
    {
        var rotated=field.Rotation is 90 or 270;
        var label=new TextBlock
        {
            Text=text,Width=(rotated?field.Height:field.Width)*scale,Height=(rotated?field.Width:field.Height)*scale,
            FontFamily=new FontFamily(field.FontFamily),FontSize=field.FontSize*96/72*scale/DipsPerMm,
            FontWeight=field.Bold?FontWeights.Bold:FontWeights.Normal,FontStyle=field.Italic?FontStyles.Italic:FontStyles.Normal,
            Foreground=new SolidColorBrush(Color.FromArgb((byte)(field.Argb>>24),(byte)(field.Argb>>16),(byte)(field.Argb>>8),(byte)field.Argb)),
            TextWrapping=TextWrapping.Wrap,TextAlignment=field.Alignment switch {"CenterAlign"=>TextAlignment.Center,"RightAlign"=>TextAlignment.Right,"JustifiedAlign"=>TextAlignment.Justify,_=>TextAlignment.Left},
            HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top
        };
                label.TextDecorations=new TextDecorationCollection();
        if(field.Underline)foreach(var decoration in TextDecorations.Underline)label.TextDecorations.Add(decoration);
        if(field.Strikeout)foreach(var decoration in TextDecorations.Strikethrough)label.TextDecorations.Add(decoration);
        if(field.Rotation!=0)label.LayoutTransform=new RotateTransform(-field.Rotation);
        return new Border{Width=field.Width*scale,Height=field.Height*scale,ClipToBounds=true,Child=label};
    }
    private FixedDocument BuildLegacyDocument(InvoiceInfo invoice,TicketBlank blank,IReadOnlyDictionary<long,InvoiceInfo> headers,double mediaWidth,double mediaHeight,Rect? printableArea)
    {
        var document=new FixedDocument();var address=Store.TicketVenueAddress(eventId);var printed=DateTime.Now;
        var originalLayout=blank.Legacy!;
        var layout=originalLayout with{ContentWidth=mediaWidth/DipsPerMm-originalLayout.LeftMargin-originalLayout.RightMargin,ContentHeight=mediaHeight/DipsPerMm-originalLayout.TopMargin-originalLayout.BottomMargin};
        foreach(var layoutPage in LegacyTicketTemplates.Paginate(layout,invoice.Tickets.Count))
        {
            var page=new FixedPage{Width=mediaWidth,Height=mediaHeight,Background=Brushes.White};
            foreach(var placement in layoutPage.Elements)
            {
                var field=placement.Element;var ticket=invoice.Tickets[placement.TicketIndex];var source=headers.TryGetValue(ticket.Id,out var original)?original:invoice;
                var text=field.Binding.Length==0?field.Text:LegacyTicketTemplates.Value(field.Binding,source,ticket,address,printed,placement.PageNumber);
                if(text.Length==0)continue;
                var box=new Rect(Mm(placement.X),Mm(placement.Y),Mm(field.Width),Mm(field.Height));
                if(printableArea is {} area&&(box.Left<area.Left-.5||box.Top<area.Top-.5||box.Right>area.Right+.5||box.Bottom>area.Bottom+.5))
                    throw new TicketException($"Поле «{field.Name}» исходного бланка выходит за печатную область принтера. Выберите бумагу и принтер с подходящими полями. Исходные координаты не сдвигаются.");
                var visual=LegacyElementVisual(field,text,DipsPerMm);FixedPage.SetLeft(visual,box.Left);FixedPage.SetTop(visual,box.Top);page.Children.Add(visual);
            }
            document.Pages.Add(new PageContent{Child=page});
        }
        return document;
    }
    internal static FixedDocument BuildDocument(InvoiceInfo invoice,double pageWidth=794,double pageHeight=1123)
        =>BuildDocumentCore(invoice,pageWidth,pageHeight,null);
    private static FixedDocument BuildDocumentCore(InvoiceInfo invoice,double pageWidth,double pageHeight,IReadOnlyDictionary<long,InvoiceInfo>? headers)
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
                var source=headers is not null&&headers.TryGetValue(ticket.Id,out var original)?original:invoice;
                var form=new Grid{Margin=new Thickness(10)};
                foreach(var weight in new[]{1.5,1.0,1.0,1.0,1.0,1.0})form.RowDefinitions.Add(new RowDefinition{Height=new GridLength(weight,GridUnitType.Star)});
                void Line(int row,string text,double size,bool bold=false)
                {
                    var label=new TextBlock{Text=text,FontFamily=new FontFamily("Arial"),FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,TextWrapping=TextWrapping.NoWrap};
                    var fit=new Viewbox{Child=label,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
                    Grid.SetRow(fit,row);form.Children.Add(fit);
                }
                Line(0,source.Title,20,true);Line(1,source.Venue,15);Line(2,source.EventDate,16,true);
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
