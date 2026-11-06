using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Approved050UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var hall=(HallView)main.FindName("Hall");var legend=(Grid)main.FindName("LegendPanel");
        var statuses=(WrapPanel)main.FindName("StatusLegendPanel");
        hall.ClearFocus030();Pump();
        var normal=typeof(HallView).GetField("drawing",Private)!.GetValue(hall);
        var borders=legend.Children.Cast<object>().ToArray();var entries=statuses.Children.Cast<object>().ToArray();
        var selected=hall.Selected.ToArray();
        foreach(var value in Enum.GetValues<HallStatus030>())
        {
            hall.FocusStatus030=value;hall.ClearFocus030();Pump();
            Require(ReferenceEquals(normal,typeof(HallView).GetField("drawing",Private)!.GetValue(hall)),"focus reset must reuse normal drawing");
            Require(legend.Children.Cast<object>().SequenceEqual(borders)&&statuses.Children.Cast<object>().SequenceEqual(entries),"focus must not recreate legend controls");
            Require(hall.Selected.SetEquals(selected),"focus changed selected seats");
        }
        var highContrast=typeof(CompactText043).GetProperty("HighContrast050")??throw new Exception("High contrast price rendering missing");
        foreach(var label in legend.Children.OfType<Border>().Select(b=>b.Child).OfType<CompactText043>())
            Require(label.Foreground==Brushes.White&&(bool)highContrast.GetValue(label)!&&label.FontSize==13.44,"price label style or size differs");
        var text=new CompactText043{Text="1000₽×852=852000₽",FontSize=13.44};
        text.Measure(new Size(240,100));var size=text.DesiredSize;highContrast.SetValue(text,true);text.Measure(new Size(240,100));
        Require(text.DesiredSize==size,"contrast changed text measurement");
        Console.WriteLine("PASS 050 focus caching, stable controls, preserved selection and label dimensions");
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
