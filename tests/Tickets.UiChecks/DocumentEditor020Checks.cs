using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Tickets.Core;

internal static class DocumentEditor020Checks
{
    internal static void Run(Store store)
    {
        const string recipient="Все редакторы 020";
        store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000020",Signatory:"Петров П.П."));
        var e=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(e.Id).First(s=>s.Available);var id=store.Issue(e.Id,[seat.Id],recipient,"UI020",1);
        var assembly=typeof(Tickets.App.App).Assembly;
        foreach(var kind in new[]{"invoice","contract","act","bill","ttn","specification"})
            Edit(kind,false);
        // Explicit legacy text still uses the same editor and keeps its wording.
        var otherSeat=store.Seats(e.Id).First(s=>s.Available);id=store.Issue(e.Id,[otherSeat.Id],recipient,"UI020-OLD",1);
        foreach(var kind in IssueDocumentKinds.All){store.SaveIssueDocument(e.Id,id,kind,"Собственный старый текст "+kind+" {recipient} {amount}");Edit(kind,true);}

        void Edit(string kind,bool legacy)
        {
            var originalTemplate=CustomDocumentForms.Text(store.CustomIssueTemplate(e.Id,id,kind)!);
            var originalTokens=Tokens(originalTemplate);
            var originalBody=(store.CustomIssueDocument(e.Id,id,kind)??store.GetIssueDocument(e.Id,id,kind)).Body;
            var type=assembly.GetType(kind=="invoice"?"Tickets.App.InvoiceWindow":"Tickets.App.CustomIssueDocumentWindow")!;
            var window=(Window)(kind=="invoice"?Activator.CreateInstance(type,store,e.Id,id,false)!:Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,[store,e.Id,id,kind],null)!);
            try
            {
                var native=type.GetField("textEditor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
                var editor=(RichTextBox)native.GetType().GetField("editor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(native)!;
                var runs=Paragraphs(editor.Document.Blocks).SelectMany(p=>p.Inlines.OfType<Run>()).ToArray();
                Check(runs.Any(r=>r.Text=="avto"&&r.Foreground==Brushes.Red),"protected red avto in "+kind);
                var ordinary=runs.First(r=>!string.IsNullOrWhiteSpace(r.Text)&&r.Text!="avto");
                var marker=" Правка020-"+kind;ordinary.Text+=marker;
                type.GetMethod(kind=="invoice"?"SaveText":"SaveForNavigation",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
                var template=CustomDocumentForms.Text(store.CustomIssueTemplate(e.Id,id,kind)!);
                Check(template.Contains(marker),"ordinary edit persists in "+kind);
                Check(originalTokens.Length>0&&originalTokens.SequenceEqual(Tokens(template)),"every original protected placeholder persists in "+kind);
                var info=store.CustomIssueDocument(e.Id,id,kind)!;
                Check(info.Body.Contains(marker),"filled "+kind+" retains ordinary edit");
                Check(Normalize(info.Body.Replace(marker,""))==Normalize(originalBody),"edited "+kind+" retains all filled issue data");
                if(legacy)Check(info.Body.Contains("Собственный старый текст "+kind),"legacy wording survives "+kind);
            }
            finally{window.Close();}
        }
    }
    private static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach(var block in blocks){if(block is Paragraph p)yield return p;else if(block is Table table)foreach(var group in table.RowGroups)foreach(var row in group.Rows)foreach(var cell in row.Cells)foreach(var nested in Paragraphs(cell.Blocks))yield return nested;}
    }
    private static string[] Tokens(string text)=>System.Text.RegularExpressions.Regex.Matches(text,@"\{\{?\w+\}\}?").Select(m=>m.Value).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
    private static string Normalize(string text)=>System.Text.RegularExpressions.Regex.Replace(text,@"\s+"," ").Trim();
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
