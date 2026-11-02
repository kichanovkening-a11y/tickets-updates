using System.Windows;
using System.Windows.Controls;

namespace Tickets.App;

internal static class DocumentReadOnly047
{
    internal static void Apply(DependencyObject root,bool readOnly)
    {
        if(!readOnly)return;
        foreach(var item in Walk(root))
        {
            if(item is TabItem tab&&Equals(tab.Header,"Изменить текст"))tab.IsEnabled=false;
            if(item is TextBox text)text.IsReadOnly=true;
            if(item is ComboBox or DatePicker)((Control)item).IsEnabled=false;
            if(item is Button button&&button.Content is string caption&&
                (caption.StartsWith("Сохранить как форму",StringComparison.Ordinal)||caption is "+ Отправитель" or "Реквизиты" or "Реквизиты получателя"))button.IsEnabled=false;
        }
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
