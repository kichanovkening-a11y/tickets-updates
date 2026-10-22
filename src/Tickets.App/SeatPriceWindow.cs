using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class SeatPriceWindow:EditorWindow
{
    private readonly TextBox amount;
    private readonly TextBlock error=new(){Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0),Visibility=Visibility.Collapsed};
    internal long Amount{get;private set;}
    internal string? Color{get;private set;}
    internal SeatPriceWindow(Store store,long initial,int count):base(store,"Сменить цену",380,225)
    {
        var body=new StackPanel{Margin=new Thickness(12)};Content=body;
        body.Children.Add(new TextBlock{Text="Новая цена, ₽",Margin=new Thickness(0,0,0,4)});
        amount=new TextBox{Text=initial.ToString(CultureInfo.InvariantCulture),MinHeight=32};body.Children.Add(amount);
        body.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};body.Children.Add(actions);
        var apply=Button(actions,"Применить",Apply);apply.IsDefault=true;apply.Width=140;apply.Height=34;apply.Margin=new Thickness(0,6,0,0);apply.ContentTemplate=null;
        var cancel=Button(actions,"Отмена",()=>Close());cancel.Width=140;cancel.Height=34;cancel.ContentTemplate=null;cancel.IsCancel=true;cancel.Margin=new Thickness(8,6,0,0);
        void Refresh()
        {
            var valid=long.TryParse(amount.Text,out var price)&&price>=0;apply.IsEnabled=valid;error.Text=valid?"":"Введите неотрицательную целую цену.";error.Visibility=valid?Visibility.Collapsed:Visibility.Visible;
        }
        amount.TextChanged+=(_,_)=>Refresh();Refresh();
        body.AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler((_,e)=>{if(e.Key==Key.Enter){e.Handled=true;if(apply.IsEnabled)Apply();}}),true);
        OnShown(()=>{amount.Focus();amount.SelectAll();});
    }
    private void Apply()
    {
        if(!long.TryParse(amount.Text,out var value)||value<0)return;
        Amount=value;Color=null;Finish(true);
    }
}
