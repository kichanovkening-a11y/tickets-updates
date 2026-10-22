using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Tickets.Core;

namespace Tickets.App;

// Edits text in existing OOXML parts. Tables, paragraph/run properties, drawings,
// relationships and all unedited package entries remain in the original DOCX.
internal sealed class DocumentTextEditor:UserControl
{
    internal sealed record AutoField(string Key,string Caption);
    internal static readonly AutoField[] Fields=[
        new("recipient","Получатель"),new("sender","Отправитель"),new("buyerRepresentative","Представитель получателя"),new("recipientSignatory","ФИО представителя получателя"),new("recipientPosition","Должность представителя получателя"),new("recipientAuthority","Основание полномочий получателя"),new("senderSignatory","Подписант отправителя"),new("senderPosition","Должность отправителя"),new("senderAuthority","Основание полномочий отправителя"),
        new("event","Мероприятие"),new("eventDate","Дата и время показа"),new("eventCalendarDate","Дата показа / акта"),new("venue","Площадка"),
        new("contractNumber","Номер договора"),new("contractDate","Дата договора"),new("issueId","Номер накладной"),new("receivedBy","Получил: представитель или получатель"),new("date","Дата выдачи"),
        new("count","Количество билетов"),new("countWords","Количество прописью"),new("amount","Сумма"),new("amountWords","Сумма прописью"),new("total","Количество и сумма"),
        new("senderDetails","Реквизиты отправителя"),new("recipientDetails","Реквизиты получателя"),new("senderDetailsInline","Реквизиты отправителя в строку"),new("recipientDetailsInline","Реквизиты получателя в строку"),new("itemDescriptionShort","Краткое описание билетов"),new("itemDescription","Описание билетов в строке"),new("itemSection","Сектор в строке"),new("itemRow","Ряд в строке"),new("itemSeat","Места в строке"),new("itemCount","Количество в строке"),new("itemPrice","Цена в строке"),new("itemTotal","Сумма в строке")];
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private readonly RichTextBox editor=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,AcceptsTab=false,AllowDrop=false,IsUndoEnabled=true};
    private readonly List<(Paragraph View,XElement Source)> paragraphs=[];
    private readonly List<(Run View,string Token)> tokens=[];
    private byte[] original=[];
    private XDocument xml=new();
    private bool loading;
    internal bool IsDirty{get;private set;}
    internal DocumentTextEditor()
    {
        var root=new DockPanel();Content=root;
        var tools=new StackPanel{Margin=new Thickness(4)};DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        tools.Children.Add(new TextBlock{Text="Изменяйте обычный текст. Красное «avto» — защищённое поле. Выделите обычный текст и выберите, чем заполнять его avtoматически.",TextWrapping=TextWrapping.Wrap,FontSize=12});
        var row=new WrapPanel{Margin=new Thickness(0,5,0,5)};tools.Children.Add(row);
        var fields=new ComboBox{ItemsSource=Fields,DisplayMemberPath=nameof(AutoField.Caption),SelectedIndex=0,MinWidth=240};row.Children.Add(fields);
        var map=new Button{Content="Назначить выделенному тексту",Padding=new Thickness(8,3,8,3),Margin=new Thickness(8,0,0,0)};row.Children.Add(map);
        map.Click+=(_,_)=>{
            if(fields.SelectedItem is not AutoField field||editor.Selection.IsEmpty||!CanEditSelection())return;
            var start=editor.Selection.Start;editor.Selection.Text="";
            var run=new Run("avto",start){Foreground=Brushes.Red,ToolTip=field.Caption};tokens.Add((run,"{"+field.Key+"}"));IsDirty=true;
        };
        root.Children.Add(editor);
        editor.PreviewTextInput+=(_,e)=>{if(!CanEditSelection())e.Handled=true;};
        editor.PreviewKeyDown+=(_,e)=>{
            if(e.Key is Key.Enter or Key.Tab){e.Handled=true;return;}
            if(e.Key is Key.Back or Key.Delete)
            {
                if(!CanEditSelection()){e.Handled=true;return;}
                if(editor.Selection.IsEmpty)
                {
                    var direction=e.Key==Key.Back?LogicalDirection.Backward:LogicalDirection.Forward;
                    var adjacent=editor.CaretPosition.GetNextInsertionPosition(direction);
                    if(adjacent is null||adjacent.Paragraph!=editor.CaretPosition.Paragraph||TouchesToken(editor.CaretPosition,adjacent))e.Handled=true;
                }
            }
        };
        DataObject.AddPastingHandler(editor,(_,e)=>{
            e.CancelCommand();if(!CanEditSelection())return;
            var text=e.DataObject.GetData(DataFormats.UnicodeText) as string;
            if(text is not null)editor.Selection.Text=text.Replace("\r\n"," ").Replace('\n',' ').Replace('\r',' ');
        });
        CommandManager.AddPreviewExecutedHandler(editor,(_,e)=>{
            if(e.Command==ApplicationCommands.Cut&&!CanEditSelection())e.Handled=true;
        });
        editor.TextChanged+=(_,_)=>{
            if(loading)return;
            if(tokens.Any(t=>!Attached(t.View)||t.View.Text!="avto")||paragraphs.Any(p=>!Attached(p.View)))
            {loading=true;try{if(editor.CanUndo)editor.Undo();}finally{loading=false;}return;}
            IsDirty=true;
        };
    }
    private bool Attached(TextElement element)
    {
        DependencyObject? parent=element;
        while(parent is TextElement text)parent=text.Parent;
        return ReferenceEquals(parent,editor.Document);
    }
    private bool TouchesToken(TextPointer start,TextPointer end)
    {
        if(start.CompareTo(end)>0)(start,end)=(end,start);
        return tokens.Any(t=>t.View.Parent is not null&&start.CompareTo(t.View.ContentEnd)<0&&end.CompareTo(t.View.ContentStart)>0);
    }
    private bool CanEditSelection()=>editor.Selection.Start.Paragraph==editor.Selection.End.Paragraph&&!TouchesToken(editor.Selection.Start,editor.Selection.End);
    internal void Load(byte[] content,string kind)
    {
        loading=true;
        try
        {
            original=content;paragraphs.Clear();tokens.Clear();
            using var memory=new MemoryStream(content);using var zip=new ZipArchive(memory,ZipArchiveMode.Read);using var stream=zip.GetEntry("word/document.xml")!.Open();xml=XDocument.Load(stream);
            var source=xml.Descendants(W+"p").ToArray();var index=0;
            var info=new IssueDocumentInfo(kind,null!,"",""){SourceDocument=content};
            editor.Document=SourceFormPreview.Document(info,null,p=>{
                // The preview receives its own XML; bind edits to the same paragraph
                // in the untouched package, including table cells.
                var match=source.FirstOrDefault(x=>XNode.DeepEquals(x,p)&&!paragraphs.Any(pair=>ReferenceEquals(pair.Source,x)))??source[index];index++;
                var view=SourceFormPreview.Paragraph(p,kind==IssueDocumentKinds.Contract?"Times New Roman":"Arial");
                var text=string.Concat(p.Descendants(W+"t").Select(t=>t.Value));
                if(Regex.IsMatch(text,@"\{\{?\w+\}\}?"))
                {
                    var styles=view.Inlines.OfType<Run>().Where(r=>!Equals(r.Tag,"DocxTab")).ToArray();
                    Run Styled(string value,int position)
                    {
                        var run=new Run(value);var start=0;
                        foreach(var style in styles)
                        {
                            if(position<start+style.Text.Length||ReferenceEquals(style,styles.Last())){run.FontFamily=style.FontFamily;run.FontSize=style.FontSize;run.FontWeight=style.FontWeight;run.FontStyle=style.FontStyle;break;}start+=style.Text.Length;
                        }
                        return run;
                    }
                    view.Inlines.Clear();var offset=0;
                    foreach(Match token in Regex.Matches(text,@"\{\{?(\w+)\}\}?"))
                    {
                        view.Inlines.Add(Styled(text[offset..token.Index],offset));
                        var run=Styled("avto",token.Index);run.Foreground=Brushes.Red;run.ToolTip=Fields.FirstOrDefault(f=>f.Key==token.Groups[1].Value)?.Caption??"Автоматическое поле";view.Inlines.Add(run);tokens.Add((run,token.Value));offset=token.Index+token.Length;
                    }
                    view.Inlines.Add(Styled(text[offset..],offset));
                }
                paragraphs.Add((view,match));return view;
            });
            IsDirty=false;editor.IsUndoEnabled=false;editor.IsUndoEnabled=true;
        }
        finally{loading=false;}
    }
    internal byte[] SaveBytes()
    {
        if(tokens.Any(t=>!Attached(t.View)||t.View.Text!="avto")||paragraphs.Any(p=>!Attached(p.View)))throw new TicketException("Автоматические поля защищены. Отмените удаление или изменение поля перед сохранением.");
        foreach(var (view,source) in paragraphs)
        {
            var text=Read(view.Inlines);
            Patch(source,text);
        }
        using var output=new MemoryStream();output.Write(original);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update))
        {
            zip.GetEntry("word/document.xml")!.Delete();using var stream=zip.CreateEntry("word/document.xml").Open();xml.Save(stream);
        }
        return output.ToArray();
    }
    private string Read(InlineCollection inlines)=>string.Concat(inlines.Select(inline=>{
        if(inline is Span span)return Read(span.Inlines);
        if(inline is not Run run||Equals(run.Tag,"DocxTab"))return "";
        var token=tokens.FirstOrDefault(t=>ReferenceEquals(t.View,run));return token.View is not null?token.Token:run.Text;
    }));
    private static void Patch(XElement paragraph,string updated)
    {
        var nodes=paragraph.Descendants(W+"t").ToArray();var before=string.Concat(nodes.Select(n=>n.Value));
        if(before==updated)return;
        // Apply independent text changes backwards so unchanged text keeps its
        // original run (and therefore its own font/bold/italic properties).
        var edits=Changes(before,updated);
        foreach(var edit in edits.AsEnumerable().Reverse())
        {
            var offset=0;var inserted=false;var end=edit.Start+edit.Length;
            foreach(var node in nodes)
            {
                var next=offset+node.Value.Length;
                if(next>=edit.Start&&offset<=end)
                {
                    var from=Math.Clamp(edit.Start-offset,0,node.Value.Length);var to=Math.Clamp(end-offset,0,node.Value.Length);
                    node.Value=node.Value[..from]+(!inserted?edit.Value:"")+node.Value[to..];node.SetAttributeValue(XNamespace.Xml+"space","preserve");inserted=true;
                }
                offset=next;
            }
            if(!inserted)paragraph.Add(new XElement(W+"r",new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),edit.Value)));
        }
    }
    private sealed record TextEdit(int Start,int Length,string Value);
    private static List<TextEdit> Changes(string before,string updated)
    {
        var prefix=0;while(prefix<before.Length&&prefix<updated.Length&&before[prefix]==updated[prefix])prefix++;
        var suffix=0;while(suffix<before.Length-prefix&&suffix<updated.Length-prefix&&before[^(suffix+1)]==updated[^(suffix+1)])suffix++;
        var left=before.Substring(prefix,before.Length-prefix-suffix);var right=updated.Substring(prefix,updated.Length-prefix-suffix);
        if((long)left.Length*right.Length>4_000_000)throw new TicketException("Изменяйте длинный абзац небольшими фрагментами с сохранением между изменениями.");
        var lcs=new int[left.Length+1,right.Length+1];
        for(var i=left.Length-1;i>=0;i--)for(var j=right.Length-1;j>=0;j--)lcs[i,j]=left[i]==right[j]?1+lcs[i+1,j+1]:Math.Max(lcs[i+1,j],lcs[i,j+1]);
        var result=new List<TextEdit>();var x=0;var y=0;
        while(x<left.Length||y<right.Length)
        {
            if(x<left.Length&&y<right.Length&&left[x]==right[y]){x++;y++;continue;}
            var start=x;var insertion=new System.Text.StringBuilder();
            while(x<left.Length||y<right.Length)
            {
                if(x<left.Length&&y<right.Length&&left[x]==right[y])break;
                if(y<right.Length&&(x==left.Length||lcs[x,y+1]>=lcs[x+1,y]))insertion.Append(right[y++]);else x++;
            }
            result.Add(new(prefix+start,x-start,insertion.ToString()));
        }
        return result;
    }

}
