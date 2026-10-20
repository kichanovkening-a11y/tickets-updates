using System.Windows;
using System.Windows.Controls;

namespace Tickets.App;

public partial class MainWindow
{
    private bool recipientUpdating;
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
    private void RecipientChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready||recipientUpdating)return;RefreshRecipients();UpdateSelection();
        if(RecipientBox.IsKeyboardFocusWithin&&RecipientBox.Text.Length>0)RecipientBox.IsDropDownOpen=true;
    }
    private void RecipientOpened(object sender,EventArgs e){if(ready&&!recipientUpdating)RefreshRecipients();}
    private void RefreshRecipients()
    {
        if(recipientUpdating)return;recipientUpdating=true;
        try
        {
            var text=RecipientBox.Text;var box=RecipientBox.Template?.FindName("PART_EditableTextBox",RecipientBox) as TextBox;var caret=box?.CaretIndex??text.Length;
            RecipientBox.ItemsSource=store.Recipients(text);RecipientBox.Text=text;if(box is not null)box.CaretIndex=Math.Min(caret,text.Length);
        }
        finally{recipientUpdating=false;}
    }
}
