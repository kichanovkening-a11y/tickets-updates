using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace Tickets.Core;

// All exports and the Office preview use the same original workbook and cell data.
public static class ExcelTtnWorkbook
{
    private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P="http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace C="http://schemas.openxmlformats.org/package/2006/content-types";
    public static void Save(IssueDocumentInfo info,string destination)=>SaveRequest(ExcelTtnData.CreateRequest(info),destination);
    public static void SaveRequest(byte[] request,string destination)
    {
        using var payload=new ZipArchive(new MemoryStream(request));
        using var input=payload.GetEntry(ExcelTtnData.PayloadPath)!.Open();using var json=JsonDocument.Parse(input);
        var pages=json.RootElement.GetProperty("Pages").EnumerateArray().ToArray();
        using var output=new MemoryStream();
        using(var template=typeof(Store).Assembly.GetManifestResourceStream("Tickets.Core.Resources.UniversalForms.ttn-original047.xlsx")!)template.CopyTo(output);
        output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
        {
            XDocument Read(string path){using var stream=zip.GetEntry(path)!.Open();return XDocument.Load(stream);}
            void Write(string path,XDocument document){zip.GetEntry(path)?.Delete();using var stream=zip.CreateEntry(path).Open();document.Save(stream);}
            var source=Read("xl/worksheets/sheet1.xml");var sheetRels=Read("xl/worksheets/_rels/sheet1.xml.rels");
            var book=Read("xl/workbook.xml");var rels=Read("xl/_rels/workbook.xml.rels");var types=Read("[Content_Types].xml");
            book.Root!.Element(S+"sheets")!.RemoveNodes();
            var names=book.Root.Element(S+"definedNames")!;names.RemoveNodes();
            rels.Root!.Elements(P+"Relationship").Where(x=>((string?)x.Attribute("Type"))?.EndsWith("/worksheet")==true).Remove();
            for(var i=0;i<pages.Length;i++)
            {
                var page=pages[i];var sheet=new XDocument(source);
                foreach(var view in sheet.Descendants(S+"sheetView"))view.SetAttributeValue("tabSelected",i==0?"1":"0");
                sheet.Root!.Element(S+"rowBreaks")?.Remove();
                var cells=sheet.Descendants(S+"c").ToDictionary(x=>(string)x.Attribute("r")!);
                foreach(var field in page.GetProperty("Cells").EnumerateObject())
                {
                    if(!cells.TryGetValue(field.Name,out var cell))throw new TicketException("В исходной форме ТТН нет поля "+field.Name);
                    cell.RemoveNodes();cell.Attribute("t")?.Remove();
                    if(field.Value.ValueKind==JsonValueKind.Number)cell.Add(new XElement(S+"v",field.Value.GetRawText()));
                    else{cell.SetAttributeValue("t","inlineStr");cell.Add(new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),field.Value.GetString()??"")));}
                }
                var first=page.GetProperty("First").GetBoolean();var from=first?1:35;var to=first?34:71;
                foreach(var row in sheet.Descendants(S+"row").Where(row=>(int)row.Attribute("r")!<from||(int)row.Attribute("r")!>to).ToArray())row.Remove();
                var merges=sheet.Root!.Element(S+"mergeCells");
                if(merges is not null)
                {
                    foreach(var merge in merges.Elements().ToArray())
                    {var reference=(string)merge.Attribute("ref")!;var row=int.Parse(new string(reference.Split(':')[0].Where(char.IsDigit).ToArray()));if(row<from||row>to)merge.Remove();}
                    merges.SetAttributeValue("count",merges.Elements().Count());
                }
                if(!first)
                {
                    string Shift(string reference)=>System.Text.RegularExpressions.Regex.Replace(reference,@"(?<=[A-Z])\d+",m=>(int.Parse(m.Value)-34).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach(var row in sheet.Descendants(S+"row"))row.SetAttributeValue("r",(int)row.Attribute("r")!-34);
                    foreach(var cell in sheet.Descendants(S+"c"))cell.SetAttributeValue("r",Shift((string)cell.Attribute("r")!));
                    if(merges is not null)foreach(var merge in merges.Elements())merge.SetAttributeValue("ref",Shift((string)merge.Attribute("ref")!));
                }
                sheet.Root.Element(S+"dimension")!.SetAttributeValue("ref","A1:CQ"+(to-from+1));
                var number=i+1;var name="Page"+number;var id="ttnPage"+number;
                book.Root.Element(S+"sheets")!.Add(new XElement(S+"sheet",new XAttribute("name",name),new XAttribute("sheetId",number),new XAttribute(R+"id",id)));
                names.Add(new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Area"),new XAttribute("localSheetId",i),name+"!"+(page.GetProperty("First").GetBoolean()?"$A$1:$CQ$34":"$A$1:$CQ$37")));
                rels.Root.Add(new XElement(P+"Relationship",new XAttribute("Id",id),new XAttribute("Type",R.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet"+number+".xml")));
                if(i>0)types.Root!.Add(new XElement(C+"Override",new XAttribute("PartName","/xl/worksheets/sheet"+number+".xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                Write("xl/worksheets/sheet"+number+".xml",sheet);Write("xl/worksheets/_rels/sheet"+number+".xml.rels",sheetRels);
            }
            Write("xl/workbook.xml",book);Write("xl/_rels/workbook.xml.rels",rels);Write("[Content_Types].xml",types);
        }
        var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(temporary,output.ToArray());File.Move(temporary,destination,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
