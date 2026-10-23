using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tickets.App;
internal static class TreeSelection025
{
    internal static void Apply(TreeViewItem item)
    {
        if(item.Header is not TextBlock label)return;
        void Refresh(){label.Background=item.IsSelected?Brushes.DodgerBlue:Brushes.Transparent;label.Foreground=item.IsSelected?Brushes.White:SystemColors.ControlTextBrush;}
        item.Selected+=(_,e)=>{if(e.OriginalSource==item)Refresh();};item.Unselected+=(_,e)=>{if(e.OriginalSource==item)Refresh();};Refresh();
    }
}
