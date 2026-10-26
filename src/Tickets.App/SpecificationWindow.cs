using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
    private bool loading,filtering;
    private readonly bool fixedRecipient;
    private string originalRecipient="",originalSender="",originalTitle="",originalBody="";
    private bool dirty=>recipient.Text!=originalRecipient||sender.Text!=originalSender||title.Text!=originalTitle||body.Text!=originalBody;

    public SpecificationWindow(Store store,string recipient=""):this(store,recipient,false){}
    internal SpecificationWindow(Store store,string recipient,bool fixedRecipient):base(store,"Спецификации организаций",1120,820)
    {
        this.fixedRecipient=fixedRecipient;
        var root=new Grid{Margin=new Thickness(20)};
        root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)});
        root.ColumnDefinitions.Add(new ColumnDefinition());Content=root;DocumentPrintPreview.Route(root,()=>Try(Print));
        var sidebar=new DockPanel{Margin=new Thickness(0,0,20,0)};root.Children.Add(sidebar);
        if(fixedRecipient){sidebar.Visibility=Visibility.Collapsed;root.ColumnDefinitions[0].Width=new GridLength(0);}
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
        Button(actions,"Сохранить",()=>Try(Export));
        Button(actions,"Распечатать",()=>Try(Print));
        form.Children.Add(status);
        this.recipient.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((_,_)=>{
            if(loading||filtering)return;
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
            try{e.Cancel=!DocumentSaveDialog.SaveOnClose(Store,DialogOwner,$"Спецификация-{current?.Id.ToString()??"новая"}.docx",destination=>{
                var info=ForOutput()??throw new TicketException("Спецификация не сохранена.");InvoiceDocuments.SaveSpecificationWord(info,destination);
            });}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Спецификация",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
    }

    internal SpecificationWindow(Store store,long eventId,long issueId):base(store,$"Спецификация · выдача № {issueId}",1030,850)
    {
        fixedRecipient=true;sender=new TextBox();title=new TextBox();body=new TextBox();
        var root=new Grid{Margin=new Thickness(10)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());Content=root;DocumentPrintPreview.Route(root,()=>Try(Print));
        var header=new StackPanel();root.Children.Add(header);
        var reader=new FlowDocumentScrollViewer{IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(reader,1);root.Children.Add(reader);
        FlowDocument Preview(double width=794)
        {
            var invoice=Store.Invoice(eventId,issueId);var document=InvoiceWindow.Document(invoice,width);
            if(document.Blocks.FirstBlock is Paragraph heading){heading.Inlines.Clear();heading.Inlines.Add(new Run($"СПЕЦИФИКАЦИЯ № {issueId}"));}
            return document;
        }
        reader.Document=Preview();reader.SizeChanged+=(_,_)=>{if(reader.ActualWidth>100)reader.Document=Preview(reader.ActualWidth-20);};
        header.Children.Add(DocumentActions.Toolbar(
            ()=>Try(()=>IssueDocumentNavigation.OpenWord(Store.IssueSpecificationDocumentPath(issueId),()=>Store.SaveIssueSpecificationDocument(eventId,issueId))),
            ()=>Try(()=>{
            var path=DocumentSaveDialog.Save(Store,DialogOwner,$"Спецификация-выдача-{issueId}.docx",destination=>InvoiceDocuments.SaveIssueSpecificationWord(Store.Invoice(eventId,issueId),destination));
            if(path is not null)status.Text="Сохранено: "+path;
        }),()=>Try(()=>{
            DocumentPrintPreview.Show(DialogOwner,Title,width=>Preview(width));
        })));
        header.Children.Add(status);
    }

    private bool CanSwitch()=>!dirty||MessageBox.Show(DialogOwner,"Отбросить несохранённые изменения?","Спецификация",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;

    private void Load(SpecificationInfo? value,string initialRecipient="")
    {
        loading=true;
        try
        {
            current=value;saved.SelectedItem=value;recipient.ItemsSource=Store.Recipients();
            recipient.Text=value?.Recipient??initialRecipient;recipient.IsEnabled=value is null&&!fixedRecipient;
            sender.Text=value?.Sender??Store.Sender;title.Text=value?.Title??"Спецификация";body.Text=value?.Body??"";
            status.Text=value is null?"Новая спецификация. Заполните текст и сохраните.":fixedRecipient?"Сохранённая спецификация получателя текущей выдачи.":"Сохранённая спецификация. Для другой организации нажмите «Новая организация».";
            originalRecipient=recipient.Text;originalSender=sender.Text;originalTitle=title.Text;originalBody=body.Text;
        }
        finally{loading=false;}
    }

    internal bool SaveForNavigation()=>!dirty||Save() is not null;

    private SpecificationInfo? Save()
    {
        var existing=Store.Specifications().FirstOrDefault(s=>CatalogText.Key(s.Recipient)==CatalogText.Key(recipient.Text));
        if(existing is not null&&existing.Id!=current?.Id)
        {
            MessageBox.Show(DialogOwner,"У этой организации уже есть спецификация. Выберите её в списке слева для просмотра и изменения. Введённый текст оставлен в форме.","Спецификация",MessageBoxButton.OK,MessageBoxImage.Information);
            return null;
        }
        var accepted=SenderConfirmation025.Choose(DialogOwner,originalSender,sender.Text);if(accepted!=sender.Text){sender.Text=accepted;return null;}
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
        var path=DocumentSaveDialog.Save(Store,DialogOwner,$"Спецификация-{current?.Id.ToString()??"новая"}.docx",destination=>{
            var info=ForOutput();if(info is null)throw new TicketException("Спецификация не сохранена.");
            InvoiceDocuments.SaveSpecificationWord(info,destination);
        });
        if(path is not null)status.Text="Документ Word сохранён: "+path;
    }

    private void Print()
    {
        var info=ForOutput();if(info is null)return;
        DocumentPrintPreview.Show(DialogOwner,info.Title+" — "+info.Recipient,_=>Document(info));
    }

    internal static FlowDocument Document(SpecificationInfo info)
    {
        var doc=new FlowDocument{FontFamily=new FontFamily("Arial"),FontSize=13,PageWidth=794,PagePadding=new Thickness(35),ColumnWidth=724};
        void Text(string text,bool bold=false,double size=13)=>doc.Blocks.Add(new Paragraph(new Run(text)){FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,Margin=new Thickness(0,0,0,12)});
        Text(info.Title,true,22);Text("От кого: "+info.DocumentSender);Text("Получатель: "+info.DocumentRecipient);
        foreach(var line in info.Body.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))Text(line);
        Text("\nОтправитель: ____________________    Получатель: ____________________");return doc;
    }
}
