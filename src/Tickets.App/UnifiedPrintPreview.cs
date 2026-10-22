using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Tickets.App;

internal sealed class PrintAreaWarning(string message):Exception(message) {}

/// <summary>The visible pages and the submitted paginator always use the same print ticket.</summary>
internal static class UnifiedPrintPreview
{
    internal static bool Show(Window owner,string title,Func<PrintDialog,bool,FixedDocument> build,Action<PrintDialog,FixedDocument> submit,bool landscape=false,FrameworkElement? options=null,Size? preferredMedia=null)
    {
        var settings=new PrintDialog{PrintQueue=null,PrintTicket=new PrintTicket()};
        settings.PrintTicket.PageOrientation=landscape?PageOrientation.Landscape:PageOrientation.Portrait;
        settings.PrintTicket.PageMediaSize=A4Media();
        var window=new PrintPreviewWindow(owner,"Предварительный просмотр печати · "+title);
        var root=new DockPanel{Margin=new Thickness(8)};window.Content=root;
        var toolbar=new WrapPanel{Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
        var printers=new ComboBox{Width=270,DisplayMemberPath="FullName",Margin=new Thickness(4)};
        var paper=new ComboBox{Width=170,Margin=new Thickness(4)};
        var orientation=new ComboBox{Width=120,Margin=new Thickness(4),ItemsSource=new[]{"Книжная","Альбомная"},SelectedIndex=landscape?1:0};
        var copies=new TextBox{Width=45,Text="1",Margin=new Thickness(4),VerticalContentAlignment=VerticalAlignment.Center};
        void Label(string text)=>toolbar.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center});
        Label("Принтер");toolbar.Children.Add(printers);Label("Бумага");toolbar.Children.Add(paper);toolbar.Children.Add(orientation);Label("Копий");toolbar.Children.Add(copies);
        var apply=new Button{Content="Обновить просмотр",Padding=new Thickness(10,6,10,6),Margin=new Thickness(4)};toolbar.Children.Add(apply);
        var print=new Button{Content="Печатать",Padding=new Thickness(18,6,18,6),Margin=new Thickness(4)};toolbar.Children.Add(print);
        var cancel=new Button{Content="Отмена",Padding=new Thickness(10,6,10,6),Margin=new Thickness(4)};toolbar.Children.Add(cancel);cancel.Click+=(_,_)=>window.Close();
        if(options is not null){DockPanel.SetDock(options,Dock.Top);root.Children.Add(options);}
        var summary=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};DockPanel.SetDock(summary,Dock.Bottom);root.Children.Add(summary);
        // DocumentViewer's built-in print command opens the unsupported native dialog.
        // Page views keep all printing within this application's settings and confirmation.
        var pagesPanel=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
        root.Children.Add(new ScrollViewer{Content=pagesPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
        FixedDocument? pages=null;var completed=false;var ready=false;
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
            try{build(settings,false);}catch(PrintAreaWarning ex){warning=" · Внимание: "+ex.Message;}
            summary.Text=$"{settings.PrintQueue?.FullName??"Принтер не выбран"} · {size.Width*25.4/96:0.#} × {size.Height*25.4/96:0.#} мм · {orientation.SelectedItem} · Копий: {settings.PrintTicket.CopyCount} · Страниц: {pages.Pages.Count}"+warning;
        }
        void Safe(Action action){try{action();}catch(Exception ex){pages=null;pagesPanel.Children.Clear();print.IsEnabled=false;summary.Text=ex.Message;MessageBox.Show(window.DialogOwnerForHost,ex.Message,"Параметры печати",MessageBoxButton.OK,MessageBoxImage.Warning);return;}print.IsEnabled=pages is not null&&settings.PrintQueue is not null;}
        void LoadPaper()
        {
            var wasReady=ready;ready=false;
            try
            {
            if(printers.SelectedItem is PrintQueue queue){settings.PrintQueue=queue;settings.PrintTicket=queue.DefaultPrintTicket??new PrintTicket();}
            else settings.PrintQueue=null;
            var selected=paper.SelectedItem is PaperChoice previous?previous.Size:null;
            var supported=settings.PrintQueue?.GetPrintCapabilities(settings.PrintTicket).PageMediaSizeCapability;
            paper.Items.Clear();
            if(supported is not null)foreach(var size in supported.Where(p=>p.Width>0&&p.Height>0))paper.Items.Add(new PaperChoice(size));
            if(!paper.Items.OfType<PaperChoice>().Any(p=>p.Size.PageMediaSizeName==PageMediaSizeName.ISOA4))paper.Items.Add(new PaperChoice(A4Media()));
            var selectedMedia=SelectMedia(paper.Items.OfType<PaperChoice>().Select(p=>p.Size),selected);
            paper.SelectedItem=paper.Items.OfType<PaperChoice>().First(p=>ReferenceEquals(p.Size,selectedMedia));
            }
            finally{ready=wasReady;}
        }
        printers.SelectionChanged+=(_,_)=>{if(ready)Safe(()=>{LoadPaper();Rebuild();});};
        paper.SelectionChanged+=(_,_)=>{if(ready)Safe(Rebuild);};orientation.SelectionChanged+=(_,_)=>{if(ready)Safe(Rebuild);};
        apply.Click+=(_,_)=>Safe(Rebuild);
        print.Click+=(_,_)=>Safe(()=>
        {
            if(settings.PrintQueue is null)throw new InvalidOperationException("Выберите установленный принтер для печати.");
            Rebuild();if(pages is null)return;
            try{build(settings,false);}catch(PrintAreaWarning ex){if(!ConfirmOverflow(window.DialogOwnerForHost,ex.Message))return;}
            submit(settings,pages);completed=true;window.Close();
        });
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
        LoadPaper();ready=true;Safe(Rebuild);
        window.ShowDialog();return completed;
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
    private static bool ConfirmOverflow(Window owner,string message)
    {
        var dialog=new PrintPreviewWindow(owner,"Поле за печатной областью",650,450);
        var panel=new StackPanel{Margin=new Thickness(18)};dialog.Content=panel;
        panel.Children.Add(new TextBlock{Text=message+"\nЧасть текста может не попасть на бумагу. Исходные координаты и масштаб будут сохранены.",TextWrapping=TextWrapping.Wrap});
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};panel.Children.Add(actions);
        var cancel=new Button{Content="Отмена",Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,0)};actions.Children.Add(cancel);cancel.Click+=(_,_)=>dialog.Close();
        var proceed=new Button{Content="Всё равно распечатать",Padding=new Thickness(12,8,12,8)};actions.Children.Add(proceed);proceed.Click+=(_,_)=>dialog.Confirm();
        return dialog.ShowDialog()==true;
    }
}
