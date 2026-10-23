using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class SenderEditorWindow:EditorWindow
{
    public string CreatedName{get;private set;}="";
    public SenderEditorWindow(Store store,string initial="",bool editExisting=false):base(store,editExisting?"Реквизиты отправителя":"Новый отправитель",860,780)
    {
        var form=new StackPanel{Margin=new Thickness(24)};
        Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        form.Children.Add(new TextBlock{Text=editExisting?"Реквизиты отправителя":"Новый отправитель",FontSize=23,FontWeight=FontWeights.SemiBold});
        var name=Field(form,"Название отправителя",initial);name.IsReadOnly=editExisting;
        var fields=new OrganizationDetailsForm(form,editExisting?Store.GetOrganizationDetails(initial,true):new());
        form.Children.Add(new TextBlock{Text="Реквизиты сохранятся для новых выдач. Уже оформленные документы сохраняют свои реквизиты.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)});
        var actions=new WrapPanel();form.Children.Add(actions);
        var save=Button(actions,editExisting?"Сохранить":"Создать",()=>Try(()=>{
            if(editExisting&&!Store.Senders().Any(sender=>CatalogText.Key(sender)==CatalogText.Key(name.Text)))throw new TicketException("Отправитель удалён из списка. Создайте его заново, чтобы вернуть в список.");
            var details=fields.Read(name.Text);
            if(editExisting&&System.Text.Json.JsonSerializer.Serialize(details)!=System.Text.Json.JsonSerializer.Serialize(Store.GetOrganizationDetails(initial,true))&&MessageBox.Show(DialogOwner,"Сохранить изменения реквизитов выбранного отправителя?","Изменение отправителя",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            Store.SaveOrganizationDetails(name.Text,true,details);CreatedName=Store.SaveSender(name.Text);Finish(true);
        }));save.IsDefault=true;
        var cancel=Button(actions,"Отмена",()=>Finish(false));cancel.Margin=new Thickness(10,0,0,0);cancel.IsCancel=true;
        OnShown(()=>{name.Focus();name.SelectAll();});
    }
}
