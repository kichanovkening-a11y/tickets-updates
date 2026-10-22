using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

// Document commands share one content-sized row, leaving the preview full width.
internal static class DocumentActions
{
    internal static ScrollViewer Toolbar(Action openWord,Action save,Action print)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        void Add(string caption,Action action,Brush color)
        {
            var button=new Button{Content=caption,ContentTemplate=null,FontSize=16.9,Width=double.NaN,
                MinHeight=34,Padding=new Thickness(10,2,10,2),Margin=new Thickness(0,0,5,4),
                Background=color,Foreground=Brushes.White,FontWeight=FontWeights.Bold};
            button.Click+=(_,_)=>action();row.Children.Add(button);
        }
        Add("Открыть в Word",openWord,Brushes.SlateGray);
        Add("Сохранить",save,Brushes.SteelBlue);
        Add("Печать",print,Brushes.SeaGreen);
        return new ScrollViewer{Content=row,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,0,4)};
    }
}
