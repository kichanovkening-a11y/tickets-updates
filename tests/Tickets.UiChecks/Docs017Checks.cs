using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.Core;

internal static class Docs017Checks
{
    internal static void Run(Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);
        store.SaveOrganizationDetails("UI017 получатель",false,new(Name:"UI017 получатель",Inn:"017"));
        var issue=store.Issue(show.Id,[seat.Id],"UI017 получатель","UI017",1);
        store.SaveAllDocuments(show.Id,issue);
        var assembly=typeof(Tickets.App.App).Assembly;
        var navigation=assembly.GetType("Tickets.App.IssueDocumentNavigation")!;
        var toolbar=navigation.GetMethod("Toolbar",BindingFlags.Static|BindingFlags.NonPublic)!;
        var names=new[]{"Накладная","Договор","Акт","ТТН","Счёт","Спецификация"};
        foreach(var key in new[]{"invoice"}.Concat(IssueDocumentKinds.All).Append("specification"))
        {
            var path=key=="invoice"?store.InvoiceDocumentPath(issue):key=="specification"?store.IssueSpecificationDocumentPath(issue):store.IssueDocumentPath(issue,key);
            // Simulate a subsequent external Word edit; opening and closing an
            // unchanged editor must leave the existing document bytes intact.
            using(var file=File.Open(path,FileMode.Append))file.Write(new byte[]{0x17,0x20,0x26});
            var externalBytes=File.ReadAllBytes(path);
            Window Create()=>key=="invoice"
                ?(Window)Activator.CreateInstance(assembly.GetType("Tickets.App.InvoiceWindow")!,store,show.Id,issue,false)!
                :key=="specification"
                    ?(Window)Activator.CreateInstance(assembly.GetType("Tickets.App.SpecificationWindow")!,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{store,show.Id,issue},null)!
                    :(Window)Activator.CreateInstance(assembly.GetType("Tickets.App.IssueDocumentWindow")!,store,show.Id,issue,key)!;
            var window=Create();
            if(key!="invoice")
            {
                var body=(FrameworkElement)window.Content;window.Content=null;body.Margin=new Thickness(10,0,10,10);
                var frame=new DockPanel();
                var types=(ScrollViewer)toolbar.Invoke(null,new object[]{key,(Action<string>)(_=>{})})!;types.Margin=new Thickness(10,10,10,0);
                if(key!=IssueDocumentKinds.Bill)
                {
                    var actions=assembly.GetType("Tickets.App.DocumentActions")!;
                    var taken=actions.GetMethod("TakeToolbar",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[body]);
                    if(taken is not null)actions.GetMethod("Append",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[types,taken]);
                }
                DockPanel.SetDock(types,Dock.Top);frame.Children.Add(types);frame.Children.Add(body);window.Content=frame;
            }
            window.Show();Pump();
            try
            {
                var buttons=Logical(window).OfType<Button>().ToArray();
                var types=names.Select(caption=>buttons.Single(button=>Equals(button.Content,caption))).ToArray();
                var actions=new[]{"Открыть в Word","Сохранить","Печать"}.Select(caption=>buttons.Single(button=>Equals(button.Content,caption))).ToArray();
                Check(!buttons.Any(button=>Equals(button.Content,"Открыть")||Equals(button.Content,"Распечатать")||Equals(button.Content,"Обновить реквизиты")),key+": no duplicate or ambiguous document actions");
                var row=(StackPanel)actions[0].Parent;
                Check(row.Orientation==Orientation.Horizontal&&actions.All(button=>ReferenceEquals(button.Parent,row)),key+": three adjacent horizontal commands");
                var typeBounds=types.Select(button=>Bounds(button,window)).ToArray();var actionBounds=actions.Select(button=>Bounds(button,window)).ToArray();
                Check(typeBounds.Max(rect=>rect.Top)-typeBounds.Min(rect=>rect.Top)<1&&actionBounds.Max(rect=>rect.Top)-actionBounds.Min(rect=>rect.Top)<1,key+": exactly one row for types and one for commands");
                if(key!=IssueDocumentKinds.Bill)Check(Math.Abs(actionBounds[0].Top-typeBounds[0].Top)<1,key+": navigation and actions share one row");
                if(key!=IssueDocumentKinds.Bill)Check(actionBounds[0].Left>=typeBounds[^1].Right,key+": actions follow navigation");
                foreach(var button in types.Concat(actions))
                {
                    Check(button.FontSize==16.9&&button.ContentTemplate is null&&!Visual(button).OfType<Viewbox>().Any(),key+": readable unscaled caption "+button.Content);
                    var label=Visual(button).OfType<TextBlock>().Single();
                    Check(label.TextWrapping==TextWrapping.NoWrap&&label.ActualWidth<=button.ActualWidth-button.Padding.Left-button.Padding.Right+1,key+": caption fits without wrapping "+button.Content);
                }
                Check(actions.All(button=>button.ActualHeight<60&&double.IsNaN(button.Width)),key+": compact content-sized commands");
                var preview=Logical(window).OfType<FlowDocumentScrollViewer>().Single();var previewBounds=Bounds(preview,window);
                Check(preview.ActualWidth>=window.ActualWidth-(key==IssueDocumentKinds.Bill?100:70)&&previewBounds.Top>=actionBounds.Max(rect=>rect.Bottom),key+": document uses full available width below commands");
                var active=types.Single(button=>Equals(button.Background,Brushes.SteelBlue));
                Check(Equals(active.Foreground,Brushes.White)&&Equals(active.Content,key=="invoice"?"Накладная":key=="specification"?"Спецификация":IssueDocumentKinds.Name(key)),key+": active type highlighted");
            }
            finally{window.Close();Pump();}
            Check(File.ReadAllBytes(path).SequenceEqual(externalBytes),key+": unchanged close preserves external Word edits");
        }
        Console.WriteLine("Docs v0.17: six document types, compact command rows, readable captions, full-width previews, external Word edits preserved.");
    }
    private static Rect Bounds(FrameworkElement element,Visual parent)=>element.TransformToAncestor(parent).TransformBounds(new Rect(element.RenderSize));
    private static IEnumerable<DependencyObject> Logical(DependencyObject parent)
    {
        yield return parent;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var item in Logical(child))yield return item;
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject parent)
    {
        yield return parent;for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)foreach(var item in Visual(VisualTreeHelper.GetChild(parent,index)))yield return item;
    }
    private static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);}
}
