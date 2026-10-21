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
    private string persistedSender="";
    private readonly FlowDocumentScrollViewer reader=new(){IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    public InvoiceWindow(Store store,long eventId,long issueId,bool print=false):base(store,$"Накладная № {issueId}",1030,850)
    {
        this.eventId=eventId;this.issueId=issueId;
        var root=new Grid{Margin=new Thickness(10)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition());root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(152)});
        var header=new StackPanel();root.Children.Add(header);
        header.Children.Add(IssueDocumentNavigation.Toolbar(IssueDocumentNavigation.Invoice,kind=>Try(()=>{
            PersistSender();
            IssueDocumentNavigation.Open(Store,eventId,issueId,kind);
            Refresh();
        })));
        var senderRow=new Grid();senderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(90)});senderRow.ColumnDefinitions.Add(new ColumnDefinition());senderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.Children.Add(senderRow);
        senderRow.Children.Add(new TextBlock{Text="От кого",VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(senderBox,1);senderRow.Children.Add(senderBox);
        var senderActions=new WrapPanel{Margin=new Thickness(8,0,0,0)};Grid.SetColumn(senderActions,2);senderRow.Children.Add(senderActions);
        Button(senderActions,"+ Отправитель",()=>Try(()=>EditSender(false)));
        Button(senderActions,"Реквизиты",()=>Try(EditRecipient)).ToolTip="Реквизиты получателя";
        Button(header,"Обновить реквизиты",()=>Try(EditRecipient)).ToolTip="Реквизиты получателя";
        senderBox.ItemsSource=Store.Senders();senderBox.Text=Store.Invoice(eventId,issueId).Sender;persistedSender=senderBox.Text;header.Children.Add(savedStatus);
        senderBox.LostKeyboardFocus+=(_,_)=>{if(!senderBox.IsKeyboardFocusWithin)Try(()=>{PersistSender();Refresh();});};
        var actions=new StackPanel{Margin=new Thickness(10,0,0,0)};var actionsScroll=new ScrollViewer{Content=actions,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(actionsScroll,1);Grid.SetRowSpan(actionsScroll,2);root.Children.Add(actionsScroll);
        Square(actions,"Сохранить",()=>Try(()=>{
            var path=DocumentSaveDialog.Save(Store,DialogOwner,$"Накладная-{issueId}.docx",destination=>{PersistSender();InvoiceDocuments.SaveWord(Store.Invoice(eventId,issueId),destination);});
            if(path is not null){savedStatus.Text="Сохранено: "+path;Refresh();}
        }),Brushes.SteelBlue);
        Square(actions,"Распечатать",()=>Try(Print),Brushes.SeaGreen);
        Square(actions,"Открыть",()=>Try(()=>IssueDocumentNavigation.OpenWord(Store.InvoiceDocumentPath(issueId),()=>Store.SaveInvoiceDocument(eventId,issueId))),Brushes.SlateGray);
        Grid.SetRow(reader,1);root.Children.Add(reader);reader.SizeChanged+=(_,_)=>{if(reader.ActualWidth>100)reader.Document=Document(Store.Invoice(eventId,issueId),reader.ActualWidth-20);};Refresh();
        Closing+=(_,e)=>{
            try{PersistSender();}
            catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Отправитель",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
        // Printing is always an explicit choice in the document toolbar.
    }
    internal static Button Square(Panel panel,string text,Action action,Brush color)
    {
        var button=Button(panel,text,action);button.FontSize=14;button.Width=112;button.Height=112;button.Margin=new Thickness(0,0,0,8);button.Background=color;button.Foreground=Brushes.White;button.FontWeight=FontWeights.Bold;return button;
    }
    private void Refresh()
    {
        var invoice=Store.Invoice(eventId,issueId);reader.Document=Document(invoice,reader.ActualWidth>100?reader.ActualWidth-20:794);
    }
    private void SaveFiles()
    {
        try{Store.SaveAllDocuments(eventId,issueId);savedStatus.Text="Документы сохранены: "+Store.DocumentsDirectory;}
        catch(Exception ex){savedStatus.Text="Выдача сохранена в базе. "+ex.Message;}
        savedStatus.ToolTip=savedStatus.Text;
    }
    private void EditSender(bool existing)
    {
        var editor=new SenderEditorWindow(Store,existing?senderBox.Text:"",existing);
        if(editor.ShowDialog()!=true)return;
        senderBox.ItemsSource=Store.Senders();senderBox.Text=editor.CreatedName;PersistSender();Store.RefreshInvoiceOrganizations(eventId,issueId);SaveFiles();Refresh();
    }
    private void EditRecipient()
    {
        var invoice=Store.Invoice(eventId,issueId);
        var recipientEditor=new RecipientEditorWindow(Store,invoice.Recipient,true);
        if(recipientEditor.ShowDialog()!=true)return;
        // The editor persists the recipient directory for future issues. Refresh
        // only this issue's recipient; keep the historical sender snapshot.
        Store.RefreshInvoiceRecipient(eventId,issueId);SaveFiles();Refresh();
        savedStatus.Text="Реквизиты получателя обновлены. "+savedStatus.Text;
    }
    private void PersistSender()
    {
        if(senderBox.Text==persistedSender)return;
        Store.UpdateInvoiceSender(eventId,issueId,senderBox.Text);persistedSender=senderBox.Text;
        SaveFiles();
    }
    private void Print()
    {
        PersistSender();var invoice=Store.Invoice(eventId,issueId);
        DocumentPrintPreview.Show(DialogOwner,$"Накладная № {issueId}",width=>Document(invoice,width));
    }
    internal static FlowDocument Document(InvoiceInfo invoice,double pageWidth=794)
    {
        var doc=new FlowDocument{FontFamily=new FontFamily("Arial"),FontSize=13,PageWidth=pageWidth,PagePadding=new Thickness(25),ColumnWidth=Math.Max(100,pageWidth-50)};
        void Text(string text,bool bold=false,double size=13)=>doc.Blocks.Add(new Paragraph(new Run(text)){FontWeight=bold?FontWeights.Bold:FontWeights.Normal,FontSize=size,Margin=new Thickness(0,0,0,7)});
        Text($"НАКЛАДНАЯ № {invoice.Id}",true,22);Text("на передачу билетов");Text("Дата выдачи: "+invoice.Date);
        Text("Мероприятие: "+invoice.Title,true);Text("Дата и время: "+invoice.EventDate);Text("Площадка: "+invoice.Venue);Text("От кого: "+invoice.Sender);Text("Получатель: "+invoice.Recipient);

        var table=new Table{CellSpacing=0,BorderThickness=new Thickness(0.5),BorderBrush=Brushes.SlateGray};
        foreach(var width in new[]{1.5,.6,2.5,.8,1.0,1.1})table.Columns.Add(new TableColumn{Width=new GridLength((pageWidth-50)*width/7.5)});
        var rows=new TableRowGroup();table.RowGroups.Add(rows);
        void Row(string[] cells,bool header)
        {
            var row=new TableRow();foreach(var text in cells)row.Cells.Add(new TableCell(new Paragraph(new Run(text)){Margin=new Thickness(0)}){
                Padding=new Thickness(5),BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(.5),FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?Brushes.AliceBlue:Brushes.White});rows.Rows.Add(row);
        }
        Row(InvoiceDocuments.Headers,true);foreach(var row in InvoiceDocuments.Rows(invoice))Row(row,false);doc.Blocks.Add(table);
        Text(DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total));Text(DocumentTotals.Total(invoice.Tickets.Count,invoice.Total),true);
        if(invoice.ActiveCount!=invoice.Tickets.Count)Text($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount}. Осталось на руках: {invoice.ActiveCount}, на сумму {InvoiceDocuments.Money(invoice.ActiveTotal)}.");
        doc.Blocks.Add(IssueDocumentWindow.SignaturesTable(invoice,"Передал","Получил",pageWidth-50));return doc;
    }
}
