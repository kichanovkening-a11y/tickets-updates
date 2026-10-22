using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class HallEditorWindow : EditorWindow
{
    public long CreatedId{get;private set;}
    private readonly ObservableCollection<DraftSeat> seats=[];
    private readonly HallView preview=new();
    private readonly DataGrid table=new(){IsReadOnly=false,AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Extended,CanUserAddRows=false,CanUserDeleteRows=false};
    private readonly ObservableCollection<HallRowLayout> rowLayouts=[];
    private readonly DataGrid rowTable=new(){IsReadOnly=false,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,Margin=new Thickness(0,10,0,0)};
    private readonly TabControl tabs=new();
    private readonly TextBlock summary=new(){Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap};
    private readonly Dictionary<int,HallRowLayout> spareRows=[];
    private TextBox? countInput,firstLabelInput,placesInput;
    private sealed record HallState(List<DraftSeat> Seats,List<HallRowLayout> Rows,int[] Selected,Dictionary<int,HallRowLayout> Spare,string CountText,string FirstLabel,string PlacesText);
    private static HallRowLayout CloneRow(HallRowLayout row)=>new(){Label=row.Label,SeatCount=row.SeatCount,AisleAfter=row.AisleAfter,RowGapAfter=row.RowGapAfter,FirstSeat=row.FirstSeat};
    private readonly Stack<HallState> history=[];
    private Button? undo;
    private HallState? pendingTableEdit;
    private bool restoring;
    private Action? showLiveRows;
    private bool showingDraft;
    private bool refreshing;
    private static DraftSeat Clone(DraftSeat s)=>new(){Sector=s.Sector,Row=s.Row,Number=s.Number,X=s.X,Y=s.Y};
    private HallState Capture()=>new(seats.Select(Clone).ToList(),rowLayouts.Select(CloneRow).ToList(),Selection().Select(s=>seats.IndexOf(s)).ToArray(),spareRows.ToDictionary(p=>p.Key,p=>CloneRow(p.Value)),countInput?.Text??rowLayouts.Count.ToString(),firstLabelInput?.Text??"1",placesInput?.Text??"1");
    private void Remember(HallState? state=null){history.Push(state??Capture());if(undo is not null)undo.IsEnabled=true;}
    private void Undo()
    {
        table.CancelEdit(DataGridEditingUnit.Cell);table.CancelEdit(DataGridEditingUnit.Row);
        rowTable.CancelEdit(DataGridEditingUnit.Cell);rowTable.CancelEdit(DataGridEditingUnit.Row);pendingTableEdit=null;
        if(!history.TryPop(out var state))return;
        restoring=true;
        try
        {
            seats.Clear();foreach(var seat in state.Seats)seats.Add(Clone(seat));
            rowLayouts.Clear();foreach(var row in state.Rows)rowLayouts.Add(CloneRow(row));
            spareRows.Clear();foreach(var pair in state.Spare)spareRows[pair.Key]=CloneRow(pair.Value);
            if(countInput is not null)countInput.Text=state.CountText;if(firstLabelInput is not null)firstLabelInput.Text=state.FirstLabel;if(placesInput is not null)placesInput.Text=state.PlacesText;
            Refresh(false);table.SelectedItems.Clear();
            foreach(var index in state.Selected.Where(i=>i>=0&&i<seats.Count))table.SelectedItems.Add(seats[index]);
            preview.SetSelection(state.Selected.Select(i=>(long)i+1));
        }
        finally{restoring=false;if(undo is not null)undo.IsEnabled=history.Count>0;}
        if(seats.Count==0)showLiveRows?.Invoke();
    }
    public HallEditorWindow(Store store,VenueInfo? copy=null):base(store,"Создание зала")
    {
        if(copy is not null) foreach(var s in store.HallDraft(copy.Id)) seats.Add(s);
        var grid=new Grid{Margin=new Thickness(16)}; grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(320)}); grid.ColumnDefinitions.Add(new ColumnDefinition()); Content=grid;
        var sidebar=new DockPanel{Margin=new Thickness(0,0,12,0)};grid.Children.Add(sidebar);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);sidebar.Children.Add(footer);
        var settings=new TabControl();sidebar.Children.Add(settings);
        var metadata=new StackPanel{Margin=new Thickness(6)};
        settings.Items.Add(new TabItem{Header="Зал",Content=metadata});
        var form=new StackPanel{Margin=new Thickness(6)};
        settings.Items.Add(new TabItem{Header="Сектор",Content=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
        var movement=new StackPanel{Margin=new Thickness(6)};
        settings.Items.Add(new TabItem{Header="Сдвиг",Content=new ScrollViewer{Content=movement,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
        var rowPanel=new DockPanel{Margin=new Thickness(0,0,0,6),Height=170};
        var rowHeader=new StackPanel();form.Children.Add(rowHeader);
        var builtSector=seats.Count>0;
        var name=Field(metadata,"Название зала",copy is null?"":copy.Name+" (копия)");
        var address=Field(metadata,"Адрес",copy?.Address??"");
        var sector=Field(form,"Сектор","Партер");form.Children.Remove(rowHeader);form.Children.Add(rowHeader);
        var rows=Field(form,"Количество рядов (1–200)","1"); var places=Field(form,"Мест в каждом ряду (1–300)","1");
        var sizing=new UniformGrid{Columns=2};
        var rowsLabel=form.Children[form.Children.Count-4];var rowsBox=form.Children[form.Children.Count-3];
        var placesLabel=form.Children[form.Children.Count-2];var placesBox=form.Children[form.Children.Count-1];
        form.Children.Remove(rowsLabel);form.Children.Remove(rowsBox);form.Children.Remove(placesLabel);form.Children.Remove(placesBox);
        var rowSizing=new StackPanel{Margin=new Thickness(0,0,8,0)};var seatSizing=new StackPanel();
        rowSizing.Children.Add(rowsLabel);rowSizing.Children.Add(rowsBox);seatSizing.Children.Add(placesLabel);seatSizing.Children.Add(placesBox);
        sizing.Children.Add(rowSizing);sizing.Children.Add(seatSizing);rowHeader.Children.Add(sizing);
        var advanced=new StackPanel{Margin=new Thickness(8,0,8,8)};
        var commonSettings=new Expander{Header="Нумерация и общие настройки",IsExpanded=false,Content=advanced,Margin=new Thickness(0,8,0,0)};rowHeader.Children.Add(commonSettings);
        var firstRow=Field(advanced,"Ряды: начало или А, 1","1"); var firstSeat=Field(advanced,"Места: начало или А, 1","1");
        countInput=rows;firstLabelInput=firstRow;placesInput=places;
        HallState? countEdit=null,labelEdit=null;
        rows.GotKeyboardFocus+=(_,_)=>{countEdit=Capture();};
        rows.LostKeyboardFocus+=(_,_)=>{if(countEdit is not null&&countEdit.CountText!=rows.Text)Remember(countEdit);countEdit=null;};
        firstRow.GotKeyboardFocus+=(_,_)=>{labelEdit=Capture();};
        firstRow.LostKeyboardFocus+=(_,_)=>{if(labelEdit is not null&&labelEdit.FirstLabel!=firstRow.Text)Remember(labelEdit);labelEdit=null;};
        var reverse=new CheckBox{Content="Нумерация справа налево",Margin=new Thickness(0,10,0,0)}; advanced.Children.Add(reverse);
        var aisle=Field(advanced,"Общий проход после места по порядку (0 — нет)","0");
        var rowGap=Field(advanced,"Проход после каждого ряда: дополнительный промежуток (0–1000)","0");
        Button(advanced,"Применить промежуток ко всем рядам",()=>Try(()=>{
            if(!CommitEdits())return;
            if(!double.TryParse(rowGap.Text,out var gap)||!double.IsFinite(gap)||gap<0||gap>1000)throw new TicketException("Промежуток после ряда: от 0 до 1000.");
            Remember();foreach(var row in rowLayouts.Concat(spareRows.Values))row.RowGapAfter=gap;
            RefreshRows();showLiveRows?.Invoke();
        }));
        advanced.Children.Add(new TextBlock{Text="Задайте подпись, число мест и проход для каждого ряда. В колонке «Начало мест» можно указать А, 1 — сначала А, затем 1, 2… В колонке «Проход» выберите пару существующих подписей мест. «Промежуток после ряда» добавляет проход между этим и следующим рядом (0 — нет). Пустое начало мест использует общую настройку выше.",TextWrapping=TextWrapping.Wrap});
        foreach(var (header,path,width) in new[]{("Ряд","Label",85),("Мест","SeatCount",85),("Начало мест","FirstSeat",155),("Промежуток после ряда","RowGapAfter",170)})
            rowTable.Columns.Add(new DataGridTextColumn{Header=header,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=width});
        var aisleChoice=new FrameworkElementFactory(typeof(ComboBox));
        aisleChoice.SetValue(ComboBox.DisplayMemberPathProperty,"Caption");aisleChoice.SetValue(ComboBox.SelectedValuePathProperty,"After");
        var choicesBinding=new MultiBinding{Converter=new RowAisleChoicesConverter()};
        choicesBinding.Bindings.Add(new Binding("SeatCount"));choicesBinding.Bindings.Add(new Binding("FirstSeat"));
        choicesBinding.Bindings.Add(new Binding("AisleAfter"));choicesBinding.Bindings.Add(new Binding("Text"){Source=firstSeat});choicesBinding.Bindings.Add(new Binding("IsChecked"){Source=reverse});
        aisleChoice.SetBinding(ItemsControl.ItemsSourceProperty,choicesBinding);
        aisleChoice.SetBinding(ComboBox.SelectedValueProperty,new Binding("AisleAfter"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
        aisleChoice.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,new SelectionChangedEventHandler((sender,e)=>{
            if(restoring||sender is not ComboBox box||box.DataContext is not HallRowLayout row||e.RemovedItems.Count!=1||e.RemovedItems[0] is not HallLayout.AisleChoice previous||e.AddedItems.Count!=1||e.AddedItems[0] is not HallLayout.AisleChoice next||previous.After==next.After)return;
            var state=Capture();var index=rowLayouts.IndexOf(row);if(index>=0){state.Rows[index].AisleAfter=previous.After;Remember(state);}
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>showLiveRows?.Invoke()));
        }));
        rowTable.Columns.Insert(2,new DataGridTemplateColumn{Header="Проход",CellTemplate=new DataTemplate{VisualTree=aisleChoice},Width=165});
        rowTable.ItemsSource=rowLayouts;rowPanel.Children.Add(rowTable);
        void SyncRows(bool relabel=false)
        {
            if(!int.TryParse(rows.Text,out var count)||count is <1 or >200)return;
            if(!rowTable.CommitEdit(DataGridEditingUnit.Cell,true)||!rowTable.CommitEdit(DataGridEditingUnit.Row,true)||IsEditing(rowTable)||IsEditing(table))return;
            try
            {
                var labels=Enumerable.Range(0,count).Select(i=>HallLayout.Label(firstRow.Text,i)).ToArray();
                while(rowLayouts.Count>count){var index=rowLayouts.Count-1;spareRows[index]=CloneRow(rowLayouts[index]);rowLayouts.RemoveAt(index);}
                while(rowLayouts.Count<count)rowLayouts.Add(spareRows.TryGetValue(rowLayouts.Count,out var cached)?CloneRow(cached):new HallRowLayout{Label=labels[rowLayouts.Count],SeatCount=int.TryParse(places.Text,out var n)&&n is >=1 and <=300?n:1,AisleAfter=int.TryParse(aisle.Text,out var g)&&g>=0?g:0,RowGapAfter=double.TryParse(rowGap.Text,out var rowSpace)&&double.IsFinite(rowSpace)&&rowSpace is >=0 and <=1000?rowSpace:0});
                if(relabel){for(var i=0;i<count;i++)rowLayouts[i].Label=labels[i];foreach(var pair in spareRows)pair.Value.Label=HallLayout.Label(firstRow.Text,pair.Key);}
                RefreshRows();showLiveRows?.Invoke();
            }
            catch(TicketException) { /* An incomplete first label is validated when adding the sector. */ }
        }
        rows.TextChanged+=(_,_)=>{if(!restoring)SyncRows();};firstRow.TextChanged+=(_,_)=>{if(!restoring)SyncRows(true);};
        var previousPlaces=1;
        places.TextChanged+=(_,_)=>{
            if(!int.TryParse(places.Text,out var count)||count is <1 or >300)return;
            var previous=previousPlaces;previousPlaces=count;
            if(restoring||previous==count||!CommitEdits())return;
            var inheritedRows=rowLayouts.Concat(spareRows.Values).Where(row=>row.SeatCount==previous).ToArray();
            if(inheritedRows.Length==0)return;
            Remember(Capture() with{PlacesText=previous.ToString()});
            foreach(var row in inheritedRows)row.SeatCount=count;
            RefreshRows();showLiveRows?.Invoke();
        };
        var applyPlaces=Button(rowHeader,"Применить места ко всем рядам",()=>Try(()=>{
            if(!CommitEdits())return;
            var count=Number(places,1,300);
            Remember();foreach(var row in rowLayouts.Concat(spareRows.Values))row.SeatCount=count;
            RefreshRows();showLiveRows?.Invoke();
        }));
        rowHeader.Children.Remove(applyPlaces);rowHeader.Children.Insert(1,applyPlaces);
        Button(advanced,"Применить общий проход ко всем рядам",()=>Try(()=>{
            if(!CommitEdits())return;
            var gap=Number(aisle,0,300);
            if(rowLayouts.Any(row=>gap>row.SeatCount))throw new TicketException("Проход не может быть дальше последнего места ряда.");
            Remember();foreach(var row in rowLayouts)row.AisleAfter=gap;
            RefreshRows();showLiveRows?.Invoke();
        }));
        showLiveRows=()=>
        {
            if(restoring||seats.Count>0||refreshing)return;
            try
            {
                var draft=HallLayout.Generate(sector.Text,rowLayouts.ToArray(),firstSeat.Text,reverse.IsChecked==true,26);
                showingDraft=true;
                preview.Load(draft.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,"#A9C5E4",null,"","",null)).ToList(),[],true);
                summary.Text=$"Предпросмотр сектора: {draft.Count} мест. «Добавить сектор» закрепляет места.";
            }
            catch(TicketException){summary.Text="Уточните настройки рядов — схема обновится после корректного ввода.";}
        };
        sector.TextChanged+=(_,_)=>showLiveRows();firstSeat.TextChanged+=(_,_)=>showLiveRows();reverse.Checked+=(_,_)=>showLiveRows();reverse.Unchecked+=(_,_)=>showLiveRows();
        SyncRows();
        void AddSector()
        {
            if(!CommitEdits())throw new TicketException("Исправьте настройки рядов.");
            var count=Number(rows,1,200);SyncRows();
            HallLayout.Label(firstRow.Text,0);
            if(rowLayouts.Count!=count)throw new TicketException("Проверьте настройку рядов.");
            var y=seats.Count==0?26:seats.Max(s=>s.Y)+80;
            var added=HallLayout.Generate(sector.Text,rowLayouts.ToArray(),firstSeat.Text,reverse.IsChecked==true,y);
            if(seats.Count+added.Count>50000)throw new TicketException("Максимум 50 000 мест в одном зале.");
            Remember();foreach(var seat in added)seats.Add(seat);
            showingDraft=false;Refresh();tabs.SelectedIndex=0;builtSector=true;
        }
        Button(footer,"Добавить сектор",()=>Try(AddSector));
        var dx=Field(movement,"Сдвиг выбранных по X","0"); var dy=Field(movement,"Сдвиг выбранных по Y","0");
        Button(movement,"Сдвинуть выбранные места",()=>Try(()=>{
            if(!double.TryParse(dx.Text,out var x)||!double.TryParse(dy.Text,out var y)||!double.IsFinite(x)||!double.IsFinite(y)) throw new TicketException("Введите числовой сдвиг.");
            var selected=Selection(); if(selected.Count==0) throw new TicketException("Выберите места на схеме или в таблице.");
            Remember();foreach(var s in selected){s.X+=x;s.Y+=y;} Refresh();
        }));
        Button(movement,"Удалить выбранные места",()=>Try(DeleteSelected));
        undo=Button(movement,"Шаг назад",()=>Try(Undo));undo.IsEnabled=false;
        grid.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBox){Try(Undo);e.Handled=true;}};
        Button(footer,"Сохранить как новый зал",()=>Try(()=>{
            if(!CommitEdits())return;
            if(seats.Count==0&&!builtSector)AddSector();
            CreatedId=store.CreateHall(name.Text,address.Text,seats.ToArray()); Finish(true);
        }));
        footer.Children.Add(summary);
        metadata.Children.Add(new TextBlock{Text="1. Заполните название и настройки сектора.\n2. В таблице над схемой уточните места и проходы для каждого ряда.\n3. Проверьте живую схему и нажмите «Добавить сектор». Если зал ещё пуст, «Сохранить» построит первый сектор автоматически.\n\n«А, 1» означает А, 1, 2, 3… Для буквенной последовательности укажите просто А.",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,12,0,0)});
        var area=new DockPanel(); Grid.SetColumn(area,1); grid.Children.Add(area);
        var toolbar=new WrapPanel(); DockPanel.SetDock(toolbar,Dock.Top);area.Children.Add(toolbar);
        Button(toolbar,"Загрузить скан — проба",()=>Try(()=>{
            if(!CommitEdits())return;
            var picker=new OpenFileDialog{Title="Новый скан зала для пробного распознавания",Filter="Изображения PNG / JPEG|*.png;*.jpg;*.jpeg",CheckFileExists=true};
            if(picker.ShowDialog(DialogOwner)!=true)return;
            var review=new ScanReviewWindow021(store,picker.FileName,sector.Text);
            if(review.ShowSeparateDialog()!=true)return;
            if(seats.Count+review.Result.Count>50000)throw new TicketException("Максимум 50 000 мест в одном зале.");
            var y=seats.Count==0?0:seats.Max(s=>s.Y)+80;
            Remember();foreach(var mark in review.Result){mark.Y+=y;seats.Add(mark);}
            showingDraft=false;builtSector=true;Refresh();tabs.SelectedIndex=0;
        }));
        Button(toolbar,"Сетка как в старой программе",()=>Try(()=>{
            if(!CommitEdits())return;
            var editor=new LegacyHallGridWindow(store,sector.Text);
            if(editor.ShowDialog()!=true)return;
            if(seats.Count+editor.Result.Count>50000)throw new TicketException("Максимум 50 000 мест в одном зале.");
            var y=seats.Count==0?26:seats.Max(s=>s.Y)+80;
            Remember();foreach(var mark in editor.Result){mark.Y+=y;seats.Add(mark);}
            showingDraft=false;builtSector=true;Refresh();tabs.SelectedIndex=0;
        }));
        Button(toolbar,"Весь зал",()=>preview.Fit()); Button(toolbar,"Обновить схему",()=>Try(()=>Refresh()));
        Button(toolbar,"Удалить выбранные (Del)",()=>Try(DeleteSelected));
        preview.KeyDown+=(_,e)=>{if(e.Key==Key.Delete){Try(DeleteSelected);e.Handled=true;}};
        table.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Delete&&Keyboard.FocusedElement is not TextBox){Try(DeleteSelected);e.Handled=true;}};
        foreach(var (header,path,width) in new[]{("Сектор","Sector",150),("Ряд","Row",60),("Место","Number",70),("X","X",80),("Y","Y",80)})
            table.Columns.Add(new DataGridTextColumn{Header=header,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=width});
        foreach(var editable in new[]{table,rowTable})
        {
            editable.BeginningEdit+=(_,_)=>{if(!restoring)pendingTableEdit=Capture();};
            editable.CellEditEnding+=(_,e)=>{
                if(!restoring&&e.EditAction==DataGridEditAction.Commit&&pendingTableEdit is not null)Remember(pendingTableEdit);
                pendingTableEdit=null;
            };
            editable.RowEditEnding+=(_,e)=>{
                if(restoring||e.EditAction!=DataGridEditAction.Commit)return;
                // RowEditEnding precedes the actual commit. Render after binding/row commit;
                // never call Items.Refresh while either grid still owns an edit transaction.
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{
                    if(restoring||refreshing)return;
                    var active=CollectionViewSource.GetDefaultView(editable.ItemsSource) is IEditableCollectionView view&&(view.IsAddingNew||view.IsEditingItem);
                    if(editable==rowTable){if(!active)RefreshRows();showLiveRows?.Invoke();}
                    else Refresh(false,false);
                }));
            };
        }
        table.ItemsSource=seats;
        var liveEditor=new DockPanel();DockPanel.SetDock(rowPanel,Dock.Top);liveEditor.Children.Add(rowPanel);liveEditor.Children.Add(preview);
        tabs.Items.Add(new TabItem{Header="Ряды и схема",Content=liveEditor});tabs.Items.Add(new TabItem{Header="Места — редактирование",Content=table});area.Children.Add(tabs);
        tabs.SelectedIndex=0;
        OnShown(()=>{if(seats.Count==0)showLiveRows();else Refresh();});
    }
    private List<DraftSeat> Selection()=>tabs.SelectedIndex==1?table.SelectedItems.Cast<DraftSeat>().ToList():
        preview.Selected.Where(id=>!showingDraft&&id>0&&id<=seats.Count).Select(id=>seats[(int)id-1]).ToList();
    private void DeleteSelected()
    {
        if(!CommitEdits())return;
        var selected=Selection();
        if(selected.Count==0)return;
        Remember();foreach(var seat in selected)seats.Remove(seat);
        Refresh(false);
    }
    private void Refresh(bool fit=true,bool commit=true)
    {
        if(refreshing)return;
        refreshing=true;
        try
        {
        if(commit)
        {
            if(!CommitEdits())return;
            if(CollectionViewSource.GetDefaultView(table.ItemsSource) is IEditableCollectionView editing&&(editing.IsAddingNew||editing.IsEditingItem))return;
            table.Items.Refresh();
        }
        if(seats.Count==0){refreshing=false;showLiveRows?.Invoke();return;}
        showingDraft=false;
        if(seats.Any(s=>!double.IsFinite(s.X)||!double.IsFinite(s.Y)||string.IsNullOrWhiteSpace(s.Sector)||string.IsNullOrWhiteSpace(s.Row)||string.IsNullOrWhiteSpace(s.Number)))
        {MessageBox.Show(DialogOwner,"Заполните сектор, ряд, место и корректные координаты в таблице.","Зал");return;}
        var view=seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,"#7AA8D9",null,"","",null)).ToList();
        preview.Load(view,seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(x=>x.X),g.Min(x=>x.Y)-26)).ToList(),fit);
        summary.Text=$"Мест: {seats.Count} · Секторов: {seats.Select(s=>s.Sector).Distinct().Count()}";
        }
        finally{refreshing=false;}
    }
    private static bool IsEditing(DataGrid grid)=>grid.ItemsSource is not null&&CollectionViewSource.GetDefaultView(grid.ItemsSource) is IEditableCollectionView view&&(view.IsAddingNew||view.IsEditingItem);
    private void RefreshRows()
    {
        // Focus changes can request a refresh from inside a DataGrid commit callback.
        // A successful CommitEdit call alone does not guarantee both views are idle.
        if(IsEditing(rowTable)||IsEditing(table))return;
        rowTable.Items.Refresh();
    }
    private bool CommitEdits()
    {
        if(table.CommitEdit(DataGridEditingUnit.Cell,true)&&table.CommitEdit(DataGridEditingUnit.Row,true)&&
            rowTable.CommitEdit(DataGridEditingUnit.Cell,true)&&rowTable.CommitEdit(DataGridEditingUnit.Row,true))
        {if(IsEditing(rowTable)||IsEditing(table))return false;return true;}
        MessageBox.Show(DialogOwner,"Исправьте ошибку в таблице: координаты X и Y, количество мест, проход и промежуток после ряда должны быть числами.","Зал");return false;
    }
}


internal sealed class RowAisleChoicesConverter : IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,System.Globalization.CultureInfo culture)
    {
        if(values.Length<5||values[0] is not int count||values[2] is not int aisle)return Array.Empty<HallLayout.AisleChoice>();
        try{return HallLayout.AisleChoices(new HallRowLayout{SeatCount=count,FirstSeat=values[1] as string??"",AisleAfter=aisle},values[3] as string??"1",values[4] is true);}
        catch(TicketException){return Array.Empty<HallLayout.AisleChoice>();}
    }
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}
