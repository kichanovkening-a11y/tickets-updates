using System.Windows;
using System.Windows.Controls;

namespace Tickets.App;

internal static class RecipientTypography
{
    internal static void Apply(ComboBox box)
    {
        // Typography is scoped to the recipient selector, never to all controls.
        box.FontSize=17;box.FontWeight=FontWeights.Bold;
        var items=new Style(typeof(ComboBoxItem));
        items.Setters.Add(new Setter(Control.HeightProperty,24d));
        items.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(2,0,2,0)));
        items.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Center));
        items.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));
        box.ItemContainerStyle=items;
    }
}
