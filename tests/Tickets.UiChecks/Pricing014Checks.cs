using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

internal static class Pricing014Checks
{
    // Run against a shown editor with at least two seats; verifies assignment, not just deselection.
    internal static void Editor(Window editor)
    {
        var type=editor.GetType();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var hall=(Tickets.App.HallView)type.GetField("preview",flags)!.GetValue(editor)!;
        var prices=(IDictionary)type.GetField("prices",flags)!.GetValue(editor)!;
        var seats=(List<SeatInfo>)type.GetField("seats",flags)!.GetValue(editor)!;
        var amount=Descendants(editor).OfType<TextBox>().Single(box=>box.Parent is Panel p&&p.Children.OfType<TextBlock>().Any(t=>t.Text=="Цена, ₽"));
        var chosen=seats.Take(2).Select(s=>s.Id).ToArray();
        hall.SetSelection(chosen);amount.Text="10000";amount.Focus();
        var original=(UIElement)editor.Content;
        KeyEventHandler consumed=(_,e)=>{if(e.Key==Key.Enter)e.Handled=true;};
        original.PreviewKeyDown+=consumed;
        try{amount.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(amount),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});}
        finally{original.PreviewKeyDown-=consumed;}
        Require(chosen.All(id=>prices[id] is PriceValue {Amount:10000}),"Enter assigns entered amount despite an ancestor handling the routed event");
        Require(hall.Selected.Count==0,"Successful Enter assignment clears selected seats");
        var summary=(Grid)type.GetField("priceSummary",flags)!.GetValue(editor)!;
        foreach(var cell in summary.Children.OfType<Border>())
        {
            var text=(TextBlock)cell.Child;
            Require(text.Text=="10000₽×2=20000₽"&&text.TextWrapping==TextWrapping.NoWrap,"Summary uses exact unbroken price expression");
            Require(cell.HorizontalAlignment==HorizontalAlignment.Left,"Colored summaries fit content");
        }
        Require(summary.ColumnDefinitions.All(c=>c.Width.IsAuto),"Summary columns measure their content");
        var tip=(ToolTip)typeof(Tickets.App.HallView).GetField("seatTip",flags)!.GetValue(hall)!;
        Require(tip.Foreground==Brushes.Black&&tip.FontWeight==FontWeights.Bold&&tip.Background is SolidColorBrush {Color.A:255}&&tip.Opacity==1,"Seat tooltip has opaque light background and bold black text");
        Require(tip.Padding.Left>=8&&tip.Padding.Top>=5,"Seat tooltip leaves room around text");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Descendants(child))yield return item;}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
