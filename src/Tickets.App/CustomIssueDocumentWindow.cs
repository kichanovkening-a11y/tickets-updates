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
    private string? editingPath;
    private IssueDocumentInfo Info=>Store.CustomIssueDocument(eventId,issueId,kind)??throw new TicketException("Форма получателя не найдена.");

    internal CustomIssueDocumentWindow(Store store,long eventId,long issueId,string kind):base(store,Tickets.Core.Store.RecipientDocumentName(kind)+$" · выдача № {issueId}",1030,850)
    {
        this.eventId=eventId;this.issueId=issueId;this.kind=kind;
        var root=new Grid{Margin=new Thickness(10)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
        var header=new StackPanel();root.Children.Add(header);
        header.Children.Add(DocumentActions.Toolbar(
            ()=>Try(OpenWord),
            ()=>Try(()=>{
                var info=Info;
                var path=DocumentSaveDialog.Save(Store,DialogOwner,$"{Tickets.Core.Store.RecipientDocumentName(kind)}-{issueId}.docx",destination=>InvoiceDocuments.SaveIssueDocumentWord(info,destination));
                if(path is not null)status.Text="Сохранено: "+path;
            }),
            ()=>Try(()=>DocumentPrintPreview.Show(DialogOwner,Title,width=>SourceFormPreview.Document(Info,width),kind==IssueDocumentKinds.Waybill,kind==IssueDocumentKinds.Contract))));
        header.Children.Add(new TextBlock{Text="Получатель: "+Info.Invoice.Recipient,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Bold});
        if(kind==IssueDocumentKinds.Contract)
        {
            var dateRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,4)};header.Children.Add(dateRow);
            dateRow.Children.Add(new TextBlock{Text="Дата договора",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)});
            var date=new DatePicker{SelectedDate=Store.ContractDate(eventId,issueId),MinWidth=160};dateRow.Children.Add(date);
            date.SelectedDateChanged+=(_,_)=>Try(()=>{
                if(date.SelectedDate is not DateTime selected)return;
                Store.SetContractDate(eventId,issueId,selected);Refresh();status.Text="Дата договора сохранена.";
            });
        }
        var editing=new WrapPanel();header.Children.Add(editing);
        CompactButton(editing,"Изменить в Word",()=>Try(EditWord));
        CompactButton(editing,"Обновить из Word",()=>Try(ImportWord));
        header.Children.Add(new TextBlock{Text="Весь текст и оформление можно изменить в Word. Нажмите «Изменить в Word», сохраните документ и нажмите «Обновить из Word». Подстановки в фигурных скобках сохраняйте для автоматического заполнения; {contractDate} — дата договора, {eventCalendarDate} — дата показа для акта.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,4,0,0)});
        header.Children.Add(status);
        Grid.SetRow(preview,1);root.Children.Add(preview);
        preview.SizeChanged+=(_,_)=>{if(preview.ActualWidth>100)Refresh();};
        Refresh();status.Text="Форма получателя подготовлена для текущей выдачи.";
    }
    private static void CompactButton(Panel parent,string caption,Action action)
    {
        var button=Button(parent,caption,action);button.Width=double.NaN;button.ContentTemplate=null;button.FontSize=16.9;button.Padding=new Thickness(10,2,10,2);button.Margin=new Thickness(0,4,6,0);button.MinHeight=34;
    }
    private void Refresh()=>preview.Document=SourceFormPreview.Document(Info,preview.ActualWidth>100?preview.ActualWidth-20:null);
    private string WorkingPath(byte[] content,bool template)
    {
        var database=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Store.Path)))[..12];
        var hash=Convert.ToHexString(SHA256.HashData(content))[..12];
        var directory=Path.Combine(Path.GetTempPath(),"Tickets-Word",database);Directory.CreateDirectory(directory);
        return Path.Combine(directory,$"{Tickets.Core.Store.RecipientDocumentName(kind)}-{issueId}-{(template?"шаблон":"документ")}-{hash}.docx");
    }
    private void OpenWord()
    {
        var bytes=Info.SourceDocument??throw new TicketException("Документ Word не подготовлен.");
        var path=WorkingPath(bytes,false);
        IssueDocumentNavigation.OpenWord(path,()=>{File.WriteAllBytes(path,bytes);return path;});
        status.Text="Документ открыт в Word. Для сохранения правок в выдаче используйте «Изменить в Word» и «Обновить из Word».";
    }
    private void EditWord()
    {
        var bytes=Store.CustomIssueTemplate(eventId,issueId,kind)??throw new TicketException("Форма получателя не найдена.");
        editingPath??=WorkingPath(bytes,true);
        IssueDocumentNavigation.OpenWord(editingPath,()=>{File.WriteAllBytes(editingPath,bytes);return editingPath;});
        status.Text="Сохраните изменения в Word, затем нажмите «Обновить из Word». Рабочий файл: "+editingPath;
    }
    private void ImportWord()
    {
        if(editingPath is null||!File.Exists(editingPath))throw new TicketException("Сначала нажмите «Изменить в Word» и сохраните изменения в открытом документе.");
        byte[] bytes;
        using(var file=new FileStream(editingPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        using(var copy=new MemoryStream()){file.CopyTo(copy);bytes=copy.ToArray();}
        Store.SaveCustomIssueForm(eventId,issueId,kind,bytes);Refresh();
        status.Text="Правки Word сохранены для этой выдачи. Предварительный просмотр обновлён.";
    }
}
