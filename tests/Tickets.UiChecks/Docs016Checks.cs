using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.Core;

internal static class Docs016Checks
{
    internal static void Run(Store store)
    {
        const string recipient="UI016 получатель";
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);
        store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"016-original",Signatory:"Получатель прежний"));
        var issue=store.Issue(show.Id,[seat.Id],recipient,"UI016",1);
        var originalSender=store.Invoice(show.Id,issue).SenderDetails;
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.InvoiceWindow")!;
        var window=(Window)Activator.CreateInstance(type,store,show.Id,issue,false)!;window.Show();Pump();
        try
        {
            CheckNavigation(window);
            foreach(var saveEdit in new[]{true,false})
            {
                const string caption="Реквизиты";
                Exception? error=null;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                    Window? editor=null;
                    try
                    {
                        editor=Application.Current.Windows.Cast<Window>().Single(w=>w.IsVisible&&w.GetType().Name=="RecipientEditorWindow");
                        Check(!Application.Current.Windows.Cast<Window>().Any(w=>w.IsVisible&&w.GetType().Name=="SenderEditorWindow"),caption+": does not open sender editor");
                        Check(Logical(editor).OfType<TextBox>().First().Text==recipient,caption+": edits actual recipient");
                        foreach(var label in new[]{"ИНН","КПП (если есть)","ОГРН / ОГРНИП","Юридический адрес","Банк","Расчётный счёт","Корреспондентский счёт","БИК","Подписант (ФИО)","Должность подписанта","Телефон","ОКПО (если есть)","ОКВЭД (если есть)","ОКАТО (если есть)"})
                            Check(Field(editor,label) is not null,caption+": full recipient field "+label);
                        if(saveEdit)
                        {
                            Field(editor,"ИНН").Text="016-recipient-new";Field(editor,"Подписант (ФИО)").Text="Получатель Новый 016";
                            Click(editor,"Сохранить");
                        }
                        else
                        {
                            Check(Field(editor,"ИНН").Text=="016-recipient-new"&&Field(editor,"Подписант (ФИО)").Text=="Получатель Новый 016","second entry point loads saved recipient");
                            Field(editor,"ИНН").Text="cancelled-change";Click(editor,"Отмена");
                        }
                    }
                    catch(Exception ex){error=ex;editor?.Close();}
                }));
                Click(window,caption);Pump();if(error is not null)throw error;
            }
            var persisted=new Store(store.Path);var invoice=persisted.Invoice(show.Id,issue);
            Check(invoice.RecipientDetails?.Inn=="016-recipient-new"&&persisted.GetOrganizationDetails(recipient,false).Inn=="016-recipient-new","recipient directory and current issue persist; cancel keeps saved details");
            Check(invoice.SenderDetails?.DisplayText==originalSender?.DisplayText,"recipient UI retains sender snapshot");
            Console.WriteLine("Docs v0.16: single-row navigation, full recipient editor from both buttons, persistence and cancellation checked.");
        }
        finally{window.Close();}
    }
    private static void CheckNavigation(Window window)
    {
        var names=new[]{"Накладная","Договор","Акт","ТТН","Счёт","Спецификация"};
        var buttons=names.Select(name=>Logical(window).OfType<Button>().Single(button=>Equals(button.Content,name))).ToArray();
        var row=(StackPanel)buttons[0].Parent;
        Check(row.Orientation==Orientation.Horizontal&&buttons.All(button=>ReferenceEquals(button.Parent,row)),"all six documents share one horizontal row");
        Check(buttons.Select(button=>button.TranslatePoint(new Point(),row).Y).Distinct().Count()==1,"all six captions have the same rendered top");
        Check(buttons.Sum(button=>button.ActualWidth+button.Margin.Left+button.Margin.Right)<=((FrameworkElement)row.Parent).ActualWidth+1,"all six document buttons fit the invoice header");
        foreach(var button in buttons)
        {
            Check(button.FontSize==16.9&&button.ContentTemplate is null,"document caption keeps normal font without shrinking/wrapping template");
            Check(!Visual(button).OfType<Viewbox>().Any(),"document caption does not scale down");
            var label=Visual(button).OfType<TextBlock>().Single();
            Check(label.TextWrapping==TextWrapping.NoWrap&&label.ActualWidth<=button.ActualWidth-button.Padding.Left-button.Padding.Right,"document caption fits without wrap or clipping");
        }
        Check(Equals(buttons[0].Background,Brushes.SteelBlue)&&Equals(buttons[0].Foreground,Brushes.White),"active document retains highlight");
    }
    private static TextBox Field(DependencyObject window,string caption)
    {
        var label=Logical(window).OfType<TextBlock>().Single(text=>text.Text==caption);var parent=(Panel)label.Parent;
        return (TextBox)parent.Children[parent.Children.IndexOf(label)+1];
    }
    private static void Click(DependencyObject parent,string caption)=>Logical(parent).OfType<Button>().Single(button=>Equals(button.Content,caption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<DependencyObject> Logical(DependencyObject parent)
    {
        yield return parent;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject parent)
    {
        yield return parent;for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)foreach(var item in Visual(VisualTreeHelper.GetChild(parent,index)))yield return item;
    }
    private static void Pump(){var frame=new DispatcherFrame();Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
