using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;
internal sealed class HallLetterWindow025:EditorWindow
{
    internal string Letter{get;private set;}="";
    internal HallLetterWindow025(Store store):base(store,"Назначить букву",420,240)
    {
        var panel=new StackPanel{Margin=new Thickness(20)};Content=panel;var input=Field(panel,"Буква");input.MaxLength=1;input.CharacterCasing=CharacterCasing.Upper;
        Button(panel,"Назначить",()=>Try(()=>{var text=input.Text.Trim().ToUpperInvariant();if(text.Length!=1||!char.IsLetter(text[0]))throw new TicketException("Введите одну букву.");Letter=text;Finish(true);}));
    }
}
