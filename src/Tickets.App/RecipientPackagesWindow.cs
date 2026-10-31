using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class RecipientPackagesWindow:EditorWindow
{
    internal RecipientPackagesWindow(Store store,string recipient):base(store,"Сохранённые комплекты",850,620)
    {
        var root=new DockPanel{Margin=new Thickness(18)};Content=root;
        var heading=new TextBlock{Text=recipient,FontSize=21,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var archive=false;
        var archiveButton=new Button{Content="Архив",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8)};
        DockPanel.SetDock(archiveButton,Dock.Top);root.Children.Add(archiveButton);
        var list=new StackPanel();root.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        void Refresh()
        {
            list.Children.Clear();var dates=Store.DocumentArchive();
            var packages=Store.RecipientPackages(recipient).Where(p=>dates.Contains(p)==archive).ToArray();
            archiveButton.Content=archive?"Активные":"Архив";
            if(packages.Length==0)list.Children.Add(new TextBlock{Text=archive?"Архив пуст":"Активных комплектов нет"});
            foreach(var package in packages)
            {
                var button=Button(list,$"Комплект № {package.Id} · выдач: {package.Issues.Count} · билетов: {package.Invoice.Tickets.Count}",()=>Try(()=>{var editor=new RecipientPackageWindow(Store,package.Id){Owner=DialogOwner};editor.ShowDialog();}));
                button.Width=double.NaN;button.ContentTemplate=null;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(10,2,10,2);button.Tag=package.Id;
            }
        }
        archiveButton.Click+=(_,_)=>Try(()=>{archive=!archive;Refresh();});Refresh();
    }
}
