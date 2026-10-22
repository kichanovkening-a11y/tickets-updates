using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class RecipientFormsWindow:EditorWindow
{
    private sealed record Choice(string Key,string Caption);
    private readonly string recipient;
    private readonly ComboBox kinds=new(){DisplayMemberPath=nameof(Choice.Caption),MinWidth=230};
    private readonly DocumentTextEditor preview=new();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};
    private readonly TextBlock filename=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};
    // Reopening this editor must recover the same Word working copies.
    private static readonly Dictionary<string,Dictionary<string,string>> editingSessions=[];
    private readonly Dictionary<string,string> editingPaths;
    private readonly Button openWord,refreshWord;
    private readonly MenuItem remove=new(){Header="Удалить форму этого документа"};
    private string loadedKind="";
    private string Kind=>(kinds.SelectedItem as Choice)?.Key??"contract";

    internal RecipientFormsWindow(Store store,string recipient):base(store,"Формы документов · "+recipient,980,760)
    {
        this.recipient=recipient;
        var session=Path.GetFullPath(store.Path)+"\n"+recipient;
        if(!editingSessions.TryGetValue(session,out var paths))editingSessions[session]=paths=[];
        editingPaths=paths;
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text="Формы получателя: "+recipient,FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        header.Children.Add(new TextBlock{Text="Загрузите форму для каждого документа. Получатель в справочнике — короткое имя: юридические названия сторон и текст загруженной формы сохраняются. Подстановки меняют только отмеченные вами поля.",TextWrapping=TextWrapping.Wrap,FontSize=13,Margin=new Thickness(0,5,0,8)});
        var toolbar=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};header.Children.Add(toolbar);toolbar.Children.Add(kinds);
        kinds.ItemsSource=new[]{"contract","act","specification","bill","ttn"}.Select(k=>new Choice(k,Tickets.Core.Store.RecipientDocumentName(k))).ToArray();
        CompactButton(toolbar,"Загрузить форму",()=>Try(Upload));
        openWord=CompactButton(toolbar,"Сохранить текст / поля",()=>Try(SaveText));
        refreshWord=CompactButton(toolbar,"Обновить просмотр",()=>Try(()=>{SaveText();Reload();}));
        var menu=new ContextMenu();menu.Items.Add(remove);preview.ContextMenu=menu;filename.ContextMenu=menu;
        remove.Click+=(_,_)=>Try(()=>{if(MessageBox.Show(DialogOwner,"Удалить форму «"+Tickets.Core.Store.RecipientDocumentName(Kind)+"» у получателя «"+recipient+"»? Прежние документы сохранятся.","Удаление формы",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;Store.DeleteRecipientForm(recipient,Kind);editingPaths.Remove(Kind);Reload();status.Text="Форма удалена. Для новых выдач будет использован стандартный документ.";});
        header.Children.Add(filename);
        header.Children.Add(new TextBlock{Text="Обычный текст редактируется внутри программы. Красные поля «avto» защищены. Выделите фрагмент формы и назначьте автоматическое поле из списка. Исходное оформление DOCX сохраняется; изменения таблиц и макета выполняйте в исходном файле перед загрузкой.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,5,0,0)});
        header.Children.Add(status);
        root.Children.Add(preview);
        kinds.SelectionChanged+=(_,_)=>{if(!string.IsNullOrEmpty(loadedKind))Try(()=>SaveText(loadedKind));Reload();status.Text="";};kinds.SelectedIndex=0;
        Closing+=(_,e)=>{try{SaveText();}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Форма",MessageBoxButton.OK,MessageBoxImage.Warning);}};
    }
    private static Button CompactButton(Panel parent,string caption,Action action)
    {
        var button=Button(parent,caption,action);button.Width=double.NaN;button.ContentTemplate=null;button.Padding=new Thickness(9,3,9,3);button.FontSize=15;button.Margin=new Thickness(6,0,0,0);button.MinHeight=32;return button;
    }
    private void Reload()
    {
        loadedKind=Kind;var custom=Store.RecipientForm(recipient,Kind);var form=custom??Store.DefaultRecipientForm(Kind);
        preview.IsEnabled=form is not null;if(form is not null)preview.Load(form.Content,Kind);
        filename.Text=form is null?"":((custom is null?"Стандартная форма: ":"Форма получателя: ")+form.FileName+" · DOCX");
        openWord.IsEnabled=form is not null;refreshWord.IsEnabled=form is not null;remove.IsEnabled=custom is not null;
    }
    private void Upload()
    {
        var dialog=new OpenFileDialog{Title="Загрузить форму: "+Tickets.Core.Store.RecipientDocumentName(Kind),Filter="Формы Office (*.docx;*.doc;*.xls;*.xlsx)|*.docx;*.doc;*.xls;*.xlsx|Документ Word (*.docx)|*.docx",CheckFileExists=true,Multiselect=false};
        if(dialog.ShowDialog(DialogOwner)!=true)return;
        if(editingPaths.ContainsKey(Kind))throw new TicketException("Сначала сохраните и закройте Word, затем нажмите «Обновить из Word», чтобы сохранить правки перед заменой формы.");
        var bytes=OfficeDocumentImport.Read(dialog.FileName);
        Store.SaveRecipientForm(recipient,Kind,bytes,Path.ChangeExtension(Path.GetFileName(dialog.FileName),".docx"));Reload();
        status.Text="Форма сохранена для получателя. Для новых выдач она будет заполнена автоматически; исходный файл сохранён.";
    }
    private void SaveText()=>SaveText(Kind);
    private void SaveText(string selectedKind)
    {
        if(!preview.IsEnabled||!preview.IsDirty)return;
        var form=Store.RecipientForm(recipient,selectedKind)??Store.DefaultRecipientForm(selectedKind)??throw new TicketException("Сначала загрузите форму.");
        var bytes=preview.SaveBytes();Store.SaveRecipientForm(recipient,selectedKind,bytes,form.FileName);preview.Load(bytes,selectedKind);status.Text="Текст и автоматические поля сохранены в форме получателя.";
    }
}
