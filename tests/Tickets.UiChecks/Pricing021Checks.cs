using System.Windows;
using System.Windows.Controls;
using Tickets.App;
using Tickets.Core;

internal static class Pricing021UiChecks
{
    internal static void Run(Store store)
    {
        var recipientWindow=new RecipientsWindow(store);
        var buttons=Logical(recipientWindow.Content as DependencyObject).OfType<Button>().Where(b=>b.Content is string text&&new[]{"Открыть выдачу","Реквизиты организации","Документы получателя"}.Contains(text)).ToArray();
        Check(buttons.Length==3&&buttons.All(b=>double.IsNaN(b.Width)&&b.ContentTemplate is null)&&buttons.Select(b=>b.Height).Distinct().Count()==1&&buttons.Select(b=>b.Margin.Top).Distinct().Count()==1,"recipient button row remains compact, same height and aligned");recipientWindow.Close();
        var venue=store.CreateHall("Расценовка UI021","",new[]{new DraftSeat{Row="1",Number="1",X=0,Y=0},new DraftSeat{Row="1",Number="2",X=18,Y=0}});
        var seats=store.VenueSeats(venue);var category=store.PriceColors().First();
        var source=store.SavePlan(venue,"Исходная UI021",seats.Select(s=>new PriceValue(s.Id,category.Amount,category.Color)).ToArray(),[]);
        var editor=new PriceEditorWindow(store,venue,source){IsInline=true};editor.NotifyShown();
        var label=Logical(editor.Content as DependencyObject).OfType<TextBlock>().Single(t=>t.Text=="Название новой расценовки");
        var parent=(Panel)LogicalTreeHelper.GetParent(label);var name=(TextBox)parent.Children[parent.Children.IndexOf(label)+1];name.Text="Другая UI021";
        editor.HeaderSaveAction!();
        Check(editor.CreatedId!=0&&editor.CreatedId!=source,"identical prices with new name create a separate plan");
        Check(store.Plans(venue).Any(p=>p.Id==editor.CreatedId&&p.Name=="Другая UI021"),"new plan name persists");
        Check(store.PlanPrices(source).SequenceEqual(store.PlanPrices(editor.CreatedId)),"identical prices remain identical");
        var totals=Logical(editor.Content as DependencyObject).OfType<TextBlock>().FirstOrDefault(t=>t.Text.StartsWith("Всего мест:"));
        Check(totals is not null&&!totals.Text.Contains('\n')&&!totals.Text.Contains("Без цены")&&totals.Text.Contains("Заблокировано:"),"pricing totals remain one concise row");
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject? parent)
    {if(parent is null)yield break;yield return parent;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var descendant in Logical(child))yield return descendant;}
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS 021 "+text);}
}
