using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tickets.Core;

internal static class FourUp015Checks
{
    private const double Mm=96.0/25.4;
    private const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);
        var original=store.TicketPrintPreview(show.Id,[seat.Id],"Four-up UI preview","UI015",1);
        var history=store.History(show.Id).ToArray();var issues=store.Issues(show.Id).Count;
        var custom=TicketBlankEditing.CreateA4();custom.Name="UI015 custom four-up";
        custom.Positions[1].Fields=custom.Fields.Select(field=>field with{}).ToList();
        custom.Positions[1].Fields![0].X=17;custom.Positions[1].Fields![0].FontFamily="Courier New";
        store.SaveTicketBlank(custom);
        try
        {
            var blanks=store.TicketBlanks();
            Check(blanks.Count(b=>b.Legacy is not null)==11,"All eleven original blank families are available as four-ticket A4 sheets");
            Check(blanks.All(b=>b.FourUpA4&&b.PageWidth==210&&b.PageHeight==297&&b.Positions.Count==4),"Every saved blank is four tickets on physical A4");
            var editorType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketBlankWindow")!;
            var editor=(Window)Activator.CreateInstance(editorType,store)!;editor.Show();Pump();
            try
            {
                var templates=(ComboBox)editorType.GetField("templates",PrivateInstance)!.GetValue(editor)!;
                foreach(var blank in templates.Items.OfType<TicketBlank>().ToArray())
                {
                    templates.SelectedItem=blank;Pump();
                    var paper=(Canvas)editorType.GetField("paper",PrivateInstance)!.GetValue(editor)!;
                    Check(Math.Abs(paper.Width-210*2.25)<.001&&Math.Abs(paper.Height-297*2.25)<.001,blank.Name+": editor paper is physical A4");
                    var visible=paper.Children.OfType<Border>().Where(border=>border.Child is not null).ToArray();
                    var expected=blank.Legacy is null?blank.Positions.Sum(position=>TicketBlankEditing.FieldsFor(blank,position).Count(field=>field.Enabled)):FourUpTicketLayouts.Paginate(blank,4).Single().Elements.Count;
                    Check(visible.Length==expected&&expected>0,blank.Name+": editor renders all four complete sample tickets");
                    foreach(var element in visible)Bounds(Canvas.GetLeft(element),Canvas.GetTop(element),element.ActualWidth,element.ActualHeight,paper.Width,paper.Height,blank.Name+": editor sample fits A4");
                    var bitmap=new RenderTargetBitmap((int)Math.Ceiling(paper.Width),(int)Math.Ceiling(paper.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(paper);
                }
            }
            finally{editor.Close();}
            var printType=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketPrintWindow")!;
            var print=(Window)Activator.CreateInstance(printType,store,show.Id,new[]{seat.Id},"UI015 preview","UI015",1L)!;
            try
            {
                foreach(var count in new[]{1,4,5,9})
                {
                    var invoice=original with{Tickets=Enumerable.Range(0,count).Select(i=>original.Tickets[0] with{Id=10000+i,Number=100001+i,Seat=(101+i).ToString()}).ToArray()};
                    var standard=(FixedDocument)printType.GetMethod("BuildDocument",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[invoice,210*Mm,297*Mm])!;
                    Check(standard.Pages.Count==(count+3)/4,"Standard print paginates "+count+" tickets in groups of four");
                    var cards=standard.Pages.SelectMany(page=>page.Child.Children.OfType<Border>()).ToArray();
                    Check(cards.Length==count,"Standard print never fabricates tickets on its partial final page");
                    for(var pageIndex=0;pageIndex<standard.Pages.Count;pageIndex++)
                    {
                        var pageCards=standard.Pages[pageIndex].Child.Children.OfType<Border>().ToArray();
                        Check(pageCards.Length==Math.Min(4,count-pageIndex*4),"Standard print uses four actual cards on each complete sheet");
                        Check(pageCards.Select(FixedPage.GetLeft).Distinct().Count()==1,"Standard print stacks four wide cards in one column");
                        for(var index=1;index<pageCards.Length;index++)Check(FixedPage.GetTop(pageCards[index-1])+pageCards[index-1].Height<=FixedPage.GetTop(pageCards[index]),"Standard ticket cards do not overlap");
                    }
                    Check(cards.Select(card=>Descendants(card).OfType<TextBlock>().Single(text=>text.Text.StartsWith("БИЛЕТ · ")).Text).SequenceEqual(invoice.Tickets.Select(ticket=>"БИЛЕТ · "+ticket.Code)),"Standard print preserves selected ticket identity and order");
                    CheckPages(standard);
                    foreach(var blank in blanks)
                    {
                        var document=(FixedDocument)printType.GetMethod("BuildBlankDocument",PrivateInstance)!.Invoke(print,[invoice,blank,210*Mm,297*Mm,new Dictionary<long,InvoiceInfo>(),null,false])!;
                        Check(document.Pages.Count==(count+3)/4,blank.Name+": actual print page count for "+count+" real tickets");
                        CheckPages(document);
                        if(blank.Legacy is not null)
                        {
                            var expectedPages=FourUpTicketLayouts.Paginate(blank,count);
                            for(var pageIndex=0;pageIndex<document.Pages.Count;pageIndex++)
                            {
                                var expected=expectedPages[pageIndex].Elements.Select(placement=>(placement,Text(placement,invoice,store.TicketVenueAddress(show.Id)))).Where(item=>item.Item2.Length>0).ToArray();
                                var actual=document.Pages[pageIndex].Child.Children.OfType<Border>().ToArray();
                                Check(actual.Length==expected.Length,blank.Name+": all fields present and no fake final-page ticket");
                                for(var fieldIndex=0;fieldIndex<actual.Length;fieldIndex++)
                                {
                                    var (placement,text)=expected[fieldIndex];var visual=actual[fieldIndex];
                                    Check(((TextBlock)visual.Child).Text==text,blank.Name+": real ticket field identity and order preserved");
                                    Check(Math.Abs(FixedPage.GetLeft(visual)-placement.X*Mm)<.001&&Math.Abs(FixedPage.GetTop(visual)-placement.Y*Mm)<.001,blank.Name+": actual legacy field uses fitted physical position");
                                }
                            }
                        }
                        else
                        {
                            var numberFields=document.Pages.SelectMany(page=>page.Child.Children.OfType<Viewbox>()).Where(box=>box.Child is TextBlock text&&invoice.Tickets.Any(ticket=>ticket.Code==text.Text)).Select(box=>((TextBlock)box.Child).Text).ToArray();
                            Check(numberFields.SequenceEqual(invoice.Tickets.Select(ticket=>ticket.Code)),blank.Name+": exactly the selected ticket numbers print once in selection order");
                            for(var pageIndex=0;pageIndex<document.Pages.Count;pageIndex++)
                            {
                                var remaining=Math.Min(4,count-pageIndex*4);
                                var expected=blank.Positions.Take(remaining).Sum(position=>TicketBlankEditing.FieldsFor(blank,position).Count(field=>field.Enabled));
                                Check(document.Pages[pageIndex].Child.Children.OfType<Viewbox>().Count()==expected,blank.Name+": partial page contains only real ticket field groups");
                            }
                        }
                    }
                }
            }
            finally{print.Close();}
            Check(store.History(show.Id).SequenceEqual(history)&&store.Issues(show.Id).Count==issues&&!store.Seats(show.Id).Single(s=>s.Id==seat.Id).TicketId.HasValue,"Four-ticket editor and actual print rendering preserve issuance and history");
            Console.WriteLine($"Four-up v0.15: {blanks.Count} saved editors and all eleven legacy layouts checked at 1, 4, 5 and 9 tickets.");
        }
        finally{store.DeleteTicketBlank(custom.Id);}
    }
    private static string Text(LegacyTicketPlacement placement,InvoiceInfo invoice,string address)
        =>placement.Element.Binding.Length==0?placement.Element.Text:LegacyTicketTemplates.Value(placement.Element.Binding,invoice,invoice.Tickets[placement.TicketIndex],address,DateTime.Now,placement.PageNumber);
    private static void CheckPages(FixedDocument document)
    {
        foreach(var content in document.Pages)
        {
            var page=content.Child;
            Check(Math.Abs(page.Width-210*Mm)<.001&&Math.Abs(page.Height-297*Mm)<.001,"Actual ticket print page is physical A4");
            foreach(var element in page.Children.OfType<FrameworkElement>())Bounds(FixedPage.GetLeft(element),FixedPage.GetTop(element),element.Width,element.Height,page.Width,page.Height,"Actual printable field stays inside physical A4");
            page.Measure(new Size(page.Width,page.Height));page.Arrange(new Rect(0,0,page.Width,page.Height));page.UpdateLayout();
            var raster=new RenderTargetBitmap((int)Math.Ceiling(page.Width),(int)Math.Ceiling(page.Height),96,96,PixelFormats.Pbgra32);raster.Render(page);
        }
    }
    private static void Bounds(double x,double y,double width,double height,double pageWidth,double pageHeight,string message)
        =>Check(double.IsFinite(x)&&double.IsFinite(y)&&width>0&&height>0&&x>=-.01&&y>=-.01&&x+width<=pageWidth+.01&&y+height<=pageHeight+.01,message);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)foreach(var child in Descendants(VisualTreeHelper.GetChild(parent,index)))yield return child;
    }
    private static void Pump(){var frame=new DispatcherFrame();Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
