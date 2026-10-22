using System.Windows;
using System.Windows.Controls;
using Tickets.Core;

namespace Tickets.App;

internal sealed class InvoiceMergeWindow:EditorWindow
{
    public long MergedIssueId{get;private set;}
    internal InvoiceMergeWindow(Store store,long eventId,IReadOnlyCollection<long>? initialIssueIds=null):base(store,"Объединить накладные",980,720)
    {
        var root=new DockPanel{Margin=new Thickness(20)};Content=root;
        var heading=new TextBlock{Text="Выберите накладные одного получателя и отправителя. Будет создана новая накладная со следующим общим номером. Дата и реквизиты взяты из самой ранней выбранной накладной. Серии, номера, цены и состояние каждого билета сохраняются; исходные накладные останутся в истории.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var summary=new TextBlock{TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,8)};footer.Children.Add(summary);
        var actions=new WrapPanel();footer.Children.Add(actions);
        var rows=new StackPanel();root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var candidates=store.Issues(eventId).Where(issue=>issue.Count>issue.ReturnedCount&&issue.ReservedCount==0)
            .Select(issue=>new Choice(issue,store.Invoice(eventId,issue.Id))).ToArray();
        var boxes=new List<(CheckBox Box,Choice Choice)>();
        var merge=Button(actions,"Объединить в одну накладную",()=>Try(()=>{
            var selected=boxes.Where(row=>row.Box.IsChecked==true).Select(row=>row.Choice).OrderBy(item=>item.Issue.Id).ToArray();
            if(selected.Length<2)throw new TicketException("Выберите минимум две накладные.");
            var chosen=selected[0];
            var message=$"Объединить накладные {string.Join(", ",selected.Select(item=>"№ "+item.Issue.Id))} в новую накладную?\nПолучатель: {chosen.Invoice.Recipient}\nОтправитель: {chosen.Invoice.Sender}\nБилетов на руках: {selected.Sum(item=>item.Invoice.ActiveCount)}, сумма: {selected.Sum(item=>item.Invoice.ActiveTotal):N0} ₽.\nБудет присвоен следующий общий номер. Будут использованы дата и реквизиты № {chosen.Issue.Id}. Исходные записи и возвраты сохранятся.";
            if(MessageBox.Show(DialogOwner,message,"Объединение накладных",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            MergedIssueId=store.MergeInvoices(eventId,selected.Select(item=>item.Issue.Id).ToArray());
            try{store.SaveAllDocuments(eventId,MergedIssueId);}catch(Exception ex){MessageBox.Show(DialogOwner,"Накладные объединены в базе. Не удалось обновить файлы: "+ex.Message,"Документы",MessageBoxButton.OK,MessageBoxImage.Warning);}
            Finish(true);
        }));merge.IsEnabled=false;
        var cancel=Button(actions,"Отмена",()=>Finish(false));cancel.IsCancel=true;
        void Refresh()
        {
            var selected=boxes.Where(row=>row.Box.IsChecked==true).Select(row=>row.Choice).OrderBy(item=>item.Issue.Id).ToArray();
            var same=selected.Select(item=>CatalogText.Key(item.Invoice.Recipient)+"\n"+CatalogText.Key(item.Invoice.Sender)).Distinct().Count()<=1;
            summary.Text=$"Выбрано: {selected.Length} · Билетов на руках: {selected.Sum(item=>item.Invoice.ActiveCount)} · Сумма: {selected.Sum(item=>item.Invoice.ActiveTotal):N0} ₽"+(same?"":"\nВыберите накладные одного получателя и отправителя.");
            merge.IsEnabled=selected.Length>=2&&same;
        }
        foreach(var candidate in candidates)
        {
            var box=new CheckBox{Content=candidate.Caption,Margin=new Thickness(0,7,0,7),IsChecked=initialIssueIds?.Contains(candidate.Issue.Id)==true};
            boxes.Add((box,candidate));rows.Children.Add(box);box.Checked+=(_,_)=>Refresh();box.Unchecked+=(_,_)=>Refresh();
        }
        if(candidates.Length<2)rows.Children.Add(new TextBlock{Text="Для объединения нужны минимум две действующие выданные накладные одного получателя и отправителя.",TextWrapping=TextWrapping.Wrap});
        Refresh();
    }
    private sealed record Choice(IssueInfo Issue,InvoiceInfo Invoice)
    {
        public string Caption=>$"№ {Issue.Id} · {Issue.Date} · {Invoice.Recipient} · {Invoice.Sender}\nНа руках: {Invoice.ActiveCount} · {Invoice.ActiveTotal:N0} ₽ · Возвращено: {Issue.ReturnedCount}";
    }
}
