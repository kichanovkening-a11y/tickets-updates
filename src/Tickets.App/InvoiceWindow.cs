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
    private readonly FlowDocumentReader reader=new(){ViewingMode=FlowDocumentReaderViewingMode.Scroll,IsTwoPageViewEnabled=false};
    private readonly ListBox tickets=new(){SelectionMode=SelectionMode.Extended,DisplayMemberPath="Label",MinHeight=80,MaxHeight=150};
    public InvoiceWindow(Store store,long eventId,long issueId,bool print=false):base(store,$"Накладная № {issueId}",1000,850)
    {
        this.eventId=eventId;this.issueId=issueId;
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
        Button(toolbar,"Сохранить в Word",()=>Try(Save));Button(toolbar,"Распечатать",()=>Try(Print));
        Button(toolbar,"Закрыть",Close);
        var returns=new StackPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(returns,Dock.Bottom);root.Children.Add(returns);
        returns.Children.Add(new TextBlock{Text="Возврат: выберите билеты ниже (Ctrl — несколько).",Margin=new Thickness(0,0,0,5)});returns.Children.Add(tickets);
        Button(returns,"Вернуть выбранные билеты",()=>Try(()=>{
            var ids=tickets.SelectedItems.Cast<ReturnRow>().Select(t=>t.Id).ToArray();
            if(ids.Length==0)throw new TicketException("Выберите билеты для возврата.");
            if(MessageBox.Show(DialogOwner,$"Вернуть билеты: {ids.Length}?","Возврат",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            Store.Return(ids);Refresh();
        }));
        root.Children.Add(reader);Refresh();
        if(print)OnShown(()=>Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>Try(Print))));
    }
    private sealed record ReturnRow(long Id,string Label);
    private void Refresh()
    {
        var invoice=Store.Invoice(eventId,issueId);reader.Document=Document(invoice);
        tickets.ItemsSource=invoice.Tickets.Where(t=>t.ReturnedAt is null).Select(t=>new ReturnRow(t.Id,$"{t.Code} · {t.Sector}, ряд {t.Row}, место {t.Seat} · {InvoiceDocuments.Money(t.Amount)}")).ToList();
    }
    private void Save()
    {
        var dialog=new SaveFileDialog{Filter="Документ Word (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,FileName=$"Накладная-{issueId}.docx"};
        if(dialog.ShowDialog(DialogOwner)==true)InvoiceDocuments.SaveWord(Store.Invoice(eventId,issueId),dialog.FileName);
    }
    private void Print()
    {
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
        Text("Мероприятие: "+invoice.Title,true);Text("Дата и время: "+invoice.EventDate);Text("Площадка: "+invoice.Venue);Text("Получатель: "+invoice.Recipient);
        var table=new Table{CellSpacing=0,BorderThickness=new Thickness(0.5),BorderBrush=Brushes.SlateGray};
        foreach(var width in new[]{1.5,1.8,.5,.6,1.1,1.4})table.Columns.Add(new TableColumn{Width=new GridLength(width,GridUnitType.Star)});
        var rows=new TableRowGroup();table.RowGroups.Add(rows);
        void Row(string[] cells,bool header)
        {
            var row=new TableRow();foreach(var text in cells)row.Cells.Add(new TableCell(new Paragraph(new Run(text)){Margin=new Thickness(0)}){
                Padding=new Thickness(5),BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(.5),FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?Brushes.AliceBlue:Brushes.White});rows.Rows.Add(row);
        }
        Row(InvoiceDocuments.Headers,true);foreach(var ticket in invoice.Tickets)Row(InvoiceDocuments.Row(ticket),false);doc.Blocks.Add(table);
        Text($"Всего билетов: {invoice.Tickets.Count}. Сумма выдачи: {InvoiceDocuments.Money(invoice.Total)}",true);
        if(invoice.ActiveCount!=invoice.Tickets.Count)Text($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount}. Осталось на руках: {invoice.ActiveCount}, на сумму {InvoiceDocuments.Money(invoice.ActiveTotal)}.");
        Text("\nПередал: ____________________    Получил: ____________________");return doc;
    }
}
