using System.IO;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;
internal sealed class RecipientPackageWindow:EditorWindow
{
    private readonly long packageId;
    private readonly ContentControl navigation=new();
    private readonly FlowDocumentScrollViewer preview=new(){IsToolBarVisible=false};
    private readonly DocumentTextEditor editor=new();
    private string current="invoice";
    private IssueDocumentInfo Info=>Store.GetRecipientPackageDocument(packageId,current);
    public RecipientPackageWindow(Store store,long id):base(store,$"Общий комплект № {id}",1030,850)
    {
        packageId=id;var root=new DockPanel{Margin=new Thickness(2)};Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        DocumentFlag025.Apply(this,root,Store.RecipientPackage(id).Invoice.Recipient,id,"Общий комплект");
        var commandRow=new StackPanel{Orientation=Orientation.Horizontal};top.Children.Add(new ScrollViewer{Content=commandRow,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});commandRow.Children.Add(navigation);RefreshNavigation();
        commandRow.Children.Add(DocumentActions.Toolbar(()=>Try(()=>{Save();var path=Path.Combine(Path.GetTempPath(),$"Комплект-{id}-{current}-{Guid.NewGuid():N}.docx");IssueDocumentNavigation.OpenWord(path,()=>{InvoiceDocuments.SaveIssueDocumentWord(Info,path);return path;});}),()=>Try(()=>{Save();DocumentSaveDialog.Save(Store,DialogOwner,$"Комплект-{id}-{Tickets.Core.Store.RecipientDocumentName(current)}.docx",p=>InvoiceDocuments.SaveIssueDocumentWord(Info,p));}),()=>Try(()=>{Save();DocumentPrintPreview.Show(DialogOwner,Title,w=>IssueDocumentWindow.Document(Info,w),current=="ttn",current=="contract");})));
        var tabs=new TabControl();root.Children.Add(tabs);tabs.Items.Add(new TabItem{Header="Документ",Content=preview});tabs.Items.Add(new TabItem{Header="Изменить текст",Content=editor});

        tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==0)Try(()=>{Save();Refresh();});};
        Closing+=(_,e)=>{try{Save();}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message);}};
        DocumentPrintPreview.Route(root,()=>Try(()=>{Save();DocumentPrintPreview.Show(DialogOwner,Title,w=>IssueDocumentWindow.Document(Info,w),current=="ttn",current=="contract");}));Load();
    }
    private void RefreshNavigation()=>navigation.Content=IssueDocumentNavigation.Toolbar(current,next=>Try(()=>{Save();Store.GetRecipientPackageDocument(packageId,next);current=next;Load();RefreshNavigation();}));
    private void Load(){editor.Load(Info.SourceDocument!,current);Refresh();}
    private void Refresh()=>preview.Document=IssueDocumentWindow.Document(Info);
    private void Save(){if(!editor.IsDirty)return;var invoice=Info.Invoice;var bytes=CustomDocumentForms.Fill(editor.SaveBytes(),invoice,current,invoice.ContractDate??DateTimeOffset.Parse(invoice.IssuedAt).Date);Store.SaveRecipientPackageDocument(packageId,current,bytes);editor.Load(bytes,current);Refresh();}
}
