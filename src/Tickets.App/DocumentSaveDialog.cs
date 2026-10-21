using System.IO;
using System.Windows;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal static class DocumentSaveDialog
{
    internal static string? Save(Store store,Window owner,string fileName,Action<string> write)
    {
        var dialog=new SaveFileDialog{Title="Сохранить документ",Filter="Документ Word (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,FileName=fileName};
        var directory=store.DocumentSaveDirectory;
        if(Directory.Exists(directory))dialog.InitialDirectory=directory;
        if(dialog.ShowDialog(owner)!=true)return null;
        write(dialog.FileName);
        store.RememberDocumentSaveDirectory(Path.GetDirectoryName(dialog.FileName)!);
        return dialog.FileName;
    }
}
