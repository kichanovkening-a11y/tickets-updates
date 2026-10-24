using System.Windows;
using System.Windows.Controls;
using Tickets.Core;
namespace Tickets.App;

internal sealed class LegacyHallGridWindow:EditorWindow
{
    private readonly HallView preview=new(){AllowUnavailableSelection=true,ShowInactivePlaceholders=false,SectorEditingEnabled=true};
    private List<DraftSeat> seats=[];
    private readonly Stack<List<DraftSeat>> history=[];
    public IReadOnlyList<DraftSeat> Result{get;private set;}=[];
    private static DraftSeat Copy(DraftSeat s)=>new(){Sector=s.Sector,Row=s.Row,Number=s.Number,X=s.X,Y=s.Y,Hidden=s.Hidden,RowAisleAnchors=s.RowAisleAnchors?.ToArray()};
    public LegacyHallGridWindow(Store store,string sectorName):base(store,"Сетка как в старой программе",1200,820)
    {
        var root=new Grid{Margin=new Thickness(16)};root.ColumnDefinitions.Add(new(){Width=new GridLength(265)});root.ColumnDefinitions.Add(new());Content=root;
        var settings=new StackPanel{Margin=new Thickness(0,0,12,0)};root.Children.Add(new ScrollViewer{Content=settings,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var sector=Field(settings,"Сектор",sectorName);var rows=Field(settings,"Количество рядов","1");var places=Field(settings,"Мест в ряду","1");
        var firstRow=Field(settings,"Начало рядов","1");var firstSeat=Field(settings,"Начало мест","1");
        var reverse=new CheckBox{Content="Нумерация справа налево",Margin=new Thickness(0,8,0,0)};settings.Children.Add(reverse);
        var showHidden=new CheckBox{Content="Показать скрытые места"};settings.Children.Add(showHidden);
        void Remember()=>history.Push(seats.Select(Copy).ToList());
        void Refresh()=>preview.Load(seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,0,s.Hidden?"#BBBBBB":"#A9C5E4",null,"","",null,IsHidden:s.Hidden&&showHidden.IsChecked!=true){RowAisleAnchors=s.RowAisleAnchors}).ToArray(),seats.GroupBy(s=>s.Sector).Select(g=>new SectorInfo(g.Key,g.Min(s=>s.X),g.Min(s=>s.Y)-26)).ToArray(),true);
        List<DraftSeat> Selection()=>preview.Selected.Where(id=>id>0&&id<=seats.Count).Select(id=>seats[(int)id-1]).ToList();
        showHidden.Checked+=(_,_)=>Refresh();showHidden.Unchecked+=(_,_)=>Refresh();
        Button(settings,"Создать сетку",()=>Try(()=>{var grid=new LegacyHallGrid(Number(rows,1,200),Number(places,1,300));var generated=grid.Compile(sector.Text,firstRow.Text,firstSeat.Text,reverse.IsChecked==true);Remember();seats=generated;Refresh();}));
        Button(settings,"Шаг назад",()=>{if(history.TryPop(out var previous)){seats=previous;Refresh();}});
        Button(settings,"Принять места",()=>Try(()=>{if(seats.Count==0)throw new TicketException("Создайте сетку мест.");Result=seats.Select(Copy).ToList();Finish(true);}));
        Button(settings,"Весь зал",()=>preview.Fit());
        Grid.SetColumn(preview,1);root.Children.Add(preview);
        void Context(long id,bool row)
        {
            if(id<1||id>seats.Count)return;var target=seats[(int)id-1];
            if(row)preview.SetSelection(seats.Select((s,i)=>(s,i)).Where(p=>p.s.Sector==target.Sector&&p.s.Row==target.Row).Select(p=>(long)p.i+1));
            else if(!preview.Selected.Contains(id))preview.SetSelection(new[]{id});
            var menu=new ContextMenu{PlacementTarget=preview};
            void Item(string name,Action action){var item=new MenuItem{Header=name};item.Click+=(_,_)=>Try(action);menu.Items.Add(item);}
            if(row)Item("Сдвинуть ряд",()=>preview.EditRow(target.Sector,target.Row));
            Item("Сделать проход",()=>{Remember();HallEditing025.MakeAisle(seats,Selection());Refresh();});
            Item("Проход после ряда",()=>{var dialog=new RowAisleWindow024(Store);if(dialog.ShowDialog()!=true)return;Remember();HallEditing024.MakeRowAisle(seats,Selection(),dialog.Gap);Refresh();});
            Item("Назначить букву",()=>{var dialog=new HallLetterWindow025(Store);if(dialog.ShowDialog()!=true)return;Remember();HallEditing025.AssignLetter(seats,target,dialog.Letter,row);Refresh();});
            Item("Удалить места",()=>{Remember();HallEditing024.Delete(seats,Selection());Refresh();});
            Item("Скрыть места",()=>{Remember();HallAnchors027.Capture(seats);foreach(var seat in Selection())seat.Hidden=true;Refresh();});
            if(Selection().Any(s=>s.Hidden))Item("Показать места",()=>{Remember();foreach(var seat in Selection())seat.Hidden=false;Refresh();});
            preview.ContextMenu=menu;menu.IsOpen=true;
        }
        preview.SectorContextRequested+=(name,_)=>Try(()=>{var dialog=new HallSectorNameWindow027(store,name);if(dialog.ShowDialog()!=true)return;var draft=seats.Select(Copy).ToList();HallEditing027.RenameSector(draft,name,dialog.Result);Remember();HallEditing027.RenameSector(seats,name,dialog.Result);Refresh();});
        preview.RowTransformCompleted+=changed=>Try(()=>{if(changed.Count==0)return;Remember();foreach(var mark in changed)if(mark.Id>0&&mark.Id<=seats.Count){seats[(int)mark.Id-1].X=mark.X;seats[(int)mark.Id-1].Y=mark.Y;seats[(int)mark.Id-1].RowAisleAnchors=mark.RowAisleAnchors?.ToArray();}Refresh();});
        preview.SeatContextRequested+=(id,_)=>Context(id,false);preview.RowContextRequested+=(id,_)=>Context(id,true);
    }
}
