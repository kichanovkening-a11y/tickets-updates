using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class DocumentForms047Checks
{
    internal static void Run(string seed)
    {
        var directory=Path.Combine(Path.GetTempPath(),"Tickets047-forms-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var db=Path.Combine(directory,"tickets.db");File.Copy(seed,db);
        var store=new Store(db);
        var owner=new Window();Application.Current.MainWindow=owner;owner.Show();
        var form=new RecipientFormsWindow(store,"Test 047");
        try
        {
            var controls=Walk(form).ToArray();
            var kinds=controls.OfType<ComboBox>().First(c=>c.Items.Count==6);
            Check(kinds.SelectedIndex==-1,"no document type selected initially");
            Check(!controls.OfType<Button>().Single(b=>Equals(b.Content,"Загрузить форму")).IsEnabled,"upload unavailable until type selection");
            Check(!controls.OfType<DocumentTextEditor>().Single().IsEnabled,"editor unavailable until type selection");
            kinds.SelectedIndex=0;
            Check(controls.OfType<Button>().Single(b=>Equals(b.Content,"Загрузить форму")).IsEnabled,"explicit choice enables upload");
            Check(controls.OfType<DocumentTextEditor>().Single().IsEnabled,"explicit choice opens selected template");
        }
        finally{form.Close();}
        try
        {
            const string recipient="Navigation 047";
            store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000045",Signatory:"Test"));
            var venue=store.CreateHall("Navigation 047","",[new DraftSeat{Number="1",X=0,Y=0}]);
            var seat=store.VenueSeats(venue).Single().Id;
            var plan=store.SavePlan(venue,"Navigation 047",[new PriceValue(seat,1000,"#FFEF44")]);
            var evt=store.CreateEvent(store.SavePerformance("Navigation 047"),venue,plan,new DateTime(2099,1,1,19,0,0));
            var issue=store.Issue(evt,[seat],recipient,"NAV047",1);
            var invoice=new InvoiceWindow(store,evt,issue);
            try
            {
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
                timer.Tick+=(_,_)=>{
                    var child=Application.Current.Windows.OfType<CustomIssueDocumentWindow>().FirstOrDefault();
                    if(child is null)return;timer.Stop();child.Close();
                };
                timer.Start();
                try{Walk(invoice).OfType<Button>().Single(b=>Equals(b.Content,"Акт")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                finally{timer.Stop();}
                Check(invoice.HasClosedForHost,"closing act also closes the underlying invoice");
            }
            finally{if(!invoice.HasClosedForHost)invoice.Close();}
        }
        finally{owner.Close();}
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Check(bool value,string message)
    {if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
