using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Tickets.Core;

namespace Tickets.App;

internal sealed class HallEditorWindow : EditorWindow
{
    public long CreatedId{get;private set;}
    private readonly ObservableCollection<DraftSeat> seats=[];
    private readonly HallView preview=new();
    private readonly DataGrid table=new(){IsReadOnly=false,AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Extended,CanUserAddRows=false,CanUserDeleteRows=false};
    private readonly ObservableCollection<HallRowLayout> rowLayouts=[];
    private readonly DataGrid rowTable=new(){IsReadOnly=false,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,Height=210,Margin=new Thickness(0,10,0,0)};
    private readonly TabControl tabs=new();
    private readonly TextBlock summary=new(){Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap};
    private readonly Dictionary<int,HallRowLayout> spareRows=[];
    private TextBox? countInput,firstLabelInput;
    private sealed record HallState(List<DraftSeat> Seats,List<HallRowLayout> Rows,int[] Selected,Dictionary<int,HallRowLayout> Spare,string CountText,string FirstLabel);
    private static HallRowLayout CloneRow(HallRowLayout row)=>new(){Label=row.Label,SeatCount=row.SeatCount,AisleAfter=row.AisleAfter};
    private readonly Stack<HallState> history=[];
    private Button? undo;
    private HallState? pendingTableEdit;
    private bool restoring;
    private static DraftSeat Clone(DraftSeat s)=>new(){Sector=s.Sector,Row=s.Row,Number=s.Number,X=s.X,Y=s.Y};
    private HallState Capture()=>new(seats.Select(Clone).ToList(),rowLayouts.Select(CloneRow).ToList(),Selection().Select(s=>seats.IndexOf(s)).ToArray(),spareRows.ToDictionary(p=>p.Key,p=>CloneRow(p.Value)),countInput?.Text??rowLayouts.Count.ToString(),firstLabelInput?.Text??"1");
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
            if(countInput is not null)countInput.Text=state.CountText;if(firstLabelInput is not null)firstLabelInput.Text=state.FirstLabel;
            Refresh(false);table.SelectedItems.Clear();
            foreach(var index in state.Selected.Where(i=>i>=0&&i<seats.Count))table.SelectedItems.Add(seats[index]);
            preview.SetSelection(state.Selected.Select(i=>(long)i+1));
        }
        finally{restoring=false;if(undo is not null)undo.IsEnabled=history.Count>0;}
    }
    public HallEditorWindow(Store store,VenueInfo? copy=null):base(store,"Создание зала")
    {
        if(copy is not null) foreach(var s in store.HallDraft(copy.Id)) seats.Add(s);
        var grid=new Grid{Margin=new Thickness(16)}; grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)}); grid.ColumnDefinitions.Add(new ColumnDefinition()); Content=grid;
        var form=new StackPanel{Margin=new Thickness(0,0,15,0)};
        grid.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var name=Field(form,"Название зала",copy is null?"":copy.Name+" (копия)");
        var address=Field(form,"Адрес",copy?.Address??"");
        var sector=Field(form,"Сектор","Партер");
        var rows=Field(form,"Количество рядов","10"); var places=Field(form,"Мест в каждом ряду","20");
        var firstRow=Field(form,"Первый ряд: число / латиница / кириллица","1"); var firstSeat=Field(form,"Первое место: число / латиница / кириллица","1");
        countInput=rows;firstLabelInput=firstRow;
        HallState? countEdit=null,labelEdit=null;
        rows.GotKeyboardFocus+=(_,_)=>{countEdit=Capture();};
        rows.LostKeyboardFocus+=(_,_)=>{if(countEdit is not null&&countEdit.CountText!=rows.Text)Remember(countEdit);countEdit=null;};
        firstRow.GotKeyboardFocus+=(_,_)=>{labelEdit=Capture();};
        firstRow.LostKeyboardFocus+=(_,_)=>{if(labelEdit is not null&&labelEdit.FirstLabel!=firstRow.Text)Remember(labelEdit);labelEdit=null;};
        var reverse=new CheckBox{Content="Нумерация справа налево",Margin=new Thickness(0,10,0,0)}; form.Children.Add(reverse);
        var aisle=Field(form,"Проход после места по порядку (0 — нет)","0");
        form.Children.Add(new TextBlock{Text="Настройка каждого ряда: подпись, мест, проход",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
        foreach(var (header,path,width) in new[]{("Ряд","Label",65),("Мест","SeatCount",65),("Проход","AisleAfter",75)})
            rowTable.Columns.Add(new DataGridTextColumn{Header=header,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=width});
        rowTable.ItemsSource=rowLayouts;form.Children.Add(rowTable);
        void SyncRows(bool relabel=false)
        {
            if(!int.TryParse(rows.Text,out var count)||count is <1 or >200)return;
            if(!rowTable.CommitEdit(DataGridEditingUnit.Cell,true)||!rowTable.CommitEdit(DataGridEditingUnit.Row,true))return;
            try
            {
                var labels=Enumerable.Range(0,count).Select(i=>HallLayout.Label(firstRow.Text,i)).ToArray();
                while(rowLayouts.Count>count){var index=rowLayouts.Count-1;spareRows[index]=CloneRow(rowLayouts[index]);rowLayouts.RemoveAt(index);}
                while(rowLayouts.Count<count)rowLayouts.Add(spareRows.TryGetValue(rowLayouts.Count,out var cached)?CloneRow(cached):new HallRowLayout{Label=labels[rowLayouts.Count],SeatCount=int.TryParse(places.Text,out var n)&&n is >=1 and <=300?n:20,AisleAfter=int.TryParse(aisle.Text,out var g)&&g>=0?g:0});
                if(relabel){for(var i=0;i<count;i++)rowLayouts[i].Label=labels[i];foreach(var pair in spareRows)pair.Value.Label=HallLayout.Label(firstRow.Text,pair.Key);}
                rowTable.Items.Refresh();
            }
            catch(TicketException) { /* An incomplete first label is validated when adding the sector. */ }
        }
        rows.TextChanged+=(_,_)=>{if(!restoring)SyncRows();};firstRow.TextChanged+=(_,_)=>{if(!restoring)SyncRows(true);};
        Button(form,"Применить мест и проход ко всем рядам",()=>Try(()=>{
            if(!CommitEdits())return;
            var count=Number(places,1,300);var gap=Number(aisle,0,count);
            Remember();foreach(var row in rowLayouts){row.SeatCount=count;row.AisleAfter=gap;}
            rowTable.Items.Refresh();
        }));
        SyncRows();
        Button(form,"Добавить сектор",()=>Try(()=>{
            if(!CommitEdits())return;
            var count=Number(rows,1,200);SyncRows();
            HallLayout.Label(firstRow.Text,0);
            if(rowLayouts.Count!=count)throw new TicketException("Проверьте настройку рядов.");
            var y=seats.Count==0?26:seats.Max(s=>s.Y)+80;
            var added=HallLayout.Generate(sector.Text,rowLayouts.ToArray(),firstSeat.Text,reverse.IsChecked==true,y);
            if(seats.Count+added.Count>50000)throw new TicketException("Максимум 50 000 мест в одном зале.");
            Remember();foreach(var seat in added)seats.Add(seat);
            Refresh();
        }));
        var dx=Field(form,"Сдвиг выбранных по X","0"); var dy=Field(form,"Сдвиг выбранных по Y","0");
        Button(form,"Сдвинуть выбранные места",()=>Try(()=>{
            if(!double.TryParse(dx.Text,out var x)||!double.TryParse(dy.Text,out var y)||!double.IsFinite(x)||!double.IsFinite(y)) throw new TicketException("Введите числовой сдвиг.");
            var selected=Selection(); if(selected.Count==0) throw new TicketException("Выберите места на схеме или в таблице.");
            Remember();foreach(var s in selected){s.X+=x;s.Y+=y;} Refresh();
        }));
        Button(form,"Удалить выбранные места",()=>Try(DeleteSelected));
        undo=Button(form,"Шаг назад",()=>Try(Undo));undo.IsEnabled=false;
        grid.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBox){Try(Undo);e.Handled=true;}};
        Button(form,"Сохранить как новый зал",()=>Try(()=>{
            if(!CommitEdits())return;
            CreatedId=store.CreateHall(name.Text,address.Text,seats.ToArray()); Finish(true);
        }));
        form.Children.Add(summary);
        form.Children.Add(new TextBlock{Text="В таблице рядов настройте каждый ряд отдельно. Проход — после места по порядку, 0 — нет. На схеме выберите места щелчками или рамкой и нажмите Del: оставшиеся подписи и координаты сохраняются. Ряды, номера и координаты можно менять в таблице «Места». Зал сохраняется новой копией.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
        var area=new DockPanel(); Grid.SetColumn(area,1); grid.Children.Add(area);
        var toolbar=new StackPanel{Orientation=Orientation.Horizontal}; DockPanel.SetDock(toolbar,Dock.Top);area.Children.Add(toolbar);
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
        }
        table.ItemsSource=seats;
        tabs.Items.Add(new TabItem{Header="Схема",Content=preview}); tabs.Items.Add(new TabItem{Header="Места — редактирование",Content=table}); area.Children.Add(tabs);
        OnShown(()=>Refresh());
    }
    private List<DraftSeat> Selection()=>tabs.SelectedIndex==1?table.SelectedItems.Cast<DraftSeat>().ToList():
        preview.Selected.Where(id=>id>0&&id<=seats.Count).Select(id=>seats[(int)id-1]).ToList();
    private void DeleteSelected()
    {
        if(!CommitEdits())return;
        var selected=Selection();
        if(selected.Count==0)return;
        Remember();foreach(var seat in selected)seats.Remove(seat);
        Refresh(false);
    }
    private void Refresh(bool fit=true)
    {
        if(!CommitEdits())return;table.Items.Refresh();
        if(seats.Any(s=>!double.IsFinite(s.X)||!double.IsFinite(s.Y)||string.IsNullOrWhiteSpace(s.Sector)||string.IsNullOrWhiteSpace(s.Row)||string.IsNullOrWhiteSpace(s.Number)))
        {MessageBox.Show(DialogOwner,"Заполните сектор, ряд, место и корректные координаты в таблице.","Зал");return;}
        var view=seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,"#7AA8D9",null,"","",null)).ToList();
        preview.Load(view,seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(x=>x.X),g.Min(x=>x.Y)-26)).ToList(),fit);
        summary.Text=$"Мест: {seats.Count} · Секторов: {seats.Select(s=>s.Sector).Distinct().Count()}";
    }
    private bool CommitEdits()
    {
        if(table.CommitEdit(DataGridEditingUnit.Cell,true)&&table.CommitEdit(DataGridEditingUnit.Row,true)&&
            rowTable.CommitEdit(DataGridEditingUnit.Cell,true)&&rowTable.CommitEdit(DataGridEditingUnit.Row,true))return true;
        MessageBox.Show(DialogOwner,"Исправьте ошибку в таблице: координаты X и Y, количество мест и проход должны быть числами.","Зал");return false;
    }
}

