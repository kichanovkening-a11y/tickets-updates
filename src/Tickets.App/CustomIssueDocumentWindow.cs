using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

// Recipient Word forms keep their original layout and an independent issue copy.
internal sealed class CustomIssueDocumentWindow:EditorWindow
{
    private readonly long eventId,issueId;
    private readonly string kind;
    private readonly FlowDocumentScrollViewer preview=new(){IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly TextBlock status=new(){FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4)};
    private readonly DocumentTextEditor textEditor=new();
    private IssueDocumentInfo Info=>Store.CustomIssueDocument(eventId,issueId,kind)??Store.GetIssueDocument(eventId,issueId,kind);

    internal CustomIssueDocumentWindow(Store store,long eventId,long issueId,string kind):base(store,Tickets.Core.Store.RecipientDocumentName(kind)+$" · выдача № {issueId}",1030,850)
    {
        this.eventId=eventId;this.issueId=issueId;this.kind=kind;
        var root=new Grid{Margin=new Thickness(10)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
        var header=new StackPanel();root.Children.Add(header);
        header.Children.Add(DocumentActions.Toolbar(
            ()=>Try(OpenWord),
            ()=>Try(()=>{
                SaveForNavigation();var info=Info;
                var path=DocumentSaveDialog.Save(Store,DialogOwner,$"{Tickets.Core.Store.RecipientDocumentName(kind)}-{issueId}.docx",destination=>InvoiceDocuments.SaveIssueDocumentWord(info,destination));
                if(path is not null)status.Text="Сохранено: "+path;
            }),
            ()=>Try(()=>{SaveForNavigation();DocumentPrintPreview.Show(DialogOwner,Title,width=>IssueDocumentWindow.Document(Info,width),kind==IssueDocumentKinds.Waybill,kind==IssueDocumentKinds.Contract);})));
        header.Children.Add(new TextBlock{Text="Получатель: "+Info.Invoice.Recipient,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Bold});
        header.Children.Add(ContractFields.Row(Store,eventId,issueId,()=>{Refresh();status.Text="Поля договора сохранены для всего комплекта.";}));
        header.Children.Add(status);
        var tabs=new TabControl();Grid.SetRow(tabs,1);root.Children.Add(tabs);
        tabs.Items.Add(new TabItem{Header="Документ",Content=preview});
        var editing=new DockPanel();var saveRow=new WrapPanel{Margin=new Thickness(0,5,0,5)};DockPanel.SetDock(saveRow,Dock.Bottom);editing.Children.Add(saveRow);
        CompactButton(saveRow,"Сохранить текст документа",()=>Try(SaveForNavigation));
        CompactButton(saveRow,"Сохранить как форму получателя",()=>Try(()=>{SaveForNavigation();var bytes=Store.CustomIssueTemplate(eventId,issueId,kind)!;Store.SaveRecipientForm(Info.Invoice.Recipient,kind,bytes,Tickets.Core.Store.RecipientDocumentName(kind)+".docx");status.Text="Форма сохранена для следующих выдач.";}));
        editing.Children.Add(textEditor);tabs.Items.Add(new TabItem{Header="Изменить текст",Content=editing});
        textEditor.Load(Store.CustomIssueTemplate(eventId,issueId,kind)!,kind);
        tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==0)Try(SaveForNavigation);};
        Closing+=(_,e)=>{try{SaveForNavigation();}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Документ",MessageBoxButton.OK,MessageBoxImage.Warning);}};
        preview.SizeChanged+=(_,_)=>{if(preview.ActualWidth>100)Refresh();};
        Refresh();status.Text="Форма получателя подготовлена для текущей выдачи.";
    }
    private static void CompactButton(Panel parent,string caption,Action action)
    {
        var button=Button(parent,caption,action);button.Width=double.NaN;button.ContentTemplate=null;button.FontSize=16.9;button.Padding=new Thickness(10,2,10,2);button.Margin=new Thickness(0,4,6,0);button.MinHeight=34;
    }
    private void Refresh()=>preview.Document=IssueDocumentWindow.Document(Info,preview.ActualWidth>100?preview.ActualWidth-20:null);
    private string WorkingPath(byte[] content,bool template)
    {
        var database=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Store.Path)))[..12];
        var hash=Convert.ToHexString(SHA256.HashData(content))[..12];
        var directory=Path.Combine(Path.GetTempPath(),"Tickets-Word",database);Directory.CreateDirectory(directory);
        return Path.Combine(directory,$"{Tickets.Core.Store.RecipientDocumentName(kind)}-{issueId}-{(template?"шаблон":"документ")}-{hash}.docx");
    }
    private void OpenWord()
    {
        SaveForNavigation();var info=Info;var bytes=info.SourceDocument;
        if(bytes is null){IssueDocumentNavigation.OpenWord(Store.IssueDocumentPath(issueId,kind),()=>Store.SaveIssueDocumentWord(eventId,issueId,kind));return;}
        var path=WorkingPath(bytes,false);
        IssueDocumentNavigation.OpenWord(path,()=>{File.WriteAllBytes(path,bytes);return path;});
        status.Text="Открыта отдельная копия документа: автоматические данные в ней можно изменять.";
    }
    internal void SaveForNavigation()
    {
        if(!textEditor.IsDirty)return;
        var bytes=textEditor.SaveBytes();Store.SaveCustomIssueForm(eventId,issueId,kind,bytes);textEditor.Load(bytes,kind);Refresh();status.Text="Текст документа сохранён. Автоматические поля заполняются из выдачи.";
    }
}
