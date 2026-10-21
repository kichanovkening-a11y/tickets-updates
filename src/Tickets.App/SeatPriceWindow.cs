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
    private readonly ComboBox color=new(){MinHeight=34};
    private readonly TextBlock error=new(){Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};
    internal long Amount{get;private set;}
    internal string? Color{get;private set;}
    internal SeatPriceWindow(Store store,long initial,int count):base(store,"Сменить цену",440,400)
    {
        var body=new StackPanel{Margin=new Thickness(18)};Content=body;
        body.Children.Add(new TextBlock{Text=$"Выбрано мест: {count}",FontWeight=FontWeights.Bold});
        amount=Field(body,"Новая цена, ₽",initial.ToString(CultureInfo.InvariantCulture));
        body.Children.Add(new TextBlock{Text="Цвет цены",Margin=new Thickness(0,10,0,4)});body.Children.Add(color);color.ItemTemplate=ColorChoices.Template();body.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};body.Children.Add(actions);
        var apply=Button(actions,"Применить",Apply);apply.IsDefault=true;apply.Width=140;apply.ContentTemplate=null;
        var cancel=Button(actions,"Отмена",()=>Close());cancel.Width=140;cancel.ContentTemplate=null;cancel.Margin=new Thickness(8,10,0,0);
        void Refresh()
        {
            var valid=long.TryParse(amount.Text,out var price)&&price>=0;apply.IsEnabled=valid;error.Text=valid?"":"Введите неотрицательную целую цену.";
            if(!valid){color.ItemsSource=null;return;}
            var colors=store.PriceColors();var mapped=colors.FirstOrDefault(p=>p.Amount==price)?.Color;
            var used=colors.Where(p=>p.Amount!=price).Select(p=>p.Color).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var choices=PricePalette.Colors.Where(c=>!used.Contains(c)).ToList();
            if(mapped is not null&&!choices.Contains(mapped))choices.Insert(0,mapped);
            if(choices.Count==0)choices.Add(PricePalette.NextAvailable(used));
            color.ItemsSource=choices;color.SelectedItem=mapped??choices[0];color.IsEnabled=mapped is null;
        }
        amount.TextChanged+=(_,_)=>Refresh();Refresh();
        body.AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler((_,e)=>{if(e.Key==Key.Enter){e.Handled=true;if(apply.IsEnabled)Apply();}}),true);
        OnShown(()=>{amount.Focus();amount.SelectAll();});
    }
    private void Apply()
    {
        if(!long.TryParse(amount.Text,out var value)||value<0||color.SelectedItem is not string selected)return;
        Amount=value;Color=selected;Finish(true);
    }
}
