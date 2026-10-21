using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class TicketBlankWindow:EditorWindow
{
    private TicketBlank blank=new();
    private readonly List<UIElement> calibrationControls=[];
    private readonly TextBlock legacyNotice=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private UIElement? copyAction;
    private readonly ComboBox templates=new(){DisplayMemberPath="Name",MinWidth=180};
    private readonly TextBox name=new(),width=new(),height=new();
    private readonly CheckBox printScan=new(){Content="Печатать скан вместе с текстом",Margin=new Thickness(0,8,0,8)};
    private readonly DataGrid positions=new(){AutoGenerateColumns=false,IsReadOnly=false,CanUserAddRows=false,CanUserDeleteRows=false,Height=145};
    private readonly DataGrid fields=new(){AutoGenerateColumns=false,IsReadOnly=false,CanUserAddRows=false,CanUserDeleteRows=false,Height=280};
    private readonly Canvas paper=new(){Background=Brushes.White};
    private readonly TextBlock scan=new(){TextWrapping=TextWrapping.Wrap};
    public TicketBlankWindow(Store store):base(store,"Бланки печати",1200,900)
    {
        var root=new Grid{Margin=new Thickness(14)};root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Content=root;
        var form=new StackPanel{Margin=new Thickness(0,0,14,0),MinWidth=430};
        var settingsScroll=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(settingsScroll);
        form.Children.Add(new TextBlock{Text="Сохранённые бланки",FontWeight=FontWeights.Bold});form.Children.Add(templates);
        templates.SelectionChanged+=(_,_)=>{if(templates.SelectedItem is TicketBlank selected)Load(selected);};
        var actions=new WrapPanel();form.Children.Add(actions);
        Button(actions,"Новый",()=>Load(new TicketBlank()));Button(actions,"Копия",()=>Try(()=>{Read();blank.Id=Guid.NewGuid().ToString("N");blank.Name+=" — копия";Load(blank);}));
        var templateMenu=new ContextMenu();var deleteTemplate=new MenuItem{Header="Удалить"};templateMenu.Items.Add(deleteTemplate);templates.ContextMenu=templateMenu;
        var templateItemStyle=new Style(typeof(ComboBoxItem));
        templateItemStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,templateMenu));
        templateItemStyle.Setters.Add(new EventSetter(UIElement.PreviewMouseRightButtonDownEvent,new System.Windows.Input.MouseButtonEventHandler((sender,_)=>{if(sender is ComboBoxItem item)templates.SelectedItem=item.Content;})));
        templates.ItemContainerStyle=templateItemStyle;
        templateMenu.Opened+=(_,_)=>deleteTemplate.IsEnabled=templates.SelectedItem is TicketBlank;
        deleteTemplate.Click+=(_,_)=>Try(()=>
        {
            if(templates.SelectedItem is not TicketBlank selected)return;
            if(MessageBox.Show(DialogOwner,"Удалить бланк «"+selected.Name+"»?","Удаление бланка",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            Store.DeleteTicketBlank(selected.Id);Refresh();Load(new());
        });
        copyAction=actions.Children[1];form.Children.Add(legacyNotice);var settingsStart=form.Children.Count;
        form.Children.Add(new TextBlock{Text="Название"});form.Children.Add(name);
        var dimensions=new WrapPanel();form.Children.Add(dimensions);
        dimensions.Children.Add(new TextBlock{Text="Лист, мм: ширина ",VerticalAlignment=VerticalAlignment.Center});width.Width=75;dimensions.Children.Add(width);dimensions.Children.Add(new TextBlock{Text=" высота ",VerticalAlignment=VerticalAlignment.Center});height.Width=75;dimensions.Children.Add(height);
        Button(form,"Загрузить скан PNG / JPEG",()=>Try(()=>{var dialog=new OpenFileDialog{Filter="Скан бланка|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(DialogOwner)==true){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(dialog.FileName);bitmap.EndInit();blank.ScanPath=Store.ImportTicketBlankScan(dialog.FileName);scan.Text="Скан загружен. Растягивается на весь лист.";Render();}}));
        Button(form,"Убрать скан",()=>{blank.ScanPath="";printScan.IsChecked=false;scan.Text="Скан не выбран";Render();});form.Children.Add(scan);form.Children.Add(printScan);
        form.Children.Add(new TextBlock{Text="Позиции билетов на листе — координаты и размеры в мм",TextWrapping=TextWrapping.Wrap});
        foreach(var pair in new[]{("X","X"),("Y","Y"),("Ширина","Width"),("Высота","Height")})Column(positions,pair.Item1,pair.Item2);form.Children.Add(positions);
        var positionActions=new WrapPanel();form.Children.Add(positionActions);
        Button(positionActions,"Добавить позицию",()=>Try(()=>{Read();var last=blank.Positions[^1];var next=new TicketBlankPosition{X=last.X,Y=last.Y+last.Height,Width=last.Width,Height=last.Height};if(next.Y+next.Height>blank.PageHeight){next.X=last.X+last.Width;next.Y=last.Y;}if(next.X+next.Width>blank.PageWidth||next.Y+next.Height>blank.PageHeight)throw new TicketException("На листе нет места для такой же позиции. Уменьшите размеры существующей позиции или увеличьте лист.");blank.Positions.Add(next);positions.Items.Refresh();Render();}));Button(positionActions,"Убрать позицию",()=>{if(positions.SelectedItem is TicketBlankPosition p&&blank.Positions.Count>1){Commit();blank.Positions.Remove(p);positions.Items.Refresh();Render();}});
        form.Children.Add(new TextBlock{Text="Поля относительно верхнего левого угла каждого билета (мм); шрифт — пункты",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,4)});
        fields.Columns.Add(new DataGridCheckBoxColumn{Header="Печатать",Binding=new Binding("Enabled"){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=80});
        fields.Columns.Add(new DataGridTextColumn{Header="Поле",Binding=new Binding("DisplayName"),IsReadOnly=true,Width=150});
        foreach(var pair in new[]{("X","X"),("Y","Y"),("Шир.","Width"),("Выс.","Height"),("Шрифт","FontSize")})Column(fields,pair.Item1,pair.Item2);form.Children.Add(fields);
        form.Children.Add(new TextBlock{Text="Координаты полей отсчитываются от края каждого билета. Текст, не помещающийся в поле, уменьшается.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
        Button(form,"Обновить предпросмотр",()=>Try(()=>{Read();Render();}));Button(form,"Сохранить бланк",()=>Try(()=>{Read();Store.SaveTicketBlank(blank);Refresh(blank.Id);MessageBox.Show(DialogOwner,"Бланк сохранён и доступен при печати.","Бланки печати");}));
        calibrationControls.AddRange(form.Children.Cast<UIElement>().Skip(settingsStart));
        var preview=new StackPanel();
        var previewScroll=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        Grid.SetColumn(previewScroll,1);root.Children.Add(previewScroll);preview.Children.Add(new TextBlock{Text="Пример размещения. Скан виден для настройки даже без печати фона.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});preview.Children.Add(paper);
        Refresh();Load(templates.SelectedItem as TicketBlank??new());
    }
    private static void Column(DataGrid grid,string label,string path)=>grid.Columns.Add(new DataGridTextColumn{Header=label,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus,ValidatesOnExceptions=true},Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
    private void Commit(){fields.CommitEdit(DataGridEditingUnit.Cell,true);fields.CommitEdit(DataGridEditingUnit.Row,true);positions.CommitEdit(DataGridEditingUnit.Cell,true);positions.CommitEdit(DataGridEditingUnit.Row,true);}
    private void Read()
    {
        if(blank.Legacy is not null)throw new TicketException("Исходный макет доступен для просмотра и печати. Новый редактор макетов пока не включён.");
        Commit();if(HasErrors(fields)||HasErrors(positions))throw new TicketException("Исправьте числовые значения в таблице.");
        static double Parse(string value){if(!double.TryParse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var result))throw new TicketException("Размер листа должен быть числом в мм.");return result;}
        blank.Name=name.Text.Trim();blank.PageWidth=Parse(width.Text);blank.PageHeight=Parse(height.Text);blank.PrintBackground=printScan.IsChecked==true;blank.Validate();
    }
    private static bool HasErrors(DependencyObject item){if(Validation.GetHasError(item))return true;for(var i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)if(HasErrors(VisualTreeHelper.GetChild(item,i)))return true;return false;}
    private void Refresh(string? id=null){templates.ItemsSource=Store.TicketBlanks();templates.SelectedItem=templates.Items.Cast<TicketBlank>().FirstOrDefault(b=>b.Id==id);}
    private void Load(TicketBlank selected){blank=selected;foreach(var control in calibrationControls)control.IsEnabled=blank.Legacy is null;if(copyAction is not null)copyAction.IsEnabled=blank.Legacy is null;legacyNotice.Text=blank.Legacy is {} legacy?$"Исходный макет: {legacy.SourceFile}. Координаты, шрифты, повороты и секции восстановлены из старой программы. Доступны просмотр, печать и удаление; изменение макета будет в новом редакторе. При печати используется выбранная бумага без масштабирования.":"";name.Text=blank.Name;width.Text=blank.PageWidth.ToString(CultureInfo.InvariantCulture);height.Text=blank.PageHeight.ToString(CultureInfo.InvariantCulture);printScan.IsChecked=blank.PrintBackground;scan.Text=blank.ScanPath.Length==0?"Скан не выбран":"Скан загружен. Растягивается на весь лист.";positions.ItemsSource=blank.Positions;fields.ItemsSource=blank.Fields;Render();}
    private void Render()
    {
        paper.Children.Clear();const double scale=2;paper.Width=Math.Clamp(blank.PageWidth,20,2000)*scale;paper.Height=Math.Clamp(blank.PageHeight,20,2000)*scale;
        if(blank.ScanPath.Length>0){var bitmap=new BitmapImage(new Uri(Store.TicketBlankScanPath(blank.ScanPath)));paper.Children.Add(new Image{Source=bitmap,Width=paper.Width,Height=paper.Height,Stretch=Stretch.Fill,Opacity=.6});}
        if(blank.Legacy is {} legacy)
        {
            foreach(var placement in LegacyTicketTemplates.Paginate(legacy,1)[0].Elements)
            {
                var field=placement.Element;var text=field.Binding.Length==0?field.Text:TicketPrintWindow.LegacySample(field.Binding);
                var visual=TicketPrintWindow.LegacyElementVisual(field,text,scale);Canvas.SetLeft(visual,placement.X*scale);Canvas.SetTop(visual,placement.Y*scale);paper.Children.Add(visual);
            }
            return;
        }
        foreach(var p in blank.Positions)
        {
            var box=new Border{Width=p.Width*scale,Height=p.Height*scale,BorderBrush=Brushes.SteelBlue,BorderThickness=new Thickness(1),IsHitTestVisible=false};Canvas.SetLeft(box,p.X*scale);Canvas.SetTop(box,p.Y*scale);paper.Children.Add(box);
            foreach(var f in blank.Fields.Where(f=>f.Enabled)){var label=new TextBlock{Text=TicketPrintWindow.BlankSample(f.Key),FontSize=f.FontSize*96/72*scale/(96/25.4),Foreground=Brushes.Black,TextWrapping=TextWrapping.NoWrap};var fit=new Viewbox{Child=label,Width=f.Width*scale,Height=f.Height*scale,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left};Canvas.SetLeft(fit,(p.X+f.X)*scale);Canvas.SetTop(fit,(p.Y+f.Y)*scale);paper.Children.Add(fit);}
        }
    }
}
