using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.Core;

namespace Tickets.App;

public partial class MainWindow
{
    public bool SeparateWindows{get;set;}
    private string WindowSettingsPath=>Path.Combine(Path.GetDirectoryName(store.Path)!,"window-mode.txt");
    private readonly Stack<Grid> editorStack=new();
    private readonly Dictionary<Grid,EditorWindow> hostedEditors=[];
    private void ReadWindowSettings()
    {
        SeparateWindows=File.Exists(WindowSettingsPath)&&File.ReadAllText(WindowSettingsPath).Trim()=="separate";
        Closing+=(_,e)=>{
            foreach(var page in editorStack.ToArray())
            {
                if(!hostedEditors.TryGetValue(page,out var editor)||editor.HasClosedForHost)continue;
                editor.Close();if(!editor.HasClosedForHost){e.Cancel=true;return;}
            }
        };
    }
    private void SettingsClick(object sender,RoutedEventArgs e)=>Run(()=>new SettingsWindow(store,this).ShowDialog());
    internal void SaveWindowSettings(bool separate)
    {
        var path=WindowSettingsPath;File.WriteAllText(path+".tmp",separate?"separate":"embedded");File.Move(path+".tmp",path,true);SeparateWindows=separate;
    }
    internal bool? PresentEditor(EditorWindow editor)
    {
        if(SeparateWindows)return editor.ShowSeparateDialog();
        var body=editor.Content as UIElement??throw new InvalidOperationException("Нет содержимого формы.");editor.Content=null;editor.IsInline=true;
        var page=new Grid{Background=new SolidColorBrush(Color.FromArgb(105,15,23,42))};
        var card=new Border{Background=Brushes.White,BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(1),Margin=new Thickness(12),
            MaxWidth=editor.Width,MaxHeight=editor.Height,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch};
        if(editor is PriceEditorWindow expandable)
            expandable.HallExpansionChanged038+=()=>{card.MaxWidth=expandable.HallExpanded038?double.PositiveInfinity:editor.Width;card.MaxHeight=expandable.HallExpanded038?double.PositiveInfinity:editor.Height;};
        TextElement.SetFontFamily(card,editor.FontFamily);TextElement.SetFontSize(card,editor.FontSize);TextElement.SetFontWeight(card,editor.FontWeight);
        var panel=new DockPanel();card.Child=panel;
        if(editor.DocumentTab is {} tab)
        {
            var documentFrame=new Grid{MaxWidth=editor.Width+34,MaxHeight=editor.Height,Margin=new Thickness(2),VerticalAlignment=VerticalAlignment.Top};
            documentFrame.ColumnDefinitions.Add(new(){Width=GridLength.Auto});documentFrame.ColumnDefinitions.Add(new());
            documentFrame.Children.Add(DocumentFlag025.Tab(tab));card.Margin=new Thickness(0);Grid.SetColumn(card,1);documentFrame.Children.Add(card);page.Children.Add(documentFrame);
        }
        else page.Children.Add(card);
        var title=new Grid{Background=Brushes.AliceBlue,Margin=new Thickness(0,0,0,editor is PriceEditorWindow or EventEditorWindow||editor.DocumentTab is not null?0:8)};DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        title.ColumnDefinitions.Add(new ColumnDefinition());
        title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var close=new Button{Content="Закрыть",Margin=new Thickness(editor.DocumentTab is not null?2:8),Width=120};close.Click+=(_,_)=>editor.Close();Grid.SetColumn(close,2);title.Children.Add(close);
        if(editor is PriceEditorWindow priceEditor&&priceEditor.HeaderSaveAction is not null)
        {
            EditorLayout044.Install(title);
            title.Children.Remove(close);close.Margin=new Thickness(0);
            var controls=new StackPanel{Orientation=Orientation.Horizontal};controls.SetResourceReference(MarginProperty,"EditorInset044");Grid.SetColumn(controls,2);title.Children.Add(controls);
            var expansion=priceEditor.HallExpansionButton038();expansion.SetResourceReference(MarginProperty,"EditorRight044");controls.Children.Add(expansion);controls.Children.Add(close);
            // Match the pricing form's sidebar so the caption is centred over the hall.
            title.ColumnDefinitions[0].Width=new GridLength(306);
            var save=new Button{Content="Сохранить",Margin=new Thickness(8),Width=180};save.Click+=(_,_)=>priceEditor.HeaderSaveAction();title.Children.Add(save);
            var caption=new TextBlock{Text="Расценовка",IsHitTestVisible=false,FontSize=21,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,8)};
            Grid.SetColumn(caption,1);Grid.SetColumnSpan(caption,2);title.Children.Add(caption);
        }
        else
        {
            var caption=new TextBlock{Text=editor.Title,FontSize=21,FontWeight=FontWeights.Bold,Margin=new Thickness(editor.DocumentTab is not null?2:15),HorizontalAlignment=HorizontalAlignment.Center,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};
            Grid.SetColumn(caption,editor is EventEditorWindow?0:1);if(editor is EventEditorWindow){Grid.SetColumnSpan(caption,3);caption.IsHitTestVisible=false;}title.Children.Add(caption);
        }
        var content=new ContentControl{Content=body,Tag=editor,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};panel.Children.Add(content);
        var previous=Keyboard.FocusedElement;if(editorStack.TryPeek(out var prior))prior.IsEnabled=false;
        editorStack.Push(page);hostedEditors[page]=editor;DialogLayer.Children.Add(page);DialogLayer.Visibility=Visibility.Visible;MainContent.IsEnabled=false;
        KeyboardNavigation.SetTabNavigation(page,KeyboardNavigationMode.Cycle);
        page.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;if(Keyboard.FocusedElement is HallView hall&&hall.CancelPendingRowMove())return;editor.Close();}};
        var frame=new DispatcherFrame();EventHandler closed=(_,_)=>frame.Continue=false;editor.Closed+=closed;
        try
        {
            DialogLayer.UpdateLayout();editor.NotifyShown();content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));Dispatcher.PushFrame(frame);return editor.InlineResult;
        }
        finally
        {
            editor.Closed-=closed;hostedEditors.Remove(page);content.Content=null;DialogLayer.Children.Remove(page);editorStack.Pop();
            if(editorStack.TryPeek(out var previousPage))previousPage.IsEnabled=true;
            else{DialogLayer.Visibility=Visibility.Collapsed;MainContent.IsEnabled=true;}
            if(previous is UIElement element&&element.IsEnabled)element.Focus();
        }
    }
}
internal sealed class SettingsWindow:EditorWindow
{
    internal SettingsWindow(Store store,MainWindow main):base(store,"Настройки",750,650)
    {
        var form=new StackPanel{Margin=new Thickness(25)};Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var separate=new CheckBox{Content="Открывать формы в отдельных окнах",IsChecked=main.SeparateWindows,Margin=new Thickness(0,15,0,20),FontSize=17};form.Children.Add(separate);
        form.Children.Add(new TextBlock{Text="По умолчанию формы открываются внутри главного окна. Настройка применяется к следующим открываемым формам.",TextWrapping=TextWrapping.Wrap});
        var sender=Field(form,"От кого по умолчанию",store.Sender);
        var directory=Field(form,"Папка накладных и спецификаций",store.DocumentsDirectory);
        Button(form,"Выбрать папку…",()=>{var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Папка документов"};if(System.IO.Directory.Exists(directory.Text))dialog.InitialDirectory=directory.Text;if(dialog.ShowDialog(DialogOwner)==true)directory.Text=dialog.FolderName;});
        Button(form,"Обновление",()=>new UpdateWindow(store).ShowDialog());
        Button(form,"Резервная копия…",()=>main.BackupFromSettings());
        Button(form,"Ticket PRO",()=>main.OpenTicketPro037());
        Button(form,"Сохранить",()=>Try(()=>{var accepted=SenderConfirmation025.Choose(DialogOwner,store.Sender,sender.Text);if(accepted!=sender.Text){sender.Text=accepted;return;}store.SaveDocumentSettings(sender.Text,directory.Text);main.SaveWindowSettings(separate.IsChecked==true);Finish(true);}));
        var transfer=new WrapPanel{Margin=new Thickness(0,10,0,0),HorizontalAlignment=HorizontalAlignment.Left};form.Children.Add(transfer);
        Button(transfer,"Импорт",()=>Try(()=>main.ImportFromSettings033(DialogOwner)));
        Button(transfer,"Экспорт",()=>Try(()=>new TransferWindow033(store){Owner=DialogOwner}.ShowDialog()));
    }
}
