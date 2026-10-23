using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;

internal static class ContractFields
{
    internal static FrameworkElement Row(Store store,long eventId,long issueId,Action changed)
    {
        var panel=new WrapPanel{Margin=new Thickness(0,0,0,2)};
        TextBox Text(string label,string value,double width){panel.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});var box=new TextBox{Text=value,Width=width,Height=34,MinHeight=34,FontSize=19,FontWeight=FontWeights.Bold,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,0,12,0)};panel.Children.Add(box);return box;}
        var number=Text("№ договора",store.ContractNumber(eventId,issueId),85);
        panel.Children.Add(new TextBlock{Text="Дата договора",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});
        var date=new DatePicker{SelectedDate=store.ContractDate(eventId,issueId),Width=140,Margin=new Thickness(0,0,12,0)};panel.Children.Add(date);
        date.Loaded+=(_,_)=>{if(date.Template.FindName("PART_TextBox",date) is System.Windows.Controls.Primitives.DatePickerTextBox input){input.TextAlignment=TextAlignment.Center;input.VerticalContentAlignment=VerticalAlignment.Center;}if(!double.IsNaN(date.Height))return;var originalHeight=date.ActualHeight;date.Height=originalHeight;number.Height=originalHeight;number.MinHeight=originalHeight;date.FontSize=19;date.FontWeight=FontWeights.Bold;};
        date.SelectedDateChanged+=(_,_)=>{if(date.SelectedDate is DateTime value){store.SetContractDate(eventId,issueId,value);changed();}};
        number.MaxLength=100;
        number.TextChanged+=(_,_)=>{
            if(string.IsNullOrWhiteSpace(number.Text)||number.Text.Any(char.IsControl))return;
            try{store.SetContractNumber(eventId,issueId,number.Text);changed();}
            catch(Exception ex){MessageBox.Show(Window.GetWindow(panel),ex.Message,"Поля договора",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
        number.LostKeyboardFocus+=(_,_)=>{if(string.IsNullOrWhiteSpace(number.Text))number.Text=store.ContractNumber(eventId,issueId);};
        return panel;
    }
}
