using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

public partial class MainWindow
{
    // Focus is presentation only: keep the existing controls and never read the database.
    private void UpdateFocusLegend050()
    {
        foreach(var button in LegendPanel.Children.OfType<Border>())
        {
            var active=button.Tag is decimal price&&Hall.FocusPrices.Contains(price);
            button.BorderBrush=active?Brushes.Black:Brushes.Gray;
            button.SetResourceReference(Border.PaddingProperty,active?"Px2Inset043":"Px4Inset043");
            button.SetResourceReference(Border.BorderThicknessProperty,active?"Px3Border043":"Px1Border043");
        }
        foreach(var panel in StatusLegendPanel.Children.OfType<StackPanel>())
        {
            var active=Equals(panel.Tag,Hall.FocusStatus030);
            if(panel.Children[0] is Border icon){icon.BorderBrush=active?Brushes.Black:null;icon.BorderThickness=new Thickness(active?2:0);}
            if(panel.Children[1] is TextBlock text)text.FontWeight=active?FontWeights.Bold:FontWeights.Normal;
        }
    }
}
