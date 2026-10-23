using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

// One issuance owns navigation. Each editor is closed before its replacement is
// presented, so switching documents never builds a chain of modal editors.
internal static class IssueDocumentNavigation
{
    internal const string Invoice="invoice",Specification="specification";

    internal static ScrollViewer Toolbar(string selected,Action<string> navigate)
    {
        var panel=new StackPanel{Orientation=Orientation.Horizontal};
        void Add(string key,string caption)
        {
            var active=key==selected;
            // Natural caption widths keep all six documents on one row at the
            // regular font size. Bypass the shared shrinking/wrapping template.
            var button=new Button{Content=caption,ContentTemplate=null,Width=double.NaN,MinWidth=0,MinHeight=34,Padding=new Thickness(2,2,2,2),Margin=new Thickness(0,0,5,4),
                HorizontalContentAlignment=HorizontalAlignment.Center};
            if(active)
            {
                button.Background=Brushes.SteelBlue;button.Foreground=Brushes.White;button.FontWeight=FontWeights.Bold;
                button.ToolTip="Текущий документ";
            }
            button.Tag=key;
            button.Click+=(_,_)=>{if(!active)navigate(key);};
            panel.Children.Add(button);
        }
        Add(Invoice,"Накладная");
        foreach(var kind in IssueDocumentKinds.All)Add(kind,IssueDocumentKinds.Name(kind));
        Add(Specification,"Спецификация");
        return new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,0,4)};
    }

    internal static void SetBusinessEnabled(ScrollViewer toolbar,bool enabled)
    {
        if(toolbar.Content is not Panel panel)return;
        foreach(var button in panel.Children.OfType<Button>().Where(b=>b.Tag is string key&&key!=Invoice))
        {button.IsEnabled=enabled;if(!enabled)button.ToolTip="Заполните реквизиты получателя";else button.ToolTip=null;}
    }

    internal static void OpenWord(string path,Func<string> create)
    {
        if(!System.IO.File.Exists(path))path=create();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});
    }

    internal static void Open(Store store,long eventId,long issueId,string selected)
    {
        if(selected!=Invoice&&!store.CanUseBusinessDocuments(eventId,issueId))throw new TicketException("Заполните реквизиты получателя. Пока доступны накладная и печать билетов.");
        while(selected!=Invoice)
        {
            string next=Invoice;
            EditorWindow editor=store.CustomIssueTemplate(eventId,issueId,selected) is not null
                ?new CustomIssueDocumentWindow(store,eventId,issueId,selected)
                :selected==Specification
                ?new SpecificationWindow(store,eventId,issueId)
                :new IssueDocumentWindow(store,eventId,issueId,selected);
            if(selected==Specification&&editor.DocumentTab is null)DocumentFlag025.Apply(editor,(UIElement)editor.Content,store.Invoice(eventId,issueId).Recipient,issueId,"Спецификация");
            var body=editor.Content as UIElement??throw new InvalidOperationException("Нет содержимого документа.");
            editor.Content=null;
            var frame=new DockPanel();
            var navigation=Toolbar(selected,target=>{
                try
                {
                    // Persist edits on document changes; the editors retain their
                    // save/discard/cancel warnings when closed normally.
                    if(editor is IssueDocumentWindow document)document.SaveForNavigation();
                    if(editor is CustomIssueDocumentWindow custom)custom.SaveForNavigation();
                    if(editor is SpecificationWindow specification&&!specification.SaveForNavigation())return;
                    editor.Close();
                    if(editor.HasClosedForHost)next=target;
                }
                catch(Exception ex){MessageBox.Show(editor.DialogOwnerForHost,ex.Message,"Документ",MessageBoxButton.OK,MessageBoxImage.Warning);}
            });
            if(DocumentActions.TakeToolbar(body) is {} actions)DocumentActions.Append(navigation,actions);
            navigation.Margin=new Thickness(2,2,2,0);
            if(body is FrameworkElement content)content.Margin=new Thickness(2,0,2,2);
            DockPanel.SetDock(navigation,Dock.Top);frame.Children.Add(navigation);frame.Children.Add(body);editor.Content=frame;
            editor.ShowDialog();
            selected=next;
        }
    }
}
