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
    private void ReadWindowSettings()=>SeparateWindows=File.Exists(WindowSettingsPath)&&File.ReadAllText(WindowSettingsPath).Trim()=="separate";
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
        TextElement.SetFontFamily(card,editor.FontFamily);TextElement.SetFontSize(card,editor.FontSize);TextElement.SetFontWeight(card,editor.FontWeight);
        var panel=new DockPanel();card.Child=panel;page.Children.Add(card);
        var title=new DockPanel{Background=Brushes.AliceBlue,Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        var close=new Button{Content="Закрыть",Margin=new Thickness(8)};close.Click+=(_,_)=>editor.Close();DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);
        title.Children.Add(new TextBlock{Text=editor.Title,FontSize=21,FontWeight=FontWeights.Bold,Margin=new Thickness(15),VerticalAlignment=VerticalAlignment.Center});
        var content=new ContentControl{Content=body,Tag=editor,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};panel.Children.Add(content);
        var previous=Keyboard.FocusedElement;if(editorStack.TryPeek(out var prior))prior.IsEnabled=false;
        editorStack.Push(page);DialogLayer.Children.Add(page);DialogLayer.Visibility=Visibility.Visible;MainContent.IsEnabled=false;
        KeyboardNavigation.SetTabNavigation(page,KeyboardNavigationMode.Cycle);
        page.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;editor.Close();}};
        var frame=new DispatcherFrame();EventHandler closed=(_,_)=>frame.Continue=false;editor.Closed+=closed;
        try
        {
            DialogLayer.UpdateLayout();editor.NotifyShown();content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));Dispatcher.PushFrame(frame);return editor.InlineResult;
        }
        finally
        {
            editor.Closed-=closed;content.Content=null;DialogLayer.Children.Remove(page);editorStack.Pop();
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
        var form=new StackPanel{Margin=new Thickness(25)};Content=form;
        var separate=new CheckBox{Content="Открывать формы в отдельных окнах",IsChecked=main.SeparateWindows,Margin=new Thickness(0,15,0,20),FontSize=17};form.Children.Add(separate);
        form.Children.Add(new TextBlock{Text="По умолчанию формы открываются внутри главного окна. Настройка применяется к следующим открываемым формам.",TextWrapping=TextWrapping.Wrap});
        var sender=Field(form,"От кого по умолчанию",store.Sender);
        var directory=Field(form,"Папка накладных и спецификаций",store.DocumentsDirectory);
        Button(form,"Выбрать папку…",()=>{var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Папка документов"};if(System.IO.Directory.Exists(directory.Text))dialog.InitialDirectory=directory.Text;if(dialog.ShowDialog(DialogOwner)==true)directory.Text=dialog.FolderName;});
        Button(form,"Спецификации",()=>new SpecificationWindow(store).ShowDialog());
        Button(form,"Сохранить",()=>Try(()=>{store.SaveDocumentSettings(sender.Text,directory.Text);main.SaveWindowSettings(separate.IsChecked==true);Finish(true);}));
    }
}
