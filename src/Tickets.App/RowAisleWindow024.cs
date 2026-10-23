using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;
internal sealed class RowAisleWindow024:EditorWindow
{
    public double Gap{get;private set;}
    public RowAisleWindow024(Store store):base(store,"Проход после ряда",440,260)
    {
        var panel=new StackPanel{Margin=new Thickness(18)};Content=panel;
        var input=Field(panel,"Ширина прохода (1–1000)","22");
        Button(panel,"Применить",()=>Try(()=>{Gap=Number(input,1,1000);Finish(true);}));
    }
}
