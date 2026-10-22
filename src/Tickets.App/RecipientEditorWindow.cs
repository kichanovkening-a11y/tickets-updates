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
        var name=Field(form,"Название получателя",initial);
        var fields=new OrganizationDetailsForm(form,editExisting?Store.GetOrganizationDetails(initial,false):new());
        form.Children.Add(new TextBlock{Text="Реквизиты можно заполнить позже. Пустые реквизиты прежних выдач дополняются из справочника. Уже заполненные реквизиты сохраняются; обновить их можно в накладной.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)});
        var actions=new WrapPanel();form.Children.Add(actions);
        var save=Button(actions,editExisting?"Сохранить":"Создать",()=>Try(()=>{
            var details=fields.Read(name.Text);
            if(editExisting&&name.Text.Trim()!=initial)Store.RenameRecipient(initial,name.Text);
            Store.SaveOrganizationDetails(name.Text,false,details);CreatedName=Store.SaveRecipient(name.Text);Finish(true);
        }));save.IsDefault=true;
        var cancel=Button(actions,"Отмена",()=>Finish(false));cancel.Margin=new Thickness(10,10,0,0);cancel.IsCancel=true;
        OnShown(()=>{name.Focus();name.SelectAll();});
    }
}

internal sealed class OrganizationDetailsForm
{
    private readonly TextBox legalName,inn,kpp,ogrn,address,bank,account,correspondentAccount,bik,signatory,position,phone,okpo,okved,okato,authority;
    private readonly List<(TextBox Label,TextBox Value)> extraFields=new();
    public OrganizationDetailsForm(Panel parent,OrganizationDetails value)
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());parent.Children.Add(grid);
        var left=new StackPanel{Margin=new Thickness(0,0,12,0)};var right=new StackPanel();Grid.SetColumn(right,1);grid.Children.Add(left);grid.Children.Add(right);
        TextBox Field(Panel panel,string label,string text){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,10,0,4)});var box=new TextBox{Text=text,MinHeight=30};panel.Children.Add(box);return box;}
        legalName=Field(left,"Название организации в документах",value.LegalName);
        inn=Field(left,"ИНН",value.Inn);kpp=Field(left,"КПП (если есть)",value.Kpp);ogrn=Field(left,"ОГРН / ОГРНИП",value.Ogrn);address=Field(left,"Юридический адрес",value.Address);signatory=Field(left,"Подписант (ФИО)",value.Signatory);position=Field(left,"Должность подписанта",value.Position);authority=Field(left,"Основание полномочий",value.Authority);
        bank=Field(right,"Банк",value.Bank);account=Field(right,"Расчётный счёт",value.Account);correspondentAccount=Field(right,"Корреспондентский счёт",value.CorrespondentAccount);bik=Field(right,"БИК",value.Bik);phone=Field(right,"Телефон",value.Phone);
        okpo=Field(left,"ОКПО (если есть)",value.Okpo);okved=Field(left,"ОКВЭД (если есть)",value.Okved);okato=Field(right,"ОКАТО (если есть)",value.Okato);
        parent.Children.Add(new TextBlock{Text="Дополнительные реквизиты",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,18,0,4)});
        var rows=new StackPanel();parent.Children.Add(rows);
        void AddExtra(string label="",string text="")
        {
            var row=new Grid{Margin=new Thickness(0,4,0,4)};
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3,GridUnitType.Star)});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var key=new TextBox{Text=label,MinHeight=30,Margin=new Thickness(0,0,8,0),ToolTip="Название реквизита"};
            var val=new TextBox{Text=text,MinHeight=30,Margin=new Thickness(0,0,8,0),ToolTip="Значение реквизита"};Grid.SetColumn(val,1);
            var remove=new Button{Content="Удалить",Padding=new Thickness(8,3,8,3)};Grid.SetColumn(remove,2);
            var pair=(key,val);extraFields.Add(pair);
            remove.Click+=(_,_)=>{extraFields.Remove(pair);rows.Children.Remove(row);};
            row.Children.Add(key);row.Children.Add(val);row.Children.Add(remove);rows.Children.Add(row);
        }
        foreach(var extra in value.ExtraFields??[])AddExtra(extra.Label,extra.Value);
        var add=new Button{Content="+ Добавить реквизит",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,0),Padding=new Thickness(10,5,10,5)};
        add.Click+=(_,_)=>AddExtra();parent.Children.Add(add);
    }
    public OrganizationDetails Read(string name)=>new(Name:name,LegalName:legalName.Text,Inn:inn.Text,Kpp:kpp.Text,Ogrn:ogrn.Text,Address:address.Text,Bank:bank.Text,Account:account.Text,CorrespondentAccount:correspondentAccount.Text,Bik:bik.Text,Signatory:signatory.Text,Position:position.Text,Authority:authority.Text,Phone:phone.Text,Okpo:okpo.Text,Okved:okved.Text,Okato:okato.Text,ExtraFields:extraFields.Select(field=>new OrganizationExtraField(field.Label.Text,field.Value.Text)).ToArray());
}
