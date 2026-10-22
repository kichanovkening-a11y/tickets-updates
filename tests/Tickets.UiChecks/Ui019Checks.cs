using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class Ui019Checks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store,MainWindow main)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        main.SelectEvent(show.Id);Pump();
        var recipient=(ComboBox)main.FindName("RecipientBox");recipient.ApplyTemplate();
        var edit=(TextBox)recipient.Template.FindName("PART_EditableTextBox",recipient);
        store.SaveRecipient("Альфа UI019");store.SaveRecipient("Бета UI019");
        Invoke(main,"RefreshRecipients",false);
        edit.Focus();edit.Text="Альфа";Pump();
        Check(recipient.IsDropDownOpen&&recipient.Items.Cast<string>().Contains("Альфа UI019")&&!recipient.Items.Cast<string>().Contains("Бета UI019"),"typing filters recipient suggestions");
        recipient.SelectedItem="Альфа UI019";Pump();
        Check(!recipient.IsDropDownOpen&&edit.Text=="Альфа UI019","recipient choice stays committed with the popup closed after queued callbacks");
        recipient.IsDropDownOpen=true;Pump();
        Check(recipient.Items.Cast<string>().Contains("Бета UI019"),"explicit dropdown open shows full directory despite selected recipient");
        recipient.IsDropDownOpen=false;
        // A selection before the background text refresh must invalidate that refresh.
        edit.Focus();edit.Text="Бета";recipient.SelectedItem="Бета UI019";Pump();
        Check(!recipient.IsDropDownOpen&&edit.Text=="Бета UI019","selection cancels a pending typing refresh without reopening and consuming the next click");
        Invoke(main,"ClearIssuanceRecipient");Pump();
        Check(edit.Text==""&&!((Button)main.FindName("IssueButton")).IsEnabled,"new issuance still requires an explicit recipient");

        var journal=(TabItem)main.FindName("JournalTab");journal.IsSelected=true;Pump();
        Invoke(main,"ClearJournalSelection");Pump();
        var merge=(Grid)main.FindName("JournalMergeActions");
        Check(merge.Visibility==Visibility.Collapsed&&merge.ActualHeight==0,"journal merge row reserves no blank height when hidden");
        ((TabControl)main.FindName("WorkspaceTabs")).SelectedIndex=0;Pump();
        var title=(TextBlock)main.FindName("EventTitle");var date=(TextBlock)main.FindName("EventDate");
        var header=(StackPanel)title.Parent;var original=title.Text;
        foreach(var text in new[]{"Коротко",new string('Ш',80)})
        {
            title.Text=text;main.UpdateLayout();
            var titleRect=title.TransformToAncestor(header).TransformBounds(new Rect(title.RenderSize));
            var dateRect=date.TransformToAncestor(header).TransformBounds(new Rect(date.RenderSize));
            Check(Math.Abs(dateRect.Left-titleRect.Right-10)<1,"date follows natural title width on the same line");
        }
        title.Text=original;

        var hall=(HallView)main.FindName("Hall");var seat=store.Seats(show.Id).First(s=>s.Available);
        hall.SetSelection([seat.Id]);hall.Zoom(1.3);Pump();
        var selection=hall.Selected.ToHashSet();var size=hall.RenderSize;
        var zoom=typeof(HallView).GetField("zoom",Private)!.GetValue(hall);
        var offset=typeof(HallView).GetField("offset",Private)!.GetValue(hall);
        var pointer=new Point(Math.Max(310,hall.ActualWidth*.7),40);
        Invoke(hall,"ShowSeatRejection",seat with{TicketId=999,IsPrinted=true},pointer);Pump();
        var popup=(Popup)typeof(HallView).GetField("rejectionPopup",Private)!.GetValue(hall)!;
        var border=(Border)popup.Child;
        Check(popup.IsOpen&&border.BorderBrush==Brushes.Red&&popup.HorizontalOffset+border.DesiredSize.Width<pointer.X,"occupied-seat explanation has a red outline to the left of the mouse");
        Check(hall.RenderSize==size&&hall.Selected.SetEquals(selection)&&Equals(zoom,typeof(HallView).GetField("zoom",Private)!.GetValue(hall))&&Equals(offset,typeof(HallView).GetField("offset",Private)!.GetValue(hall)),"occupied-seat popup preserves hall size zoom offset and selection");
        ((StackPanel)border.Child).Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!popup.IsOpen,"occupied-seat explanation closes with OK");

        var curved=new HallView();
        curved.Load(Enumerable.Range(0,20).Select(i=>seat with{Id=10000+i,Number="",Row="7",Sector="Дуга",X=i*20,Y=(i-10)*(i-10)}).ToArray(),[],true);
        var curvedDrawing=(DrawingGroup)typeof(HallView).GetField("drawing",Private)!.GetValue(curved)!;
        Check(GlyphRuns(curvedDrawing)==1,"a curved logical row draws one row caption despite distinct seat Y coordinates");

        var mixed=new HallView();
        mixed.Load([seat with{Id=20000,X=0,Y=0},seat with{Id=20001,X=20,Y=0,TicketId=999},seat with{Id=20002,X=40,Y=0,IsBlocked=true}],[],true);
        typeof(HallView).GetField("worldAnchor",Private)!.SetValue(mixed,new Point(-1,-1));
        var endpoint=(Point)Invoke(mixed,"Screen",new Point(60,20))!;
        Invoke(mixed,"SelectRange",endpoint);
        var mixedPopup=(Popup)typeof(HallView).GetField("rejectionPopup",Private)!.GetValue(mixed)!;
        Check(!mixedPopup.IsOpen&&mixed.Selected.SetEquals([20000L]),"rectangle skips occupied and withdrawn seats without showing a popup during drag");
        var rejected=(IReadOnlyList<SeatInfo>)Invoke(mixed,"RejectedSeats",endpoint)!;
        Check(rejected.Select(s=>s.Id).SequenceEqual([20001L,20002L]),"finished rectangle identifies occupied and withdrawn seats for one deferred explanation");

        var priceType=typeof(MainWindow).Assembly.GetType("Tickets.App.SeatPriceWindow")!;
        var price=(Window)Activator.CreateInstance(priceType,Private,null,[store,100L,1],null)!;
        try
        {
            price.Show();Pump();var body=(StackPanel)price.Content;
            var amount=body.Children.OfType<TextBox>().Single();
            var buttons=body.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
            Check(price.Width==380&&price.Height==225,"price editor retains existing window dimensions");
            foreach(var input in new[]{"100","incorrect"})
            {
                amount.Text=input;Pump();price.UpdateLayout();
                var bounds=buttons.Select(b=>b.TransformToAncestor(body).TransformBounds(new Rect(b.RenderSize))).ToArray();
                Check(bounds.All(b=>b.Bottom<=body.ActualHeight+1)&&buttons.All(b=>b.Height==34),"price Apply and Cancel fit in the original window including validation feedback");
            }
            amount.Text="100";Pump();
            var amountRect=amount.TransformToAncestor(body).TransformBounds(new Rect(amount.RenderSize));
            var applyRect=buttons[0].TransformToAncestor(body).TransformBounds(new Rect(buttons[0].RenderSize));
            Check(applyRect.Top-amountRect.Bottom<=8,"price actions sit immediately under valid price input");
        }
        finally{price.Close();}
        Invoke(main,"LoadEvent",true);
    }
    private static int GlyphRuns(Drawing drawing)=>drawing is GlyphRunDrawing?1:drawing is DrawingGroup group?group.Children.Sum(GlyphRuns):0;
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private)!.Invoke(target,args);
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
