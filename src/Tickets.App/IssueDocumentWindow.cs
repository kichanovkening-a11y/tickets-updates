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
    public IssueDocumentWindow(Store store,long eventId,long issueId,string kind):base(store,IssueDocumentKinds.Name(kind)+$" · выдача № {issueId}",1000,850)
    {
        this.eventId=eventId;this.issueId=issueId;this.kind=kind;
        var root=new Grid{Margin=new Thickness(10)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.ColumnDefinitions.Add(new ColumnDefinition());root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(124)});
        var header=new StackPanel();Grid.SetColumnSpan(header,2);root.Children.Add(header);
        var info=Store.GetIssueDocument(eventId,issueId,kind);original=info.Template;template.Text=original;
        header.Children.Add(new TextBlock{Text="Получатель: "+info.Invoice.Recipient,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});header.Children.Add(status);
        var actions=new StackPanel{Margin=new Thickness(10,0,0,0)};Grid.SetColumn(actions,1);Grid.SetRow(actions,1);root.Children.Add(actions);
        InvoiceWindow.Square(actions,"Сохранить",()=>Try(Save),Brushes.SteelBlue);InvoiceWindow.Square(actions,"Распечатать",()=>Try(Print),Brushes.SeaGreen);
        var tabs=new TabControl();Grid.SetRow(tabs,1);root.Children.Add(tabs);
        tabs.Items.Add(new TabItem{Header="Документ",Content=preview});
        var editing=new DockPanel{Margin=new Thickness(8)};var tools=new StackPanel();DockPanel.SetDock(tools,Dock.Bottom);editing.Children.Add(tools);
        tools.Children.Add(new TextBlock{Text="Подстановки: {recipient} — получатель, {sender} — отправитель, {event} — спектакль, {eventDate} — дата показа, {date} — дата выдачи, {count} — билетов, {amount} — сумма, {total} — итог, {items} — таблица билетов, {senderDetails} и {recipientDetails} — реквизиты, {senderPosition}, {senderSignatory}, {recipientPosition}, {recipientSignatory} — должности и подписанты.",TextWrapping=TextWrapping.Wrap,FontSize=13});
        Button(tools,"Сохранить шаблон",()=>Try(()=>{Save();Store.SaveDocumentTemplate(kind,template.Text);status.Text="Шаблон сохранён для следующих выдач. Получатель подставляется автоматически.";}));
        editing.Children.Add(template);tabs.Items.Add(new TabItem{Header="Изменить текст / шаблон",Content=editing});
        tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==0&&template.Text!=original)Try(Save);};
        preview.Document=Document(info);status.Text="Документ подготовлен для текущей выдачи.";
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
        var info=Store.SaveIssueDocument(eventId,issueId,kind,template.Text);original=info.Template;preview.Document=Document(info);
        try{status.Text="Сохранено: "+Store.SaveIssueDocumentWord(eventId,issueId,kind);}
        catch(Exception ex){status.Text="Документ сохранён в базе. Не удалось сохранить Word: "+ex.Message;}
    }
    private void Print()
    {
        Save();var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;
        var document=Document(Store.GetIssueDocument(eventId,issueId,kind));document.PageWidth=dialog.PrintableAreaWidth;document.PageHeight=dialog.PrintableAreaHeight;document.PagePadding=new Thickness(32);document.ColumnWidth=dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,Title);
    }
    internal static FlowDocument Document(IssueDocumentInfo info)
    {
        var document=new FlowDocument{FontFamily=new FontFamily("Arial"),FontSize=13,PageWidth=794,PagePadding=new Thickness(25),ColumnWidth=744};
        var firstLine=true;
        var lines=info.Body.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        for(var index=0;index<lines.Length;index++)
        {
            var line=lines[index];
            if(line.StartsWith("№ | Билеты и места |",StringComparison.Ordinal))
            {
                var table=new Table{CellSpacing=0,Margin=new Thickness(0,5,0,10)};
                foreach(var width in new[]{35d,330,45,65,85,90})table.Columns.Add(new TableColumn{Width=new GridLength(width)});
                var group=new TableRowGroup();table.RowGroups.Add(group);var header=true;
                do
                {
                    var row=new TableRow();group.Rows.Add(row);
                    foreach(var value in lines[index].Split('|'))row.Cells.Add(new TableCell(new Paragraph(new Run(value.Trim())){Margin=new Thickness(0)}){Padding=new Thickness(4),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(.5),FontSize=12,FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?Brushes.AliceBlue:Brushes.White});
                    header=false;index++;
                }while(index<lines.Length&&lines[index].Split('|').Length==6);
                index--;document.Blocks.Add(table);continue;
            }
            var bold=firstLine||InvoiceDocuments.IsBusinessHeading(line)||line.Contains(DocumentTotals.Total(info.Invoice.Tickets.Count,info.Invoice.Total),StringComparison.Ordinal);
            document.Blocks.Add(new Paragraph(new Run(line)){FontSize=firstLine?22:13,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,KeepWithNext=bold,Margin=new Thickness(0,0,0,7)});firstLine=false;
        }
        return document;
    }
}
