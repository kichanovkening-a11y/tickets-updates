using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class SpecificationWindow : EditorWindow
{
    private readonly ListBox saved=new(){DisplayMemberPath=nameof(SpecificationInfo.Recipient)};
    private readonly ComboBox recipient=new(){IsEditable=true,IsTextSearchEnabled=false,MinHeight=32};
    private readonly TextBox sender,title,body;
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)};
    private List<SpecificationInfo> specifications=[];
    private SpecificationInfo? current;
    private bool loading,dirty,filtering;

    public SpecificationWindow(Store store,string recipient=""):base(store,"Спецификации организаций",1120,820)
    {
        var root=new Grid{Margin=new Thickness(20)};
        root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)});
        root.ColumnDefinitions.Add(new ColumnDefinition());Content=root;
        var sidebar=new DockPanel{Margin=new Thickness(0,0,20,0)};root.Children.Add(sidebar);
        var tools=new StackPanel();DockPanel.SetDock(tools,Dock.Top);sidebar.Children.Add(tools);
        tools.Children.Add(new TextBlock{Text="Сохранённые спецификации",FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        Button(tools,"Новая организация",()=>Try(()=>{if(CanSwitch()){saved.SelectedItem=null;Load(null);}}));
        saved.Margin=new Thickness(0,12,0,0);sidebar.Children.Add(saved);
        var form=new StackPanel();var scroll=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);root.Children.Add(scroll);
        form.Children.Add(new TextBlock{Text="Спецификация",FontSize=23,FontWeight=FontWeights.SemiBold});
        form.Children.Add(new TextBlock{Text="Для каждой организации хранится одна спецификация. Выберите её слева для изменения или повторной печати.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});
        form.Children.Add(new TextBlock{Text="Получатель / организация",Margin=new Thickness(0,10,0,4)});form.Children.Add(this.recipient);
        this.recipient.ItemsSource=Store.Recipients();
        sender=Field(form,"От кого",Store.Sender);title=Field(form,"Название","Спецификация");
        body=Field(form,"Текст спецификации");body.AcceptsReturn=true;body.TextWrapping=TextWrapping.Wrap;body.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;body.MinHeight=260;body.MaxHeight=460;
        form.Children.Add(new TextBlock{Text="Введите согласованные условия и реквизиты. Подписи отправителя и получателя добавляются при печати и экспорте.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
        var actions=new WrapPanel();form.Children.Add(actions);
        Button(actions,"Сохранить",()=>Try(()=>Save()));
        Button(actions,"Сохранить в Word…",()=>Try(Export));
        Button(actions,"Распечатать",()=>Try(Print));
        form.Children.Add(status);
        foreach(var box in new[]{sender,title,body})box.TextChanged+=(_,_)=>{if(!loading)dirty=true;};
        this.recipient.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((_,_)=>{
            if(loading||filtering)return;dirty=true;
            if(!this.recipient.IsEnabled)return;
            filtering=true;
            try{var text=this.recipient.Text;this.recipient.ItemsSource=Store.Recipients(text);this.recipient.Text=text;}
            finally{filtering=false;}
        }));
        saved.SelectionChanged+=(_,_)=>Try(()=>{
            if(loading||saved.SelectedItem is not SpecificationInfo selected||selected.Id==current?.Id)return;
            if(CanSwitch())Load(selected);
            else{loading=true;saved.SelectedItem=current;loading=false;}
        });
        specifications=Store.Specifications();saved.ItemsSource=specifications;
        var match=specifications.FirstOrDefault(s=>CatalogText.Key(s.Recipient)==CatalogText.Key(recipient));
        Load(match,recipient);
        Closing+=(_,e)=>{
            if(!dirty)return;
            var result=MessageBox.Show(DialogOwner,"Сохранить изменения спецификации перед закрытием?","Спецификация",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
            if(result==MessageBoxResult.Cancel){e.Cancel=true;return;}
            if(result==MessageBoxResult.Yes){try{if(Save() is null)e.Cancel=true;}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Спецификация",MessageBoxButton.OK,MessageBoxImage.Warning);}}
        };
    }

    private bool CanSwitch()=>!dirty||MessageBox.Show(DialogOwner,"Отбросить несохранённые изменения?","Спецификация",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;

    private void Load(SpecificationInfo? value,string initialRecipient="")
    {
        loading=true;
        try
        {
            current=value;saved.SelectedItem=value;recipient.ItemsSource=Store.Recipients();
            recipient.Text=value?.Recipient??initialRecipient;recipient.IsEnabled=value is null;
            sender.Text=value?.Sender??Store.Sender;title.Text=value?.Title??"Спецификация";body.Text=value?.Body??"";
            status.Text=value is null?"Новая спецификация. Заполните текст и сохраните.":"Сохранённая спецификация. Для другой организации нажмите «Новая организация».";dirty=false;
        }
        finally{loading=false;}
    }

    private SpecificationInfo? Save()
    {
        var existing=Store.Specifications().FirstOrDefault(s=>CatalogText.Key(s.Recipient)==CatalogText.Key(recipient.Text));
        if(existing is not null&&existing.Id!=current?.Id)
        {
            MessageBox.Show(DialogOwner,"У этой организации уже есть спецификация. Выберите её в списке слева для просмотра и изменения. Введённый текст оставлен в форме.","Спецификация",MessageBoxButton.OK,MessageBoxImage.Information);
            return null;
        }
        var info=Store.SaveSpecification(recipient.Text,sender.Text,title.Text,body.Text);
        specifications=Store.Specifications();loading=true;saved.ItemsSource=specifications;loading=false;
        Load(specifications.First(s=>s.Id==info.Id));
        try{status.Text="Сохранено. Документ Word: "+Store.SaveSpecificationDocument(info);}
        catch(Exception ex)
        {
            status.Text="Спецификация сохранена в базе. Не удалось сохранить документ Word: "+ex.Message;
            MessageBox.Show(DialogOwner,status.Text,"Сохранение Word",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        return info;
    }

    private SpecificationInfo? ForOutput()=>current is not null&&!dirty?current:Save();

    private void Export()
    {
        var info=ForOutput();if(info is null)return;
        var dialog=new SaveFileDialog{Filter="Документ Word (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,FileName=$"Спецификация-{info.Id}.docx"};
        if(dialog.ShowDialog(DialogOwner)==true){InvoiceDocuments.SaveSpecificationWord(info,dialog.FileName);status.Text="Документ Word сохранён: "+dialog.FileName;}
    }

    private void Print()
    {
        var info=ForOutput();if(info is null)return;
        var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;
        var document=Document(info);document.PageWidth=dialog.PrintableAreaWidth;document.PageHeight=dialog.PrintableAreaHeight;
        document.PagePadding=new Thickness(32);document.ColumnWidth=dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,info.Title+" — "+info.Recipient);
    }

    internal static FlowDocument Document(SpecificationInfo info)
    {
        var doc=new FlowDocument{FontFamily=new FontFamily("Arial"),FontSize=13,PageWidth=794,PagePadding=new Thickness(35),ColumnWidth=724};
        void Text(string text,bool bold=false,double size=13)=>doc.Blocks.Add(new Paragraph(new Run(text)){FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,Margin=new Thickness(0,0,0,12)});
        Text(info.Title,true,22);Text("От кого: "+info.Sender);Text("Получатель: "+info.Recipient);
        foreach(var line in info.Body.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))Text(line);
        Text("\nОтправитель: ____________________    Получатель: ____________________");return doc;
    }
}
