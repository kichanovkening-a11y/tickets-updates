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
    private bool Editable=>true;
    public TicketBlankWindow(Store store):base(store,"Бланки печати",1240,940)
    {
        var root=new Grid{Margin=new Thickness(14)};root.ColumnDefinitions.Add(new(){Width=new GridLength(370)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Content=root;
        var form=new StackPanel{Margin=new Thickness(0,0,18,0)};root.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        form.Children.Add(new TextBlock{Text="Сохранённые бланки",FontWeight=FontWeights.Bold});form.Children.Add(templates);
        // A fixed content-sized selector keeps the popup independent of window width.
        templates.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Auto);
        templates.SelectionChanged+=(_,_)=>{if(templates.SelectedItem is TicketBlank b)Load(b);};
        var actions=new WrapPanel();form.Children.Add(actions);
        Button(actions,"Новый А4 · 4 билета",()=>Load(TicketBlankEditing.CreateA4From030879()));
        Button(actions,"Создать редактируемую копию",()=>Try(()=>{if(Editable)Read();Load(TicketBlankEditing.Clone(blank));}));
        var menu=new ContextMenu();var delete=new MenuItem{Header="Удалить"};menu.Items.Add(delete);templates.ContextMenu=menu;
        var style=new Style(typeof(ComboBoxItem));style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty,300d));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Name")));style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,menu));
        style.Setters.Add(new EventSetter(UIElement.PreviewMouseRightButtonDownEvent,new MouseButtonEventHandler((sender,_)=>{if(sender is ComboBoxItem item)templates.SelectedItem=item.Content;})));templates.ItemContainerStyle=style;
        menu.Opened+=(_,_)=>delete.IsEnabled=templates.SelectedItem is TicketBlank;
        delete.Click+=(_,_)=>Try(()=>{if(templates.SelectedItem is not TicketBlank b)return;if(MessageBox.Show(DialogOwner,"Удалить бланк «"+b.Name+"»?","Удаление бланка",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;Store.DeleteTicketBlank(b.Id);Refresh();Load(TicketBlankEditing.CreateA4From030879());});
        form.Children.Add(notice);form.Children.Add(settings);
        AddLabel(settings,"Название шаблона");settings.Children.Add(name);
        var dimensions=new WrapPanel();settings.Children.Add(dimensions);AddLabel(dimensions,"Лист, мм: ширина");width.Width=65;width.IsReadOnly=true;dimensions.Children.Add(width);AddLabel(dimensions," высота");height.Width=65;height.IsReadOnly=true;dimensions.Children.Add(height);
        Button(settings,"Загрузить скан PNG / JPEG",()=>Try(()=>{var dialog=new OpenFileDialog{Filter="Скан бланка|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(DialogOwner)==true){blank.ScanPath=Store.ImportTicketBlankScan(dialog.FileName);Render();}}));
        Button(settings,"Убрать скан",()=>{blank.ScanPath="";printScan.IsChecked=false;Render();});settings.Children.Add(printScan);
        AddLabel(settings,"Билет / секция на листе");settings.Children.Add(blocks);blocks.SelectionChanged+=(_,_)=>{if(!loading)SelectBlock();};
        Button(settings,"Перенести поля 1-го билета на остальные",()=>Try(()=>{Read();TicketBlankEditing.CopyFirstPlacement(blank);SelectBlock();Render();}));
        AddLabel(settings,"Выберите поле или нажмите на текст на листе");settings.Children.Add(fieldChoice);fieldChoice.SelectionChanged+=(_,_)=>{if(!loading)ShowProperties((fieldChoice.SelectedItem as Choice)?.Value);};
        settings.Children.Add(selection);settings.Children.Add(properties);
        Button(settings,"Обновить лист",()=>Try(()=>{Read();Render();}));
        Button(settings,"Сохранить шаблон",()=>Try(()=>{Read();Store.SaveTicketBlank(blank);Refresh(blank.Id);notice.Text="Бланк сохранён и доступен при печати.";}));
        var preview=new StackPanel();preview.Children.Add(new TextBlock{Text="Лист в физических миллиметрах. Перетаскивайте текст; за синюю рамку — весь билет. Скан закреплён и по умолчанию служит только для настройки.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});preview.Children.Add(paper);
        var scroll=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);root.Children.Add(scroll);
        Refresh();Load(templates.SelectedItem as TicketBlank??TicketBlankEditing.CreateA4From030879());
    }
    private static void AddLabel(Panel panel,string text)=>panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,4,3)});
    private void Property(object model,string label,string path)=>Property(properties,model,label,path);
    private void Property(Panel panel,object model,string label,string path)
    {
        AddLabel(panel,label);var box=new TextBox{MinWidth=100,HorizontalAlignment=HorizontalAlignment.Stretch};
        box.SetBinding(TextBox.TextProperty,new Binding(path){Source=model,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.LostFocus,ValidatesOnExceptions=true});box.LostFocus+=(_,_)=>{if(!draggingField&&!HasErrors(properties))Try(()=>{blank.Validate();Render();});};panel.Children.Add(box);
    }
    private void PreciseProperties(object model)
    {
        var geometry=new StackPanel();
        Property(geometry,model,"Слева (X), мм","X");Property(geometry,model,"Сверху (Y), мм","Y");Property(geometry,model,"Ширина, мм","Width");Property(geometry,model,"Высота, мм","Height");
        properties.Children.Add(new Expander{Header="Точная настройка",IsExpanded=false,Content=geometry,Margin=new Thickness(0,8,0,0)});
    }
    private void ShowProperties(object? model)
    {
        properties.Children.Clear();selection.Text=model is null?"Выберите поле на листе или в списке, чтобы настроить текст.":"Свойства выбранного элемента";
        if(model is null)return;
        if(model is LegacyTicketBand band){selection.Text="Секция: "+band.Name;AddLabel(properties,"Перемещайте секцию за синюю рамку. Исходные секции и их высота сохраняются.");return;}
        if(model is TicketBlankPosition){selection.Text="Билет целиком · перемещайте за синюю рамку";PreciseProperties(model);return;}
        Property(model,"Семейство шрифта","FontFamily");Property(model,"Размер шрифта, пункты","FontSize");Property(model,"Поворот, градусы (0, 90, 180, 270)","Rotation");
        if(model is TicketBlankField field){selection.Text="Поле: "+field.DisplayName;var check=new CheckBox{Content="Печатать поле"};check.SetBinding(CheckBox.IsCheckedProperty,new Binding("Enabled"){Source=field,Mode=BindingMode.TwoWay});check.Click+=(_,_)=>Render();properties.Children.Add(check);}
        if(model is LegacyTicketElement element){selection.Text="Поле: "+ElementLabel(element);
            AddLabel(properties,"Содержимое");var bindings=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Value",ItemsSource=new[]{new Choice("Постоянный текст","")}.Concat(LegacyTicketTemplates.Bindings.Select(b=>new Choice(TicketBlankEditing.BindingName(b),b))).ToArray(),SelectedValue=element.Binding};
            bindings.SelectionChanged+=(_,_)=>{if(bindings.SelectedValue is string value){element.Binding=value;ShowProperties(element);Render();}};properties.Children.Add(bindings);
            if(element.Binding.Length==0)Property(model,"Постоянный текст","Text");
            var enabled=new CheckBox{Content="Печатать поле",IsChecked=!element.Suppressed};enabled.Click+=(_,_)=>Try(()=>
            {
                var previous=element.Suppressed;element.Suppressed=enabled.IsChecked!=true;
                try{blank.Validate();Render();}
                catch{element.Suppressed=previous;enabled.IsChecked=!previous;throw;}
            });properties.Children.Add(enabled);
            foreach(var (label,path) in new[]{("Полужирный","Bold"),("Курсив","Italic"),("Подчёркнутый","Underline")}){var check=new CheckBox{Content=label};check.SetBinding(CheckBox.IsCheckedProperty,new Binding(path){Source=element,Mode=BindingMode.TwoWay});check.Click+=(_,_)=>Render();properties.Children.Add(check);}}
        PreciseProperties(model);
    }
    private static string ElementLabel(LegacyTicketElement element)=>TicketBlankEditing.BindingName(element.Binding)+(element.Binding.Length==0?" · "+element.Text:" · "+element.Name);
    private void SelectBlock(bool render=true,bool showBlockProperties=true)
    {
        loading=true;var model=(blocks.SelectedItem as Choice)?.Value;fieldChoice.ItemsSource=model switch
        {
            TicketBlankPosition p when blank.Legacy is not null=>TicketBlankEditing.LayoutFor(blank,p).Bands.Where(b=>!b.Suppressed).SelectMany(b=>b.Elements).Select(f=>new Choice(ElementLabel(f),f)).ToArray(),
            TicketBlankPosition p=>TicketBlankEditing.FieldsFor(blank,p).Select(f=>new Choice(f.DisplayName,f)).ToArray(),
            LegacyTicketBand b=>b.Elements.Select(f=>new Choice(ElementLabel(f),f)).ToArray(),
            _=>Array.Empty<Choice>()
        };fieldChoice.SelectedIndex=-1;loading=false;ShowProperties(showBlockProperties?model:null);if(render)Render();
    }
    private void Read()
    {
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
        TicketBlankEditing.EnsureFourUp(blank);
        TicketBlankEditing.EnsureIndependentLayouts(blank);
        if(blank.Legacy is null)foreach(var p in blank.Positions)p.Fields??=blank.Fields.Select(f=>f with{}).ToList();
        settings.IsEnabled=Editable;notice.Text="А4 · 210 × 297 мм · 4 билета. "+(blank.Legacy is {} legacy?"Исходный RPT: "+legacy.SourceFile+". Координаты, шрифты, повороты и секции сохранены. Все поля и постоянные подписи можно редактировать отдельно для каждого билета.":"А4: 210 × 297 мм. Четыре билета можно настраивать отдельно.");
        name.Text=blank.Name;width.Text=blank.PageWidth.ToString(CultureInfo.InvariantCulture);height.Text=blank.PageHeight.ToString(CultureInfo.InvariantCulture);printScan.IsChecked=blank.PrintBackground;
        loading=true;blocks.ItemsSource=blank.Positions.Select((p,i)=>new Choice("Билет "+(i+1),p)).ToArray();blocks.SelectedIndex=0;loading=false;SelectBlock(showBlockProperties:false);
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
            var placements=FourUpTicketLayouts.Paginate(blank,4)[0].Elements;
            for(var index=0;index<4;index++)
            {
                var position=blank.Positions[index];
                Outline(position,position.X,position.Y,position.Width,position.Height,(dx,dy)=>TicketBlankEditing.MovePosition(blank,position,position.X+dx,position.Y+dy));
                foreach(var placement in placements.Where(p=>p.TicketIndex==index))
                {
                    var f=placement.Element;var source=placement.SourceElement??f;
                    var text=f.Binding.Length==0?f.Text:TicketPrintWindow.LegacySample(f.Binding,index+1);
                    var visual=TicketPrintWindow.LegacyElementVisual(f,text,Scale);
                    Canvas.SetLeft(visual,placement.X*Scale);Canvas.SetTop(visual,placement.Y*Scale);paper.Children.Add(visual);
                    Drag(visual,position,source,(dx,dy)=>
                    {
                        var scale=placement.SourceScale;
                        var sourceDx=placement.SourceQuarterTurn?-dy/scale:dx/scale;
                        var sourceDy=placement.SourceQuarterTurn?dx/scale:dy/scale;
                        source.X=Math.Max(0,source.X+sourceDx);source.Y=Math.Max(0,source.Y+sourceDy);
                    });
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
