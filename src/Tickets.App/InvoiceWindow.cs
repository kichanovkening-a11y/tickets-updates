using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class InvoiceWindow : EditorWindow
{
    private readonly long eventId,issueId;
    private readonly ComboBox senderBox=new(){IsEditable=true,IsTextSearchEnabled=true,MinHeight=34,VerticalContentAlignment=VerticalAlignment.Center};
    private readonly TextBlock savedStatus=new(){TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,3),FontSize=12};
    private readonly Button printTickets,extendInvoice;
    private readonly FlowDocumentScrollViewer reader=new(){IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    public InvoiceWindow(Store store,long eventId,long issueId,bool print=false):base(store,$"Накладная № {issueId}",1000,850)
    {
        this.eventId=eventId;this.issueId=issueId;
        var root=new Grid{Margin=new Thickness(10)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition());root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(124)});
        var header=new StackPanel();Grid.SetColumnSpan(header,2);root.Children.Add(header);
        header.Children.Add(IssueDocumentNavigation.Toolbar(IssueDocumentNavigation.Invoice,kind=>Try(()=>{
            PersistSender();
            IssueDocumentNavigation.Open(Store,eventId,issueId,kind);
            Refresh();
        })));
        var senderRow=new Grid();senderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(90)});senderRow.ColumnDefinitions.Add(new ColumnDefinition());senderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.Children.Add(senderRow);
        senderRow.Children.Add(new TextBlock{Text="От кого",VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(senderBox,1);senderRow.Children.Add(senderBox);
        var senderActions=new WrapPanel{Margin=new Thickness(8,0,0,0)};Grid.SetColumn(senderActions,2);senderRow.Children.Add(senderActions);
        Button(senderActions,"+ Отправитель",()=>Try(()=>EditSender(false)));
        Button(senderActions,"Реквизиты",()=>Try(()=>EditSender(true)));
        Button(header,"Обновить реквизиты документа",()=>Try(()=>{PersistSender();Store.RefreshInvoiceOrganizations(eventId,issueId);Refresh();}));
        senderBox.ItemsSource=Store.Senders();senderBox.Text=Store.Invoice(eventId,issueId).Sender;header.Children.Add(savedStatus);
        senderBox.LostKeyboardFocus+=(_,_)=>{if(!senderBox.IsKeyboardFocusWithin)Try(()=>{PersistSender();Refresh();});};
        var actions=new StackPanel{Margin=new Thickness(10,0,0,0)};var actionsScroll=new ScrollViewer{Content=actions,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(actionsScroll,1);Grid.SetRow(actionsScroll,1);root.Children.Add(actionsScroll);
        Square(actions,"Сохранить",()=>Try(()=>{PersistSender();Refresh();}),Brushes.SteelBlue);
        Square(actions,"Распечатать",()=>Try(Print),Brushes.SeaGreen);
        printTickets=Button(actions,"Печать билетов",()=>Try(()=>{
            PersistSender();var seats=Store.Invoice(eventId,issueId).Tickets.Where(t=>t.ReturnedAt is null).Select(t=>t.SeatId).ToArray();
            if(seats.Length==0)throw new TicketException("В накладной нет выданных билетов для печати.");
            new TicketPrintWindow(Store,eventId,seats).ShowDialog();Refresh();
        }));printTickets.Width=112;printTickets.Height=112;printTickets.Content=new TextBlock{Text="Печать билетов",TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};
        extendInvoice=Button(actions,"Дополнить накладную",()=>Try(()=>{
            PersistSender();new InvoiceExtensionWindow(Store,eventId,issueId).ShowDialog();Refresh();
        }));extendInvoice.Width=112;extendInvoice.Height=112;extendInvoice.Content=new TextBlock{Text="Дополнить накладную",TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};
        Grid.SetRow(reader,1);root.Children.Add(reader);Refresh();
        Closing+=(_,e)=>{
            try{PersistSender();Refresh();}
            catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Отправитель",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
        // Printing is always an explicit choice in the document toolbar.
    }
    internal static Button Square(Panel panel,string text,Action action,Brush color)
    {
        var button=Button(panel,text,action);button.Width=112;button.Height=112;button.Margin=new Thickness(0,0,0,8);button.Background=color;button.Foreground=Brushes.White;button.FontWeight=FontWeights.Bold;return button;
    }
    private void Refresh()
    {
        var invoice=Store.Invoice(eventId,issueId);reader.Document=Document(invoice);
        printTickets.IsEnabled=invoice.Tickets.Any(t=>t.ReturnedAt is null);
        extendInvoice.IsEnabled=!Store.IsEventArchived(eventId)&&invoice.Tickets.Any(t=>t.ReturnedAt is null)&&!invoice.Tickets.Any(t=>t.ReturnedAt is null&&t.IsReserved);
        try{Store.SaveAllDocuments(eventId,issueId);savedStatus.Text="Документы сохранены: "+Store.DocumentsDirectory;}
        catch(Exception ex){savedStatus.Text="Выдача сохранена в базе. "+ex.Message;}
        savedStatus.ToolTip=savedStatus.Text;
    }
    private void EditSender(bool existing)
    {
        var editor=new SenderEditorWindow(Store,existing?senderBox.Text:"",existing);
        if(editor.ShowDialog()!=true)return;
        senderBox.ItemsSource=Store.Senders();senderBox.Text=editor.CreatedName;PersistSender();Store.RefreshInvoiceOrganizations(eventId,issueId);Refresh();
    }
    private void PersistSender()=>Store.UpdateInvoiceSender(eventId,issueId,senderBox.Text);
    private void Print()
    {
        PersistSender();Refresh();var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;
        var document=Document(Store.Invoice(eventId,issueId));document.PageWidth=dialog.PrintableAreaWidth;document.PageHeight=dialog.PrintableAreaHeight;
        document.PagePadding=new Thickness(32);document.ColumnWidth=dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,$"Накладная № {issueId}");
    }
    internal static FlowDocument Document(InvoiceInfo invoice)
    {
        var doc=new FlowDocument{FontFamily=new FontFamily("Arial"),FontSize=13,PageWidth=794,PagePadding=new Thickness(25),ColumnWidth=744};
        void Text(string text,bool bold=false,double size=13)=>doc.Blocks.Add(new Paragraph(new Run(text)){FontWeight=bold?FontWeights.Bold:FontWeights.Normal,FontSize=size,Margin=new Thickness(0,0,0,7)});
        Text($"НАКЛАДНАЯ № {invoice.Id}",true,22);Text("на передачу билетов");Text("Дата выдачи: "+invoice.Date);
        Text("Мероприятие: "+invoice.Title,true);Text("Дата и время: "+invoice.EventDate);Text("Площадка: "+invoice.Venue);Text("От кого: "+invoice.Sender);Text("Получатель: "+invoice.Recipient);
        var table=new Table{CellSpacing=0,BorderThickness=new Thickness(0.5),BorderBrush=Brushes.SlateGray};
        foreach(var width in new[]{1.5,.6,2.5,.8,1.0,1.1,1.3})table.Columns.Add(new TableColumn{Width=new GridLength(width,GridUnitType.Star)});
        var rows=new TableRowGroup();table.RowGroups.Add(rows);
        void Row(string[] cells,bool header)
        {
            var row=new TableRow();foreach(var text in cells)row.Cells.Add(new TableCell(new Paragraph(new Run(text)){Margin=new Thickness(0)}){
                Padding=new Thickness(5),BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(.5),FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?Brushes.AliceBlue:Brushes.White});rows.Rows.Add(row);
        }
        Row(InvoiceDocuments.Headers,true);foreach(var row in InvoiceDocuments.Rows(invoice))Row(row,false);doc.Blocks.Add(table);
        Text(DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total));Text(DocumentTotals.Total(invoice.Tickets.Count,invoice.Total),true);
        if(invoice.ActiveCount!=invoice.Tickets.Count)Text($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount}. Осталось на руках: {invoice.ActiveCount}, на сумму {InvoiceDocuments.Money(invoice.ActiveTotal)}.");
        Text("\nПередал: ____________________    Получил: ____________________");return doc;
    }
}
