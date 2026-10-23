using System.Windows;
using Tickets.Core;
namespace Tickets.App;
internal static class SenderConfirmation025
{
    internal static string Choose(Window owner,string previous,string next)=>SenderChange025.Accept(previous,next,()=>MessageBox.Show(owner,$"Изменить отправителя «{previous}» на «{next}»?","Изменение отправителя",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes);
}
