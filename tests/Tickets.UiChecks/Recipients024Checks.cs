using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Recipients024Checks
{
    internal static void Run(Store store)
    {
        const string recipient="Получатель UI024";
        store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7701234567",Address:"Реквизиты UI024 скрыты"));
        var choice=store.Events().Select(e=>(Event:e,Seat:store.Seats(e.Id).FirstOrDefault(s=>s.Available))).First(x=>x.Seat is not null);
        var issue=store.Issue(choice.Event.Id,[choice.Seat!.Id],recipient,"UI024",1);
        var package=store.CreateRecipientPackage(recipient,[(choice.Event.Id,issue)]);
        var recipients=new RecipientsWindow(store);recipients.Show();Pump();
        try
        {
            var organizations=Walk(recipients).OfType<ListBox>().Single();
            organizations.SelectedItem=organizations.Items.Cast<RecipientDirectoryInfo>().Single(x=>x.Name==recipient);Pump();
            Check(!Walk(recipients).OfType<Button>().Any(b=>Equals(b.Content,"Открыть выдачу")),"recipient history omits redundant open action");
            Check(!Walk(recipients).OfType<TextBlock>().Any(t=>t.Text.Contains("Реквизиты UI024 скрыты")),"requisites stay in their editor");
            Check(Walk(recipients).OfType<Button>().Any(b=>Equals(b.Content,"Реквизиты организации")),"requisites editor action remains");
            if(store.DocumentArchive().Contains(choice.Event.Id,choice.Event.Date))Walk(recipients).OfType<Button>().Single(b=>Equals(b.Content,"Архив")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var history=Walk(recipients).OfType<TreeView>().Single();
            var performance=Walk(history).OfType<TreeViewItem>().First(x=>x.Header is TextBlock t&&t.Text==choice.Event.Title);
            performance.IsSelected=true;Pump();var title=(TextBlock)performance.Header;
            Check(title.Background==Brushes.SteelBlue&&title.Foreground==Brushes.White,"selected history performance has white text on blue");
            var date=Walk(history).OfType<TextBlock>().SelectMany(t=>t.Inlines.OfType<Run>()).Single(r=>r.FontSize>=19);
            Check(date.Background!=Brushes.Black&&date.Foreground!=Brushes.Red&&date.FontSize>=19,"history event date is larger in ordinary colors");
        }
        finally{recipients.Close();}
        var saved=new RecipientPackagesWindow(store,recipient);saved.Show();Pump();
        try
        {
            if(store.DocumentArchive().Contains(package))Walk(saved).OfType<Button>().Single(b=>Equals(b.Content,"Архив")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var buttons=Walk(saved).OfType<Button>().Where(b=>b.Tag is long).ToArray();
            Check(buttons.Length==1&&Equals(buttons[0].Tag,package.Id),"saved packages window lists stored package");
            Check(!Walk(saved).OfType<ContextMenu>().Any(),"saved packages use a window list");
            Exception? openedError=null;
            saved.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                var child=Application.Current.Windows.OfType<RecipientPackageWindow>().FirstOrDefault();
                try{Check(child is {IsVisible:true}&&ReferenceEquals(child.Owner,saved),"saved-list click opens a usable child window in embedded mode");}
                catch(Exception ex){openedError=ex;}
                finally{child?.Close();}
            }));
            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();if(openedError is not null)throw openedError;
        }
        finally{saved.Close();}
        var editor=new RecipientPackageWindow(store,package.Id);editor.Show();Pump();
        try
        {
            Check(!Walk(editor).OfType<ComboBox>().Any(c=>c.Items.OfType<KeyValuePair<string,string>>().Any(i=>i.Key=="invoice")),"package document selection has no combo box");
            string[] captions=["Накладная","Договор","Акт","ТТН","Счёт","Спецификация"];
            var navigation=Walk(editor).OfType<ScrollViewer>().Single(s=>s.Content is StackPanel p&&p.Children.OfType<Button>().Count()==6);
            var buttons=((StackPanel)navigation.Content).Children.OfType<Button>().ToArray();
            Check(buttons.Select(b=>b.Content?.ToString()).SequenceEqual(captions),"package uses journal document order and captions");
            Check(buttons[0].Background==Brushes.SteelBlue&&buttons[0].Foreground==Brushes.White,"active package document has journal highlight");
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(Walk(editor).OfType<Button>().Single(b=>Equals(b.Content,"Договор")).Background==Brushes.SteelBlue,"package button switches active document");
            Check(Walk(editor).OfType<TabControl>().Single().Items.Count==2,"package retains preview and text editing tabs");
        }
        finally{editor.Close();}
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 024 "+message);}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Walk(DependencyObject? root)
    {if(root is null)yield break;yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
