using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tickets.Core;

internal static class Blank017Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketBlankWindow")!;
        var editor=(Window)Activator.CreateInstance(type,store)!;
        try
        {
            T Get<T>(string name)=>(T)type.GetField(name,Private)!.GetValue(editor)!;
            var properties=Get<StackPanel>("properties");
            var choices=Get<ComboBox>("fieldChoice");
            var blocks=Get<ComboBox>("blocks");
            var templates=Get<ComboBox>("templates");
            var originals=templates.Items.OfType<TicketBlank>().ToArray();
            foreach(var original in originals)
            {
                type.GetMethod("Load",Private)!.Invoke(editor,[original]);
                Check(properties.Children.Count==0&&choices.SelectedIndex==-1,"opening a template leaves irrelevant properties hidden: "+original.Name);
                Check(Get<TextBlock>("selection").Text.Contains("Выберите поле"),"unselected editor explains how to choose a field");
                choices.SelectedIndex=0;
                Check(properties.Children.OfType<TextBox>().Take(3).Select(Path).SequenceEqual(new[]{"FontFamily","FontSize","Rotation"}),"selected text leads with font family, size and rotation");
                CheckGeometry(properties);
            }
            type.GetMethod("Load",Private)!.Invoke(editor,[TicketBlankEditing.CreateA4From030879()]);
            choices.SelectedIndex=0;
            var working=Get<TicketBlank>("blank");
            var field=TicketBlankEditing.LayoutFor(working,working.Positions[0]).Bands.Where(b=>!b.Suppressed).SelectMany(b=>b.Elements).First();
            var otherField=TicketBlankEditing.LayoutFor(working,working.Positions[1]).Bands.Where(b=>!b.Suppressed).SelectMany(b=>b.Elements).First();
            var otherSize=otherField.FontSize;
            var size=properties.Children.OfType<TextBox>().Single(box=>Path(box)=="FontSize");
            size.Text="13";size.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Check(field.FontSize==13&&otherField.FontSize==otherSize,"font edits affect only the selected ticket's selected text");
            blocks.SelectedIndex=1;
            Check(!properties.Children.OfType<TextBox>().Any(),"whole-ticket selection shows no text formatting controls");
            var geometry=CheckGeometry(properties);
            var x=geometry.Children.OfType<TextBox>().Single(box=>Path(box)=="X");
            var expectedX=working.Positions[1].X+.25;
            x.Text=expectedX.ToString(System.Globalization.CultureInfo.InvariantCulture);x.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Check(working.Positions[1].X==expectedX,"whole-ticket precise coordinates remain editable");
            Get<TextBox>("name").Text="Blank 017 selected property UI check";
            Logical(editor).OfType<Button>().Single(button=>Equals(button.Content,"Сохранить шаблон")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Get<TextBlock>("notice").Text=="Бланк сохранён и доступен при печати.","saving reports success in the editor without a separate message window");
            var saved=store.TicketBlanks().Single(blank=>blank.Name=="Blank 017 selected property UI check");
            Check(saved.Positions[1].X==expectedX&&TicketBlankEditing.LayoutFor(saved,saved.Positions[0]).Bands.Where(b=>!b.Suppressed).SelectMany(b=>b.Elements).First().FontSize==13,"saving retains selected text formatting and precise whole-ticket placement");
        }
        finally{editor.Close();}
    }
    private static StackPanel CheckGeometry(StackPanel properties)
    {
        var expander=properties.Children.OfType<Expander>().Single();
        Check(Equals(expander.Header,"Точная настройка")&&!expander.IsExpanded,"precise geometry starts collapsed");
        var geometry=(StackPanel)expander.Content;
        Check(geometry.Children.OfType<TextBox>().Select(Path).SequenceEqual(new[]{"X","Y","Width","Height"}),"precise geometry retains all coordinates and dimensions");
        return geometry;
    }
    private static string? Path(TextBox box)=>BindingOperations.GetBinding(box,TextBox.TextProperty)?.Path.Path;
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Logical(child))yield return node;
    }
}
