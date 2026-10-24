using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Tickets.Core;

namespace Tickets.App;

internal sealed class IssueDocumentWindow:EditorWindow
{
    private readonly long eventId,issueId;
    private readonly string kind;
    private readonly TextBox template=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=200};
    private readonly FlowDocumentScrollViewer preview=new(){IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly TextBlock status=new(){FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)};
    private string original="";
    public IssueDocumentWindow(Store store,long eventId,long issueId,string kind):base(store,IssueDocumentKinds.Name(kind)+$" · выдача № {issueId}",1030,850)
    {
        this.eventId=eventId;this.issueId=issueId;this.kind=kind;
        var root=new Grid{Margin=new Thickness(2)};Content=root;DocumentPrintPreview.Route(root,()=>Try(Print));
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
        var header=new StackPanel();root.Children.Add(header);
        var info=Store.GetIssueDocument(eventId,issueId,kind);original=info.Template;template.Text=original;
        header.Children.Add(DocumentActions.Toolbar(
            ()=>Try(()=>{SaveForNavigation();IssueDocumentNavigation.OpenWord(Store.IssueDocumentPath(issueId,kind),()=>Store.SaveIssueDocumentWord(eventId,issueId,kind));}),
            ()=>Try(()=>{
            var path=DocumentSaveDialog.Save(Store,DialogOwner,$"{IssueDocumentKinds.Name(kind)}-{issueId}.docx",destination=>{Save();InvoiceDocuments.SaveIssueDocumentWord(Store.GetIssueDocument(eventId,issueId,kind),destination);});
            if(path is not null)status.Text="Сохранено: "+path;
        }),()=>Try(Print)));
        header.Children.Add(status);
        var tabs=new TabControl();Grid.SetRow(tabs,1);root.Children.Add(tabs);
        tabs.Items.Add(new TabItem{Header="Документ",Content=preview});
        var editing=new DockPanel{Margin=new Thickness(8)};var tools=new StackPanel();DockPanel.SetDock(tools,Dock.Bottom);editing.Children.Add(tools);
        tools.Children.Add(new TextBlock{Text="Подстановки: {recipient} — получатель, {sender} — отправитель, {event} — спектакль, {eventDate} — дата показа, {date} — дата выдачи, {count} — билетов, {amount} — сумма, {total} — итог, {items} — таблица билетов, {senderDetails} и {recipientDetails} — реквизиты, {senderPosition}, {senderSignatory}, {recipientPosition}, {recipientSignatory} — должности и подписанты.",TextWrapping=TextWrapping.Wrap,FontSize=13});
        Button(tools,"Сохранить шаблон",()=>Try(()=>{Save();Store.SaveDocumentTemplate(kind,template.Text);status.Text="Шаблон сохранён для следующих выдач. Получатель подставляется автоматически.";}));
        editing.Children.Add(template);tabs.Items.Add(new TabItem{Header="Изменить текст",Content=editing});
        tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==0&&template.Text!=original)Try(Save);};
        preview.SizeChanged+=(_,_)=>{if(preview.ActualWidth>100)preview.Document=Document(Store.GetIssueDocument(eventId,issueId,kind),preview.ActualWidth-20);};
        preview.Document=Document(info);status.Text="Документ подготовлен для текущей выдачи.";
        DocumentFlag025.Apply(this,root,info.Invoice.Recipient,issueId,IssueDocumentKinds.Name(kind));
        Closing+=(_,e)=>{
            if(template.Text==original)return;
            var answer=MessageBox.Show(DialogOwner,"Сохранить изменения документа?",Title,MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
            if(answer==MessageBoxResult.Cancel){e.Cancel=true;return;}
            if(answer==MessageBoxResult.Yes)try{Save();}catch(Exception ex){e.Cancel=true;MessageBox.Show(DialogOwner,ex.Message,"Документ",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
    }
    internal void SaveForNavigation(){if(template.Text!=original)Save();}
    private void Save()
    {
        var info=template.Text==original?Store.GetIssueDocument(eventId,issueId,kind):Store.SaveIssueDocument(eventId,issueId,kind,template.Text);original=info.Template;preview.Document=Document(info);
        try{status.Text="Сохранено: "+Store.SaveIssueDocumentWord(eventId,issueId,kind);}
        catch(Exception ex){status.Text="Документ сохранён в базе. Не удалось сохранить Word: "+ex.Message;}
    }
    private void Print()
    {
        SaveForNavigation();var info=Store.GetIssueDocument(eventId,issueId,kind);
        DocumentPrintPreview.Show(DialogOwner,Title,width=>Document(info,width),kind==IssueDocumentKinds.Waybill,kind==IssueDocumentKinds.Contract);
    }
    internal static FlowDocument Document(IssueDocumentInfo info,double? pageWidth=null)
    {
        if(InvoiceDocuments.UsesSourceForm(info))return SourceFormPreview.Document(info,pageWidth);
        var wide=info.Kind==IssueDocumentKinds.Waybill;
        var contract=info.Kind==IssueDocumentKinds.Contract;
        var available=pageWidth??(wide?1123:794);
        var document=new FlowDocument{FontFamily=new FontFamily(contract?"Times New Roman":"Arial"),FontSize=contract?16:13,PageWidth=available,PagePadding=new Thickness(25),ColumnWidth=Math.Max(100,available-50)};
        var firstLine=true;
        var lines=BusinessDocumentTemplates.Layout(info);
        for(var index=0;index<lines.Count;index++)
        {
            if(lines[index].Signatures){var labels=BusinessDocumentTemplates.PartyLabels(info.Kind);document.Blocks.Add(SignaturesTable(info.Invoice,labels.Seller,labels.Buyer,available-50));continue;}
            if(lines[index].Requisites){var labels=BusinessDocumentTemplates.PartyLabels(info.Kind);document.Blocks.Add(RequisitesTable(info.Invoice,labels.Seller,labels.Buyer,info.Kind is IssueDocumentKinds.Contract or IssueDocumentKinds.Act,contract,available-50));continue;}
            var line=lines[index].Text;
            if(line.StartsWith("№ | Билеты и места |",StringComparison.Ordinal))
            {
                var table=new Table{CellSpacing=0,Margin=new Thickness(0,5,0,10)};
                var columns=line.Split('|').Length;
                var widths=BusinessDocumentTemplates.ItemWidths(info.Kind);
                if(widths.Length!=columns)widths=BusinessDocumentTemplates.ItemWidths(IssueDocumentKinds.Contract);
                foreach(var width in widths)table.Columns.Add(new TableColumn{Width=new GridLength((available-50)*width/widths.Sum())});
                var group=new TableRowGroup();table.RowGroups.Add(group);var header=true;
                do
                {
                    var row=new TableRow();group.Rows.Add(row);
                    foreach(var value in lines[index].Text.Split('|'))row.Cells.Add(new TableCell(new Paragraph(new Run(value.Trim())){Margin=new Thickness(0)}){Padding=new Thickness(4),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(.5),FontSize=contract?14.67:wide?10.67:12,FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?Brushes.AliceBlue:Brushes.White});
                    header=false;index++;
                }while(index<lines.Count&&!lines[index].Requisites&&lines[index].Text.Split('|').Length==columns);
                index--;document.Blocks.Add(table);continue;
            }
            var formNote=wide&&(line.StartsWith("Унифицированная форма",StringComparison.Ordinal)||line.StartsWith("Утверждена постановлением",StringComparison.Ordinal)||line.StartsWith("Форма по ОКУД",StringComparison.Ordinal));
            var title=InvoiceDocuments.IsDocumentTitle(line);
            var bold=(!formNote&&firstLine)||title||InvoiceDocuments.IsBusinessHeading(line)||line.Contains(DocumentTotals.Total(info.Invoice.Tickets.Count,info.Invoice.Total),StringComparison.Ordinal);
            document.Blocks.Add(new Paragraph(new Run(line)){FontSize=title?contract?17.33:22:formNote?10:contract?16:13,TextAlignment=title?TextAlignment.Center:formNote?TextAlignment.Right:TextAlignment.Left,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,KeepWithNext=bold,Margin=new Thickness(0,0,0,7)});firstLine=false;
        }
        return document;
    }
    internal static Table SignaturesTable(InvoiceInfo invoice,string left,string right,double width=744)
    {
        var table=new Table{CellSpacing=0,Margin=new Thickness(0,32,0,0)};
        table.Columns.Add(new TableColumn{Width=new GridLength(width/2)});table.Columns.Add(new TableColumn{Width=new GridLength(width/2)});
        var group=new TableRowGroup();table.RowGroups.Add(group);
        var identities=new[]{SignatureBlocks.Sender(invoice),SignatureBlocks.Recipient(invoice)};if(SignatureBlocks.IsIndividualEntrepreneur(invoice)&&System.Text.RegularExpressions.Regex.IsMatch(left,@"(?i)руководитель|бухгалтер|директор"))left="";
        var labels=new[]{left,right};
        foreach(var stage in new[]{0,1,2})
        {
            var row=new TableRow();group.Rows.Add(row);
            for(var i=0;i<2;i++)
            {
                var cell=new TableCell{Padding=new Thickness(0)};
                cell.Blocks.Add(new Paragraph(new Run(stage==0?(string.IsNullOrWhiteSpace(labels[i])?"":labels[i]+": ")+identities[i].Position:stage==1?"":"________________")){TextAlignment=i==0?TextAlignment.Left:TextAlignment.Right,Margin=new Thickness(0),KeepWithNext=stage<2});
                if(stage==0)cell.Blocks.Add(new Paragraph(new Run(identities[i].Name)){TextAlignment=i==0?TextAlignment.Left:TextAlignment.Right,Margin=new Thickness(0),KeepWithNext=true});
                row.Cells.Add(cell);
            }
        }
        return table;
    }
    internal static Table RequisitesTable(InvoiceInfo invoice,string seller,string buyer,bool signatures=false,bool contract=false,double width=744)
    {
        var table=new Table{CellSpacing=0,Margin=new Thickness(0,5,0,12)};
        table.Columns.Add(new TableColumn{Width=new GridLength(width/2)});
        table.Columns.Add(new TableColumn{Width=new GridLength(width/2)});
        var group=new TableRowGroup();table.RowGroups.Add(group);var row=new TableRow();group.Rows.Add(row);
        var details=new[]{invoice.SenderDetails??new OrganizationDetails(Name:invoice.Sender),invoice.RecipientDetails??new OrganizationDetails(Name:invoice.Recipient)};
        var labels=new[]{seller,buyer};
        for(var index=0;index<2;index++)
        {
            var cell=new TableCell{Padding=new Thickness(6),FontSize=contract?14.67:12};
            cell.Blocks.Add(new Paragraph(new Run(labels[index])){FontWeight=FontWeights.Bold,KeepWithNext=true,Margin=new Thickness(0,0,0,5)});
            foreach(var line in BusinessDocumentTemplates.Requisites(details[index]).Split('\n'))cell.Blocks.Add(new Paragraph(new Run(line)){Margin=new Thickness(0,0,0,4)});

            row.Cells.Add(cell);
        }
        if(signatures)
        {
            for(var blank=0;blank<2;blank++){var gap=new TableRow();for(var i=0;i<2;i++)gap.Cells.Add(new TableCell(new Paragraph(new Run("")){Margin=new Thickness(0)}));group.Rows.Add(gap);}
            var signaturesTable=SignaturesTable(invoice,seller,buyer,width);
            foreach(var signature in signaturesTable.RowGroups[0].Rows.ToArray())
            {signaturesTable.RowGroups[0].Rows.Remove(signature);group.Rows.Add(signature);}
        }
        return table;
    }
}
