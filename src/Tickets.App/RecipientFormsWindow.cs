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
    private readonly TextBox preview=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(10)};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};
    private readonly TextBlock filename=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};
    // Reopening this editor must recover the same Word working copies.
    private static readonly Dictionary<string,Dictionary<string,string>> editingSessions=[];
    private readonly Dictionary<string,string> editingPaths;
    private readonly Button openWord,refreshWord;
    private readonly MenuItem remove=new(){Header="Удалить форму этого документа"};
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
        openWord=CompactButton(toolbar,"Изменить в Word",()=>Try(OpenWord));
        refreshWord=CompactButton(toolbar,"Обновить из Word",()=>Try(RefreshWord));
        var menu=new ContextMenu();menu.Items.Add(remove);preview.ContextMenu=menu;filename.ContextMenu=menu;
        remove.Click+=(_,_)=>Try(()=>{if(MessageBox.Show(DialogOwner,"Удалить форму «"+Tickets.Core.Store.RecipientDocumentName(Kind)+"» у получателя «"+recipient+"»? Прежние документы сохранятся.","Удаление формы",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;Store.DeleteRecipientForm(recipient,Kind);editingPaths.Remove(Kind);Reload();status.Text="Форма удалена. Для новых выдач будет использован стандартный документ.";});
        header.Children.Add(filename);
        header.Children.Add(new TextBlock{Text="После редактирования сохраните файл в Word и нажмите «Обновить из Word». При повторном открытии рабочий файл не перезаписывается. Удаление формы — в контекстном меню просмотра.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,5,0,0)});
        header.Children.Add(status);
        var mapping=new Expander{Header="Настроить автоматическое заполнение",Margin=new Thickness(0,0,0,8)};
        DockPanel.SetDock(mapping,Dock.Bottom);root.Children.Add(mapping);
        var fields=new StackPanel{Margin=new Thickness(0,5,0,0)};mapping.Content=fields;
        fields.Children.Add(new TextBlock{Text="Замените точный фрагмент формы подстановкой. Для даты акта используйте {eventCalendarDate}: она всегда берётся из даты показа. Сначала сохраните правки Word и обновите форму.",TextWrapping=TextWrapping.Wrap,FontSize=12});
        var existing=Field(fields,"Текст в форме (будут заменены все точные совпадения)");
        var tokens=new[]{
            new Choice("event","Название мероприятия"),new Choice("eventDate","Дата и время показа"),new Choice("eventCalendarDate","Календарная дата показа / дата акта"),
            new Choice("venue","Площадка"),new Choice("count","Количество билетов"),new Choice("countWords","Количество прописью"),new Choice("amount","Сумма"),new Choice("amountWords","Сумма прописью"),
            new Choice("contractDate","Дата договора"),new Choice("issueId","Номер накладной"),new Choice("total","Итого: количество и сумма"),
            new Choice("itemDescription","Описание билетов / строка таблицы"),new Choice("itemSection","Сектор / строка таблицы"),new Choice("itemRow","Ряд / строка таблицы"),new Choice("itemSeat","Места / строка таблицы"),
            new Choice("itemCount","Количество / строка таблицы"),new Choice("itemPrice","Цена / строка таблицы"),new Choice("itemTotal","Сумма / строка таблицы")};
        var token=Combo(fields,"Заполнять значением",tokens,nameof(Choice.Caption));
        CompactButton(fields,"Заменить и сохранить форму",()=>Try(()=>{
            var form=Store.RecipientForm(recipient,Kind)??throw new TicketException("Сначала загрузите форму.");
            if(token.SelectedItem is not Choice selected)return;
            if(string.IsNullOrWhiteSpace(existing.Text)||!CustomDocumentForms.Text(form.Content).Contains(existing.Text,StringComparison.Ordinal))throw new TicketException("В просмотре формы нет такого фрагмента. Скопируйте точный текст из просмотра.");
            if(editingPaths.ContainsKey(Kind))throw new TicketException("Сохраните файл в Word, закройте его и нажмите «Обновить из Word» перед настройкой подстановок.");
            var bytes=CustomDocumentForms.Map(form.Content,existing.Text,selected.Key);
            Store.SaveRecipientForm(recipient,Kind,bytes,form.FileName);Reload();status.Text="Подстановка {"+selected.Key+"} сохранена. Оформление проверяйте в Word.";
        }));
        root.Children.Add(preview);
        kinds.SelectionChanged+=(_,_)=>{Reload();status.Text="";};kinds.SelectedIndex=0;
    }
    private static Button CompactButton(Panel parent,string caption,Action action)
    {
        var button=Button(parent,caption,action);button.Width=double.NaN;button.ContentTemplate=null;button.Padding=new Thickness(9,3,9,3);button.FontSize=15;button.Margin=new Thickness(6,0,0,0);button.MinHeight=32;return button;
    }
    private void Reload()
    {
        var form=Store.RecipientForm(recipient,Kind);
        preview.Text=form is null?"Форма не загружена. Будет использован стандартный документ.":CustomDocumentForms.Text(form.Content);
        filename.Text=form is null?"":("Файл: "+form.FileName+" · DOCX");
        openWord.IsEnabled=form is not null;refreshWord.IsEnabled=editingPaths.ContainsKey(Kind);remove.IsEnabled=form is not null;
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
    private void OpenWord()
    {
        var form=Store.RecipientForm(recipient,Kind)??throw new TicketException("Сначала загрузите форму.");
        if(!editingPaths.TryGetValue(Kind,out var path))
        {
            var directory=Path.Combine(Path.GetTempPath(),"Tickets-recipient-forms");Directory.CreateDirectory(directory);
            path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".docx");File.WriteAllBytes(path,form.Content);editingPaths[Kind]=path;
        }
        IssueDocumentNavigation.OpenWord(path,()=>{File.WriteAllBytes(path,form.Content);return path;});Reload();
        status.Text="Сохраните и закройте Word, затем нажмите «Обновить из Word». Рабочий файл: "+path;
    }
    private void RefreshWord()
    {
        if(!editingPaths.TryGetValue(Kind,out var path))throw new TicketException("Сначала откройте форму для изменения в Word.");
        var current=Store.RecipientForm(recipient,Kind)??throw new TicketException("Форма не найдена.");
        var bytes=OfficeDocumentImport.CheckedBytes(path);Store.SaveRecipientForm(recipient,Kind,bytes,current.FileName);
        editingPaths.Remove(Kind);Reload();status.Text="Правки Word сохранены в форме получателя. Рабочий файл остаётся на диске.";
    }
}
