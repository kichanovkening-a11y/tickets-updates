using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

// A small text surface whose layout bounds are the visible glyphs, not the
// font's ascender/descender reserve. Used only by the compact hall interface.
public sealed class CompactText043 : Control
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(CompactText043),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty TextWrappingProperty=DependencyProperty.Register(nameof(TextWrapping),typeof(TextWrapping),typeof(CompactText043),new FrameworkPropertyMetadata(TextWrapping.NoWrap,FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty TextAlignmentProperty=DependencyProperty.Register(nameof(TextAlignment),typeof(TextAlignment),typeof(CompactText043),new FrameworkPropertyMetadata(TextAlignment.Center,FrameworkPropertyMetadataOptions.AffectsRender));
    public string Text{get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    public TextWrapping TextWrapping{get=>(TextWrapping)GetValue(TextWrappingProperty);set=>SetValue(TextWrappingProperty,value);}
    public TextAlignment TextAlignment{get=>(TextAlignment)GetValue(TextAlignmentProperty);set=>SetValue(TextAlignmentProperty,value);}
    private readonly List<(FormattedText Text,Rect Ink)> lines=[];
    private double lineGap;
    public CompactText043(){Focusable=false;IsTabStop=false;}

    internal static Vector PhysicalScale(Visual visual)
    {
        var source=PresentationSource.FromVisual(visual);
        var dpi=VisualTreeHelper.GetDpi(visual);
        if(source?.RootVisual is not Visual root)return new Vector(dpi.DpiScaleX,dpi.DpiScaleY);
        var transform=visual.TransformToAncestor(root);
        var zero=transform.Transform(new Point());
        var x=transform.Transform(new Point(1,0));var y=transform.Transform(new Point(0,1));
        return new Vector(Math.Max(.1,(x-zero).Length*dpi.DpiScaleX),Math.Max(.1,(y-zero).Length*dpi.DpiScaleY));
    }
    private FormattedText Format(string text)=>new(text,CultureInfo.CurrentUICulture,FlowDirection,
        new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),FontSize,Foreground,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    protected override Size MeasureOverride(Size availableSize)
    {
        lines.Clear();lineGap=3/PhysicalScale(this).Y;
        var width=TextWrapping==TextWrapping.NoWrap?double.PositiveInfinity:Math.Max(1,availableSize.Width);
        foreach(var paragraph in (Text??"").Replace("\r","").Split('\n'))
        {
            var remaining=paragraph;
            while(remaining.Length>0)
            {
                var count=remaining.Length;
                if(!double.IsPositiveInfinity(width)&&Format(remaining).WidthIncludingTrailingWhitespace>width)
                {
                    var low=1;var high=count;
                    while(low<high){var mid=(low+high+1)/2;if(Format(remaining[..mid]).WidthIncludingTrailingWhitespace<=width)low=mid;else high=mid-1;}
                    count=low;
                    var space=remaining.LastIndexOf(' ',Math.Min(count,remaining.Length-1));
                    if(space>0)count=space;
                    if(count<remaining.Length&&count>1&&char.IsHighSurrogate(remaining[count-1]))count--;
                }
                var text=Format(remaining[..count].Trim());var ink=text.BuildGeometry(new Point()).Bounds;
                if(!ink.IsEmpty)lines.Add((text,ink));
                remaining=remaining[count..].TrimStart();
            }
        }
        return lines.Count==0?new Size():new Size(lines.Max(x=>x.Ink.Width),lines.Sum(x=>x.Ink.Height)+lineGap*(lines.Count-1));
    }
    protected override void OnRender(DrawingContext dc)
    {
        var y=0d;
        foreach(var line in lines)
        {
            line.Text.SetForegroundBrush(Foreground);
            var x=TextAlignment==TextAlignment.Center?(ActualWidth-line.Ink.Width)/2:TextAlignment==TextAlignment.Right?ActualWidth-line.Ink.Width:0;
            dc.DrawText(line.Text,new Point(x-line.Ink.X,y-line.Ink.Y));
            y+=line.Ink.Height+lineGap;
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new CompactTextPeer(this);
    private sealed class CompactTextPeer(CompactText043 owner):FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore()=>nameof(CompactText043);
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Text;
        protected override string GetNameCore()=>owner.Text;
    }
}
