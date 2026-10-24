using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal static class ReturnedTicketDetails027
{
    private static readonly object ToggleMarker=new();

    internal static TextBlock CreateToggle(int count,Action toggle)
    {
        var text=new TextBlock{Text=$"Возврат: {count}",Tag=ToggleMarker,Cursor=Cursors.Hand,Focusable=true,VerticalAlignment=VerticalAlignment.Center};
        text.PreviewMouseLeftButtonUp+=(_,e)=>{e.Handled=true;toggle();};
        text.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){e.Handled=true;toggle();}};
        return text;
    }

    internal static bool IsToggleSource(DependencyObject? source)
    {
        while(source is not null)
        {
            if(source is FrameworkElement element&&ReferenceEquals(element.Tag,ToggleMarker))return true;
            source=source is FrameworkContentElement content?content.Parent:
                source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    internal static FrameworkElement CreateTable(Store store,long eventId,long issueId)
    {
        var table=new DataGrid{Tag=ToggleMarker,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,
            HeadersVisibility=DataGridHeadersVisibility.Column,SelectionMode=DataGridSelectionMode.Single,
            Margin=new Thickness(0,6,0,6),FontSize=16,MinWidth=520,ItemsSource=store.ReturnedTicketsForIssue(eventId,issueId)};
        foreach(var (caption,path,width) in new[]{("Сектор","Sector",120d),("Ряд","Row",60d),("Место","Seat",65d),("Билет №","Code",120d),("Сумма, ₽","Amount",100d),("Дата возврата","ReturnedAt",165d)})
        {
            var binding=new Binding(path);
            if(path=="Amount")binding.StringFormat="N0";
            if(path=="ReturnedAt")binding.Converter=new ReturnDateConverter();
            table.Columns.Add(new DataGridTextColumn{Header=caption,Binding=binding,Width=new DataGridLength(width)});
        }
        return table;
    }

    private sealed class ReturnDateConverter:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>
            value is string text&&DateTimeOffset.TryParse(text,out var date)?date.ToString("dd.MM.yyyy HH:mm:ss"):"";
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
}
