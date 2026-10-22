using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;

internal static class DocumentEditor019Checks
{
    internal static void Run()
    {
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        byte[] content;
        using(var memory=new MemoryStream())
        {
            using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
            {
                using(var writer=new StreamWriter(zip.CreateEntry("word/document.xml").Open()))writer.Write($"<w:document xmlns:w='{w}'><w:body><w:tbl><w:tblPr><w:tblW w:w='9000'/></w:tblPr><w:tblGrid><w:gridCol w:w='9000'/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:rPr><w:b/><w:rFonts w:ascii='Times New Roman'/></w:rPr><w:t>Original </w:t></w:r><w:r><w:t>{{recipient}}</w:t></w:r></w:p><w:tbl><w:tblPr><w:tblW w:w='4000'/></w:tblPr><w:tblGrid><w:gridCol w:w='4000'/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:t>Nested ordinary</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:tc></w:tr></w:tbl></w:body></w:document>");
                using var keep=new StreamWriter(zip.CreateEntry("custom/untouched.txt").Open());keep.Write("unchanged");
            }
            content=memory.ToArray();
        }
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.DocumentTextEditor")!;
        var control=(UserControl)Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,[],null)!;
        type.GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(control,[content,"contract"]);
        var editor=(RichTextBox)type.GetField("editor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(control)!;
        var table=(Table)editor.Document.Blocks.FirstBlock;var paragraph=(Paragraph)table.RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock;
        var auto=paragraph.Inlines.OfType<Run>().Single(r=>r.Text=="avto");
        Check(auto.Foreground==Brushes.Red,"auto field is red avto inside a native table");
        paragraph.Inlines.OfType<Run>().First().Text="Revised ";
        var nested=table.RowGroups[0].Rows[0].Cells[0].Blocks.OfType<Table>().Single();
        var nestedParagraph=(Paragraph)nested.RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock;
        Check(nestedParagraph.Inlines.OfType<Run>().Single().Text=="Nested ordinary","nested native table ordinary text is visible to the editor");
        nestedParagraph.Inlines.OfType<Run>().Single().Text="Nested revised";
        var bytes=(byte[])type.GetMethod("SaveBytes",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(control,null)!;
        using var output=new MemoryStream(bytes);using var archive=new ZipArchive(output,ZipArchiveMode.Read);
        using var stream=archive.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
        Check(xml.Descendants(w+"t").Aggregate("",(text,node)=>text+node.Value)=="Revised {recipient}Nested revised","ordinary and nested text edits retain actual token identity");
        Check(xml.Descendants(w+"tbl").Count()==2&&xml.Descendants(w+"tbl").First().Descendants(w+"tbl").Count()==1,"nested Word table structure remains nested after edit and save");
        Check(xml.Descendants(w+"tblPr").Any()&&xml.Descendants(w+"b").Any()&&xml.Descendants(w+"rFonts").Any(),"source table and run font properties survive ordinary edits");
        using var reader=new StreamReader(archive.GetEntry("custom/untouched.txt")!.Open());Check(reader.ReadToEnd()=="unchanged","unrelated DOCX package part unchanged");
        var formatted=XElement.Parse($"<w:p xmlns:w='{w}'><w:r><w:t>First </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>BOLD</w:t></w:r><w:r><w:t> end</w:t></w:r></w:p>");
        type.GetMethod("Patch",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[formatted,"Updated BOLD finish"]);
        Check(formatted.Descendants(w+"r").Single(r=>r.Element(w+"rPr")?.Element(w+"b") is not null).Element(w+"t")!.Value=="BOLD","two ordinary edits preserve formatting of unchanged text between them");
        auto.Text="bad";
        try
        {
            type.GetMethod("SaveBytes",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(control,null);
            Check(auto.Text=="avto","token edit is undone before save");
        }
        catch(TargetInvocationException error)when(error.InnerException is Tickets.Core.TicketException){Console.WriteLine("PASS damaged automatic token is refused at save");}
    }
    internal static void RunInvoice(Tickets.Core.Store store)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var seat=store.Seats(show.Id).First(s=>s.Available);
        var id=store.Issue(show.Id,[seat.Id],"Редактор накладной 019","Документы019",1);
        var assembly=typeof(Tickets.App.App).Assembly;var type=assembly.GetType("Tickets.App.InvoiceWindow")!;
        var window=(Window)Activator.CreateInstance(type,store,show.Id,id,false)!;
        try
        {
            window.Show();Application.Current.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var native=type.GetField("textEditor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
            var editor=(RichTextBox)native.GetType().GetField("editor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(native)!;
            Check(new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text.Contains("avto"),"invoice has protected automatic fields in native editor");
            var heading=editor.Document.Blocks.OfType<Paragraph>().SelectMany(p=>p.Inlines.OfType<Run>()).First(r=>r.Text.Contains("НАКЛАДНАЯ"));
            heading.Text=heading.Text.Replace("НАКЛАДНАЯ","ИЗМЕНЁННАЯ НАКЛАДНАЯ");
            type.GetMethod("SaveText",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
            Check(Tickets.Core.CustomDocumentForms.Text(store.CustomIssueTemplate(show.Id,id,"invoice")!).Contains("ИЗМЕНЁННАЯ НАКЛАДНАЯ"),"invoice ordinary text persists in issue DOCX template");
            var info=store.CustomIssueDocument(show.Id,id,"invoice")!;
            Check(info.Body.Contains("ИЗМЕНЁННАЯ НАКЛАДНАЯ")&&!System.Text.RegularExpressions.Regex.IsMatch(info.Body,@"\{\w+\}"),"invoice filled document retains native edit and actual automatic values");
            Check(store.Invoice(show.Id,id).Id==id&&store.Invoice(show.Id,id).Tickets.Count==1,"invoice editor preserves source identity and ticket data");
        }
        finally{window.Close();}
        var reopened=(Window)Activator.CreateInstance(type,store,show.Id,id,false)!;
        try
        {
            var native=type.GetField("textEditor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(reopened)!;
            var editor=(RichTextBox)native.GetType().GetField("editor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(native)!;
            Check(new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text.Contains("ИЗМЕНЁННАЯ НАКЛАДНАЯ"),"invoice native edit survives reopening");
        }
        finally{reopened.Close();}
    }
    private static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
}
