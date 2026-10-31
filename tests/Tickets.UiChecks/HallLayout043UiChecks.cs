using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallLayout043UiChecks
{
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static readonly List<string> failures=[];
    private static void Check(bool ok,string text)
    {Console.WriteLine((ok?"PASS ":"FAIL ")+"043 "+text);if(!ok)failures.Add(text);}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject node)
    {yield return node;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Visuals(VisualTreeHelper.GetChild(node,i)))yield return child;}
    private static MainWindow Open(Store store)
    {
        var main=new MainWindow(store){WindowState=WindowState.Normal,WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        main.Show();main.SelectEvent(store.Events().First().Id);Pump();return main;
    }
    private static Rect Screen(FrameworkElement element)
    {var start=element.PointToScreen(new Point());var end=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));return new Rect(start,end);}
    private static void CheckSpacing(MainWindow main)
    {
        var banner=(Border)main.FindName("TopBanner043");var box=Screen(banner);
        var buttons=Visuals(banner).OfType<Button>().Select(Screen).ToArray();
        Check(Math.Abs(buttons.Min(b=>b.Top)-box.Top-5)<.6&&Math.Abs(box.Bottom-buttons.Max(b=>b.Bottom)-5)<.6,"navy banner has 5 physical pixels around its button rows");
        Check(Math.Abs(Screen((FrameworkElement)main.FindName("EventTitle")).Top-box.Bottom-5)<.6,"title starts 5 physical pixels below banner");
        var summary=(CompactText043)main.FindName("SelectedSummary");var sidebar=(Border)main.FindName("SelectionSidebar");
        summary.Text="Мест: 104 · Сумма: 104 000 000 000 ₽";Pump();
        Check(Math.Abs(Screen(summary).Top-Screen(sidebar).Top-5)<.6,"selection text starts 5 pixels below sidebar edge");
        var label=(FrameworkElement)main.FindName("RecipientLabel043");
        Check(Math.Abs(Screen(label).Top-Screen(summary).Bottom-3)<.6,"recipient label follows selection text by 3 pixels");
        var list=(ListBox)main.FindName("SelectedPriceSummary");
        ((ComboBox)main.FindName("SelectedViewBox")).SelectedIndex=1;
        list.ItemsSource=new[]{new PriceSummaryRow044("1 000×97 = 97 000",true),new PriceSummaryRow044("500×1 = 500"),new PriceSummaryRow044("Всего: 98 · 97 500",false,true)};Pump();
        var text=Visuals(list).OfType<CompactText043>().ToArray();
        Check(text.Length==3&&Math.Abs(Screen(text[0]).Top-Screen(list).Top-5)<.6,"price summary starts 5 pixels below border");
        if(text.Length==3)
        {
            Check(Math.Abs(Screen(text[1]).Top-Screen(text[0]).Bottom-4)<.6,"price summary rows have a 4 pixel gap");
            Check(Math.Abs(Screen(text[2]).Top-Screen(text[1]).Bottom-10)<.6,"price summary and total have a 10 pixel gap");
            Check(text.All(row=>Math.Abs(Screen(row).Left-Screen(list).Left-5)<.6&&row.TextAlignment==TextAlignment.Left),"all summary rows share the left edge five pixels from the border");
        }
        foreach(var plaque in ((Grid)main.FindName("LegendPanel")).Children.OfType<Border>().Take(1))
        {
            var ink=Screen((FrameworkElement)plaque.Child);var bounds=Screen(plaque);
            Check(Math.Abs(ink.Left-bounds.Left-5)<.6&&Math.Abs(ink.Top-bounds.Top-5)<.6&&Math.Abs(bounds.Bottom-ink.Bottom-5)<.6,"price plaque has 5 pixel text insets");
            Check(Math.Abs(bounds.Left-Screen(sidebar).Left-5)<.6,"price plaques start 5 pixels from sidebar edge");
        }
        foreach(var button in new[]{(Button)main.FindName("PrintTicketsButton"),(Button)main.FindName("IssueButton")})
        {
            var ink=Visuals(button).OfType<CompactText043>().Single();var a=Screen(button);var b=Screen(ink);
            Check(Math.Abs(b.Top-a.Top-10)<.6&&Math.Abs(a.Bottom-b.Bottom-10)<.6&&Math.Abs(b.Left-a.Left-5)<.6&&Math.Abs(a.Right-b.Right-5)<.6,"document button adds 10 pixels in height and keeps centered text and 5 pixel side insets: "+button.Name);
            Check(ink.FontSize==14,"document button font unchanged");
        }
        CheckRasterLines(summary);
    }
    private static void CheckRasterLines(CompactText043 text)
    {
        // Render the actual text drawing at physical resolution and find its red ink.
        var bounds=Screen(text);var sx=bounds.Width/text.ActualWidth;var sy=bounds.Height/text.ActualHeight;
        var width=Math.Max(1,(int)Math.Ceiling(bounds.Width));var height=Math.Max(1,(int)Math.Ceiling(bounds.Height));
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(text),null,new Rect(0,0,text.ActualWidth,text.ActualHeight));
        var bmp=new RenderTargetBitmap(width,height,96*sx,96*sy,PixelFormats.Pbgra32);bmp.Render(visual);
        var data=new byte[width*height*4];bmp.CopyPixels(data,width*4,0);
        var rows=Enumerable.Range(0,height).Where(y=>Enumerable.Range(0,width).Any(x=>{var i=(y*width+x)*4;return data[i+3]>180&&data[i+2]>120&&data[i+1]<100&&data[i]<100;})).ToArray();
        var gaps=rows.Zip(rows.Skip(1),(a,b)=>b-a-1).Where(g=>g>0).ToArray();
        Check(rows.Length>0&&gaps.Length>0&&gaps.All(g=>g is >=2 and <=4),"rendered wrapped selection lines have 3 pixel gaps (one raster pixel tolerance)");
    }
    private static void CheckTextRefresh(MainWindow main)
    {
        var text=(CompactText043)main.FindName("SelectedSummary");text.Text="88";Pump();
        string PaintedText(Drawing? drawing)
        {
            if(drawing is GlyphRunDrawing glyph)return new string(glyph.GlyphRun.Characters.ToArray());
            return drawing is DrawingGroup group?string.Concat(group.Children.Select(PaintedText)):"";
        }
        Check(PaintedText(VisualTreeHelper.GetDrawing(text))=="88","compact summary paints its initial text");
        text.Text="00";Pump();
        Check(PaintedText(VisualTreeHelper.GetDrawing(text))=="00","same-size summary text repaints after selection changes");
    }
    internal static void Run(string seed)
    {
        failures.Clear();var work=Path.Combine(Path.GetTempPath(),"Tickets-layout043-"+Guid.NewGuid());Directory.CreateDirectory(work);
        var db=Path.Combine(work,"tickets.db");File.Copy(seed,db);var store=new Store(db);
        var main=Open(store);
        try
        {
            var root=(FrameworkElement)main.Content;var workspace=(Grid)main.FindName("Workspace");
            var toggle=(Button)main.FindName("ToggleHallButton038");var refresh=(Button)main.FindName("RefreshTicketlandButton029");
            foreach(var scale in new[]{1d,1.25,1.5})
            {
                main.Width=1536;main.Height=880;root.LayoutTransform=new ScaleTransform(scale,scale);Pump();
                if(main.ActualWidth/scale<main.MinWidth||main.ActualHeight/scale<main.MinHeight)continue;
                for(var mode=0;mode<2;mode++)
                {
                    var a=Screen(refresh);var b=Screen(toggle);
                    Check(Math.Abs(a.Top-b.Top)<.6&&Math.Abs(a.Bottom-b.Bottom)<.6&&Math.Abs(b.Left-a.Right-5)<.6,$"icons in one row with 5 physical pixels, scale={scale}, expanded={mode==1}");
                    Check(Math.Abs(a.Width-b.Width)<.6&&Math.Abs(a.Height-b.Height)<.6,$"equal icon bounds: {a.Width}x{a.Height} / {b.Width}x{b.Height}");
                    CheckSpacing(main);
                    HallLayout042UiChecks.CheckLayout(main);
                    if(scale==1&&mode==1)Corrections025UiChecks.Capture(main,"043-selected-compact");
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                }
            }
            root.LayoutTransform=Transform.Identity;Pump();
            CheckTextRefresh(main);
            var splitter=Visuals(workspace).OfType<GridSplitter>().Single();
            workspace.ColumnDefinitions[2].Width=new GridLength(340);Pump();
            splitter.RaiseEvent(new DragCompletedEventArgs(0,0,false){RoutedEvent=Thumb.DragCompletedEvent});Pump();
            var other=store.Events().Skip(1).FirstOrDefault();if(other is not null){main.SelectEvent(other.Id);Pump();}
            Check(Math.Abs(workspace.ColumnDefinitions[2].ActualWidth-340)<1,"manual width survives event switch");
            main.Width=800;main.Height=600;Pump();
            Check(((HallView)main.FindName("Hall")).ActualWidth>=200,"narrow window keeps usable hall width");
            splitter.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});
            splitter.RaiseEvent(new DragDeltaEventArgs(-300,0){RoutedEvent=Thumb.DragDeltaEvent});Pump();
            Check(((HallView)main.FindName("Hall")).ActualWidth>=200,"dragging sidebar in a narrow window cannot cover the hall");
            splitter.RaiseEvent(new DragCompletedEventArgs(-300,0,false){RoutedEvent=Thumb.DragCompletedEvent});Pump();
            Check(((HallView)main.FindName("Hall")).ActualWidth>=200,"completed narrow-window drag preserves hall access");
            for(var i=0;i<40;i++)splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(splitter),0,Key.Left){RoutedEvent=Keyboard.KeyDownEvent});Pump();
            Check(((HallView)main.FindName("Hall")).ActualWidth>=200,"keyboard sidebar resizing also preserves hall access");
            main.Width=1420;main.Height=880;Pump();
            workspace.ColumnDefinitions[2].Width=new GridLength(340);Pump();splitter.RaiseEvent(new DragCompletedEventArgs(0,0,false){RoutedEvent=Thumb.DragCompletedEvent});
            main.Width=800;Pump();main.Width=1420;Pump();
            Check(Math.Abs(workspace.ColumnDefinitions[2].ActualWidth-340)<1,"widening window restores the user's preferred sidebar width");
        }
        finally{main.Close();}
        main=Open(new Store(db));
        try
        {
            var workspace=(Grid)main.FindName("Workspace");
            Check(Math.Abs(workspace.ColumnDefinitions[2].ActualWidth-340)<1,"manual sidebar width survives a new window and Store");
            Corrections025UiChecks.Capture(main,"043-hall-normal");
            ((Button)main.FindName("ToggleHallButton038")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Corrections025UiChecks.Capture(main,"043-hall-expanded");
        }
        finally{main.Close();}
        var preference=Path.Combine(work,"hall-sidebar-width.txt");
        File.WriteAllText(preference,"NaN");main=Open(new Store(db));
        try{Check(Math.Abs(((Grid)main.FindName("Workspace")).ColumnDefinitions[2].ActualWidth-250)<1,"invalid saved width uses safe default");}
        finally{main.Close();}
        File.WriteAllText(preference,"340");main=Open(new Store(db));
        try
        {
            using(var locked=new FileStream(preference,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                var workspace=(Grid)main.FindName("Workspace");workspace.ColumnDefinitions[2].Width=new GridLength(320);Pump();
                Visuals(workspace).OfType<GridSplitter>().Single().RaiseEvent(new DragCompletedEventArgs(0,0,false){RoutedEvent=Thumb.DragCompletedEvent});Pump();
                Check(((TextBlock)main.FindName("StatusText")).Text.Contains("Не удалось сохранить ширину"),"preference write failure is reported without closing the app");
            }
            Check(File.ReadAllText(preference)=="340","failed atomic save preserves last good preference");
        }
        finally{main.Close();}
        if(failures.Count>0)throw new Exception(string.Join("; ",failures));
    }
}
