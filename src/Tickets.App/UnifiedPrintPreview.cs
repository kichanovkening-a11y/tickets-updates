using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;

namespace Tickets.App;

internal sealed class PrintAreaWarning(string message):Exception(message) {}

/// <summary>The visible pages and the submitted paginator always use the same print ticket.</summary>
internal static class UnifiedPrintPreview
{
    internal static bool Show(Window owner,string title,Func<PrintDialog,bool,FixedDocument> build,Action<PrintDialog,FixedDocument> submit,bool landscape=false,FrameworkElement? options=null,Size? preferredMedia=null,Action<Action>? refreshReady=null,Func<PrintDialog,Task<FixedDocument>>? asyncBuild=null)
    {
        var window=new PrintPreviewWindow(owner,"Предварительный просмотр печати · "+title);
        var completed=false;
        var refresh=Attach(window,build,(settings,pages)=>{submit(settings,pages);completed=true;window.Close();},options,landscape:landscape,preferredMedia:preferredMedia,asyncBuild:asyncBuild);
        refreshReady?.Invoke(refresh);
        window.ShowDialog();return completed;
    }

    // Reuse the same controls in ticket issuance so Print never opens another settings window.
    internal static Action Attach(EditorWindow window,Func<PrintDialog,bool,FixedDocument> build,Action<PrintDialog,FixedDocument> submit,FrameworkElement? options=null,FrameworkElement? ticketBlank=null,Func<bool>? warningSuppressed=null,Action? suppressWarning=null,bool landscape=false,Size? preferredMedia=null,Func<PrintDialog,Task<FixedDocument>>? asyncBuild=null)
    {
        var settings=new PrintDialog{PrintQueue=null,PrintTicket=new PrintTicket()};
        settings.PrintTicket.PageOrientation=landscape?PageOrientation.Landscape:PageOrientation.Portrait;
        settings.PrintTicket.PageMediaSize=A4Media();
        var root=new DockPanel{Margin=new Thickness(8)};window.Content=root;
        var toolbar=new WrapPanel{Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
        var ticketMode=ticketBlank is not null;
        var printers=new ComboBox{Width=ticketMode?160:270,DisplayMemberPath="FullName",Margin=new Thickness(4)};
        var paper=new ComboBox{Width=ticketMode?120:170,Margin=new Thickness(4)};
        var orientation=new ComboBox{Width=ticketMode?105:120,Margin=new Thickness(4),ItemsSource=new[]{"Книжная","Альбомная"},SelectedIndex=landscape?1:0};
        var copies=new TextBox{Width=45,Text="1",Margin=new Thickness(4),VerticalContentAlignment=VerticalAlignment.Center};
        void Field(string? text,FrameworkElement field)
        {
            if(!ticketMode){if(text is not null)toolbar.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center});toolbar.Children.Add(field);return;}
            var column=new StackPanel{VerticalAlignment=VerticalAlignment.Bottom};
            column.Children.Add(new TextBlock{Text=text??"",HorizontalAlignment=HorizontalAlignment.Center});column.Children.Add(field);toolbar.Children.Add(column);
            field.Height=44;field.MinHeight=44;field.VerticalAlignment=VerticalAlignment.Bottom;
        }
        Field("Принтер",printers);Field("Бумага",paper);Field(null,orientation);Field("Копий",copies);
        if(ticketBlank is not null)Field("Бланк",ticketBlank);
        var print=new Button{Content=ticketMode?"Печать":"Печатать",Padding=new Thickness(6,2,6,2),Margin=new Thickness(4),VerticalAlignment=VerticalAlignment.Bottom};toolbar.Children.Add(print);
        if(ticketMode){print.Width=86;print.Height=44;print.Background=(Brush)new BrushConverter().ConvertFromString("#1B7A43")!;print.Foreground=Brushes.White;print.FontWeight=FontWeights.Bold;print.FontSize=16.9;print.ContentTemplate=null;}
        if(ticketMode)
        {
            // The selected item uses a short caption; popup items retain the full queue name.
            printers.DisplayMemberPath="";
            var factory=new FrameworkElementFactory(typeof(TextBlock));
            var caption=new MultiBinding{Converter=new PrinterCaption025()};caption.Bindings.Add(new Binding("FullName"));caption.Bindings.Add(new Binding("IsDropDownOpen"){Source=printers});factory.SetBinding(TextBlock.TextProperty,caption);
            printers.ItemTemplate=new DataTemplate{VisualTree=factory};
        }
        if(options is not null){DockPanel.SetDock(options,Dock.Top);root.Children.Add(options);}
        var summary=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};DockPanel.SetDock(summary,Dock.Bottom);root.Children.Add(summary);
        // DocumentViewer's built-in print command opens the unsupported native dialog.
        // Page views keep all printing within this application's settings and confirmation.
        var pagesPanel=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
        root.Children.Add(new ScrollViewer{Content=pagesPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
        FixedDocument? pages=null;var ready=false;
        var generation=0;var closed=false;var printing=false;
        void ReleasePages(){pagesPanel.Children.Clear();(pages?.Tag as IDisposable)?.Dispose();pages=null;}
        window.Closed+=(_,_)=>{closed=true;generation++;ReleasePages();};
        void ReadSettings()
        {
            if(!int.TryParse(copies.Text,out var count)||count<1||count>999)throw new InvalidOperationException("Укажите количество копий от 1 до 999.");
            settings.PrintTicket.CopyCount=count;
            settings.PrintTicket.PageOrientation=orientation.SelectedIndex==1?PageOrientation.Landscape:PageOrientation.Portrait;
            if(paper.SelectedItem is PaperChoice choice)settings.PrintTicket.PageMediaSize=choice.Size;
        }
        void Rebuild()
        {
            ReadSettings();pages=build(settings,true);pages.DocumentPaginator.ComputePageCount();pagesPanel.Children.Clear();
            for(var i=0;i<pages.Pages.Count;i++)pagesPanel.Children.Add(new System.Windows.Controls.Primitives.DocumentPageView{DocumentPaginator=pages.DocumentPaginator,PageNumber=i,Margin=new Thickness(0,0,0,12)});
            var size=PageSize(settings);var warning="";
            try{if(warningSuppressed?.Invoke()!=true)build(settings,false);}catch(PrintAreaWarning ex){warning=" · Внимание: "+ex.Message;}
            summary.Text=$"{settings.PrintQueue?.FullName??"Принтер не выбран"} · {size.Width*25.4/96:0.#} × {size.Height*25.4/96:0.#} мм · {orientation.SelectedItem} · Копий: {settings.PrintTicket.CopyCount} · Страниц: {pages.Pages.Count}"+warning;
        }
        void Safe(Action action){try{action();}catch(Exception ex){ReleasePages();print.IsEnabled=false;summary.Text=ex.Message;MessageBox.Show(window.DialogOwnerForHost,ex.Message,"Параметры печати",MessageBoxButton.OK,MessageBoxImage.Warning);return;}print.IsEnabled=pages is not null&&settings.PrintQueue is not null;}
        async Task<bool> RebuildAsync(bool showError=false)
        {
            if(closed)return false;
            var requested=++generation;print.IsEnabled=false;ReleasePages();summary.Text="Подготовка страниц Word…";
            try
            {
                ReadSettings();var prepared=await asyncBuild!(settings);
                if(closed||requested!=generation){(prepared.Tag as IDisposable)?.Dispose();return false;}
                pages=prepared;pages.DocumentPaginator.ComputePageCount();
                for(var i=0;i<pages.Pages.Count;i++)pagesPanel.Children.Add(new System.Windows.Controls.Primitives.DocumentPageView{DocumentPaginator=pages.DocumentPaginator,PageNumber=i,Margin=new Thickness(0,0,0,12)});
                var size=PageSize(settings);
                summary.Text=$"Макет Word · {size.Width*25.4/96:0.#} × {size.Height*25.4/96:0.#} мм · {orientation.SelectedItem} · Копий: {settings.PrintTicket.CopyCount} · Страниц: {pages.Pages.Count}";
                print.IsEnabled=settings.PrintQueue is not null;return true;
            }
            catch(Exception ex)
            {
                if(!closed&&requested==generation){ReleasePages();summary.Text=ex.Message;if(showError)MessageBox.Show(window.DialogOwnerForHost,ex.Message,"Параметры печати",MessageBoxButton.OK,MessageBoxImage.Warning);}
                return false;
            }
        }
        void RequestRebuild(){if(printing)return;if(asyncBuild is null)Safe(Rebuild);else _=RebuildAsync();}
        void LoadPaper()
        {
            var wasReady=ready;ready=false;
            try
            {
            if(printers.SelectedItem is PrintQueue queue){settings.PrintQueue=queue;settings.PrintTicket=queue.DefaultPrintTicket??new PrintTicket();}
            else settings.PrintQueue=null;
            var selected=paper.SelectedItem is PaperChoice previous?previous.Size:preferredMedia is {} initial?new PageMediaSize(initial.Width,initial.Height):null;
            var supported=settings.PrintQueue?.GetPrintCapabilities(settings.PrintTicket).PageMediaSizeCapability;
            paper.Items.Clear();
            if(supported is not null)foreach(var size in supported.Where(p=>p.Width>0&&p.Height>0))paper.Items.Add(new PaperChoice(size));
            if(!paper.Items.OfType<PaperChoice>().Any(p=>p.Size.PageMediaSizeName==PageMediaSizeName.ISOA4))paper.Items.Add(new PaperChoice(A4Media()));
            if(selected is not null&&!paper.Items.OfType<PaperChoice>().Any(p=>Math.Abs((p.Size.Width??0)-(selected.Width??0))<1&&Math.Abs((p.Size.Height??0)-(selected.Height??0))<1))paper.Items.Add(new PaperChoice(selected));
            if(selected is not null)selected=paper.Items.OfType<PaperChoice>().FirstOrDefault(p=>Math.Abs((p.Size.Width??0)-(selected.Width??0))<1&&Math.Abs((p.Size.Height??0)-(selected.Height??0))<1)?.Size??selected;
            var selectedMedia=SelectMedia(paper.Items.OfType<PaperChoice>().Select(p=>p.Size),selected);
            paper.SelectedItem=paper.Items.OfType<PaperChoice>().First(p=>ReferenceEquals(p.Size,selectedMedia));
            }
            finally{ready=wasReady;}
        }
        printers.SelectionChanged+=(_,_)=>{if(ready)Safe(()=>{LoadPaper();RequestRebuild();});};
        paper.SelectionChanged+=(_,_)=>{if(ready)RequestRebuild();};orientation.SelectionChanged+=(_,_)=>{if(ready)RequestRebuild();};
        copies.LostKeyboardFocus+=(_,_)=>{if(ready)RequestRebuild();};
        copies.TextChanged+=(_,_)=>{if(ready&&int.TryParse(copies.Text,out var count)&&count is >=1 and <=999)RequestRebuild();};
        print.Click+=async (_,_)=>
        {
            if(asyncBuild is not null)
            {
                if(printing)return;
                printing=true;toolbar.IsEnabled=false;if(options is not null)options.IsEnabled=false;
                try
                {
                    if(await RebuildAsync(true)&&pages is not null)Safe(()=>{if(settings.PrintQueue is null)throw new InvalidOperationException("Выберите установленный принтер для печати.");submit(settings,pages);});
                }
                finally{printing=false;toolbar.IsEnabled=true;if(options is not null)options.IsEnabled=true;}
                return;
            }
            Safe(()=>{
            if(settings.PrintQueue is null)throw new InvalidOperationException("Выберите установленный принтер для печати.");
            Rebuild();if(pages is null)return;
            try{if(warningSuppressed?.Invoke()!=true)build(settings,false);}catch(PrintAreaWarning ex){if(!ConfirmOverflow(window.DialogOwnerForHost,ex.Message,suppressWarning))return;}
            submit(settings,pages);
            });
        };
        root.CommandBindings.Add(new CommandBinding(ApplicationCommands.Print,(_,e)=>{e.Handled=true;print.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
        try
        {
            using var server=new LocalPrintServer();
            foreach(var queue in server.GetPrintQueues(new[]{EnumeratedPrintQueueTypes.Local,EnumeratedPrintQueueTypes.Connections}))printers.Items.Add(queue);
            string? defaultName=null;
            try{defaultName=LocalPrintServer.GetDefaultPrintQueue()?.FullName;}catch{}
            printers.SelectedItem=printers.Items.OfType<PrintQueue>().FirstOrDefault(p=>p.FullName==defaultName)??printers.Items.OfType<PrintQueue>().FirstOrDefault();
        }
        catch{settings.PrintQueue=null;}
        LoadPaper();ready=true;RequestRebuild();
        return RequestRebuild;
    }
    internal static PageMediaSize A4Media()=>new(PageMediaSizeName.ISOA4,210*96/25.4,297*96/25.4);
    internal static PageMediaSize SelectMedia(IEnumerable<PageMediaSize> supported,PageMediaSize? explicitlySelected=null)
    {
        var media=supported.ToArray();
        if(explicitlySelected is not null)
        {
            var match=media.FirstOrDefault(p=>p.PageMediaSizeName==explicitlySelected.PageMediaSizeName&&Math.Abs((p.Width??0)-(explicitlySelected.Width??0))<1&&Math.Abs((p.Height??0)-(explicitlySelected.Height??0))<1);
            if(match is not null)return match;
        }
        return media.FirstOrDefault(p=>p.PageMediaSizeName==PageMediaSizeName.ISOA4)??A4Media();
    }
    internal static Size PageSize(PrintDialog settings)
    {
        var size=settings.PrintTicket.PageMediaSize;var width=size?.Width??793.7;var height=size?.Height??1122.5;
        var landscape=settings.PrintTicket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape;
        if((landscape&&width<height)||(!landscape&&width>height))(width,height)=(height,width);
        return new Size(width,height);
    }
    private sealed record PaperChoice(PageMediaSize Size)
    {
        public override string ToString()=>$"{Size.PageMediaSizeName} · {Size.Width*25.4/96:0.#} × {Size.Height*25.4/96:0.#} мм";
    }
    private static bool ConfirmOverflow(Window owner,string message,Action? suppressWarning=null)
    {
        var dialog=new PrintPreviewWindow(owner,"Поле за печатной областью",650,280);
        var panel=new StackPanel{Margin=new Thickness(18)};dialog.Content=panel;
        panel.Children.Add(new TextBlock{Text=message+"\nЧасть текста может не попасть на бумагу. Исходные координаты и масштаб будут сохранены.",TextWrapping=TextWrapping.Wrap});
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};panel.Children.Add(actions);
        var remember=new CheckBox{Content="Больше не показывать для этого бланка",Margin=new Thickness(0,10,0,0)};
        if(suppressWarning is not null)panel.Children.Insert(1,remember);
        var proceed=new Button{Content="Всё равно распечатать",Padding=new Thickness(12,8,12,8)};actions.Children.Add(proceed);proceed.Click+=(_,_)=>{if(remember.IsChecked==true)suppressWarning?.Invoke();dialog.Confirm();};
        return dialog.ShowDialog()==true;
    }
}

internal sealed class PrinterCaption025:IMultiValueConverter
{
    public object Convert(object[] value,Type targetType,object parameter,CultureInfo culture)
    {
        var full=value[0] as string??"";
        if(value[1] is true)return full;
        var words=full.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length>2)return string.Join(" ",words.Take(2));
        // ComboBoxItem is present in the popup, absent in the closed selection box.
        return full;
    }
    public object[] ConvertBack(object value,Type[] targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
