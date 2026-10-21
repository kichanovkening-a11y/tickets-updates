using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class RecipientEditorWindow:EditorWindow
{
    public string CreatedName{get;private set;}="";

    public RecipientEditorWindow(Store store,string initial="",bool editExisting=false):base(store,editExisting?"Реквизиты получателя":"Новый получатель",860,780)
    {
        var form=new StackPanel{Margin=new Thickness(24)};
        Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        form.Children.Add(new TextBlock{Text=editExisting?"Реквизиты получателя / организации":"Новый получатель / организация",FontSize=23,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        var name=Field(form,"Название получателя",initial);name.IsReadOnly=editExisting;
        var fields=new OrganizationDetailsForm(form,editExisting?Store.GetOrganizationDetails(initial,false):new());
        form.Children.Add(new TextBlock{Text="Реквизиты можно заполнить позже. Пустые реквизиты прежних выдач дополняются из справочника. Уже заполненные реквизиты сохраняются; обновить их можно в накладной.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)});
        var actions=new WrapPanel();form.Children.Add(actions);
        var save=Button(actions,editExisting?"Сохранить":"Создать",()=>Try(()=>{
            Store.SaveOrganizationDetails(name.Text,false,fields.Read(name.Text));CreatedName=Store.SaveRecipient(name.Text);Finish(true);
        }));save.IsDefault=true;
        var cancel=Button(actions,"Отмена",()=>Finish(false));cancel.Margin=new Thickness(10,10,0,0);cancel.IsCancel=true;
        OnShown(()=>{name.Focus();name.SelectAll();});
    }
}

internal sealed class OrganizationDetailsForm
{
    private readonly TextBox inn,kpp,ogrn,address,bank,account,correspondentAccount,bik,signatory,position,phone,okpo,okved,okato;
    public OrganizationDetailsForm(Panel parent,OrganizationDetails value)
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());parent.Children.Add(grid);
        var left=new StackPanel{Margin=new Thickness(0,0,12,0)};var right=new StackPanel();Grid.SetColumn(right,1);grid.Children.Add(left);grid.Children.Add(right);
        TextBox Field(Panel panel,string label,string text){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,10,0,4)});var box=new TextBox{Text=text,MinHeight=30};panel.Children.Add(box);return box;}
        inn=Field(left,"ИНН",value.Inn);kpp=Field(left,"КПП (если есть)",value.Kpp);ogrn=Field(left,"ОГРН / ОГРНИП",value.Ogrn);address=Field(left,"Юридический адрес",value.Address);signatory=Field(left,"Подписант (ФИО)",value.Signatory);position=Field(left,"Должность подписанта",value.Position);
        bank=Field(right,"Банк",value.Bank);account=Field(right,"Расчётный счёт",value.Account);correspondentAccount=Field(right,"Корреспондентский счёт",value.CorrespondentAccount);bik=Field(right,"БИК",value.Bik);phone=Field(right,"Телефон",value.Phone);
        okpo=Field(left,"ОКПО (если есть)",value.Okpo);okved=Field(left,"ОКВЭД (если есть)",value.Okved);okato=Field(right,"ОКАТО (если есть)",value.Okato);
    }
    public OrganizationDetails Read(string name)=>new(Name:name,Inn:inn.Text,Kpp:kpp.Text,Ogrn:ogrn.Text,Address:address.Text,Bank:bank.Text,Account:account.Text,CorrespondentAccount:correspondentAccount.Text,Bik:bik.Text,Signatory:signatory.Text,Position:position.Text,Phone:phone.Text,Okpo:okpo.Text,Okved:okved.Text,Okato:okato.Text);
}
