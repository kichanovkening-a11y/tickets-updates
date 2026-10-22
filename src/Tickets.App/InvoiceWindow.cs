using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class InvoiceWindow : EditorWindow
{
    private readonly long eventId,issueId;
    private readonly ComboBox senderBox=new(){FontSize=18,IsEditable=true,IsTextSearchEnabled=true,Height=34,MinHeight=34,VerticalContentAlignment=VerticalAlignment.Center};
    private readonly TextBlock savedStatus=new(){TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,3),FontSize=12};
    private string persistedSender="";
    private readonly DocumentTextEditor textEditor=new();
    private ScrollViewer? documentNavigation;
    private readonly FlowDocumentScrollViewer reader=new(){IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    public InvoiceWindow(Store store,long eventId,long issueId,bool print=false):base(store,$"Накладная № {issueId}",1030,850)
    {
        this.eventId=eventId;this.issueId=issueId;
        var root=new Grid{Margin=new Thickness(10)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
        var header=new StackPanel();root.Children.Add(header);
        header.Children.Add(documentNavigation=IssueDocumentNavigation.Toolbar(IssueDocumentNavigation.Invoice,kind=>Try(()=>{
            PersistSender();SaveText();
            IssueDocumentNavigation.Open(Store,eventId,issueId,kind);
            Refresh();
        })));
        header.Children.Add(DocumentActions.Toolbar(
            ()=>Try(OpenWord),
            ()=>Try(()=>{
                var path=DocumentSaveDialog.Save(Store,DialogOwner,$"Накладная-{issueId}.docx",SaveWord);
                if(path is not null){savedStatus.Text="Сохранено: "+path;Refresh();}
            }),()=>Try(Print),()=>Try(PrintInvoiceTickets)));
        var senderRow=new Grid();senderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(90)});senderRow.ColumnDefinitions.Add(new ColumnDefinition());senderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.Children.Add(senderRow);
        senderRow.Children.Add(new TextBlock{Text="От кого",VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(senderBox,1);senderRow.Children.Add(senderBox);
        var senderActions=new WrapPanel{Margin=new Thickness(8,0,0,0)};Grid.SetColumn(senderActions,2);senderRow.Children.Add(senderActions);
        Button(senderActions,"+ Отправитель",()=>Try(()=>EditSender(false)));
        Button(senderActions,"Реквизиты",()=>Try(()=>EditSender(true))).ToolTip="Реквизиты выбранного отправителя";
        Button(senderActions,"Реквизиты получателя",()=>Try(EditRecipient)).ToolTip="Реквизиты получателя этой накладной";
        var removeSender=new MenuItem{Header="Удалить отправителя из списка"};
        senderBox.ContextMenu=new ContextMenu();senderBox.ContextMenu.Items.Add(removeSender);
        senderBox.ContextMenuOpening+=(_,e)=>{if(senderBox.SelectedItem is not string)e.Handled=true;};
        removeSender.Click+=(_,_)=>Try(()=>{
            if(senderBox.SelectedItem is not string selected)return;
            if(MessageBox.Show(DialogOwner,$"Удалить отправителя «{selected}» из списка?\nРанее оформленные документы сохранятся.","Удаление отправителя",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            Store.RemoveSenderChoice(selected);
            var current=senderBox.Text;senderBox.ItemsSource=Store.Senders();senderBox.Text=Store.Senders().Contains(current)?current:"";persistedSender=senderBox.Text;
        });
        senderBox.ItemsSource=Store.Senders();senderBox.Text=Store.Senders().Contains(Store.Invoice(eventId,issueId).Sender)?Store.Invoice(eventId,issueId).Sender:"";persistedSender=senderBox.Text;header.Children.Add(savedStatus);
        senderBox.LostKeyboardFocus+=(_,_)=>{if(!senderBox.IsKeyboardFocusWithin)Try(()=>{PersistSender();Refresh();});};
        var tabs=new TabControl();Grid.SetRow(tabs,1);root.Children.Add(tabs);tabs.Items.Add(new TabItem{Header="Документ",Content=reader});
        var editing=new DockPanel();var saveRow=new WrapPanel{Margin=new Thickness(0,5,0,5)};DockPanel.SetDock(saveRow,Dock.Bottom);editing.Children.Add(saveRow);
        var saveText=Button(saveRow,"Сохранить текст накладной",()=>Try(SaveText));saveText.ContentTemplate=null;saveText.Width=double.NaN;saveText.Padding=new Thickness(10,3,10,3);
        editing.Children.Add(textEditor);tabs.Items.Add(new TabItem{Header="Изменить текст / шаблон",Content=editing});
        textEditor.Load(Store.CustomIssueTemplate(eventId,issueId,"invoice")??throw new TicketException("Шаблон накладной не найден."),"invoice");
        tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==0)Try(SaveText);};
        reader.SizeChanged+=(_,_)=>{if(reader.ActualWidth>100)reader.Document=Preview(reader.ActualWidth-20);};Refresh();
        Closing+=(_,e)=>{
            try{PersistSender();SaveText();}
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
        var invoice=Store.Invoice(eventId,issueId);if(documentNavigation is not null)IssueDocumentNavigation.SetBusinessEnabled(documentNavigation,Store.CanUseBusinessDocuments(eventId,issueId));reader.Document=Preview(reader.ActualWidth>100?reader.ActualWidth-20:794);
    }
    private FlowDocument Preview(double width)
    {
        var custom=Store.CustomIssueDocument(eventId,issueId,"invoice");
        return custom is not null?SourceFormPreview.Document(custom,width):Document(Store.Invoice(eventId,issueId),width);
    }
    private void SaveText()
    {
        if(!textEditor.IsDirty)return;
        var bytes=textEditor.SaveBytes();Store.SaveCustomIssueForm(eventId,issueId,"invoice",bytes);textEditor.Load(bytes,"invoice");Refresh();savedStatus.Text="Текст накладной сохранён. Автоматические поля защищены.";
    }
    private void SaveWord(string destination)
    {
        PersistSender();SaveText();var custom=Store.CustomIssueDocument(eventId,issueId,"invoice");
        if(custom is not null)InvoiceDocuments.SaveIssueDocumentWord(custom,destination);
        else InvoiceDocuments.SaveWord(Store.Invoice(eventId,issueId),destination);
    }
    private void OpenWord()
    {
        var directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Tickets-export");System.IO.Directory.CreateDirectory(directory);
        var path=System.IO.Path.Combine(directory,$"Накладная-{issueId}-{Guid.NewGuid():N}.docx");SaveWord(path);IssueDocumentNavigation.OpenWord(path,()=>path);
        savedStatus.Text="Открыта отдельная копия накладной: автоматические данные в ней можно изменять.";
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
        senderBox.ItemsSource=Store.Senders();senderBox.Text=editor.CreatedName;PersistSender();Store.RefreshInvoiceSender(eventId,issueId);SaveFiles();Refresh();
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
        if(senderBox.Text==persistedSender||string.IsNullOrWhiteSpace(senderBox.Text))return;
        Store.UpdateInvoiceSender(eventId,issueId,senderBox.Text);persistedSender=senderBox.Text;
        SaveFiles();
    }
    private void PrintInvoiceTickets()
    {
        PersistSender();var invoice=Store.Invoice(eventId,issueId);
        var seats=invoice.Tickets.Where(t=>t.ReturnedAt is null&&!t.IsReserved).Select(t=>t.SeatId).ToArray();
        if(seats.Length==0)throw new TicketException("В этой накладной нет выданных билетов для печати.");
        new TicketPrintWindow(Store,eventId,seats).ShowDialog();Refresh();
    }
    private void Print()
    {
        PersistSender();SaveText();
        DocumentPrintPreview.Show(DialogOwner,$"Накладная № {issueId}",Preview);
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
