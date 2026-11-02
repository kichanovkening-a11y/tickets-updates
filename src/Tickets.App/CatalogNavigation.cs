using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Tickets.App;

public partial class MainWindow
{
    private bool recipientUpdating;
    private int recipientEditVersion;
    private bool openingRecipientSuggestions;
    private string RecipientName => ((RecipientBox.Template?.FindName("PART_EditableTextBox",RecipientBox) as TextBox)?.Text??RecipientBox.Text);
    private void ClearIssuanceRecipient()
    {
        recipientEditVersion++;recipientUpdating=true;
        try
        {
            RecipientBox.SelectedItem=null;RecipientBox.Text="";
            if(RecipientBox.Template?.FindName("PART_EditableTextBox",RecipientBox) is TextBox box)box.Clear();
            RecipientBox.IsDropDownOpen=false;
        }
        finally{recipientUpdating=false;}
        if(ready)UpdateSelection();
    }
    private IEnumerable<TreeViewItem> EventRows()
    {
        IEnumerable<TreeViewItem> Walk(ItemsControl parent)
        {
            foreach(var item in parent.Items.OfType<TreeViewItem>()){yield return item;foreach(var child in Walk(item))yield return child;}
        }
        return Walk(EventsList);
    }
    public void SelectEvent(long id)
    {
        List<TreeViewItem>? Find(ItemsControl owner,List<TreeViewItem> path)
        {
            foreach(var item in owner.Items.OfType<TreeViewItem>())
            {
                var next=path.Append(item).ToList();if(item.Tag is Tickets.Core.EventInfo e&&e.Id==id)return next;
                var found=Find(item,next);if(found is not null)return found;
            }
            return null;
        }
        var chain=Find(EventsList,[]);if(chain is null)return;
        foreach(var ancestor in chain.SkipLast(1))ancestor.IsExpanded=true;EventsList.UpdateLayout();chain[^1].IsSelected=true;chain[^1].BringIntoView();
    }
    private void RecipientSelected(object sender,SelectionChangedEventArgs e)
    {
        if(recipientUpdating||e.AddedItems.Count==0)return;
        // Selection commits the name and invalidates a queued typing refresh. Otherwise
        // that callback reopens the popup and consumes the next toolbar click.
        recipientEditVersion++;
        RecipientBox.IsDropDownOpen=false;
        if(ready)UpdateSelection();
    }
    private void RecipientChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready||recipientUpdating)return;
        UpdateSelection();
        var version=++recipientEditVersion;
        if(RecipientBox.SelectedItem is string selected&&selected==RecipientName)return;
        if(e.OriginalSource is not TextBox box||!box.IsKeyboardFocused)return;
        Dispatcher.BeginInvoke(new Action(()=>{
            if(!ready||recipientUpdating||version!=recipientEditVersion||!box.IsKeyboardFocused)return;
            RefreshRecipients(true);UpdateSelection();
            openingRecipientSuggestions=true;
            try{RecipientBox.IsDropDownOpen=RecipientName.Length>0;}
            finally{openingRecipientSuggestions=false;}
        }),System.Windows.Threading.DispatcherPriority.Background);
    }
    private void RecipientOpened(object sender,EventArgs e)
    {
        if(!ready||recipientUpdating||openingRecipientSuggestions)return;
        // An explicit arrow / keyboard open always browses the entire directory.
        recipientEditVersion++;
        RefreshRecipients();
    }
    private void RecipientFieldPressed(object sender,MouseButtonEventArgs e)
    {
        if(!ready||recipientUpdating)return;
        // Only the field opens the directory here. The arrow and popup choices
        // retain their native toggle / commit behavior.
        var source=e.OriginalSource as DependencyObject;
        while(source is not null&&source!=RecipientBox)
        {
            if(source is ToggleButton or ComboBoxItem)return;
            source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
        }
        if(source!=RecipientBox)return;
        if(RecipientBox.Template?.FindName("PART_EditableTextBox",RecipientBox) is TextBox box)box.Focus();
        if(RecipientBox.IsDropDownOpen)
        {
            recipientEditVersion++;
            RefreshRecipients();
        }
        else RecipientBox.IsDropDownOpen=true;
        // Do not handle the event: the textbox must still position its caret.
    }
    private void RefreshRecipients(bool filter=false)
    {
        if(recipientUpdating)return;recipientUpdating=true;
        try
        {
            var text=RecipientName;var box=RecipientBox.Template?.FindName("PART_EditableTextBox",RecipientBox) as TextBox;
            var caret=box?.CaretIndex??text.Length;
            RecipientBox.ItemsSource=filter?store.Recipients(text):store.Recipients();
            RecipientBox.SelectedItem=null;RecipientBox.Text=text;
            if(box is not null){box.Text=text;box.CaretIndex=Math.Min(caret,text.Length);}
        }
        finally{recipientUpdating=false;}
    }
}
