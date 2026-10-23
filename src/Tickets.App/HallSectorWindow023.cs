using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;

internal sealed class HallSectorWindow023:EditorWindow
{
    public IReadOnlyList<DraftSeat> Result{get;private set;}=[];
    public HallSectorWindow023(Store store):base(store,"Добавить сектор",510,420)
    {
        var form=new StackPanel{Margin=new Thickness(16)};Content=form;
        var name=Field(form,"Название сектора","");
        var rows=Field(form,"Количество рядов","");var places=Field(form,"Количество мест в ряду","");
        Button(form,"Сохранить",()=>Try(()=>{
            var count=Number(rows,1,200);var seats=Number(places,1,300);
            if(count*seats>50000)throw new TicketException("Максимум 50 000 мест.");
            var layout=Enumerable.Range(1,count).Select(i=>new HallRowLayout{Label=i.ToString(),SeatCount=seats}).ToArray();
            Result=HallLayout.Generate(name.Text,layout,"1",false,0);Finish(true);
        }));
    }
}
