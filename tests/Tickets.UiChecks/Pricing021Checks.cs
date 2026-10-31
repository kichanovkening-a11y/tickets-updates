using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Tickets.App;
using Tickets.Core;

internal static class Pricing021UiChecks
{
    internal static void Run(Store store)
    {
        var recipientWindow=new RecipientsWindow(store);
        var buttons=Logical(recipientWindow.Content as DependencyObject).OfType<Button>().Where(b=>b.Content is string text&&new[]{"Реквизиты организации","Документы получателя"}.Contains(text)).ToArray();
        Check(buttons.Length==2&&buttons.All(b=>double.IsNaN(b.Width)&&b.ContentTemplate is null)&&buttons.Select(b=>b.Height).Distinct().Count()==1&&buttons.Select(b=>b.Margin.Top).Distinct().Count()==1,"recipient button row remains compact, same height and aligned");recipientWindow.Close();
        var venue=store.CreateHall("Расценовка UI021","",new[]{new DraftSeat{Row="1",Number="1",X=0,Y=0},new DraftSeat{Row="1",Number="2",X=18,Y=0}});
        var seats=store.VenueSeats(venue);var category=store.PriceColors().First();
        var source=store.SavePlan(venue,"Исходная UI021",seats.Select(s=>new PriceValue(s.Id,category.Amount,category.Color)).ToArray(),[]);
        var editor=new PriceEditorWindow(store,venue,source){IsInline=true};editor.NotifyShown();
        var label=Logical(editor.Content as DependencyObject).OfType<TextBlock>().Single(t=>t.Text=="Название новой расценовки");
        var labelHost=label.Parent is Decorator decorator?(FrameworkElement)decorator:label;
        var parent=(Panel)labelHost.Parent;var name=(TextBox)parent.Children[parent.Children.IndexOf(labelHost)+1];name.Text="Другая UI021";
        editor.HeaderSaveAction!();
        Check(editor.CreatedId!=0&&editor.CreatedId!=source,"identical prices with new name create a separate plan");
        Check(store.Plans(venue).Any(p=>p.Id==editor.CreatedId&&p.Name=="Другая UI021"),"new plan name persists");
        Check(store.PlanPrices(source).SequenceEqual(store.PlanPrices(editor.CreatedId)),"identical prices remain identical");
        var totals=Logical(editor.Content as DependencyObject).OfType<TextBlock>().FirstOrDefault(t=>string.Concat(t.Inlines.OfType<Run>().Select(r=>r.Text)).StartsWith("Всего мест:"));
        var renderedText=totals is null?"":string.Concat(totals.Inlines.OfType<Run>().Select(r=>r.Text));
        Check(totals is not null&&totals.TextWrapping==TextWrapping.NoWrap&&!totals.Inlines.OfType<LineBreak>().Any()&&!renderedText.Contains('\n')&&!renderedText.Contains("Без цены")&&renderedText.Contains("Заблокировано:"),"pricing totals remain one concise row: "+renderedText);
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject? parent)
    {if(parent is null)yield break;yield return parent;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var descendant in Logical(child))yield return descendant;}
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS 021 "+text);}
}
