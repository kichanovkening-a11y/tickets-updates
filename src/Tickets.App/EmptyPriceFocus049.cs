using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Tickets.App;

internal static class EmptyPriceFocus049
{
    private static readonly List<WeakReference<HallView>> halls=[];
    private static WeakReference<Window>? originWindow;
    private static Point origin;
    private static bool moved;
    static EmptyPriceFocus049()
    {
        EventManager.RegisterClassHandler(typeof(Window),Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler(Down));
        EventManager.RegisterClassHandler(typeof(Window),Mouse.PreviewMouseMoveEvent,new MouseEventHandler(Move));
        EventManager.RegisterClassHandler(typeof(Window),Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(Up));
    }
    internal static void Install(HallView hall)
    {
        halls.RemoveAll(reference=>!reference.TryGetTarget(out _));
        halls.Add(new WeakReference<HallView>(hall));
    }
    private static void Down(object sender,MouseButtonEventArgs e)
    {
        originWindow=null;moved=false;
        if(sender is not Window window||e.ChangedButton!=MouseButton.Left||!IsEmpty(e.OriginalSource as DependencyObject))return;
        originWindow=new WeakReference<Window>(window);origin=e.GetPosition(window);
    }
    private static void Move(object sender,MouseEventArgs e)
    {
        if(originWindow is null||!originWindow.TryGetTarget(out var window))return;
        if(!ReferenceEquals(sender,window)||(e.GetPosition(window)-origin).Length>=3)moved=true;
    }
    private static void Up(object sender,MouseButtonEventArgs e)
    {
        var started=originWindow;originWindow=null;
        if(started is null||!started.TryGetTarget(out var window)||!ReferenceEquals(sender,window)||
            e.ChangedButton!=MouseButton.Left||moved||(e.GetPosition(window)-origin).Length>=3||!IsEmpty(e.OriginalSource as DependencyObject))return;
        // A blank click in any application window dismisses price highlighting,
        // including a documents window that contains no hall of its own.
        halls.RemoveAll(reference=>!reference.TryGetTarget(out _));
        foreach(var reference in halls.ToArray())
            if(reference.TryGetTarget(out var hall))hall.ClearPriceFocus049();
    }
    internal static bool IsEmpty(DependencyObject? source)
    {
        if(source is null)return false;
        for(var node=source;node is not null;node=Parent(node))
        {
            if(node is HallView or ButtonBase or TextBoxBase or PasswordBox or ComboBox or Slider or Thumb or ListBoxItem or DataGridCell or DataGridRow or TreeViewItem or TabItem or MenuItem or Hyperlink)return false;
            if(node is FrameworkElement element&&element.Cursor==Cursors.Hand)return false;
        }
        return true;
    }
    private static DependencyObject? Parent(DependencyObject node)=>node is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(node):node is ContentElement content?ContentOperations.GetParent(content)??(content as FrameworkContentElement)?.Parent:LogicalTreeHelper.GetParent(node);
}

// The old 11.44-point plaque height plus five physical pixels. Larger text
// stays centered; wrapped labels may grow further to keep every line visible.
internal sealed class PriceLegendBorder049:Border
{
    private bool? focusStyle;
    public PriceLegendBorder049(){LayoutUpdated+=(_,_)=>ApplyFocusStyle();}
    internal void SetFocusStyle049(bool active){focusStyle=active;ApplyFocusStyle();}
    private void ApplyFocusStyle()
    {
        if(focusStyle is not {} active)return;
        var scale=CompactText043.PhysicalScale(this);
        BorderBrush=active?Brushes.Black:Brushes.Gray;
        BorderThickness=new Thickness((active?3:1)/scale.X,(active?3:1)/scale.Y,(active?3:1)/scale.X,(active?3:1)/scale.Y);
        Padding=new Thickness((active?2:4)/scale.X,(active?2:4)/scale.Y,(active?2:4)/scale.X,(active?2:4)/scale.Y);
    }
    protected override Size MeasureOverride(Size constraint)
    {
        var size=base.MeasureOverride(constraint);
        if(Child is not CompactText043 text)return size;
        text.VerticalAlignment=VerticalAlignment.Center;
        var baseline=new CompactText043{Text=text.Text,TextWrapping=text.TextWrapping,FontSize=11.44,FontFamily=text.FontFamily,FontWeight=text.FontWeight,LineGap=text.LineGap};
        baseline.Measure(new Size(Math.Max(1,constraint.Width-Padding.Left-Padding.Right-BorderThickness.Left-BorderThickness.Right),double.PositiveInfinity));
        return new Size(size.Width,Math.Max(size.Height,baseline.DesiredSize.Height+15/CompactText043.PhysicalScale(this).Y));
    }
}
