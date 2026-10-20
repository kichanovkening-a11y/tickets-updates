using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class InvoiceWindow : EditorWindow
{
    private readonly long eventId,issueId;
    private readonly TextBox senderBox=new();
    private readonly TextBlock savedStatus=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,8),FontSize=12};
    private readonly FlowDocumentReader reader=new(){ViewingMode=FlowDocumentReaderViewingMode.Scroll,IsTwoPageViewEnabled=false};
    private readonly ListBox tickets=new(){SelectionMode=SelectionMode.Extended,DisplayMemberPath="Label",MinHeight=80,MaxHeight=150};
    public InvoiceWindow(Store store,long eventId,long issueId,bool print=false):base(store,$"Накладная № {issueId}",1000,850)
    {
        this.eventId=eventId;this.issueId=issueId;
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
        Button(toolbar,"Сохранить в Word",()=>Try(()=>{PersistSender();Refresh();}));
        Button(toolbar,"Сохранить как…",()=>Try(Save));Button(toolbar,"Распечатать",()=>Try(Print));
        Button(toolbar,"Спецификация",()=>new SpecificationWindow(Store,Store.Invoice(eventId,issueId).Recipient).ShowDialog());
        Button(toolbar,"Закрыть",Close);
        var details=new StackPanel{Margin=new Thickness(0,8,0,5)};DockPanel.SetDock(details,Dock.Top);root.Children.Add(details);
        details.Children.Add(new TextBlock{Text="От кого"});senderBox.Text=Store.Invoice(eventId,issueId).Sender;details.Children.Add(senderBox);details.Children.Add(savedStatus);
        senderBox.LostKeyboardFocus+=(_,_)=>Try(()=>{PersistSender();Refresh();});
        var returns=new StackPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(returns,Dock.Bottom);root.Children.Add(returns);
        returns.Children.Add(new TextBlock{Text="Возврат: выберите билеты ниже (Ctrl — несколько).",Margin=new Thickness(0,0,0,5)});returns.Children.Add(tickets);
        Button(returns,"Вернуть выбранные билеты",()=>Try(()=>{
            var ids=tickets.SelectedItems.Cast<ReturnRow>().Select(t=>t.Id).ToArray();
            if(ids.Length==0)throw new TicketException("Выберите билеты для возврата.");
            if(MessageBox.Show(DialogOwner,$"Вернуть билеты: {ids.Length}?","Возврат",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            Store.Return(ids);Refresh();
        }));
        root.Children.Add(reader);Refresh();
        Closing+=(_,e)=>{
            try{PersistSender();Refresh();}
            catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Отправитель",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
        if(print)OnShown(()=>Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>Try(Print))));
    }
    private sealed record ReturnRow(long Id,string Label);
    private void Refresh()
    {
        var selectedIds=tickets.SelectedItems.Cast<ReturnRow>().Select(t=>t.Id).ToHashSet();
        var invoice=Store.Invoice(eventId,issueId);reader.Document=Document(invoice);
        try{savedStatus.Text="Сохранено: "+Store.SaveInvoiceDocument(eventId,issueId);}
        catch(Exception ex){savedStatus.Text="Выдача сохранена в базе. Не удалось записать Word: "+ex.Message;}
        tickets.ItemsSource=invoice.Tickets.Where(t=>t.ReturnedAt is null).Select(t=>new ReturnRow(t.Id,$"{t.Sector}, ряд {t.Row}, место {t.Seat} · {InvoiceDocuments.Money(t.Amount)}")).ToList();
        foreach(var row in tickets.Items.Cast<ReturnRow>().Where(t=>selectedIds.Contains(t.Id)))tickets.SelectedItems.Add(row);
    }
    private void PersistSender()=>Store.UpdateInvoiceSender(eventId,issueId,senderBox.Text);
    private void Save()
    {
        PersistSender();Refresh();
        var dialog=new SaveFileDialog{Filter="Документ Word (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,FileName=$"Накладная-{issueId}.docx"};
        if(dialog.ShowDialog(DialogOwner)==true)InvoiceDocuments.SaveWord(Store.Invoice(eventId,issueId),dialog.FileName);
    }
    private void Print()
    {
        PersistSender();Refresh();
        var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;
        var document=Document(Store.Invoice(eventId,issueId));
        document.PageWidth=dialog.PrintableAreaWidth;document.PageHeight=dialog.PrintableAreaHeight;
        document.PagePadding=new Thickness(32);document.ColumnWidth=dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,$"Накладная № {issueId}");
    }
    internal static FlowDocument Document(InvoiceInfo invoice)
    {
        var doc=new FlowDocument{FontFamily=new FontFamily("Arial"),FontSize=13,PageWidth=794,PagePadding=new Thickness(35),ColumnWidth=724};
        void Text(string text,bool bold=false,double size=13)=>doc.Blocks.Add(new Paragraph(new Run(text)){FontWeight=bold?FontWeights.Bold:FontWeights.Normal,FontSize=size,Margin=new Thickness(0,0,0,9)});
        Text($"НАКЛАДНАЯ № {invoice.Id}",true,22);Text("на передачу билетов");Text("Дата выдачи: "+invoice.Date);
        Text("Мероприятие: "+invoice.Title,true);Text("Дата и время: "+invoice.EventDate);Text("Площадка: "+invoice.Venue);Text("От кого: "+invoice.Sender);Text("Получатель: "+invoice.Recipient);
        var table=new Table{CellSpacing=0,BorderThickness=new Thickness(0.5),BorderBrush=Brushes.SlateGray};
        foreach(var width in new[]{1.5,.5,2.5,.6,1.0,1.1,1.2})table.Columns.Add(new TableColumn{Width=new GridLength(width,GridUnitType.Star)});
        var rows=new TableRowGroup();table.RowGroups.Add(rows);
        void Row(string[] cells,bool header)
        {
            var row=new TableRow();foreach(var text in cells)row.Cells.Add(new TableCell(new Paragraph(new Run(text)){Margin=new Thickness(0)}){
                Padding=new Thickness(5),BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(.5),FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?Brushes.AliceBlue:Brushes.White});rows.Rows.Add(row);
        }
        Row(InvoiceDocuments.Headers,true);foreach(var row in InvoiceDocuments.Rows(invoice))Row(row,false);doc.Blocks.Add(table);
        Text($"Всего билетов: {invoice.Tickets.Count}. Сумма выдачи: {InvoiceDocuments.Money(invoice.Total)}",true);
        Text("Количество прописью: "+RussianWords.Quantity(invoice.Tickets.Count)+".");
        Text("Сумма прописью: "+RussianWords.Money(invoice.Total)+".");
        if(invoice.ActiveCount!=invoice.Tickets.Count)Text($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount}. Осталось на руках: {invoice.ActiveCount}, на сумму {InvoiceDocuments.Money(invoice.ActiveTotal)}.");
        Text("\nПередал: ____________________    Получил: ____________________");return doc;
    }
}
