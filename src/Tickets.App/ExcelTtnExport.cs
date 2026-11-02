using System.IO;
using System.Windows;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal static class ExcelTtnExport
{
    internal static async void Open(Window owner,IssueDocumentInfo info)
    {
        try
        {
            await Task.CompletedTask;
            var path=Path.Combine(Path.GetTempPath(),"ТТН-"+info.Invoice.Id+"-"+Guid.NewGuid().ToString("N")+".xlsx");
            ExcelTtnWorkbook.Save(info,path);
            IssueDocumentNavigation.OpenWord(path,()=>path);
        }
        catch(Exception ex){MessageBox.Show(owner,ex.Message,"ТТН",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    internal static async void Save(Store store,Window owner,IssueDocumentInfo info)
    {
        var dialog=new SaveFileDialog{Title="Сохранить документ",Filter="Книга Excel (*.xlsx)|*.xlsx",DefaultExt=".xlsx",AddExtension=true,FileName="ТТН-"+info.Invoice.Id+".xlsx"};
        if(Directory.Exists(store.DocumentSaveDirectory))dialog.InitialDirectory=store.DocumentSaveDirectory;
        if(dialog.ShowDialog(owner)!=true)return;
        try
        {
            await Task.CompletedTask;
            ExcelTtnWorkbook.Save(info,dialog.FileName);
            store.RememberDocumentSaveDirectory(Path.GetDirectoryName(dialog.FileName)!);
        }
        catch(Exception ex){MessageBox.Show(owner,ex.Message,"ТТН",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
}
