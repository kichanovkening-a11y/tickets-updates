using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Tickets.App;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

internal static class Documents045UiChecks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    internal static void Run()
    {
        var type=typeof(Tickets.App.App).Assembly.GetType("Tickets.App.DocumentTextEditor")!;
        var control=(UserControl)Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,[],null)!;
        var editor=(RichTextBox)type.GetField("editor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(control)!;
        void Load(byte[] bytes)=>type.GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(control,[bytes,"contract"]);
        byte[] Save()=>(byte[])type.GetMethod("SaveBytes",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(control,null)!;
        var host=new Window{Content=control,Width=800,Height=600,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
        host.Show();
        try
        {
            Load(Fixture());
            FocusManager.SetFocusedElement(host,editor);editor.Focus();
            var cell=((Table)editor.Document.Blocks.FirstBlock).RowGroups[0].Rows[0].Cells[0];
            var original=(Paragraph)cell.Blocks.FirstBlock;
            Check(new TextRange(original.ContentStart,original.ContentEnd).Text.Contains("\nTail"),"opening a paragraph with avto preserves its existing line break");
            var prefix=original.Inlines.OfType<Run>().First(r=>r.Text.Contains("First"));
            editor.CaretPosition=prefix.ContentStart.GetPositionAtOffset(5)!;
            var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(host)!,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};
            editor.RaiseEvent(key);
            Check(!key.Handled,"Enter is permitted in ordinary text");
            EditingCommands.EnterParagraphBreak.Execute(null,editor);
            Check(cell.Blocks.OfType<Paragraph>().Count()==2,"Enter splits a paragraph inside its original table cell");
            EditingCommands.EnterParagraphBreak.Execute(null,editor);
            var split=Xml(Save());
            Check(split.Descendants(W+"tc").First().Elements(W+"p").Count()==3,"blank paragraph survives saving");
            Check(split.Descendants(W+"t").Any(t=>t.Value.Contains("{recipient}")),"splitting a paragraph retains the automatic field");
            editor.Undo();
            Check(Xml(Save()).Descendants(W+"tc").First().Elements(W+"p").Count()==2,"undo removes only the inserted blank paragraph");
            editor.Redo();
            Check(Xml(Save()).Descendants(W+"tc").First().Elements(W+"p").Count()==3,"redo restores the inserted blank paragraph");
            var bytes=Save();Load(bytes);
            cell=((Table)editor.Document.Blocks.FirstBlock).RowGroups[0].Rows[0].Cells[0];
            Check(cell.Blocks.OfType<Paragraph>().Count()==3,"saved paragraph structure survives reopening");
            original=(Paragraph)cell.Blocks.FirstBlock;editor.CaretPosition=original.ContentEnd;
            EditingCommands.EnterLineBreak.Execute(null,editor);
            editor.CaretPosition.InsertTextInRun("New line");
            Check(Xml(Save()).Descendants(W+"br").Count()==2,"Shift Enter produces a real Word line break alongside the original break");
            editor.CaretPosition=original.ContentStart;
            var data=new DataObject();data.SetData(DataFormats.UnicodeText,"Paste one\r\n\r\nPaste three");
            editor.RaiseEvent(new DataObjectPastingEventArgs(data,false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent});
            bytes=Save();var pasted=Xml(bytes);
            var texts=pasted.Descendants(W+"tc").First().Elements(W+"p").Select(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value))).ToArray();
            Check(texts[0]=="Paste one"&&texts[1]==""&&texts[2].StartsWith("Paste threeFirst"),"multiline paste retains empty lines and inserts before existing text");
            Check(pasted.Descendants(W+"tbl").Count()==2&&pasted.Descendants(W+"b").Any(),"paragraph editing preserves nested tables and bold formatting");
            using(var zip=new ZipArchive(new MemoryStream(bytes)))using(var read=new StreamReader(zip.GetEntry("custom/untouched.txt")!.Open()))Check(read.ReadToEnd()=="unchanged","paragraph editing preserves unrelated DOCX parts");
            Load(bytes);
            var auto=Paragraphs(editor.Document.Blocks).SelectMany(p=>p.Inlines.OfType<Run>()).Single(r=>r.Text=="avto");
            editor.Selection.Select(auto.ContentStart,auto.ContentEnd);
            var denied=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(host)!,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};editor.RaiseEvent(denied);
            Check(denied.Handled,"Enter cannot replace a protected automatic field");
            Check(string.Join("",Xml(Save()).Descendants(W+"t").Select(t=>t.Value)).Contains("{recipient}"),"protected field remains usable after editing and reopening");
            DocumentEditor019Checks.Run();
            var reviewErrors=new List<string>();
            try{StructuralBreaks(Load,Save,editor);}catch(Exception ex){reviewErrors.Add(ex.Message);}
            try{PrintDuringPendingRefresh();}catch(Exception ex){reviewErrors.Add(ex.Message);}
            Check(reviewErrors.Count==0,string.Join("; ",reviewErrors));
            PrintControls();
            AsyncPrintKeepsLatestPages();
            PageSettingsPreserveSource();
        }
        finally{host.Close();}
    }
    private static void PrintDuringPendingRefresh()
    {
        var window=new EditorWindow(null!,"Проверка ожидающего обновления");
        var pending=new TaskCompletionSource<FixedDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls=0;var submitted=0;
        FixedDocument Page(){var result=new FixedDocument();result.Pages.Add(new PageContent{Child=new FixedPage{Width=300,Height=400}});return result;}
        try
        {
            var refresh=UnifiedPrintPreview.Attach(window,(_,_)=>throw new Exception("expected async build"),(_,_)=>submitted++,asyncBuild:_=>++calls==1?Task.FromResult(Page()):pending.Task);
            var toolbar=((DockPanel)window.Content).Children.OfType<WrapPanel>().Single();
            var hasPrinter=toolbar.Children.OfType<ComboBox>().First().SelectedItem is not null;
            var click=Application.Current.Dispatcher.InvokeAsync(()=>toolbar.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            while(!click.Task.IsCompleted){Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(5);}
            click.Task.GetAwaiter().GetResult();
            // Same callback a pending margin timer invokes while Word is preparing Print.
            refresh();
            var noSupersedingRender=calls==2;
            if(!hasPrinter)window.Close(); // No installed queue on some CI hosts: never open a print error dialog.
            pending.SetResult(Page());
            var until=DateTime.UtcNow.AddSeconds(3);
            while(submitted==0&&hasPrinter&&DateTime.UtcNow<until){Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(5);}
            Check(noSupersedingRender&&(!hasPrinter||submitted==1),"pending margin refresh preserves exactly one Print submission");
        }
        finally{window.Close();}
    }
    private static void StructuralBreaks(Action<byte[]> load,Func<byte[]> save,RichTextBox editor)
    {
        byte[] FixtureWith(string body)
        {
            using var output=new MemoryStream();output.Write(Fixture());output.Position=0;
            using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
            {
                zip.GetEntry("word/document.xml")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("word/document.xml").Open());
                writer.Write($"<w:document xmlns:w='{W}'><w:body>{body}</w:body></w:document>");
            }
            return output.ToArray();
        }
        load(FixtureWith("<w:p><w:r><w:t>Alpha</w:t><w:br w:type='page'/><w:t>Omega</w:t></w:r></w:p>"));
        var run=((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().First();run.Text="Alphx";
        var pageKept=Xml(save()).Descendants(W+"br").Any(b=>(string?)b.Attribute(W+"type")=="page");
        load(FixtureWith("<w:p><w:pPr><w:sectPr><w:type w:val='nextPage'/><w:pgSz w:w='11906' w:h='16838'/></w:sectPr></w:pPr><w:r><w:t>Alpha Omega</w:t></w:r></w:p>"));
        run=((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().First();editor.CaretPosition=run.ContentStart.GetPositionAtOffset(5)!;
        EditingCommands.EnterParagraphBreak.Execute(null,editor);
        var split=Xml(save());var parts=split.Root!.Element(W+"body")!.Elements(W+"p").ToArray();
        var sectionKept=parts.Length==2&&split.Descendants(W+"sectPr").Count()==1&&parts[^1].Element(W+"pPr")?.Element(W+"sectPr") is not null;
        Check(pageKept&&sectionKept,$"editing preserves structural page breaks ({pageKept}) and keeps one final section boundary ({sectionKept})");
    }
    private static void AsyncPrintKeepsLatestPages()
    {
        var window=new EditorWindow(null!,"Асинхронная проверка печати");
        var first=new TaskCompletionSource<FixedDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second=new TaskCompletionSource<FixedDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldResource=new PageResource();var newResource=new PageResource();var calls=0;
        FixedDocument Page(double width,PageResource resource)
        {var result=new FixedDocument{Tag=resource};result.Pages.Add(new PageContent{Child=new FixedPage{Width=width,Height=300}});return result;}
        void PumpUntil(Func<bool> done)
        {
            var until=DateTime.UtcNow.AddSeconds(5);
            while(!done()&&DateTime.UtcNow<until){Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(5);}
            Check(done(),"asynchronous print update finishes without blocking the dispatcher");
        }
        try
        {
            var setup=Application.Current.Dispatcher.InvokeAsync(()=>UnifiedPrintPreview.Attach(window,(_,_)=>throw new Exception("async preview called the fallback builder"),(_,_)=>throw new Exception("test must not print"),asyncBuild:_=>++calls==1?first.Task:second.Task));
            PumpUntil(()=>setup.Task.IsCompleted);setup.Task.GetAwaiter().GetResult();
            var root=(DockPanel)window.Content;var toolbar=root.Children.OfType<WrapPanel>().Single();
            Check(!toolbar.Children.OfType<Button>().Single().IsEnabled,"Print remains disabled while native pages are being prepared");
            var change=Application.Current.Dispatcher.InvokeAsync(()=>toolbar.Children.OfType<TextBox>().Single().Text="2");PumpUntil(()=>change.Task.IsCompleted);change.Task.GetAwaiter().GetResult();
            Check(calls==2,"changed print settings request a fresh set of pages");
            second.SetResult(Page(200,newResource));
            PumpUntil(()=>Logical(root).OfType<DocumentPageView>().Any());
            first.SetResult(Page(100,oldResource));PumpUntil(()=>oldResource.Disposed==1);
            var view=Logical(root).OfType<DocumentPageView>().Single();
            Check(view.DocumentPaginator.GetPage(0).Size.Width==200,"late old rendering cannot replace pages for the latest settings");
            window.Close();Check(newResource.Disposed==1,"closing print preview releases its current native page package");
        }
        finally{window.Close();}
    }
    private sealed class PageResource:IDisposable{internal int Disposed;public void Dispose()=>Disposed++;}
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var value in Logical(child))yield return value;}
    private static void PageSettingsPreserveSource()
    {
        using var output=new MemoryStream();output.Write(Fixture());output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            var entry=zip.GetEntry("word/document.xml")!;XDocument xml;using(var s=entry.Open())xml=XDocument.Load(s);
            var section=XElement.Parse($"<w:sectPr xmlns:w='{W}'><w:pgSz w:w='16838' w:h='11906' w:orient='landscape'/><w:pgMar w:left='907' w:top='510' w:right='567' w:bottom='397'/></w:sectPr>");
            xml.Root!.Element(W+"body")!.Add(section);entry.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
        }
        var source=output.ToArray();var layout=DocxPageLayout.Read(source);
        Check(layout.Landscape&&Math.Abs(layout.Margins.Left*25.4/96-16)<.02&&Math.Abs(layout.Margins.Bottom*25.4/96-7)<.02,"printing reads source landscape and asymmetric margins");
        Check(layout.Apply(source,layout.Paper,layout.Margins).AsSpan().SequenceEqual(source),"unchanged print settings preserve the complete original package");
        var changed=Xml(layout.Apply(source,new Size(794,1123),new Thickness(20)));
        var size=changed.Descendants(W+"pgSz").Single();var margin=changed.Descendants(W+"pgMar").Single();
        Check((int?)size.Attribute(W+"w")==11910&&(int?)size.Attribute(W+"h")==16845&&(int?)margin.Attribute(W+"left")==300&&(int?)margin.Attribute(W+"bottom")==300,"explicit paper and margin changes reach the Word document in twips");
        Check(changed.Descendants(W+"tbl").Count()==2&&changed.Descendants(W+"t").Any(t=>t.Value=="{recipient}"),"print setup changes preserve tables and automatic field text");
    }
    private static void PrintControls()
    {
        var window=new EditorWindow(null!,"Проверка печати");
        try
        {
            var landscape=false;
            UnifiedPrintPreview.Attach(window,(settings,_)=>{landscape=settings.PrintTicket.PageOrientation==System.Printing.PageOrientation.Landscape;return new FixedDocument();},(_,_)=>throw new Exception("test must not print"),landscape:true);
            Check(landscape,"landscape documents reach the paginator with landscape printer settings");
            var root=(DockPanel)window.Content;
            var toolbar=root.Children.OfType<WrapPanel>().Single();
            var buttons=toolbar.Children.OfType<Button>().ToArray();
            Check(buttons.Length==1&&Equals(buttons[0].Content,"Печатать"),"document print settings expose Print once without duplicate Cancel or Refresh");
            Check(toolbar.Children.IndexOf(buttons[0])==toolbar.Children.Count-1,"Print follows printer paper orientation and copies in the same toolbar");
        }
        finally{window.Close();}
    }
    private static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach(var block in blocks)
            if(block is Paragraph p)yield return p;
            else if(block is Table t)foreach(var cell in t.RowGroups.SelectMany(g=>g.Rows).SelectMany(r=>r.Cells))foreach(var p2 in Paragraphs(cell.Blocks))yield return p2;
    }
    private static XDocument Xml(byte[] bytes){using var zip=new ZipArchive(new MemoryStream(bytes));using var s=zip.GetEntry("word/document.xml")!.Open();return XDocument.Load(s);}
    private static byte[] Fixture()
    {
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            using(var writer=new StreamWriter(zip.CreateEntry("word/document.xml").Open()))writer.Write($"<w:document xmlns:w='{W}'><w:body><w:tbl><w:tblPr><w:tblW w:w='9000'/></w:tblPr><w:tblGrid><w:gridCol w:w='9000'/></w:tblGrid><w:tr><w:tc><w:p><w:pPr><w:spacing w:after='80'/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val='24'/></w:rPr><w:t xml:space='preserve'>First bold </w:t></w:r><w:r><w:t>{{recipient}}</w:t></w:r><w:r><w:br/><w:t>Tail</w:t></w:r></w:p><w:tbl><w:tblGrid><w:gridCol w:w='4000'/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:t>Nested</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:tc></w:tr></w:tbl></w:body></w:document>");
            using var keep=new StreamWriter(zip.CreateEntry("custom/untouched.txt").Open());keep.Write("unchanged");
        }
        return output.ToArray();
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
