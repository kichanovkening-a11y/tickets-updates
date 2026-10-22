using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Documents;
using Tickets.App;
using Tickets.Core;

internal static class Docs021UiChecks
{
    internal static void Run(Store store)
    {
        var printHost=new Grid();var printReader=new FlowDocumentScrollViewer{Document=new FlowDocument(new Paragraph(new Run("Проба печати")))};printHost.Children.Add(printReader);
        var routedPrint=0;DocumentPrintPreview.Route(printHost,()=>routedPrint++);ApplicationCommands.Print.Execute(null,printReader);
        Check(routedPrint==1,"viewer print command routes to application preview, bypassing unsupported native dialog");
        const string recipient="UI Документы 021";
        store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000021",Signatory:"Петров П.П."));
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));
        var id=store.Issue(show.Id,[store.Seats(show.Id).First(s=>s.Available).Id],recipient,"Д021UI",1);
        var changes=0;var fields=(WrapPanel)ContractFields.Row(store,show.Id,id,()=>changes++);
        Check(fields.Children.OfType<TextBox>().Count()==1&&!fields.Children.OfType<Button>().Any(),"contract header contains only automatic number/date fields");
        fields.Children.OfType<TextBox>().Single().Text="UI-AUTO-021";
        fields.Children.OfType<DatePicker>().Single().SelectedDate=new DateTime(2026,10,22);
        var reopened=new Store(store.Path);
        Check(changes>=1&&reopened.ContractNumber(show.Id,id)=="UI-AUTO-021"&&reopened.ContractDate(show.Id,id)==new DateTime(2026,10,22),"metadata automatically persists without applying");
        var window=new CustomIssueDocumentWindow(store,show.Id,id,"ttn");
        var path=typeof(CustomIssueDocumentWindow).GetMethod("WorkingPath",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var first=(string)path.Invoke(window,null)!;var second=(string)path.Invoke(window,null)!;
        Check(first!=second&&!System.IO.File.Exists(first)&&!System.IO.File.Exists(second),"Word open never reuses an old hashed export");
        var native=(DocumentTextEditor)typeof(CustomIssueDocumentWindow).GetField("textEditor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        var rich=(RichTextBox)typeof(DocumentTextEditor).GetField("editor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(native)!;
        var run=Runs(rich.Document.Blocks).First(r=>r.Text.Length>0&&r.Text!="avto");run.Text+=" правка021UI";
        Check(native.IsDirty,"native ordinary edit is tracked");window.SaveForNavigation();
        var info=store.CustomIssueDocument(show.Id,id,"ttn")!;
        Check(info.Body.Contains("правка021UI")&&!info.Body.Contains("avto"),"native text edit survives filling protected automatic fields");
        var output=store.Path+"-native-edited-ttn.docx";InvoiceDocuments.SaveIssueDocumentWord(info,output);
        Check(System.IO.File.Exists(output),"native edited TTN exports through Word package pipeline");
        window.Close();
    }
    private static IEnumerable<Run> Runs(BlockCollection blocks)
    {
        foreach(var block in blocks){
            if(block is Paragraph p)foreach(var run in p.Inlines.OfType<Run>())yield return run;
            if(block is Table table)foreach(var group in table.RowGroups)foreach(var row in group.Rows)foreach(var cell in row.Cells)foreach(var run in Runs(cell.Blocks))yield return run;
            if(block is Section section)foreach(var run in Runs(section.Blocks))yield return run;
        }
    }
    private static void Check(bool success,string message){if(!success)throw new Exception(message);Console.WriteLine("PASS 021 "+message);}
}
