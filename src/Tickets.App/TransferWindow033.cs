using System.IO;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;

internal sealed class TransferWindow033:EditorWindow
{
    internal TransferWindow033(Store store):base(store,"Экспорт",650,450)
    {
        var form=new StackPanel{Margin=new Thickness(24)};Content=form;
        var kinds=new[]{new Kind("hall","Зал"),new Kind("performance","Спектакль"),new Kind("pricing","Расценовка"),new Kind("blank","Бланк")};
        var kind=Combo(form,"Что передать",kinds,nameof(Kind.Name));var item=Combo(form,"Выберите",Array.Empty<TransferChoice033>(),nameof(TransferChoice033.Name));
        kind.SelectionChanged+=(_,_)=>{item.ItemsSource=store.TransferChoices033(((Kind)kind.SelectedItem).Id);item.SelectedIndex=-1;};
        kind.SelectedIndex=0;
        Button(form,"Экспорт",()=>Try(()=>{
            if(item.SelectedItem is not TransferChoice033 choice)throw new TicketException("Выберите данные для экспорта.");
            var bytes=store.ExportTransfer033(choice.Kind,choice.Id);var package=TransferPackage033.Read(bytes);
            var fileName=package.TypeName+" — "+package.Name;foreach(var ch in Path.GetInvalidFileNameChars())fileName=fileName.Replace(ch,'_');
            var dialog=new Microsoft.Win32.SaveFileDialog{Title="Экспорт",Filter="Файл обмена Билетов (*.tickets)|*.tickets",DefaultExt=".tickets",FileName=fileName+".tickets"};
            if(dialog.ShowDialog(DialogOwner)!=true)return;
            File.WriteAllBytes(dialog.FileName,bytes);Finish(true);
        }));
    }
    private sealed record Kind(string Id,string Name);
}
internal sealed class TransferImportWindow033:EditorWindow
{
    internal TransferImportWindow033(Store store,byte[] bytes):base(store,"Импорт",650,450)
    {
        var p=TransferPackage033.Read(bytes);var duplicate=store.TransferNameExists033(p.Kind,p.Name);
        var form=new StackPanel{Margin=new Thickness(24)};Content=form;
        form.Children.Add(new TextBlock{Text=p.TypeName+": "+p.Name,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,FontWeight=FontWeights.Bold});
        if(p.Hall is {} hall)form.Children.Add(new TextBlock{Text=$"Секторов: {hall.Seats.Select(s=>s.Sector).Distinct().Count()} · Мест: {hall.Seats.Count}",TextAlignment=TextAlignment.Center,Margin=new Thickness(0,8,0,8)});
        var initial=p.Name;
        if(duplicate){var n=2;do{var suffix=$" (копия {n++})";initial=p.Name[..Math.Min(p.Name.Length,(p.Kind=="blank"?100:200)-suffix.Length)]+suffix;}while(store.TransferNameExists033(p.Kind,initial));}
        var name=Field(form,"Название",initial);name.HorizontalContentAlignment=HorizontalAlignment.Center;
        form.Children.Add(new TextBlock{Text=duplicate?"Такое название уже есть. Можно добавить отдельную копию с новым названием.":p.Kind=="pricing"?"Расценовка будет добавлена вместе с отдельной копией её зала.":"Данные будут добавлены в соответствующий раздел.",TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,10,0,0)});
        Button(form,duplicate?"Добавить копию":"Импорт",()=>Try(()=>{store.ImportTransfer033(bytes,name.Text);Finish(true);}));
        OnShown(()=>{name.Focus();name.SelectAll();});
    }
}
public partial class MainWindow
{
    internal void ImportFromSettings033(Window owner)
    {
        var dialog=new Microsoft.Win32.OpenFileDialog{Title="Импорт",Filter="Файлы обмена (*.tickets)|*.tickets|Все файлы (*.*)|*.*",CheckFileExists=true};
        if(dialog.ShowDialog(owner)!=true)return;
        if(new FileInfo(dialog.FileName).Length>32*1024*1024)throw new TicketException("Размер файла импорта должен быть до 32 МБ.");
        var window=new TransferImportWindow033(store,File.ReadAllBytes(dialog.FileName)){Owner=owner};
        if(window.ShowDialog()==true)RefreshCatalogNavigation();
    }
}
