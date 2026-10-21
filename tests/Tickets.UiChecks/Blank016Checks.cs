using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

internal static class Blank016Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.TicketBlankWindow")!;
        var editor=(Window)Activator.CreateInstance(type,store)!;
        try
        {
            var templates=(ComboBox)type.GetField("templates",Private)!.GetValue(editor)!;
            foreach(var original in templates.Items.OfType<TicketBlank>().Where(b=>b.Legacy is not null).ToArray())
            {
                templates.SelectedItem=original;
                var settings=(StackPanel)type.GetField("settings",Private)!.GetValue(editor)!;
                if(!settings.IsEnabled)throw new Exception(original.Name+": all original templates must permit editing");
                var working=(TicketBlank)type.GetField("blank",Private)!.GetValue(editor)!;
                if(working.Positions.Any(p=>p.Legacy is null))throw new Exception("Original editor needs independent per-ticket layout");
            }
            type.GetMethod("Load",Private)!.Invoke(editor,[TicketBlankEditing.CreateA4From030879()]);
            var choices=(ComboBox)type.GetField("fieldChoice",Private)!.GetValue(editor)!;
            var labels=choices.Items.Cast<object>().Select(item=>item.GetType().GetProperty("Label")!.GetValue(item)?.ToString()??"").ToArray();
            foreach(var label in new[]{"Адрес","Площадка","Серия","Номер","Дата","Время","Ряд","Место","Постоянный текст"})
                if(!labels.Any(text=>text.StartsWith(label+" · ")))throw new Exception("New editor missing separate field choice: "+label);
        }
        finally{editor.Close();}
    }
}
