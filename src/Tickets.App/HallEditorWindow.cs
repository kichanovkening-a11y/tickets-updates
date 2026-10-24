using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Tickets.Core;

namespace Tickets.App;

internal sealed class HallEditorWindow : EditorWindow
{
    public long CreatedId{get;private set;}
    private readonly ObservableCollection<DraftSeat> seats=[];
    private readonly HallView preview=new(){ShowInactivePlaceholders=false,SectorEditingEnabled=true};
    private readonly DataGrid table=new(){IsReadOnly=false,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false};
    private readonly CheckBox showHidden=new(){Content="Показать скрытые места",Margin=new Thickness(6)};
    private readonly TextBlock summary=new(){Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap};
    private sealed record HallState(List<DraftSeat> Seats,long[] Selected);
    private readonly Stack<HallState> history=[];
    private Button? undo;
    private static DraftSeat Clone(DraftSeat s)=>new(){Sector=s.Sector,Row=s.Row,Number=s.Number,X=s.X,Y=s.Y,Hidden=s.Hidden,RowAisleAnchors=s.RowAisleAnchors?.ToArray(),SectorCaption=s.SectorCaption};
    private void Remember(){history.Push(new(seats.Select(Clone).ToList(),preview.Selected.ToArray()));if(undo is not null)undo.IsEnabled=true;}
    private void Undo()
    {
        if(!history.TryPop(out var state))return;
        seats.Clear();foreach(var seat in state.Seats)seats.Add(Clone(seat));Refresh(false);preview.SetSelection(state.Selected);
        if(undo is not null)undo.IsEnabled=history.Count>0;
    }
    public HallEditorWindow(Store store,VenueInfo? copy=null):this(store,copy,false){}
    public HallEditorWindow(Store store,VenueInfo? copy,bool editExisting):base(store,editExisting?"Редактирование зала":"Создание зала")
    {
        if(editExisting&&copy is null)throw new ArgumentException("Выберите зал.",nameof(copy));
        if(copy is not null)foreach(var seat in store.HallDraft(copy.Id))seats.Add(seat);
        var grid=new Grid{Margin=new Thickness(16)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(320)});grid.ColumnDefinitions.Add(new());Content=grid;
        var sidebar=new StackPanel{Margin=new Thickness(0,0,12,0)};grid.Children.Add(sidebar);
        var name=Field(sidebar,"Название зала",copy is null?"":editExisting?copy.Name:copy.Name+" (копия)");
        var address=Field(sidebar,"Адрес",copy?.Address??"");
        // Building is confined to the two dedicated creation windows.
        void Append(IReadOnlyList<DraftSeat> added,double initialY)
        {
            if(seats.Count+added.Count>50000)throw new TicketException("Максимум 50 000 мест в одном зале.");
            if(added.Any(s=>seats.Any(p=>StringComparer.OrdinalIgnoreCase.Equals(p.Sector,s.Sector))))throw new TicketException("Сектор с таким названием уже есть. Измените название сектора.");
            var y=seats.Count==0?initialY:seats.Max(s=>s.Y)+80;
            Remember();foreach(var seat in added){var draft=Clone(seat);draft.Y+=y;draft.RowAisleAnchors=HallAnchors027.Move(draft.RowAisleAnchors,0,y);seats.Add(draft);}Refresh();
        }
        Button(sidebar,"Добавить сектор",()=>Try(()=>{
            var dialog=new HallSectorWindow023(Store,!editExisting);if(dialog.ShowDialog()!=true)return;Append(dialog.Result,26);
        }));
        undo=Button(sidebar,"Шаг назад",()=>Try(Undo));undo.IsEnabled=false;
        Button(sidebar,editExisting?"Сохранить изменения":"Сохранить как новый зал",()=>Try(()=>{
            if(!CommitEdits())return;
            CreatedId=editExisting?store.SaveEditedHall(copy!.Id,name.Text,address.Text,seats.ToArray()):store.CreateHall(name.Text,address.Text,seats.ToArray());Finish(true);
        }));
        if(editExisting)sidebar.Children.Add(new TextBlock{Text="Названия и положение надписей секторов сохраняются в существующем зале. Изменение мест или схемы требует другого названия и сохраняется как новый зал.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
        sidebar.Children.Add(summary);
        var area=new DockPanel();Grid.SetColumn(area,1);grid.Children.Add(area);
        var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);area.Children.Add(toolbar);
        if(!editExisting)
        {
            Button(toolbar,"Загрузить скан — проба",()=>Try(()=>{
                var picker=new OpenFileDialog{Title="Новый скан зала для пробного распознавания",Filter="Изображения PNG / JPEG|*.png;*.jpg;*.jpeg",CheckFileExists=true};
                if(picker.ShowDialog(DialogOwner)!=true)return;
                var review=new ScanReviewWindow021(store,picker.FileName,"Партер");if(review.ShowSeparateDialog()!=true)return;Append(review.Result,0);
            }));
            Button(toolbar,"Сетка как в старой программе",()=>Try(()=>{
                var editor=new LegacyHallGridWindow(store,"Партер");if(editor.ShowDialog()!=true)return;Append(editor.Result,26);
            }));
        }
        Button(toolbar,"Весь зал",()=>preview.Fit());toolbar.Children.Add(showHidden);
        showHidden.Checked+=(_,_)=>Refresh(false);showHidden.Unchecked+=(_,_)=>Refresh(false);
        area.Children.Add(preview);
        grid.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBox){Try(Undo);e.Handled=true;}};
        void Context(long id,bool rowCaption)=>Try(()=>{
            if(id<1||id>seats.Count)return;var target=seats[(int)id-1];
            if(rowCaption)preview.SetSelection(seats.Select((s,i)=>(s,i)).Where(p=>p.s.Sector==target.Sector&&p.s.Row==target.Row).Select(p=>(long)p.i+1));
            else if(!preview.Selected.Contains(id))preview.SetSelection(new[]{id});
            var menu=new ContextMenu{PlacementTarget=preview};
            void Item(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>Try(action);menu.Items.Add(item);}
            if(rowCaption)Item("Сдвинуть ряд",()=>preview.EditRow(target.Sector,target.Row));
            Item("Сделать проход",()=>{Remember();HallEditing025.MakeAisle(seats,Selection());Refresh(false);});
            Item("Проход после ряда",()=>{var dialog=new RowAisleWindow024(Store);if(dialog.ShowDialog()!=true)return;Remember();HallEditing024.MakeRowAisle(seats,Selection(),dialog.Gap);Refresh(false);});
            Item("Назначить букву",()=>{var dialog=new HallLetterWindow025(Store);if(dialog.ShowDialog()!=true)return;Remember();HallEditing025.AssignLetter(seats,target,dialog.Letter,rowCaption);Refresh(false);});
            Item("Удалить места",DeleteSelected);
            Item("Скрыть места",()=>{Remember();HallAnchors027.Capture(seats);foreach(var seat in Selection())seat.Hidden=true;Refresh(false);});
            if(Selection().Any(s=>s.Hidden))Item("Показать места",()=>{Remember();foreach(var seat in Selection())seat.Hidden=false;Refresh(false);});
            menu.Items.Add(new Separator());
            Item("Переместить сектор",()=>preview.EditSector(target.Sector,false));
            Item("Повернуть сектор",()=>preview.EditSector(target.Sector,true));
            preview.ContextMenu=menu;menu.IsOpen=true;
        });
        preview.SeatContextRequested+=(id,_)=>Context(id,false);preview.RowContextRequested+=(id,_)=>Context(id,true);
        preview.SectorContextRequested+=(sector,_)=>Try(()=>{
            var original=seats.First(s=>s.Sector==sector).SectorCaption;
            var dialog=new HallSectorNameWindow027(store,sector,preview.SectorCaption029ForEdit(sector),original is not null);if(dialog.ShowDialog()!=true)return;
            // Validate before recording undo, then apply the complete sector rename.
            var draft=seats.Select(Clone).ToList();HallEditing027.RenameSector(draft,sector,dialog.Result);
            Remember();HallEditing027.RenameSector(seats,sector,dialog.Result);foreach(var seat in seats.Where(s=>s.Sector==dialog.Result))seat.SectorCaption=dialog.ResultCaption;Refresh(false);
        });
        preview.SectorCaptionChanged029+=(sector,caption)=>Try(()=>{Remember();foreach(var seat in seats.Where(s=>s.Sector==sector))seat.SectorCaption=caption;Refresh(false);});
        void Transform(IReadOnlyList<SeatInfo> changed)=>Try(()=>{
            if(changed.Count==0)return;Remember();
            foreach(var mark in changed)if(mark.Id>0&&mark.Id<=seats.Count){seats[(int)mark.Id-1].X=mark.X;seats[(int)mark.Id-1].Y=mark.Y;seats[(int)mark.Id-1].RowAisleAnchors=mark.RowAisleAnchors?.ToArray();}
            Refresh(false);
        });
        preview.SectorTransformCompleted+=Transform;preview.RowTransformCompleted+=Transform;
        preview.KeyDown+=(_,e)=>{if(e.Key==Key.Delete){Try(DeleteSelected);e.Handled=true;}};
        foreach(var (header,path,width) in new[]{("Сектор","Sector",150),("Ряд","Row",60),("Место","Number",70),("X","X",80),("Y","Y",80)})
            table.Columns.Add(new DataGridTextColumn{Header=header,Binding=new Binding(path),Width=width,IsReadOnly=path is "X" or "Y"});
        table.ItemsSource=seats;OnShown(()=>Refresh());
    }
    private List<DraftSeat> Selection()=>preview.Selected.Where(id=>id>0&&id<=seats.Count).Select(id=>seats[(int)id-1]).ToList();
    private void DeleteSelected(){var selected=Selection();if(selected.Count==0)return;Remember();HallEditing024.Delete(seats,selected);Refresh(false);}
    private bool CommitEdits()=>table.CommitEdit(DataGridEditingUnit.Cell,true)&&table.CommitEdit(DataGridEditingUnit.Row,true);
    private void Refresh(bool fit=true)
    {
        var view=seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,s.Hidden?"#BBBBBB":"#7AA8D9",null,"","",null,IsHidden:s.Hidden&&showHidden.IsChecked!=true){RowAisleAnchors=s.RowAisleAnchors}).ToList();
        preview.Load(view,seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(s=>s.X),g.Min(s=>s.Y)-26){Caption=g.First().SectorCaption}).ToList(),fit);
        summary.Text=$"Мест: {seats.Count(s=>!s.Hidden)} · Скрыто: {seats.Count(s=>s.Hidden)} · Секторов: {seats.Select(s=>s.Sector).Distinct().Count()}";
    }
}

internal sealed class HallSectorNameWindow027:EditorWindow
{
    public string Result{get;private set;}="";
    public SectorCaption029? ResultCaption{get;private set;}
    public HallSectorNameWindow027(Store store,string sector):this(store,sector,new SectorCaption029(sector,0,0,0),false){}
    public HallSectorNameWindow027(Store store,string sector,SectorCaption029 caption,bool hasOverride):base(store,"Название сектора",500,360)
    {
        var panel=new StackPanel{Margin=new Thickness(16)};Content=panel;
        panel.Children.Add(new TextBlock{Text="Название сектора (одна или две строки)",Margin=new Thickness(0,0,0,4)});
        var name=new TextBox{Text=caption.Text,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=74,VerticalContentAlignment=VerticalAlignment.Center};panel.Children.Add(name);
        var angle=caption.Angle;var angleLabel=new TextBlock{Text=$"Угол надписи: {angle:0.##}°",Margin=new Thickness(0,10,0,4)};panel.Children.Add(angleLabel);
        Button(panel,"Повернуть надпись",()=>Try(()=>{
            var dialog=new SectorCaptionAngleWindow029(store,angle);if(dialog.ShowDialog()!=true)return;angle=dialog.Angle;angleLabel.Text=$"Угол надписи: {angle:0.##}°";
        }));
        Button(panel,"Сохранить",()=>Try(()=>{
            var text=name.Text.Replace("\r\n","\n").Trim();
            if(text.Length is 0 or >200||text.Count(c=>c=='\n')>1||text.Any(c=>char.IsControl(c)&&c!='\n')||text.Split('\n').Any(string.IsNullOrWhiteSpace))throw new TicketException("Укажите название в одну или две строки (до 200 символов).");
            var logical=System.Text.RegularExpressions.Regex.Replace(text,@"\s+"," ");
            var draft=new[]{new DraftSeat{Sector=sector}};HallEditing027.RenameSector(draft,sector,logical);Result=draft[0].Sector;
            ResultCaption=hasOverride||text.Contains('\n')||angle!=0?caption with{Text=text,Angle=angle}:null;Finish(true);
        }));
    }
}
internal sealed class SectorCaptionAngleWindow029:EditorWindow
{
    internal double Angle{get;private set;}
    internal SectorCaptionAngleWindow029(Store store,double angle):base(store,"Поворот надписи",420,230)
    {
        var panel=new StackPanel{Margin=new Thickness(16)};Content=panel;
        var input=Field(panel,"Угол в градусах",angle.ToString(System.Globalization.CultureInfo.CurrentCulture));
        Button(panel,"Сохранить",()=>Try(()=>{
            if(!double.TryParse(input.Text.Replace(',', '.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var result)||!double.IsFinite(result))throw new TicketException("Укажите угол в градусах.");
            Angle=result%360;Finish(true);
        }));
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
