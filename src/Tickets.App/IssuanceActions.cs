using System.Windows;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private void RefreshSenders(string? selected=null)
    {
        var value=selected??SenderBox.Text;
        SenderBox.ItemsSource=store.Senders();SenderBox.SelectedItem=null;
        SenderBox.Text=string.IsNullOrWhiteSpace(value)?store.Sender:value;
    }
    private void PersistSelectedSender()
    {
        var name=store.SaveSender(SenderBox.Text);
        store.SaveDocumentSettings(name,store.DocumentsDirectory);RefreshSenders(name);
    }
    private void EditSenderClick(object sender,RoutedEventArgs e)=>Run(()=>{
        var editor=new SenderEditorWindow(store,SenderBox.Text,store.Senders().Contains(SenderBox.Text));
        if(editor.ShowDialog()==true)RefreshSenders(editor.CreatedName);
    });
    private void EditRecipientClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(string.IsNullOrWhiteSpace(RecipientName))throw new TicketException("Выберите получателя или нажмите «Создать получателя».");
        var editor=new RecipientEditorWindow(store,RecipientBox.Text,true);
        if(editor.ShowDialog()==true){RefreshRecipients();RecipientBox.Text=editor.CreatedName;UpdateSelection();}
    });
    private void ReserveClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;
        if(!long.TryParse(NumberBox.Text,out var first))throw new TicketException("Введите целый положительный номер.");
        PersistSelectedSender();var selected=Hall.SelectedSeats;
        var id=store.Reserve(current.Id,selected.Select(s=>s.Id).ToArray(),RecipientName,SeriesBox.Text,first);
        LoadEvent(false);Hall.Clear();StatusText.Text=$"Забронировано мест: {selected.Count}. Бронь № {id}. Для накладной выберите места и нажмите «Оформить накладную».";
    });
    private void ExtendInvoiceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;var selected=Hall.SelectedSeats;
        if(selected.Count==0||selected.Any(s=>!s.Available))throw new TicketException("Выберите дополнительные свободные места с ценой.");
        var choice=new InvoiceChoiceWindow(store,current.Id,RecipientName);
        if(choice.ShowDialog()!=true)return;
        var invoice=store.Invoice(current.Id,choice.SelectedIssueId);
        if(MessageBox.Show(this,$"Добавить {selected.Count} мест в накладную № {invoice.Id} для {invoice.Recipient}?\nОт кого: {invoice.Sender}","Дополнение накладной",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        store.ExtendInvoice(current.Id,invoice.Id,selected.Select(s=>s.Id).ToArray());LoadEvent(false);Hall.Clear();SaveIssuedDocuments(current.Id,invoice.Id);OpenInvoice(invoice.Id);
    });
}
