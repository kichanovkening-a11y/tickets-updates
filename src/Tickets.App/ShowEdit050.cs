using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class ShowEdit050Window:EditorWindow
{
    internal ShowEdit050Window(Store store,EventInfo show):base(store,"Редактировать показ",700,450)
    {
        var panel=new StackPanel{Margin=new Thickness(20)};Content=panel;
        var title=Field(panel,"Название спектакля",show.Title);
        var time=Field(panel,"Время (ЧЧ:ММ:СС)",DateTime.Parse(show.SortDate,CultureInfo.InvariantCulture).ToString("HH:mm:ss",CultureInfo.InvariantCulture));
        Button(panel,"Сохранить",()=>Try(()=>{
            if(!TimeSpan.TryParseExact(time.Text.Trim(),["hh\\:mm\\:ss","hh\\:mm"],CultureInfo.InvariantCulture,out var value))
                throw new TicketException("Введите время в формате ЧЧ:ММ:СС.");
            store.EditShow050(show.Id,title.Text,value);Finish(true);
        }));
    }
}
public partial class MainWindow
{
    private void EditShow050(EventInfo show)=>Run(()=>{
        if(new ShowEdit050Window(store,show).ShowDialog()!=true)return;
        ReadEventCatalog();SearchBox.Clear();ApplyFilter();SelectEvent(show.Id);
    });
}
