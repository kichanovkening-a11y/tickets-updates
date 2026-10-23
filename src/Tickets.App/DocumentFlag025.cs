using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tickets.App;
internal static class DocumentFlag025
{
    internal static void Apply(Window window,UIElement document,string recipient,long issueId,string name="Счёт")
    {
        window.Title=recipient;
        if(window is EditorWindow editor){editor.DocumentTab=$"{name} — выдача № {issueId}";
            window.Loaded+=(_,_)=>{if(!editor.IsInline&&window.Content is UIElement body){window.Content=null;window.Content=Separate(body,editor.DocumentTab);}};
        }
    }
    internal static Border Tab(string text)=>new(){Background=Brushes.AliceBlue,BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(1,1,0,1),
        HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,60,-1,0),
        Child=new TextBlock{Text=text,LayoutTransform=new RotateTransform(90),Margin=new Thickness(5),FontWeight=FontWeights.Bold,TextAlignment=TextAlignment.Center}};
    internal static UIElement Separate(UIElement body,string text)
    {
        var root=new Grid();root.ColumnDefinitions.Add(new(){Width=GridLength.Auto});root.ColumnDefinitions.Add(new());
        root.Children.Add(Tab(text));var frame=new Border{BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(1),Child=body};Grid.SetColumn(frame,1);root.Children.Add(frame);return root;
    }
}
