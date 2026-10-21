using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

internal static class PriceSummaryLayout
{
    internal static void Install(Grid grid)
    {
        grid.Loaded+=(_,_)=>{
            if(Scroll(grid) is {} scroll)scroll.SizeChanged+=(_,_)=>Arrange(grid);
            Arrange(grid);
        };
    }
    private static ScrollViewer? Scroll(DependencyObject node)
    {
        for(var parent=VisualTreeHelper.GetParent(node);parent is not null;parent=VisualTreeHelper.GetParent(parent))if(parent is ScrollViewer scroll)return scroll;
        return null;
    }
    internal static void Arrange(Grid grid)
    {
        var items=grid.Children.OfType<FrameworkElement>().ToArray();
        foreach(var item in items)item.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        double Width(int rows)
        {
            var total=0d;
            for(var index=0;index<items.Length;index+=rows){var widest=0d;for(var i=index;i<Math.Min(items.Length,index+rows);i++)widest=Math.Max(widest,items[i].DesiredSize.Width);total+=widest;}
            return total;
        }
        var scroll=Scroll(grid);var available=scroll?.ViewportWidth??0;if(available<=0)available=scroll?.ActualWidth??0;
        var rows=items.Length>2&&available>0&&Width(2)>available+.5?3:2;
        grid.RowDefinitions.Clear();grid.ColumnDefinitions.Clear();
        for(var row=0;row<Math.Min(rows,items.Length);row++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        for(var column=0;column<(items.Length+rows-1)/rows;column++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        for(var index=0;index<items.Length;index++){Grid.SetRow(items[index],index%rows);Grid.SetColumn(items[index],index/rows);}
    }
}
