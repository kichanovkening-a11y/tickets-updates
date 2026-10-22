using System.Windows;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    private void RefreshSenders(string? selected=null)
    {
        var value=selected??SenderBox.Text;
        var senders=store.Senders();SenderBox.ItemsSource=senders;SenderBox.SelectedItem=null;
        var preferred=string.IsNullOrWhiteSpace(value)||!senders.Contains(value)?store.Sender:value;
        SenderBox.Text=senders.Contains(preferred)?preferred:"";
    }
    private void SenderDropDownOpened(object sender,EventArgs e)=>RefreshSenders();
    private void SenderContextRequested(object sender,System.Windows.Input.MouseButtonEventArgs e)
    {
        var name=SenderBox.SelectedItem as string??SenderBox.Text;
        if(string.IsNullOrWhiteSpace(name)||!store.Senders().Contains(name))return;
        var menu=new System.Windows.Controls.ContextMenu();
        var remove=new System.Windows.Controls.MenuItem{Header="Удалить отправителя из списка"};
        remove.Click+=(_,_)=>Run(()=>{
            if(MessageBox.Show(this,$"Удалить отправителя «{name}» из списка? Существующие накладные сохранятся.","Отправители",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            store.RemoveSenderChoice(name);SenderBox.SelectedItem=null;SenderBox.Text="";RefreshSenders();
        });
        menu.Items.Add(remove);SenderBox.ContextMenu=menu;menu.IsOpen=true;e.Handled=true;
    }
    private void PersistSelectedSender()
    {
        // A sender may have been deleted in an invoice window while this field
        // retained its old text. Refresh before validating a stale catalog choice.
        if(!store.Senders().Contains(SenderBox.Text))RefreshSenders();
        if(string.IsNullOrWhiteSpace(SenderBox.Text))throw new TicketException("Выберите отправителя или создайте нового в разделе «Организации и номера».");
        store.SaveDocumentSettings(SenderBox.Text,store.DocumentsDirectory);RefreshSenders(store.Sender);
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
        var selected=Hall.SelectedSeats;
        if(selected.Count==0||selected.Any(s=>!s.Available))throw new TicketException("Для брони выберите только свободные места с ценой.");
        if(string.IsNullOrWhiteSpace(RecipientName))throw new TicketException("Выберите получателя для новой брони.");
        PersistSelectedSender();
        var id=store.Reserve(current.Id,selected.Select(s=>s.Id).ToArray(),RecipientName,SeriesBox.Text,first);
        LoadEvent(false);Hall.Clear();ClearIssuanceRecipient();StatusText.Text=$"Забронировано мест: {selected.Count}. Бронь № {id}. Для оформления откройте исходную накладную из журнала.";
    });
    private void ExtendInvoiceClick(object sender,RoutedEventArgs e)=>Run(()=>{
        if(current is null)return;var selected=Hall.SelectedSeats;
        if(selected.Count==0||selected.Any(s=>!s.Available))throw new TicketException("Выберите дополнительные свободные места с ценой.");
        var choice=new InvoiceChoiceWindow(store,current.Id);
        if(choice.ShowDialog()!=true)return;
        var invoice=store.Invoice(current.Id,choice.SelectedIssueId);
        if(MessageBox.Show(this,$"Добавить {selected.Count} мест в накладную № {invoice.Id} для {invoice.Recipient}?\nОт кого: {invoice.Sender}","Дополнение накладной",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        store.ExtendInvoice(current.Id,invoice.Id,selected.Select(s=>s.Id).ToArray());LoadEvent(false);Hall.Clear();SaveIssuedDocuments(current.Id,invoice.Id);OpenInvoice(invoice.Id);
    });
}
