using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

internal static class EditorLayout044
{
    internal static void Install(FrameworkElement scope)
    {
        var previous=new Vector();
        void Refresh()
        {
            var scale=CompactText043.PhysicalScale(scope);
            if((scale-previous).Length<.001)return;previous=scale;
            scope.Resources["EditorInset044"]=new Thickness(5/scale.X,5/scale.Y,5/scale.X,5/scale.Y);
            scope.Resources["EditorBottom044"]=new Thickness(0,0,0,5/scale.Y);
            scope.Resources["EditorLeft044"]=new Thickness(5/scale.X,0,0,0);
            scope.Resources["EditorRight044"]=new Thickness(0,0,5/scale.X,0);
            scope.Resources["EditorPlaquePadding044"]=new Thickness(4/scale.X,4/scale.Y,4/scale.X,4/scale.Y);
            scope.Resources["EditorBorder044"]=new Thickness(1/scale.X,1/scale.Y,1/scale.X,1/scale.Y);
        }
        scope.Loaded+=(_,_)=>Refresh();scope.LayoutUpdated+=(_,_)=>Refresh();Refresh();
    }
    internal static Button ExpandButton(Action toggle)
    {
        var canvas=new Canvas{Width=24,Height=24};
        foreach(var angle in new[]{0,90,180,270})canvas.Children.Add(new System.Windows.Shapes.Path
        {Fill=new SolidColorBrush(Color.FromRgb(0,180,40)),Data=Geometry.Parse("M2,2 L10,2 L10,5 L7,5 L12,10 L10,12 L5,7 L5,10 L2,10 Z"),RenderTransform=new RotateTransform(angle,12,12)});
        var button=new Button{Width=32,Height=32,Padding=new Thickness(3),Margin=new Thickness(0),ContentTemplate=null,Content=new Viewbox{Width=24,Height=24,Child=canvas},VerticalAlignment=VerticalAlignment.Center};
        button.Click+=(_,_)=>toggle();return button;
    }
    internal static void CompactPanel(Panel panel)
    {
        for(var i=0;i<panel.Children.Count;i++)
        {
            if(panel.Children[i] is TextBlock text)
            {
                panel.Children.RemoveAt(i);text.Margin=new Thickness(0);
                var host=new InkTextHost044{Child=text};panel.Children.Insert(i,host);
                host.SetResourceReference(FrameworkElement.MarginProperty,"EditorBottom044");
            }
            else if(panel.Children[i] is FrameworkElement element)
            {
                element.SetResourceReference(FrameworkElement.MarginProperty,"EditorBottom044");
                if(element is Panel nested)CompactPanel(nested);
            }
        }
        if(panel.Children.Count>0&&panel.Children[^1] is FrameworkElement last)last.Margin=new Thickness(0);
    }
}

// Preserve TextBlock runs (including red totals), while excluding font leading
// from the form's measured spacing. Text and font remain owned by the original control.
internal sealed class InkTextHost044:Decorator
{
    private double top;
    protected override Size MeasureOverride(Size constraint)
    {
        if(Child is not TextBlock text)return base.MeasureOverride(constraint);
        text.Measure(constraint);
        var formatted=new FormattedText(text.Text,CultureInfo.CurrentUICulture,text.FlowDirection,
            new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if(text.TextWrapping!=TextWrapping.NoWrap&&double.IsFinite(constraint.Width))formatted.MaxTextWidth=Math.Max(1,constraint.Width);
        var ink=formatted.BuildGeometry(new Point()).Bounds;
        top=ink.IsEmpty?0:ink.Top;
        return new Size(text.DesiredSize.Width,ink.IsEmpty?0:ink.Height);
    }
    protected override Size ArrangeOverride(Size size)
    {Child?.Arrange(new Rect(0,-top,size.Width,Child.DesiredSize.Height));return size;}
}
