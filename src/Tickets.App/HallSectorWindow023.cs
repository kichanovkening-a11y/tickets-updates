using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;

internal sealed class HallSectorWindow023:EditorWindow
{
    public IReadOnlyList<DraftSeat> Result{get;private set;}=[];
    public HallSectorWindow023(Store store,bool allowNumberingDirection=true):base(store,"Добавить сектор",510,420)
    {
        var form=new StackPanel{Margin=new Thickness(16)};Content=form;
        var name=Field(form,"Название сектора","");
        var rows=Field(form,"Количество рядов","");var places=Field(form,"Количество мест в ряду","");
        var reverse=new CheckBox{Content="Нумерация справа налево",Margin=new Thickness(0,8,0,0)};
        if(allowNumberingDirection)form.Children.Add(reverse);
        Button(form,"Сохранить",()=>Try(()=>{
            var count=Number(rows,1,200);var seats=Number(places,1,300);
            if(count*seats>50000)throw new TicketException("Максимум 50 000 мест.");
            var layout=Enumerable.Range(1,count).Select(i=>new HallRowLayout{Label=i.ToString(),SeatCount=seats}).ToArray();
            Result=HallLayout.Generate(name.Text,layout,"1",allowNumberingDirection&&reverse.IsChecked==true,0);Finish(true);
        }));
    }
}

internal sealed class HallSectorVariant2Window:EditorWindow
{
    public IReadOnlyList<DraftSeat> Result{get;private set;}=[];
    public HallSectorVariant2Window(Store store):base(store,"Добавить зону — Вариант 2",510,700)
    {
        var form=new DockPanel{Margin=new Thickness(16)};Content=form;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);form.Children.Add(header);
        var name=Field(header,"Название зоны");var count=Field(header,"Количество рядов");
        var rows=new StackPanel();var fields=new List<TextBox>();
        void Rebuild()
        {
            if(!int.TryParse(count.Text,out var n)||n<1||n>200)return;
            var saved=fields.Select(f=>f.Text).ToArray();fields.Clear();rows.Children.Clear();
            for(var i=0;i<n;i++)
            {
                var line=new DockPanel{Margin=new Thickness(0,3,0,3)};rows.Children.Add(line);
                var label=new TextBlock{Text=$"Ряд {i+1}",Width=100};line.Children.Add(label);
                var input=new TextBox{Text=i<saved.Length?saved[i]:"20",MinWidth=90};line.Children.Add(input);fields.Add(input);
            }
        }
        count.TextChanged+=(_,_)=>Rebuild();
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);form.Children.Add(footer);
        Button(footer,"Создать зону",()=>Try(()=>{
            var n=Number(count,1,200);if(fields.Count!=n)Rebuild();
            Result=HallLayout.Generate(name.Text,fields.Select((f,i)=>new HallRowLayout{Label=(i+1).ToString(),SeatCount=Number(f,1,300)}).ToArray(),"1",false,0);Finish(true);
        }));
        form.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
    }
}
