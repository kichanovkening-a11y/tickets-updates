using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tickets.App;
internal static class DocumentFlag025
{
    internal static void Apply(Window window,UIElement document,string recipient,long issueId)
    {
        window.Content=null;window.Title=recipient;
        var root=new DockPanel();var label=new TextBlock{Text=$"Счёт — выдача № {issueId}",LayoutTransform=new RotateTransform(90),Margin=new Thickness(6),FontWeight=FontWeights.Bold};
        DockPanel.SetDock(label,Dock.Left);root.Children.Add(label);root.Children.Add(document);window.Content=root;
    }
}
