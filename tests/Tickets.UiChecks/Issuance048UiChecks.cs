using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Issuance048UiChecks
{
    internal static void Run(Store store,MainWindow main)
    {
        var venue=store.CreateHall("UI048 выдача","",Enumerable.Range(1,5).Select(i=>new DraftSeat{Number=i.ToString(),X=i*26}).ToArray());
        var seats=store.VenueSeats(venue).Select(s=>s.Id).ToArray();
        var plan=store.SavePlan(venue,"UI048 цены",seats.Select(id=>new PriceValue(id,1000,"#FFFFFF")).ToArray());
        var show=store.CreateEvent(store.SavePerformance("UI048 выдача"),venue,plan,DateTime.Today.AddDays(30));
        Invoke(main,"RefreshCatalogNavigation");main.SelectEvent(show);Pump();
        var recipient=(ComboBox)main.FindName("RecipientBox");recipient.ApplyTemplate();
        var editor=(TextBox)recipient.Template.FindName("PART_EditableTextBox",recipient);
        var hall=(HallView)main.FindName("Hall");
        var plain=main.FindName("IssueWithoutInvoiceButton048") as Button;
        Check(plain is not null,"sidebar provides direct issuance");
        var panel=(StackPanel)recipient.Parent;
        Check(panel.Children.IndexOf(plain!)==panel.Children.IndexOf(recipient)+1,"direct issuance is immediately below recipient");
        var invoice=(Button)main.FindName("IssueButton");
        ((TextBox)main.FindName("SeriesBox")).Text="UI048";
        ((TextBox)main.FindName("NumberBox")).Text="1";
        var documents=Path.Combine(Path.GetDirectoryName(store.Path)!,"issuance048-documents");
        store.SaveDocumentSettings(store.Sender,documents);
        const string organization="UI048 организация";
        store.SaveOrganizationDetails(organization,false,new OrganizationDetails(Name:organization,Inn:"1234567890"));
        Invoke(main,"RefreshRecipients",false);

        var opened=0;
        var watcher=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        watcher.Tick+=(_,_)=>{
            foreach(var window in Application.Current.Windows.OfType<Window>().Where(w=>w.GetType().Name=="InvoiceWindow").ToArray())
            {opened++;window.Close();}
        };
        watcher.Start();
        try
        {
            Select(0,organization);
            Check(plain!.IsEnabled&&invoice.IsEnabled,"valid selection enables both issuance actions");
            plain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            var issued=store.Seats(show).Single(s=>s.Id==seats[0]);
            Check(issued.TicketId.HasValue&&issued.Recipient==organization&&opened==0,"direct issuance saves selected seats without opening invoice");
            CheckCleared();
            var issue=issued.IssueId!.Value;
            Check(File.Exists(store.InvoiceDocumentPath(issue))&&File.Exists(store.IssueSpecificationDocumentPath(issue)),"direct issuance prepares invoice and specification");
            foreach(var kind in IssueDocumentKinds.All)
            {
                var document=store.GetIssueDocument(show,issue,kind);
                var extension=ExcelTtnData.CanRender(document)?".xlsx":".docx";
                var path=Path.ChangeExtension(store.IssueDocumentPath(issue,kind),extension);
                Check(File.Exists(path),"direct issuance prepares "+IssueDocumentKinds.Name(kind)+" in its applicable "+extension+" format");
            }

            Select(1,"UI048 без реквизитов");plain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            var simple=store.Seats(show).Single(s=>s.Id==seats[1]).IssueId!.Value;
            Check(File.Exists(store.InvoiceDocumentPath(simple))&&!File.Exists(store.IssueSpecificationDocumentPath(simple))&&opened==0,"recipient without requisites receives the applicable invoice only");
            CheckCleared();

            Select(2,organization);invoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(store.Seats(show).Single(s=>s.Id==seats[2]).TicketId.HasValue&&opened==1,"existing invoice action still issues and opens invoice");
            CheckCleared();

            var blockedPath=Path.Combine(Path.GetDirectoryName(store.Path)!,"issuance048-blocked");
            store.SaveDocumentSettings(store.Sender,blockedPath);File.WriteAllText(Path.Combine(blockedPath,"Накладные"),"file blocks invoice directory");
            Select(3,organization);plain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(store.Seats(show).Single(s=>s.Id==seats[3]).TicketId.HasValue&&opened==1,"document write failure retains successful direct issuance without invoice window");
            CheckCleared();
            Check(((TextBlock)main.FindName("StatusText")).Text.Contains("Не удалось"),"document write failure remains visible");
            store.SaveDocumentSettings(store.Sender,documents);

            store.DeleteVenue(venue);Invoke(main,"RefreshCatalogNavigation");Select(4,organization);
            Check(!plain.IsEnabled&&!invoice.IsEnabled,"archived event disables both issuance actions");
            Invoke(main,"IssueWithoutInvoiceClick048",plain,new RoutedEventArgs());
            Invoke(main,"IssueClick",invoice,new RoutedEventArgs());
            Check(!store.Seats(show).Single(s=>s.Id==seats[4]).TicketId.HasValue&&opened==1,"archive guard also blocks directly invoked issuance handlers");
        }
        finally{watcher.Stop();store.SaveDocumentSettings(store.Sender,documents);}

        void Select(int seat,string name)
        {editor.Text=name;hall.SetSelection([seats[seat]]);Pump();}
        void CheckCleared()
        {Check(recipient.SelectedItem is null&&recipient.Text==""&&editor.Text==""&&!recipient.IsDropDownOpen&&!plain!.IsEnabled&&!invoice.IsEnabled,"successful issuance clears editable recipient and pending suggestions");}
    }
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
