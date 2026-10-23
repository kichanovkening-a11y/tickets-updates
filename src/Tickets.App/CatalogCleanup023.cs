using System.Windows;
using System.Windows.Controls;
namespace Tickets.App;
public partial class MainWindow
{
    private ContextMenu CleanupMenu023(long[] ids,string caption)
    {
        var menu=new ContextMenu();var remove=new MenuItem{Header=caption};menu.Items.Add(remove);
        remove.Click+=(_,_)=>Run(()=>{
            if(MessageBox.Show(this,$"Окончательно удалить показы: {ids.Length}?\nСвязанные выдачи, возвраты и документы также будут удалены.\n\nЗалы, схемы залов и расценовки сохранятся. Перед очисткой создаётся резервная копия.","Очистка базы",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            try{store.DeleteEventsPermanently(ids);}
            finally{current=null;Hall.Load([],[],true);HistoryGrid.ItemsSource=null;ReadEventCatalog();ApplyFilter();ShowHome();}
        });
        return menu;
    }
}
