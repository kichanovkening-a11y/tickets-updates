using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Documents045MenuChecks
{
    internal static void Run(string seed)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Tickets045-menus-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var db=Path.Combine(folder,"tickets.db");File.Copy(seed,db);var store=new Store(db);
        const string recipient="Получатель меню 045";
        store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000045",Signatory:"Петров П. П."));
        var venue=store.CreateHall("Зал меню 045","",[new DraftSeat{Number="1",X=0,Y=0},new DraftSeat{Number="2",X=25,Y=0}]);
        var seats=store.VenueSeats(venue).Select(s=>s.Id).ToArray();var plan=store.SavePlan(venue,"Цены меню 045",seats.Select(s=>new PriceValue(s,1000,"#FFEF44")).ToArray());
        var evt=store.CreateEvent(store.SavePerformance("Меню документов 045"),venue,plan,new DateTime(2099,1,1,19,0,0));
        var issue=store.Issue(evt,[seats[0]],recipient,"MENU045",1);
        var owner=new Window{Width=1200,Height=900,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};Application.Current.MainWindow=owner;owner.Show();
        try
        {
            var forms=new RecipientFormsWindow(store,recipient);Offscreen(forms);forms.Show();
            try
            {
                var choices=Walk(forms).OfType<ComboBox>().First(c=>c.Items.Count>=5);
                var keys=choices.Items.Cast<object>().Select(x=>(string)x.GetType().GetProperty("Key")!.GetValue(x)!).ToArray();
                Check(keys.Contains("invoice")&&keys.Length==6,"recipient form menu includes all six document kinds");
            }
            finally{forms.Close();}
            foreach(var kind in new[]{"invoice","contract","act","bill","ttn","specification"})
            {
                EditorWindow window=kind=="invoice"?new InvoiceWindow(store,evt,issue):new CustomIssueDocumentWindow(store,evt,issue,kind);
                Offscreen(window);window.Show();
                try
                {
                    Check(Walk(window).OfType<DocumentPagePreview>().Count()==1,"native document preview is present in "+kind);
                    Check(Walk(window).OfType<Button>().Any(b=>Equals(b.Content,"Сохранить как форму получателя")),"recipient-specific reuse action is present in "+kind);
                    WaitForPages(window,kind);
                }
                finally{window.Close();}
            }
            var package=store.CreateRecipientPackage(recipient,[(evt,issue)]);var shared=new RecipientPackageWindow(store,package.Id);Offscreen(shared);shared.Show();
            try{WaitForPages(shared,"saved package");}finally{shared.Close();}
        }
        finally{owner.Close();}
    }
    private static void WaitForPages(Window window,string label)
    {
        var excel=label=="ttn";
        if(excel?Type.GetTypeFromProgID("Excel.Application") is null:!WordDocumentPages.Available)
        {
            Check(Walk(window).OfType<TextBlock>().Any(t=>t.Text.Contains(excel?"установите Microsoft Excel":"Просмотр без Word")),"missing Office is identified in "+label);
            if(excel)Check(!Walk(window).OfType<FlowDocumentScrollViewer>().Any(),"approved TTN is never replaced by a portable form");
            return;
        }
        var until=DateTime.UtcNow.AddSeconds(75);
        while(DateTime.UtcNow<until)
        {
            Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
            if(Walk(window).OfType<DocumentPageView>().Any()){Check(true,"Office fixed pages load through "+label+" menu");return;}
            Thread.Sleep(20);
        }
        throw new Exception("No Office pages in "+label+": "+string.Join(" | ",Walk(window).OfType<TextBlock>().Select(t=>t.Text)));
    }
    private static void Offscreen(Window window){window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-20000;window.Top=-20000;window.ShowInTaskbar=false;window.ShowActivated=false;}
    private static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
