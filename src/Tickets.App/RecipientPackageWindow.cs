using System.IO;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;
internal sealed class RecipientPackageWindow:EditorWindow
{
    private readonly long packageId;
    private readonly ComboBox kinds=new(){ItemsSource=new[]{"contract","bill","act","specification","ttn","invoice"},SelectedIndex=5,Margin=new Thickness(0,4,0,4)};
    private readonly FlowDocumentScrollViewer preview=new(){IsToolBarVisible=false};
    private readonly DocumentTextEditor editor=new();
    private string current="invoice";
    private IssueDocumentInfo Info=>Store.GetRecipientPackageDocument(packageId,current);
    public RecipientPackageWindow(Store store,long id):base(store,$"Общий комплект № {id}",1030,850)
    {
        packageId=id;var root=new DockPanel{Margin=new Thickness(10)};Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        top.Children.Add(new TextBlock{Text="Общий комплект: "+Store.RecipientPackage(id).Invoice.Recipient,FontSize=20});
        // Native captions are shown separately to keep kind keys stable.
        kinds.ItemsSource=new[]{"contract","bill","act","specification","ttn","invoice"}.Select(k=>new KeyValuePair<string,string>(k,Tickets.Core.Store.RecipientDocumentName(k))).ToArray();kinds.DisplayMemberPath="Value";kinds.ItemTemplate=null;kinds.SelectedValuePath="Key";kinds.SelectedValue="invoice";top.Children.Add(kinds);
        top.Children.Add(DocumentActions.Toolbar(()=>Try(()=>{Save();var path=Path.Combine(Path.GetTempPath(),$"Комплект-{id}-{current}-{Guid.NewGuid():N}.docx");IssueDocumentNavigation.OpenWord(path,()=>{InvoiceDocuments.SaveIssueDocumentWord(Info,path);return path;});}),()=>Try(()=>{Save();DocumentSaveDialog.Save(Store,DialogOwner,$"Комплект-{id}-{Tickets.Core.Store.RecipientDocumentName(current)}.docx",p=>InvoiceDocuments.SaveIssueDocumentWord(Info,p));}),()=>Try(()=>{Save();DocumentPrintPreview.Show(DialogOwner,Title,w=>IssueDocumentWindow.Document(Info,w),current=="ttn",current=="contract");})));
        var tabs=new TabControl();root.Children.Add(tabs);tabs.Items.Add(new TabItem{Header="Документ",Content=preview});tabs.Items.Add(new TabItem{Header="Изменить текст",Content=editor});
        kinds.SelectionChanged+=(_,_)=>Try(()=>{Save();var next=kinds.SelectedValue as string??"invoice";try{Store.GetRecipientPackageDocument(id,next);current=next;Load();}catch{kinds.SelectedValue=current;throw;}});
        tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==0)Try(()=>{Save();Refresh();});};
        Closing+=(_,e)=>{try{Save();}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message);}};
        DocumentPrintPreview.Route(root,()=>Try(()=>{Save();DocumentPrintPreview.Show(DialogOwner,Title,w=>IssueDocumentWindow.Document(Info,w),current=="ttn",current=="contract");}));Load();
    }
    private void Load(){editor.Load(Info.SourceDocument!,current);Refresh();}
    private void Refresh()=>preview.Document=IssueDocumentWindow.Document(Info);
    private void Save(){if(!editor.IsDirty)return;var invoice=Info.Invoice;var bytes=CustomDocumentForms.Fill(editor.SaveBytes(),invoice,current,invoice.ContractDate??DateTimeOffset.Parse(invoice.IssuedAt).Date);Store.SaveRecipientPackageDocument(packageId,current,bytes);editor.Load(bytes,current);Refresh();}
}
