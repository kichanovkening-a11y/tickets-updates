using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Tickets.Core;

namespace Tickets.App;

/// <summary>A bounded scan trial: geometry proposals remain untrusted until individually reviewed.</summary>
internal sealed class ScanReviewWindow021 : EditorWindow
{
    private sealed class Mark : System.ComponentModel.INotifyPropertyChanged
    {
        public string Row{get;set;}="1";
        public string Number{get;set;}="1";
        public double X{get;set;}
        public double Y{get;set;}
        private bool reviewed;
        public bool Reviewed{get=>reviewed;set{reviewed=value;PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(Reviewed)));}}
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private readonly ObservableCollection<Mark> marks=[];
    private readonly Canvas overlay=new();
    private readonly DataGrid table=new(){IsReadOnly=false,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Extended};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};
    private readonly double chairSize;
    public IReadOnlyList<DraftSeat> Result{get;private set;}=[];
    public ScanReviewWindow021(Store store,string path,string sector):base(store,"Проба распознавания скана — проверка каждого места",1250,850)
    {
        // The obsolete reference is explicitly excluded, including accidental reselection in the picker.
        if(System.IO.Path.GetFileName(path).Equals("zal-ruzpesnya.png",StringComparison.OrdinalIgnoreCase))
            throw new TicketException("Этот старый скан исключён из импорта. Выберите новый скан.");
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.DecodePixelWidth=1600;bitmap.EndInit();bitmap.Freeze();
        BitmapSource source=bitmap;
        if((long)source.PixelWidth*source.PixelHeight>4000000)
        {
            var factor=Math.Sqrt(4000000.0/(source.PixelWidth*(double)source.PixelHeight));
            source=new TransformedBitmap(source,new ScaleTransform(factor,factor));
        }
        var gray=new FormatConvertedBitmap(source,PixelFormats.Gray8,null,0);var pixels=new byte[gray.PixelWidth*gray.PixelHeight];gray.CopyPixels(pixels,gray.PixelWidth,0);
        var found=ScanChairRecognition021.Detect(pixels,gray.PixelWidth,gray.PixelHeight);
        chairSize=found.Count==0?16:found.Select(c=>(double)c.Width).Order().ElementAt(found.Count/2);
        var rowCenters=new List<double>();
        foreach(var candidate in found)
        {
            var row=rowCenters.FindIndex(y=>Math.Abs(y-candidate.Y)<=chairSize*.7);
            if(row<0){row=rowCenters.Count;rowCenters.Add(candidate.Y);}
            marks.Add(new Mark{Row=(row+1).ToString(),Number="",X=candidate.X,Y=candidate.Y});
        }
        foreach(var group in marks.GroupBy(m=>m.Row))
        {var number=1;foreach(var mark in group.OrderBy(m=>m.X))mark.Number=(number++).ToString();}
        var layout=new Grid{Margin=new Thickness(12)};layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(390)});Content=layout;
        overlay.Width=source.PixelWidth;overlay.Height=source.PixelHeight;overlay.Background=Brushes.Transparent;
        var picture=new Image{Source=source,Width=source.PixelWidth,Height=source.PixelHeight,IsHitTestVisible=false};
        var imageLayer=new Grid{Width=source.PixelWidth,Height=source.PixelHeight};imageLayer.Children.Add(picture);imageLayer.Children.Add(overlay);
        layout.Children.Add(new Viewbox{Child=imageLayer,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,12,0)});
        var sidebar=new DockPanel();Grid.SetColumn(sidebar,1);layout.Children.Add(sidebar);
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);sidebar.Children.Add(header);
        header.Children.Add(new TextBlock{Text="Найдены похожие контуры кресел. Красные подписи с ? — предположения по расположению, НЕ прочитанные номера. Сверьте сектор, каждый ряд и каждое место со сканом; исправьте номера и отметьте «Проверено». Лишнее удалите. Пропущенное добавьте двойным щелчком по скану.",TextWrapping=TextWrapping.Wrap});
        var sectorBox=Field(header,"Сектор для проверенных мест",sector);
        header.Children.Add(status);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);sidebar.Children.Add(footer);
        var actions=new WrapPanel();footer.Children.Add(actions);
        Button(actions,"Удалить выбранные",()=>{if(!Commit())return;foreach(var mark in table.SelectedItems.Cast<Mark>().ToArray())marks.Remove(mark);Draw();});
        Button(actions,"Отмена",()=>Finish(false));
        var accept=Button(footer,"Принять проверенные места",()=>Try(()=>{
            if(!Commit())return;
            if(marks.Count==0)throw new TicketException("Нет мест для переноса. Добавьте и проверьте места либо отмените пробу.");
            if(marks.Any(m=>!m.Reviewed))throw new TicketException("Каждое место должно быть проверено вручную. Красные предположения переносить нельзя.");
            if(string.IsNullOrWhiteSpace(sectorBox.Text)||marks.Any(m=>string.IsNullOrWhiteSpace(m.Row)||string.IsNullOrWhiteSpace(m.Number)||!double.IsFinite(m.X)||!double.IsFinite(m.Y)||m.X<0||m.Y<0||m.X>overlay.Width||m.Y>overlay.Height))throw new TicketException("Проверьте сектор, подписи и координаты мест в пределах скана.");
            if(marks.GroupBy(m=>(m.Row.Trim().ToUpperInvariant(),m.Number.Trim().ToUpperInvariant())).Any(g=>g.Count()>1))throw new TicketException("В одном ряду повторяется номер места. Исправьте подписи.");
            var minX=marks.Min(m=>m.X);var minY=marks.Min(m=>m.Y);var scale=20/Math.Max(5,chairSize);
            Result=marks.Select(m=>new DraftSeat{Sector=sectorBox.Text.Trim(),Row=m.Row.Trim(),Number=m.Number.Trim(),X=(m.X-minX)*scale,Y=26+(m.Y-minY)*scale}).ToArray();Finish(true);
        }));
        accept.ToolTip="Перенос возможен только после ручной отметки каждого места. Номера не распознаются автоматически.";
        var uncertain=new Style(typeof(DataGridRow));uncertain.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.Firebrick));
        var reviewed=new DataTrigger{Binding=new Binding("Reviewed"),Value=true};reviewed.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.DarkGreen));uncertain.Triggers.Add(reviewed);table.RowStyle=uncertain;
        table.Columns.Add(new DataGridCheckBoxColumn{Header="Проверено",Binding=new Binding("Reviewed"){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=80});
        foreach(var (title,property,width) in new[]{("Ряд ?","Row",65),("Место ?","Number",70),("X","X",65),("Y","Y",65)})table.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(property){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=width});
        table.ItemsSource=marks;sidebar.Children.Add(table);
        table.RowEditEnding+=(_,_)=>Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(Draw));
        overlay.MouseLeftButtonDown+=(_,e)=>{
            if(e.ClickCount!=2){table.SelectedItems.Clear();e.Handled=true;return;}var point=e.GetPosition(overlay);marks.Add(new Mark{X=point.X,Y=point.Y,Row="",Number=""});Draw();e.Handled=true;
        };
        Draw();
    }
    private bool Commit()=>table.CommitEdit(DataGridEditingUnit.Cell,true)&&table.CommitEdit(DataGridEditingUnit.Row,true);
    private void Draw()
    {
        overlay.Children.Clear();
        foreach(var mark in marks)
        {
            if(!double.IsFinite(mark.X)||!double.IsFinite(mark.Y))continue;
            var color=mark.Reviewed?Brushes.DarkGreen:Brushes.Red;
            var box=new Rectangle{Width=chairSize,Height=chairSize,Stroke=color,StrokeThickness=2,Fill=Brushes.Transparent};
            Canvas.SetLeft(box,mark.X-chairSize/2);Canvas.SetTop(box,mark.Y-chairSize/2);overlay.Children.Add(box);
            var label=new TextBlock{Text=$"{mark.Row}/{mark.Number}"+(mark.Reviewed?"":" ?"),Foreground=color,Background=Brushes.White,FontSize=Math.Max(9,chairSize*.6),IsHitTestVisible=false};
            Canvas.SetLeft(label,mark.X-chairSize/2);Canvas.SetTop(label,mark.Y-chairSize);overlay.Children.Add(label);
            box.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==1){table.SelectedItem=mark;table.ScrollIntoView(mark);e.Handled=true;}};
        }
        status.Text=$"Контуров: {marks.Count}. Требуют проверки: {marks.Count(m=>!m.Reviewed)}. Нумерация условная.";
    }
}
