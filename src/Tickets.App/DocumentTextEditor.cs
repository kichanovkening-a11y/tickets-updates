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
            if(e.Key==Key.Tab){e.Handled=true;return;}
            if(e.Key==Key.Enter&&!CanEditSelection()){e.Handled=true;return;}
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
            if(text is not null)editor.Selection.Text=text.Replace("\r\n","\n").Replace('\r','\n');
        });
        CommandManager.AddPreviewExecutedHandler(editor,(_,e)=>{
            if(e.Command==ApplicationCommands.Cut&&!CanEditSelection())e.Handled=true;
            if((e.Command==EditingCommands.EnterParagraphBreak||e.Command==EditingCommands.EnterLineBreak)&&!CanEditSelection())e.Handled=true;
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
    private bool CanEditSelection()=>editor.Selection.Start.Paragraph is not null&&editor.Selection.Start.Paragraph==editor.Selection.End.Paragraph&&!TouchesToken(editor.Selection.Start,editor.Selection.End);
    internal void Load(byte[] content,string kind)
    {
        loading=true;
        try
        {
            original=content;paragraphs.Clear();tokens.Clear();
            using var memory=new MemoryStream(content);using var zip=new ZipArchive(memory,ZipArchiveMode.Read);using var stream=zip.GetEntry("word/document.xml")!.Open();xml=XDocument.Load(stream);
            var info=new IssueDocumentInfo(kind,null!,"",""){SourceDocument=content};
            editor.Document=SourceFormPreview.Document(info,null,p=>{
                // Render the original tree so each displayed paragraph already has
                // its exact source identity, including nested/merged Word cells.
                var match=p;
                var view=SourceFormPreview.Paragraph(p,kind==IssueDocumentKinds.Contract?"Times New Roman":"Arial");
                var text=SourceText(p);
                if(Regex.IsMatch(text,@"\{\{?\w+\}\}?"))
                {
                    var pieces=view.Inlines.Select(inline=>(Inline:inline,Text:inline is LineBreak?"\n":inline is Run r?(Equals(r.Tag,"DocxTab")?"\t":r.Text):"")).ToArray();
                    Run Styled(string value,int position)
                    {
                        var run=new Run(value);var start=0;
                        foreach(var piece in pieces)
                        {
                            if(position<start+piece.Text.Length&&piece.Inline is Run style){run.FontFamily=style.FontFamily;run.FontSize=style.FontSize;run.FontWeight=style.FontWeight;run.FontStyle=style.FontStyle;break;}start+=piece.Text.Length;
                        }
                        return run;
                    }
                    void AddRange(int from,int to)
                    {
                        var start=0;
                        foreach(var piece in pieces)
                        {
                            var end=start+piece.Text.Length;var left=Math.Max(start,from);var right=Math.Min(end,to);
                            if(left<right)
                            {
                                if(piece.Inline is LineBreak)view.Inlines.Add(new LineBreak());
                                else if(piece.Text=="\t")view.Inlines.Add(new Run("    "){Tag="DocxTab"});
                                else view.Inlines.Add(Styled(text[left..right],left));
                            }
                            start=end;
                        }
                    }
                    view.Inlines.Clear();var offset=0;
                    foreach(Match token in Regex.Matches(text,@"\{\{?(\w+)\}\}?"))
                    {
                        AddRange(offset,token.Index);
                        var run=Styled("avto",token.Index);run.Foreground=Brushes.Red;run.ToolTip=Fields.FirstOrDefault(f=>f.Key==token.Groups[1].Value)?.Caption??"Автоматическое поле";view.Inlines.Add(run);tokens.Add((run,token.Value));offset=token.Index+token.Length;
                    }
                    AddRange(offset,text.Length);
                }
                paragraphs.Add((view,match));return view;
            },xml);
            IsDirty=false;editor.IsUndoEnabled=false;editor.IsUndoEnabled=true;
        }
        finally{loading=false;}
    }
    internal byte[] SaveBytes()
    {
        if(tokens.Any(t=>!Attached(t.View)||t.View.Text!="avto")||paragraphs.Any(p=>!Attached(p.View)))throw new TicketException("Автоматические поля защищены. Отмените удаление или изменение поля перед сохранением.");
        // Keep the loaded XML immutable: saving must not change the base used
        // by undo/redo or a later save. Added paragraphs inherit their source
        // paragraph's OOXML properties, including styles and run formatting.
        var saved=new XDocument(xml);
        var sourceCopies=xml.Descendants(W+"p").Zip(saved.Descendants(W+"p")).ToDictionary(pair=>pair.First,pair=>pair.Second);
        var originalViews=paragraphs.Select(p=>p.View).ToHashSet();
        foreach(var (view,source) in paragraphs)
        {
            var texts=new List<string>{Read(view.Inlines)};
            for(var next=view.NextBlock as Paragraph;next is not null&&!originalViews.Contains(next);next=next.NextBlock as Paragraph)texts.Add(Read(next.Inlines));
            var copy=sourceCopies[source];Patch(copy,string.Join('\u2029',texts));
            if(copy.Descendants(ParagraphBreak).Any())copy.ReplaceWith(SplitParagraph(copy));
        }
        using var output=new MemoryStream();output.Write(original);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update))
        {
            zip.GetEntry("word/document.xml")!.Delete();using var stream=zip.CreateEntry("word/document.xml").Open();saved.Save(stream);
        }
        return output.ToArray();
    }
    private string Read(InlineCollection inlines)=>string.Concat(inlines.Select(inline=>{
        if(inline is Span span)return Read(span.Inlines);
        if(inline is LineBreak)return "\n";
        if(inline is not Run run)return "";
        if(Equals(run.Tag,"DocxTab"))return "\t";
        var token=tokens.FirstOrDefault(t=>ReferenceEquals(t.View,run));return token.View is not null?token.Token:run.Text;
    }));
    private static void Patch(XElement paragraph,string updated)
    {
        var nodes=TextNodes(paragraph).ToArray();var before=SourceText(paragraph);
        if(before==updated)return;
        // Apply independent text changes backwards so unchanged text keeps its
        // original run (and therefore its own font/bold/italic properties).
        var edits=Changes(before,updated);
        foreach(var edit in edits.AsEnumerable().Reverse())
        {
            var offset=0;var inserted=false;var end=edit.Start+edit.Length;
            foreach(var node in nodes)
            {
                var value=NodeText(node);var next=offset+value.Length;
                // An adjacent page/column break or tab is not part of a text
                // replacement. Insertion at its boundary must preserve it too.
                var overlaps=edit.Length>0&&offset<end&&next>edit.Start;
                var insertHere=!inserted&&node.Name==W+"t"&&offset<=edit.Start&&next>=edit.Start;
                if(overlaps||insertHere)
                {
                    var from=Math.Clamp(edit.Start-offset,0,value.Length);var to=Math.Clamp(end-offset,0,value.Length);
                    node.Name=W+"t";node.RemoveAttributes();node.Value=value[..from]+(!inserted?edit.Value:"")+value[to..];node.SetAttributeValue(XNamespace.Xml+"space","preserve");inserted=true;
                }
                else if(!inserted&&edit.Length==0&&offset==edit.Start)
                {node.AddBeforeSelf(InlineNodes(edit.Value));inserted=true;}
                offset=next;
            }
            if(!inserted)paragraph.Add(new XElement(W+"r",new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),edit.Value)));
        }
        foreach(var text in paragraph.Descendants(W+"t").Where(t=>t.Value.IndexOfAny(['\n','\t','\u2029'])>=0).ToArray())text.ReplaceWith(InlineNodes(text.Value));
    }
    private static readonly XName ParagraphBreak=XName.Get("paragraphBreak","urn:tickets:editor");
    private static IEnumerable<XElement> TextNodes(XElement p)=>p.Descendants().Where(n=>n.Name==W+"t"||n.Name==W+"br"||n.Name==W+"tab");
    private static string NodeText(XElement node)=>node.Name==W+"br"?"\n":node.Name==W+"tab"?"\t":node.Value;
    private static string SourceText(XElement p)=>string.Concat(TextNodes(p).Select(NodeText));
    private static IEnumerable<XElement> InlineNodes(string value)
    {
        var start=0;
        for(var i=0;i<value.Length;i++)
        {
            if(value[i] is not ('\n' or '\t' or '\u2029'))continue;
            if(i>start)yield return new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),value[start..i]);
            yield return new XElement(value[i]=='\n'?W+"br":value[i]=='\t'?W+"tab":ParagraphBreak);start=i+1;
        }
        if(start<value.Length)yield return new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),value[start..]);
    }
    private static IEnumerable<XElement> SplitParagraph(XElement element)
    {
        var properties=element.Elements().Where(e=>e.Name==W+"pPr"||e.Name==W+"rPr").ToArray();
        var section=element.Element(W+"pPr")?.Element(W+"sectPr");
        XElement Empty()
        {
            var copy=new XElement(element.Name,element.Attributes(),properties.Select(p=>new XElement(p)));
            copy.Element(W+"pPr")?.Element(W+"sectPr")?.Remove();return copy;
        }
        var current=Empty();
        foreach(var child in element.Nodes())
        {
            if(child is not XElement node){current.Add(child is XText t?new XText(t.Value):null);continue;}
            if(properties.Contains(node))continue;
            if(node.Name==ParagraphBreak){yield return current;current=Empty();continue;}
            if(!node.Descendants(ParagraphBreak).Any()){current.Add(new XElement(node));continue;}
            var parts=SplitParagraph(node).ToArray();
            for(var i=0;i<parts.Length;i++)
            {
                if(i>0){yield return current;current=Empty();}
                current.Add(parts[i]);
            }
        }
        if(section is not null)current.Element(W+"pPr")!.Add(new XElement(section));
        yield return current;
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
