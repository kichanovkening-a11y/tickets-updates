using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class RecipientEditorWindow:EditorWindow
{
    public string CreatedName{get;private set;}="";

    public RecipientEditorWindow(Store store,string initial=""):base(store,"Новый получатель",680,450)
    {
        var form=new StackPanel{Margin=new Thickness(24)};Content=form;
        form.Children.Add(new TextBlock{Text="Новый получатель / организация",FontSize=23,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        var name=Field(form,"Название получателя",initial);
        form.Children.Add(new TextBlock{Text="Получатель сохранится в справочнике. Выдачу билетов можно оформить позже.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)});
        var actions=new WrapPanel();form.Children.Add(actions);
        var save=Button(actions,"Создать",()=>Try(()=>{CreatedName=Store.SaveRecipient(name.Text);Finish(true);}));save.IsDefault=true;
        var cancel=Button(actions,"Отмена",()=>Finish(false));cancel.Margin=new Thickness(10,10,0,0);cancel.IsCancel=true;
        OnShown(()=>{name.Focus();name.SelectAll();});
    }
}
