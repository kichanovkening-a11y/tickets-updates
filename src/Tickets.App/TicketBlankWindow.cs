using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketBlankWindow:EditorWindow
{
    private TicketBlank blank=new();
    private readonly ComboBox templates=new(){DisplayMemberPath="Name",Width=300,HorizontalAlignment=HorizontalAlignment.Left};
    private readonly TextBox name=new(),width=new(),height=new();
    private readonly CheckBox printScan=new(){Content="Печатать скан вместе с текстом",Margin=new Thickness(0,8,0,8)};
    private readonly Canvas paper=new(){Background=Brushes.White};
    private readonly TextBlock notice=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly StackPanel settings=new(),properties=new();
    private readonly ComboBox blocks=new(){DisplayMemberPath="Label"},fieldChoice=new(){DisplayMemberPath="Label"};
    private readonly TextBlock selection=new(){FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap};
    private bool loading;
    private bool draggingField;
    private const double Scale=2.25;
    private sealed record Choice(string Label,object Value);
    private bool Editable=>blank.Legacy is null||blank.IsEditableCopy;
    public TicketBlankWindow(Store store):base(store,"Бланки печати",1240,940)
    {
        var root=new Grid{Margin=new Thickness(14)};root.ColumnDefinitions.Add(new(){Width=new GridLength(370)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Content=root;
        var form=new StackPanel{Margin=new Thickness(0,0,18,0)};root.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        form.Children.Add(new TextBlock{Text="Сохранённые бланки",FontWeight=FontWeights.Bold});form.Children.Add(templates);
        // A fixed content-sized selector keeps the popup independent of window width.
        templates.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Auto);
        templates.SelectionChanged+=(_,_)=>{if(templates.SelectedItem is TicketBlank b)Load(b);};
        var actions=new WrapPanel();form.Children.Add(actions);
        Button(actions,"Новый А4 · 4 билета",()=>Load(TicketBlankEditing.CreateA4()));
        Button(actions,"Создать редактируемую копию",()=>Try(()=>{if(Editable)Read();Load(TicketBlankEditing.Clone(blank));}));
        var menu=new ContextMenu();var delete=new MenuItem{Header="Удалить"};menu.Items.Add(delete);templates.ContextMenu=menu;
        var style=new Style(typeof(ComboBoxItem));style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,menu));
        style.Setters.Add(new EventSetter(UIElement.PreviewMouseRightButtonDownEvent,new MouseButtonEventHandler((sender,_)=>{if(sender is ComboBoxItem item)templates.SelectedItem=item.Content;})));templates.ItemContainerStyle=style;
        menu.Opened+=(_,_)=>delete.IsEnabled=templates.SelectedItem is TicketBlank;
        delete.Click+=(_,_)=>Try(()=>{if(templates.SelectedItem is not TicketBlank b)return;if(MessageBox.Show(DialogOwner,"Удалить бланк «"+b.Name+"»?","Удаление бланка",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;Store.DeleteTicketBlank(b.Id);Refresh();Load(TicketBlankEditing.CreateA4());});
        form.Children.Add(notice);form.Children.Add(settings);
        AddLabel(settings,"Название шаблона");settings.Children.Add(name);
        var dimensions=new WrapPanel();settings.Children.Add(dimensions);AddLabel(dimensions,"Лист, мм: ширина");width.Width=65;dimensions.Children.Add(width);AddLabel(dimensions," высота");height.Width=65;dimensions.Children.Add(height);
        Button(settings,"Загрузить скан PNG / JPEG",()=>Try(()=>{var dialog=new OpenFileDialog{Filter="Скан бланка|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(DialogOwner)==true){blank.ScanPath=Store.ImportTicketBlankScan(dialog.FileName);Render();}}));
        Button(settings,"Убрать скан",()=>{blank.ScanPath="";printScan.IsChecked=false;Render();});settings.Children.Add(printScan);
        AddLabel(settings,"Билет / секция на листе");settings.Children.Add(blocks);blocks.SelectionChanged+=(_,_)=>{if(!loading)SelectBlock();};
        Button(settings,"Перенести поля 1-го билета на остальные",()=>Try(()=>{Read();TicketBlankEditing.CopyFirstPlacement(blank);SelectBlock();Render();}));
        AddLabel(settings,"Выберите поле или нажмите на текст на листе");settings.Children.Add(fieldChoice);fieldChoice.SelectionChanged+=(_,_)=>{if(!loading)ShowProperties((fieldChoice.SelectedItem as Choice)?.Value);};
        settings.Children.Add(selection);settings.Children.Add(properties);
        Button(settings,"Обновить лист",()=>Try(()=>{Read();Render();}));
        Button(settings,"Сохранить шаблон",()=>Try(()=>{Read();Store.SaveTicketBlank(blank);Refresh(blank.Id);MessageBox.Show(DialogOwner,"Бланк сохранён и доступен при печати.","Бланки печати");}));
        var preview=new StackPanel();preview.Children.Add(new TextBlock{Text="Лист в физических миллиметрах. Перетаскивайте текст; за синюю рамку — весь билет. Скан закреплён и по умолчанию служит только для настройки.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});preview.Children.Add(paper);
        var scroll=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);root.Children.Add(scroll);
        Refresh();Load(templates.SelectedItem as TicketBlank??TicketBlankEditing.CreateA4());
    }
    private static void AddLabel(Panel panel,string text)=>panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,4,3)});
    private void Property(object model,string label,string path)
    {
        AddLabel(properties,label);var box=new TextBox{MinWidth=100,HorizontalAlignment=HorizontalAlignment.Stretch};
        box.SetBinding(TextBox.TextProperty,new Binding(path){Source=model,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.LostFocus,ValidatesOnExceptions=true});box.LostFocus+=(_,_)=>{if(!draggingField&&!HasErrors(properties))Try(()=>{blank.Validate();Render();});};properties.Children.Add(box);
    }
    private void ShowProperties(object? model)
    {
        properties.Children.Clear();selection.Text=model is null?"":"Свойства выбранного элемента";
        if(model is null)return;
        if(model is LegacyTicketBand band){selection.Text="Секция: "+band.Name;AddLabel(properties,"Перемещайте секцию за синюю рамку. Исходные секции и их высота сохраняются.");return;}
        Property(model,"Слева (X), мм","X");Property(model,"Сверху (Y), мм","Y");Property(model,"Ширина, мм","Width");Property(model,"Высота, мм","Height");
        if(model is TicketBlankPosition)return;
        if(model is TicketBlankField field){selection.Text="Поле: "+field.DisplayName;var check=new CheckBox{Content="Печатать поле"};check.SetBinding(CheckBox.IsCheckedProperty,new Binding("Enabled"){Source=field,Mode=BindingMode.TwoWay});check.Click+=(_,_)=>Render();properties.Children.Add(check);}
        if(model is LegacyTicketElement element){selection.Text="Поле: "+element.Name+" · "+(element.Binding.Length>0?element.Binding:element.Text);if(element.Binding.Length==0)Property(model,"Текст","Text");}
        Property(model,"Семейство шрифта","FontFamily");Property(model,"Размер шрифта, пункты","FontSize");Property(model,"Поворот, градусы (0, 90, 180, 270)","Rotation");
    }
    private void SelectBlock(bool render=true)
    {
        loading=true;var model=(blocks.SelectedItem as Choice)?.Value;fieldChoice.ItemsSource=model switch
        {
            TicketBlankPosition p=>TicketBlankEditing.FieldsFor(blank,p).Select(f=>new Choice(f.DisplayName,f)).ToArray(),
            LegacyTicketBand b=>b.Elements.Select(f=>new Choice(f.Name+" · "+(f.Binding.Length>0?f.Binding:f.Text),f)).ToArray(),
            _=>Array.Empty<Choice>()
        };fieldChoice.SelectedIndex=-1;loading=false;ShowProperties(model);if(render)Render();
    }
    private void Read()
    {
        if(!Editable)throw new TicketException("Создайте редактируемую копию исходного макета.");
        Keyboard.ClearFocus();if(HasErrors(properties))throw new TicketException("Исправьте числовые значения свойств.");
        static double Parse(string s)=>double.TryParse(s.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var v)?v:throw new TicketException("Размер листа должен быть числом в мм.");
        blank.Name=name.Text.Trim();blank.PageWidth=Parse(width.Text);blank.PageHeight=Parse(height.Text);blank.PrintBackground=printScan.IsChecked==true;
        if(blank.Legacy is {} layout){layout.ContentWidth=blank.PageWidth-layout.LeftMargin-layout.RightMargin;layout.ContentHeight=blank.PageHeight-layout.TopMargin-layout.BottomMargin;}
        blank.Validate();
    }
    private static bool HasErrors(DependencyObject item){if(Validation.GetHasError(item))return true;for(var i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)if(HasErrors(VisualTreeHelper.GetChild(item,i)))return true;return false;}
    private void Refresh(string? id=null){templates.ItemsSource=Store.TicketBlanks();templates.SelectedItem=templates.Items.Cast<TicketBlank>().FirstOrDefault(b=>b.Id==id);}
    private void Load(TicketBlank selected)
    {
        // Work on detached data so unsaved gestures never alter a saved selector item.
        blank=System.Text.Json.JsonSerializer.Deserialize<TicketBlank>(System.Text.Json.JsonSerializer.Serialize(selected))!;
        if(blank.Legacy is null)foreach(var p in blank.Positions)p.Fields??=blank.Fields.Select(f=>f with{}).ToList();
        settings.IsEnabled=Editable;notice.Text=blank.Legacy is {} legacy?"Исходный RPT: "+legacy.SourceFile+". Координаты, шрифты, повороты и секции сохранены. "+(Editable?"Редактируемая копия.":"Для изменений создайте копию."):"А4: 210 × 297 мм. Четыре билета можно настраивать отдельно.";
        name.Text=blank.Name;width.Text=blank.PageWidth.ToString(CultureInfo.InvariantCulture);height.Text=blank.PageHeight.ToString(CultureInfo.InvariantCulture);printScan.IsChecked=blank.PrintBackground;
        loading=true;blocks.ItemsSource=blank.Legacy is {} l?l.Bands.Select(b=>new Choice(b.Name+" ("+b.Kind+")",b)).ToArray():blank.Positions.Select((p,i)=>new Choice("Билет "+(i+1),p)).ToArray();blocks.SelectedIndex=0;loading=false;SelectBlock();
    }
    private void Select(object block,object field)
    {
        loading=true;blocks.SelectedItem=blocks.Items.Cast<Choice>().First(c=>ReferenceEquals(c.Value,block));loading=false;SelectBlock(false);loading=true;fieldChoice.SelectedItem=fieldChoice.Items.Cast<Choice>().First(c=>ReferenceEquals(c.Value,field));loading=false;ShowProperties(field);
    }
    private void Drag(FrameworkElement visual,object block,object? field,Action<double,double> move)
    {
        Point start=default;bool dragging=false;
        visual.Cursor=Editable?Cursors.SizeAll:Cursors.Arrow;
        visual.MouseLeftButtonDown+=(_,e)=>{if(!Editable)return;draggingField=true;Keyboard.ClearFocus();if(field is not null)Select(block,field);else {loading=true;blocks.SelectedItem=blocks.Items.Cast<Choice>().First(c=>ReferenceEquals(c.Value,block));loading=false;SelectBlock(false);}start=e.GetPosition(paper);dragging=true;visual.CaptureMouse();e.Handled=true;};
        visual.MouseMove+=(_,e)=>{if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;var point=e.GetPosition(paper);visual.RenderTransform=new TranslateTransform(point.X-start.X,point.Y-start.Y);};
        visual.LostMouseCapture+=(_,_)=>{if(!dragging)return;dragging=false;draggingField=false;visual.RenderTransform=Transform.Identity;};
        visual.MouseLeftButtonUp+=(_,e)=>{if(!dragging)return;var point=e.GetPosition(paper);dragging=false;draggingField=false;visual.ReleaseMouseCapture();move((point.X-start.X)/Scale,(point.Y-start.Y)/Scale);ShowProperties(field??block);Render();e.Handled=true;};
    }
    private void Outline(object model,double x,double y,double w,double h,Action<double,double> move)
    {
        var border=new Border{Width=Math.Max(1,w*Scale),Height=Math.Max(1,h*Scale),BorderBrush=Brushes.SteelBlue,BorderThickness=new Thickness(2)};Canvas.SetLeft(border,x*Scale);Canvas.SetTop(border,y*Scale);paper.Children.Add(border);Drag(border,model,null,move);
    }
    private void Render()
    {
        paper.Children.Clear();paper.Width=Math.Clamp(blank.PageWidth,20,2000)*Scale;paper.Height=Math.Clamp(blank.PageHeight,20,2000)*Scale;
        if(blank.ScanPath.Length>0){var bitmap=new BitmapImage(new Uri(Store.TicketBlankScanPath(blank.ScanPath)));paper.Children.Add(new Image{Source=bitmap,Width=paper.Width,Height=paper.Height,Stretch=Stretch.Fill,Opacity=.6,IsHitTestVisible=false});}
        if(blank.Legacy is {} legacy)
        {
            var physicalLayout=legacy with{ContentWidth=blank.PageWidth-legacy.LeftMargin-legacy.RightMargin,ContentHeight=blank.PageHeight-legacy.TopMargin-legacy.BottomMargin};
            var placements=LegacyTicketTemplates.Paginate(physicalLayout,1)[0].Elements;
            foreach(var band in legacy.Bands.Where(b=>!b.Suppressed))
            {
                var items=placements.Where(p=>band.Elements.Any(e=>ReferenceEquals(e,p.Element))).ToArray();if(items.Length==0)continue;
                var x=items.Min(p=>p.X);var y=items.Min(p=>p.Y);var right=items.Max(p=>p.X+p.Element.Width);var bottom=items.Max(p=>p.Y+p.Element.Height);
                Outline(band,x,y,right-x,bottom-y,(dx,dy)=>{dx=Math.Max(dx,-band.Elements.Min(e=>e.X));dy=Math.Max(dy,-band.Elements.Min(e=>e.Y));foreach(var e in band.Elements){e.X+=dx;e.Y+=dy;}});
                foreach(var placement in items)
                {
                    var f=placement.Element;var text=f.Binding.Length==0?f.Text:TicketPrintWindow.LegacySample(f.Binding);var visual=TicketPrintWindow.LegacyElementVisual(f,text,Scale);Canvas.SetLeft(visual,placement.X*Scale);Canvas.SetTop(visual,placement.Y*Scale);paper.Children.Add(visual);Drag(visual,band,f,(dx,dy)=>{f.X=Math.Max(0,f.X+dx);f.Y=Math.Max(0,f.Y+dy);});
                }
            }
            return;
        }
        foreach(var p in blank.Positions)
        {
            Outline(p,p.X,p.Y,p.Width,p.Height,(dx,dy)=>TicketBlankEditing.MovePosition(blank,p,p.X+dx,p.Y+dy));
            foreach(var f in TicketBlankEditing.FieldsFor(blank,p).Where(f=>f.Enabled))
            {
                var fit=TicketPrintWindow.BlankFieldVisual(f,TicketPrintWindow.BlankSample(f.Key),Scale);
                var hit=new Border{Child=fit,Background=Brushes.Transparent};Canvas.SetLeft(hit,(p.X+f.X)*Scale);Canvas.SetTop(hit,(p.Y+f.Y)*Scale);paper.Children.Add(hit);Drag(hit,p,f,(dx,dy)=>{f.X=Math.Clamp(f.X+dx,0,Math.Max(0,p.Width-f.Width));f.Y=Math.Clamp(f.Y+dy,0,Math.Max(0,p.Height-f.Height));});
            }
        }
    }
}
