using System.Windows;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    internal Store PrintStore=>store;
}

internal sealed class PrintPreviewWindow:EditorWindow
{
    internal PrintPreviewWindow(Window owner,string title,double width=1120,double height=850)
        :base(ResolveStore(owner),title,width,height)
    {
        Owner=owner;
        ShowInTaskbar=false;
        Loaded+=(_,_)=>
        {
            var work=SystemParameters.WorkArea;
            Width=Math.Min(Width,work.Width);Height=Math.Min(Height,work.Height);
            Left=Math.Clamp(Left,work.Left,work.Right-Width);
            Top=Math.Clamp(Top,work.Top,work.Bottom-Height);
        };
    }
    private static Store ResolveStore(Window owner)=>owner switch
    {
        EditorWindow editor=>editor.PrintStore,
        MainWindow main=>main.PrintStore,
        _=>throw new InvalidOperationException("Не удалось определить окно приложения для печати.")
    };
    internal void Confirm()=>Finish(true);
}
