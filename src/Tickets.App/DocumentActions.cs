using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tickets.App;

// Document commands share one content-sized row, leaving the preview full width.
internal static class DocumentActions
{
    internal static void Append(ScrollViewer navigation,ScrollViewer actions)
    {
        if(navigation.Content is not Panel row||actions.Content is not UIElement content)return;
        actions.Content=null;row.Children.Add(content);
    }
    internal static ScrollViewer? TakeToolbar(DependencyObject root)
    {
        if(root is Panel panel)
        {
            foreach(var item in panel.Children.OfType<UIElement>().ToArray())
            {
                if(item is ScrollViewer scroll&&scroll.Content is DockPanel){panel.Children.Remove(item);return scroll;}
                var found=TakeToolbar(item);if(found is not null)return found;
            }
        }
        return null;
    }
    internal static ScrollViewer Toolbar(Action openWord,Action save,Action print,Action? printTickets=null)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        void Add(string caption,Action action,Brush color)
        {
            var button=new Button{Content=caption,ContentTemplate=null,FontSize=16.9,Width=double.NaN,
                MinHeight=34,Padding=new Thickness(2,2,2,2),Margin=new Thickness(0,0,5,4),
                Background=color,Foreground=Brushes.White,FontWeight=FontWeights.Bold};
            button.Click+=(_,_)=>action();row.Children.Add(button);
        }
        Add("Открыть в Word",openWord,Brushes.SlateGray);
        Add("Сохранить",save,Brushes.SteelBlue);
        Add("Печать",print,Brushes.SeaGreen);
        var layout=new DockPanel{LastChildFill=false};DockPanel.SetDock(row,Dock.Left);layout.Children.Add(row);
        if(printTickets is not null)
        {
            var tickets=new Button{Content="Печать билетов",ContentTemplate=null,FontSize=16.9,Width=double.NaN,MinHeight=34,Padding=new Thickness(2,2,2,2),Margin=new Thickness(8,0,0,4),Background=Brushes.SeaGreen,Foreground=Brushes.White,FontWeight=FontWeights.Bold};tickets.Click+=(_,_)=>printTickets();DockPanel.SetDock(tickets,Dock.Right);layout.Children.Add(tickets);
        }
        return new ScrollViewer{Content=layout,HorizontalContentAlignment=HorizontalAlignment.Stretch,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,0,4)};
    }
}
