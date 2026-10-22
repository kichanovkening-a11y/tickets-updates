using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;

internal static class ContractFields
{
    internal static FrameworkElement Row(Store store,long eventId,long issueId,Action changed)
    {
        var panel=new WrapPanel{Margin=new Thickness(0,4,0,4)};
        TextBox Text(string label,string value,double width){panel.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});var box=new TextBox{Text=value,Width=width,MinHeight=30,Margin=new Thickness(0,0,12,0)};panel.Children.Add(box);return box;}
        var number=Text("№ договора",store.ContractNumber(eventId,issueId),120);
        panel.Children.Add(new TextBlock{Text="Дата договора",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});
        var date=new DatePicker{SelectedDate=store.ContractDate(eventId,issueId),Width=140,Margin=new Thickness(0,0,12,0)};panel.Children.Add(date);
        date.SelectedDateChanged+=(_,_)=>{if(date.SelectedDate is DateTime value){store.SetContractDate(eventId,issueId,value);changed();}};
        var representative=store.ContractRepresentative(eventId,issueId);
        var signatory=Text("Представитель получателя",representative.Signatory,220);
        var position=Text("Должность",representative.Position,180);
        var save=new Button{Content="Применить к комплекту",Padding=new Thickness(8,3,8,3),MinHeight=30};panel.Children.Add(save);
        save.Click+=(_,_)=>{
            try{store.SetContractNumber(eventId,issueId,number.Text);if(date.SelectedDate is DateTime value)store.SetContractDate(eventId,issueId,value);store.SetContractRepresentative(eventId,issueId,signatory.Text,position.Text);changed();}
            catch(Exception ex){MessageBox.Show(Window.GetWindow(panel),ex.Message,"Поля договора",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
        return panel;
    }
}
