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

    internal static WrapPanel Toolbar(string selected,Action<string> navigate)
    {
        var panel=new WrapPanel{Margin=new Thickness(0,0,0,4)};
        void Add(string key,string caption)
        {
            var active=key==selected;
            var button=new Button{Content=caption,Width=145,MinHeight=34,Margin=new Thickness(0,0,5,4),
                HorizontalContentAlignment=HorizontalAlignment.Center};
            if(active)
            {
                button.Background=Brushes.SteelBlue;button.Foreground=Brushes.White;button.FontWeight=FontWeights.Bold;
                button.ToolTip="Текущий документ";
            }
            button.Click+=(_,_)=>{if(!active)navigate(key);};
            panel.Children.Add(button);
        }
        Add(Invoice,"Накладная");
        foreach(var kind in IssueDocumentKinds.All)Add(kind,IssueDocumentKinds.Name(kind));
        Add(Specification,"Спецификация");
        return panel;
    }

    internal static void Open(Store store,long eventId,long issueId,string selected)
    {
        while(selected!=Invoice)
        {
            string next=Invoice;
            EditorWindow editor=selected==Specification
                ?new SpecificationWindow(store,store.Invoice(eventId,issueId).Recipient,true)
                :new IssueDocumentWindow(store,eventId,issueId,selected);
            if(selected==Specification)editor.Title=$"Спецификация · выдача № {issueId}";
            var body=editor.Content as UIElement??throw new InvalidOperationException("Нет содержимого документа.");
            editor.Content=null;
            var frame=new DockPanel{Margin=new Thickness(10,10,10,0)};
            var navigation=Toolbar(selected,target=>{
                try
                {
                    // Persist edits on document changes; the editors retain their
                    // save/discard/cancel warnings when closed normally.
                    if(editor is IssueDocumentWindow document)document.SaveForNavigation();
                    if(editor is SpecificationWindow specification&&!specification.SaveForNavigation())return;
                    editor.Close();
                    if(editor.HasClosedForHost)next=target;
                }
                catch(Exception ex){MessageBox.Show(editor.DialogOwnerForHost,ex.Message,"Документ",MessageBoxButton.OK,MessageBoxImage.Warning);}
            });
            DockPanel.SetDock(navigation,Dock.Top);frame.Children.Add(navigation);frame.Children.Add(body);editor.Content=frame;
            editor.ShowDialog();
            selected=next;
        }
    }
}
