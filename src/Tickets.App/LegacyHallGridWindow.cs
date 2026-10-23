using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Tickets.Core;

namespace Tickets.App;

/// <summary>Opt-in legacy cell classification; returns a draft only after explicit acceptance.</summary>
internal sealed class LegacyHallGridWindow : EditorWindow
{
    public sealed class GridRow
    {
        public int Index{get;}
        private readonly string[] labels;
        public GridRow(int index,string[] labels){Index=index;this.labels=labels;}
        public string this[int column]=>labels[column];
    }
    private LegacyHallGrid draft=new(1,1);
    private readonly Stack<LegacyHallGrid> history=[];
    private readonly DataGrid table=new(){AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,CanUserReorderColumns=false,SelectionUnit=DataGridSelectionUnit.Cell,SelectionMode=DataGridSelectionMode.Extended,EnableColumnVirtualization=true,EnableRowVirtualization=true,RowHeaderWidth=35,ColumnWidth=36};
    private readonly HallView preview=new();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
    private readonly TextBox sector,rows,places,firstRow,firstSeat;
    private readonly CheckBox reverse=new(){Content="Нумерация справа налево",Margin=new Thickness(0,8,0,0)};
    public IReadOnlyList<DraftSeat> Result{get;private set;}=[];
    public LegacyHallGridWindow(Store store,string sectorName):base(store,"Сетка как в старой программе",1200,820)
    {
        var root=new Grid{Margin=new Thickness(16)};root.ColumnDefinitions.Add(new(){Width=new GridLength(265)});root.ColumnDefinitions.Add(new());Content=root;
        var settings=new StackPanel{Margin=new Thickness(0,0,12,0)};root.Children.Add(new ScrollViewer{Content=settings,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        sector=Field(settings,"Сектор",sectorName);rows=Field(settings,"Количество рядов (включая проходы)","1");places=Field(settings,"Ячеек мест в ряду (включая проходы)","1");
        firstRow=Field(settings,"Начало рядов","1");firstSeat=Field(settings,"Начало мест","1");settings.Children.Add(reverse);
        Button(settings,"Создать сетку",()=>Try(()=>{
            var next=new LegacyHallGrid(Number(rows,1,200),Number(places,1,300));history.Push(draft.Clone());draft=next;ShowCells();preview.Load([],[],true);
        }));
        settings.Children.Add(new TextBlock{Text="Выделите ячейки или прямоугольник. Правой кнопкой задайте места, подписи рядов или проходы. Крайние столбцы — подписи, они не становятся местами. Полностью пустые ряды не расходуют номера. Для буквенных ячеек числовое начало означает А.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)});
        Button(settings,"Шаг назад",()=>Try(()=>{if(history.TryPop(out var state)){draft=state;rows.Text=draft.Rows.ToString();places.Text=draft.Places.ToString();ShowCells();preview.Load([],[],true);}}));
        Button(settings,"Применить",()=>Try(()=>Apply()));
        Button(settings,"Принять места",()=>Try(()=>{var compiled=Apply();Result=compiled;Finish(true);}));
        settings.Children.Add(status);
        var area=new Grid();Grid.SetColumn(area,1);root.Children.Add(area);area.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});area.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});area.Children.Add(table);Grid.SetRow(preview,1);area.Children.Add(preview);
        var menu=new ContextMenu();
        foreach(var (caption,kind) in new[]{("Установить места с цифровой нумерацией",LegacyHallCell.NumberSeat),("Установить места с буквенной нумерацией",LegacyHallCell.LetterSeat),("Установить ряд с цифровой нумерацией",LegacyHallCell.NumberRow),("Установить ряд с буквенной нумерацией",LegacyHallCell.LetterRow),("Установить проход",LegacyHallCell.Empty)})
        {
            var item=new MenuItem{Header=caption};item.Click+=(_,_)=>Try(()=>Classify(kind));menu.Items.Add(item);
        }
        table.ContextMenu=menu;
        var scale=1.0;
        void Zoom(double factor)
        {
            scale=Math.Clamp(scale*factor,.25,2);
            table.RowHeight=Math.Max(12,28*scale);table.FontSize=Math.Max(8,14*scale);
            foreach(var column in table.Columns)column.Width=Math.Max(12,36*scale);
            table.ColumnHeaderHeight=Math.Max(18,28*scale);
        }
        var zoomButtons=new WrapPanel();settings.Children.Add(zoomButtons);
        Button(zoomButtons,"−",()=>Zoom(1/1.2));Button(zoomButtons,"+",()=>Zoom(1.2));
        Button(settings,"Вся сетка",()=>{var cell=table.ActualWidth/Math.Max(1,draft.Columns);scale=1;Zoom(Math.Clamp(Math.Min(cell/36,table.ActualHeight/Math.Max(1,draft.Rows)/28),.25,2));});
        table.PreviewMouseWheel+=(_,e)=>{Zoom(e.Delta>0?1.15:1/1.15);e.Handled=true;};
        ShowCells();
    }
    private void Classify(LegacyHallCell kind)
    {
        if(table.SelectedCells.Count==0)return;
        history.Push(draft.Clone());
        foreach(var cell in table.SelectedCells.ToArray())
            if(cell.Item is GridRow row)draft.Set(row.Index,cell.Column.DisplayIndex,kind);
        ShowCells();preview.Load([],[],true);
    }
    private List<DraftSeat> Apply()
    {
        var compiled=draft.Compile(sector.Text,firstRow.Text,firstSeat.Text,reverse.IsChecked==true);
        ShowCells(compiled);
        preview.Load(compiled.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,"#A9C5E4",null,"","",null)).ToArray(),[],true);
        status.Text=$"Предпросмотр: {compiled.Count} мест. «Принять места» добавляет их в редактор зала.";return compiled;
    }
    private void ShowCells(IReadOnlyList<DraftSeat>? compiled=null)
    {
        if(table.Columns.Count!=draft.Columns)
        {
            table.Columns.Clear();
            for(var c=0;c<draft.Columns;c++)table.Columns.Add(new DataGridTextColumn{Header=c==0||c==draft.Columns-1?"Ряд":c.ToString(),Binding=new Binding($"[{c}]"),Width=36});
        }
        var labels=compiled?.ToDictionary(s=>((int)Math.Round(s.Y/22),(int)Math.Round(s.X/20)+1),s=>s.Number);
        var rowLabels=compiled?.GroupBy(s=>(int)Math.Round(s.Y/22)).ToDictionary(g=>g.Key,g=>g.First().Row);
        table.ItemsSource=Enumerable.Range(0,draft.Rows).Select(r=>new GridRow(r,Enumerable.Range(0,draft.Columns).Select(c=>{
            var kind=draft.Get(r,c);
            if(kind is LegacyHallCell.NumberRow or LegacyHallCell.LetterRow)return rowLabels?.GetValueOrDefault(r)??(kind==LegacyHallCell.LetterRow?"А":"Ряд");
            if(kind==LegacyHallCell.Empty)return "";
            return labels?.GetValueOrDefault((r,c))??(kind==LegacyHallCell.LetterSeat?"А":"1");
        }).ToArray())).ToArray();
        if(compiled is null)status.Text="Сетка изменена. Нажмите «Применить» для проверки нумерации.";
    }
}
