using System.Reflection;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Tickets.Core;

internal static class Print014Checks
{
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);
        var blank=new TicketBlank{Name="UI014 exact positions",Positions=[new(){X=10,Y=10},new(){X=10,Y=110}]};
        blank.Positions[1].Fields=blank.Fields.Select(f=>f with{}).ToList();
        blank.Positions[1].Fields![0].X=17;blank.Positions[1].Fields![0].FontFamily="Courier New";blank.Positions[1].Fields![0].Rotation=90;
        store.SaveTicketBlank(blank);
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketPrintWindow")!;
        var before=store.Issues(show.Id).Count;
        var window=(System.Windows.Window)Activator.CreateInstance(type,store,show.Id,new[]{seat.Id},"UI014 preview","UI014PRINT",1L)!;
        var choices=(ComboBox)type.GetField("blankChoice",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        choices.SelectedItem=choices.Items.OfType<TicketBlank>().Single(b=>b.Id==blank.Id);
        var invoice=store.TicketPrintPreview(show.Id,new[]{seat.Id},"UI014 preview","UI014PRINT",1);
        invoice=invoice with{Tickets=Enumerable.Repeat(invoice.Tickets[0],3).ToArray()};
        var build=type.GetMethod("BuildWithSources",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var document=(FixedDocument)build.Invoke(window,new object?[]{invoice,794d,1123d,null,false})!;
        Check(document.Pages.Count==2,"Chosen two-position blank paginates three tickets onto two actual pages");
        var first=document.Pages[0].Child!;var visuals=first.Children.OfType<Viewbox>().ToArray();
        Check(visuals.Length==16,"Preview contains both complete ticket placements");
        Check(Math.Abs(FixedPage.GetLeft(visuals[0])-15*96/25.4)<.01,"First placement retains calibrated physical coordinates");
        Check(Math.Abs(FixedPage.GetLeft(visuals[8])-27*96/25.4)<.01&&Math.Abs(FixedPage.GetTop(visuals[8])-113*96/25.4)<.01,"Independent second-position fields appear at selected coordinates");
        var label=(TextBlock)visuals[8].Child;
        Check(label.FontFamily.Source=="Courier New"&&label.LayoutTransform is RotateTransform rotation&&rotation.Angle==-90,"Independent field typography and rotation reach preview");
        var standard=(FixedDocument)type.GetMethod("BuildDocument",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{invoice,794d,1123d})!;
        var validate=type.GetMethod("ValidatePrintableArea",BindingFlags.Static|BindingFlags.NonPublic)!;
        var hardwareArea=new System.Windows.Rect(30,30,734,1063);var standardWarned=false;
        var originalX=FixedPage.GetLeft(standard.Pages[0].Child!.Children.OfType<System.Windows.FrameworkElement>().First());
        try{validate.Invoke(null,new object[]{standard,hardwareArea,false});}catch(TargetInvocationException ex) when(ex.InnerException?.GetType().Name=="PrintAreaWarning"){standardWarned=true;}
        Check(standardWarned,"Standard ticket cards warn when hardware margins exceed fixed card margins");
        validate.Invoke(null,new object[]{standard,hardwareArea,true});
        Check(FixedPage.GetLeft(standard.Pages[0].Child!.Children.OfType<System.Windows.FrameworkElement>().First())==originalX,"Standard overflow override preserves original coordinates");
        var legacy=store.TicketBlanks().First(b=>b.Legacy is not null);
        var legacyBuild=type.GetMethod("BuildLegacyDocument",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var args=new object?[]{invoice,legacy,new Dictionary<long,InvoiceInfo>(),legacy.PageWidth*96/25.4,legacy.PageHeight*96/25.4,new System.Windows.Rect(0,0,1,1),false};
        var warned=false;
        try{legacyBuild.Invoke(window,args);}catch(TargetInvocationException ex) when(ex.InnerException?.GetType().Name=="PrintAreaWarning"){warned=true;}
        Check(warned,"Legacy fields beyond printer margins produce an overridable warning");
        args[^1]=true;
        var overridden=(FixedDocument)legacyBuild.Invoke(window,args)!;
        Check(overridden.Pages.Count>0&&overridden.Pages[0].Child!.Children.Count>0,"Explicit override keeps the original legacy visual layout printable");
        window.Close();
        Check(store.Issues(show.Id).Count==before&&!store.Seats(show.Id).Single(s=>s.Id==seat.Id).TicketId.HasValue,"Closing preview does not issue tickets");
        store.DeleteTicketBlank(blank.Id);
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
