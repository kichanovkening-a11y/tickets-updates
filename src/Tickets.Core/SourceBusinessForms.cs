using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tickets.Core;

/// <summary>The supplied DOC and Excel forms, with their original editable Word cell grids.</summary>
internal static class SourceBusinessForms
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string StampPath="tickets/generated.xml";
    private static Stream Template(string kind)=>typeof(SourceBusinessForms).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.BusinessForms.{kind}.docx")??throw new TicketException("Исходный бланк документа не найден.");
    private static XDocument Xml(ZipArchive zip,string name){using var s=zip.GetEntry(name)!.Open();return XDocument.Load(s);}
    internal static string Version012Text(string kind)
    {
        using var stream=Template(kind);using var zip=new ZipArchive(stream,ZipArchiveMode.Read);var xml=Xml(zip,"word/document.xml");
        return string.Join("\n",xml.Descendants(W+"p").Select(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value))));
    }
    public static string DefaultText(string kind)=>CurrentText(kind,Version012Text(kind));
    internal static string CurrentText(string kind,string text)=>kind==IssueDocumentKinds.Contract
        ?text.Replace("не позднее ____________.","не позднее 3 дней с момента выставления счёта.",StringComparison.Ordinal):text;
    internal static string EventCalendarDate(InvoiceInfo invoice)
    {
        var ru=System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        return DateTime.TryParse(invoice.EventDate,ru,System.Globalization.DateTimeStyles.None,out var date)
            ||DateTime.TryParse(invoice.EventDate,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out date)
            ?date.ToString("dd.MM.yyyy",ru):System.Text.RegularExpressions.Regex.Replace(invoice.EventDate,@"\s+\d{1,2}:\d{2}.*$","");
    }
    internal static bool CanUseSource(string kind,string template)=>template.Replace("\r\n","\n").Replace('\r','\n').Split('\n').Length==DefaultText(kind).Split('\n').Length;
    public static Dictionary<string,string> Values(InvoiceInfo invoice)
    {
        var seller=invoice.SenderDetails??new OrganizationDetails(Name:invoice.Sender);var buyer=invoice.RecipientDetails??new OrganizationDetails(Name:invoice.Recipient);
        string Representative(OrganizationDetails details)=>string.IsNullOrWhiteSpace(details.Signatory)?"":"в лице "+string.Join(" ",new[]{details.Position,details.Signatory}.Where(v=>!string.IsNullOrWhiteSpace(v)));
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["issueId"]=invoice.Id.ToString(),["date"]=invoice.Date,["sender"]=string.IsNullOrWhiteSpace(seller.LegalName)?invoice.Sender:seller.LegalName,["recipient"]=string.IsNullOrWhiteSpace(buyer.LegalName)?invoice.Recipient:buyer.LegalName,["event"]=invoice.Title,["venue"]=invoice.Venue,["eventDate"]=invoice.EventDate,["count"]=invoice.Tickets.Count.ToString(),["amount"]=DocumentTotals.Amount(invoice.Total),["amountWords"]=RussianWords.Money(invoice.Total),["countWords"]=RussianWords.Number(invoice.Tickets.Count),["total"]=DocumentTotals.Total(invoice.Tickets.Count,invoice.Total),["lineCountWords"]=RussianWords.Number(BusinessLines(invoice).Count),["sellerRepresentative"]=Representative(seller),["buyerRepresentative"]=Representative(buyer)};
        foreach(var (prefix,details) in new[]{("sender",seller),("recipient",buyer)})
        {
            values[prefix+"Details"]=BusinessDocumentTemplates.Requisites(details);
            values[prefix+"Position"]=details.Position;values[prefix+"Signatory"]=details.Signatory;
            values[prefix+"InnKpp"]=string.Join(" / ",new[]{details.Inn,details.Kpp}.Where(v=>!string.IsNullOrWhiteSpace(v)));
            values[prefix+"Bank"]=details.Bank;values[prefix+"Bik"]=details.Bik;values[prefix+"Account"]=details.Account;values[prefix+"CorrespondentAccount"]=details.CorrespondentAccount;values[prefix+"Address"]=details.Address;
        }
        var lines=BusinessLines(invoice);
        values["itemDescription"]=string.Join("\n",lines.Select(line=>$"Театральные билеты на культурно-массовое мероприятие «{invoice.Title}». {invoice.Venue}. {invoice.EventDate}."));
        values["itemNumber"]="1";values["itemCount"]=invoice.Tickets.Count.ToString();values["itemPrice"]=string.Join(" / ",lines.Select(line=>DocumentTotals.Amount(line.Price)).Distinct());values["itemTotal"]=DocumentTotals.Amount(invoice.Total);
        return values;
    }
    private static IReadOnlyList<InvoiceLine> BusinessLines(InvoiceInfo invoice)=>invoice.Tickets.GroupBy(t=>t.Amount).OrderBy(g=>g.Key).Select(g=>new InvoiceLine("","","",g.Count(),g.Key,g.Sum(t=>(decimal)t.Amount),"")).ToArray();
    private static string Replace(string text,IReadOnlyDictionary<string,string> values)=>Regex.Replace(text,@"\{(\w+)\}",m=>values.TryGetValue(m.Groups[1].Value,out var value)?value:m.Value);
    private static void SetParagraph(XElement p,string text)
    {
        var props=p.Element(W+"pPr");var format=p.Descendants(W+"rPr").FirstOrDefault();
        p.ReplaceNodes(props is null?null:new XElement(props),new XElement(W+"r",format is null?null:new XElement(format),text.Split('\n').SelectMany((line,i)=>i==0?new XElement[]{new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}:new XElement[]{new(W+"br"),new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)})));
    }
    private static void AlignContractDate(XDocument xml,int width)
    {
        var p=xml.Descendants(W+"p").FirstOrDefault(x=>string.Concat(x.Descendants(W+"t").Select(t=>t.Value)).StartsWith("г. Москва\t",StringComparison.Ordinal));
        if(p is null)return;var text=string.Concat(p.Descendants(W+"t").Select(t=>t.Value));var pieces=text.Split('\t',2);
        var props=p.Element(W+"pPr");if(props is null){props=new XElement(W+"pPr");p.AddFirst(props);}
        props.Element(W+"tabs")?.Remove();props.Add(new XElement(W+"tabs",new XElement(W+"tab",new XAttribute(W+"val","right"),new XAttribute(W+"pos",width))));
        p.Elements(W+"r").Remove();p.Add(new XElement(W+"r",new XElement(W+"t",pieces[0]),new XElement(W+"tab"),new XElement(W+"t",pieces[1])));
    }
    public static bool TrySave(IssueDocumentInfo document,string destination)
    {
        if(!CanUseSource(document.Kind,document.Template))return false;
        CheckExternalEdits(destination);var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var source=Template(document.Kind))using(var target=File.Create(temporary))source.CopyTo(target);
            using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Update))
            {
                var xml=Xml(zip,"word/document.xml");var values=Values(document.Invoice);
                if(document.Kind==IssueDocumentKinds.Act)values["date"]=EventCalendarDate(document.Invoice);
                if(document.Kind==IssueDocumentKinds.Waybill)
                {values["senderDetails"]=values["senderDetails"].Replace("\n",", ");values["recipientDetails"]=values["recipientDetails"].Replace("\n",", ");}
                foreach(var paragraph in xml.Descendants(W+"p"))
                {var text=string.Concat(paragraph.Descendants(W+"t").Select(t=>t.Value));var current=CurrentText(document.Kind,text);if(current!=text)SetParagraph(paragraph,current);}
                if(document.Template!=DefaultText(document.Kind))
                {
                    var lines=document.Template.Replace("\r\n","\n").Replace('\r','\n').Split('\n');var paragraphs=xml.Descendants(W+"p").ToArray();
                    for(var i=0;i<paragraphs.Length;i++)SetParagraph(paragraphs[i],lines[i]);
                }
                // Expand the one source item row; it stays a native editable table row.
                var item=xml.Descendants(W+"tr").FirstOrDefault(r=>string.Concat(r.Descendants(W+"t").Select(t=>t.Value)).Contains("{itemDescription}",StringComparison.Ordinal));
                if(item is not null)
                {
                    if(document.Kind==IssueDocumentKinds.Waybill)
                    {
                        // Excel's two-row merged heading must remain one complete
                        // Word table heading, including on continuation pages.
                        var headerRows=item.ElementsBeforeSelf(W+"tr").TakeLast(6).ToArray();
                        var sourceTable=item.Parent!;
                        var detailsTable=new XElement(W+"tbl",sourceTable.Elements().Where(e=>e.Name!=W+"tr").Select(e=>new XElement(e)));
                        sourceTable.AddAfterSelf(detailsTable);
                        foreach(var row in headerRows[0].ElementsAfterSelf(W+"tr").Prepend(headerRows[0]).ToArray()){row.Remove();detailsTable.Add(row);}
                        foreach(var height in detailsTable.Descendants(W+"trHeight").ToArray())height.Remove();
                        foreach(var run in detailsTable.Descendants(W+"r"))
                        {
                            var format=run.Element(W+"rPr");if(format is null){format=new XElement(W+"rPr");run.AddFirst(format);}
                            format.Element(W+"sz")?.Remove();format.Add(new XElement(W+"sz",new XAttribute(W+"val",16)));
                        }
                        foreach(var header in headerRows)
                        {
                            var props=header.Element(W+"trPr");if(props is null){props=new XElement(W+"trPr");header.AddFirst(props);}
                            props.Add(new XElement(W+"tblHeader"),new XElement(W+"cantSplit"));
                            foreach(var p in header.Descendants(W+"p")){var pPr=p.Element(W+"pPr");if(pPr is null){pPr=new XElement(W+"pPr");p.AddFirst(pPr);}pPr.Add(new XElement(W+"keepNext"));}
                        }
                    }
                    var index=0;
                    foreach(var line in BusinessLines(document.Invoice))
                    {
                        var row=new XElement(item);var rowProps=row.Element(W+"trPr");if(rowProps is null){rowProps=new XElement(W+"trPr");row.AddFirst(rowProps);}rowProps.Add(new XElement(W+"cantSplit"));var itemValues=new Dictionary<string,string>(values,StringComparer.OrdinalIgnoreCase){["itemNumber"]=(++index).ToString(),["itemDescription"]=$"Театральные билеты на культурно-массовое мероприятие «{document.Invoice.Title}». {document.Invoice.Venue}. {document.Invoice.EventDate}.",["itemCount"]=line.Count.ToString(),["itemPrice"]=DocumentTotals.Amount(line.Price),["itemTotal"]=DocumentTotals.Amount(line.Total)};
                        foreach(var p in row.Descendants(W+"p").ToArray()){var text=string.Concat(p.Descendants(W+"t").Select(t=>t.Value));if(text.Contains('{'))SetParagraph(p,Replace(text,itemValues));}
                        // The source description spans several blank Excel rows. Each generated item has its own expandable row.
                        foreach(var merge in row.Descendants(W+"vMerge").ToArray())merge.Remove();
                        foreach(var height in row.Descendants(W+"trHeight").ToArray())height.Remove();
                        item.AddBeforeSelf(row);
                    }
                    item.Remove();
                }
                foreach(var p in xml.Descendants(W+"p").ToArray())
                {
                    var text=string.Concat(p.Descendants(W+"t").Select(t=>t.Value));
                    if(!text.Contains('{'))continue;
                    var replacement=Replace(text,values);
                    if(replacement.Length==0)
                    {
                        // Remove the entire labelled requisite row, including its label, when unfilled.
                        var row=p.Ancestors(W+"tr").FirstOrDefault();
                        if(row is not null&&Regex.IsMatch(text,@"^\{(?:sender|recipient)(?:InnKpp|Bank|Bik|Account|CorrespondentAccount|Address)\}$")){row.Remove();continue;}
                    }
                    SetParagraph(p,replacement);
                }
                // Carry custom populated requisites in the bill along with its source bank block.
                if(document.Kind==IssueDocumentKinds.Bill)
                {
                    foreach(var prefix in new[]{"sender","recipient"})
                    {
                        var details=prefix=="sender"?document.Invoice.SenderDetails:document.Invoice.RecipientDetails;
                        if(details?.ExtraFields is null)continue;
                        var name=prefix=="sender"?document.Invoice.Sender:document.Invoice.Recipient;
                        var p=xml.Descendants(W+"p").FirstOrDefault(x=>string.Concat(x.Descendants(W+"t").Select(t=>t.Value))==name);
                        if(p is not null)foreach(var field in details.ExtraFields.Where(f=>!string.IsNullOrWhiteSpace(f.Label)&&!string.IsNullOrWhiteSpace(f.Value)))p.AddAfterSelf(InvoiceDocuments.Paragraph(field.Label+": "+field.Value,false,20));
                    }
                }
                if(document.Kind==IssueDocumentKinds.Bill)
                {
                    var row=xml.Descendants(W+"tr").FirstOrDefault(r=>string.Concat(r.Descendants(W+"t").Select(t=>t.Value)).Contains("НДС не облагается.",StringComparison.Ordinal));
                    if(row is not null)row.ReplaceNodes(new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"gridSpan",new XAttribute(W+"val",9))),InvoiceDocuments.Paragraph("НДС не облагается.",false,20)));
                    foreach(var t in xml.Descendants(W+"t").Where(t=>t.Value=="Итого к оплате:"))t.Value="";
                }
                if(document.Kind==IssueDocumentKinds.Waybill)
                {
                    foreach(var height in xml.Descendants(W+"trHeight").ToArray())height.Remove();
                    foreach(var run in xml.Descendants(W+"r"))
                    {
                        var format=run.Element(W+"rPr");if(format is null){format=new XElement(W+"rPr");run.AddFirst(format);}
                        format.Element(W+"rFonts")?.Remove();format.Element(W+"sz")?.Remove();
                        format.Add(new XElement(W+"rFonts",new XAttribute(W+"ascii","Times New Roman"),new XAttribute(W+"hAnsi","Times New Roman")),new XElement(W+"sz",new XAttribute(W+"val",16)));
                    }
                }
                NormalizeSignatures(xml,document);
                var entry=zip.GetEntry("word/document.xml")!;entry.Delete();using(var s=zip.CreateEntry("word/document.xml").Open())xml.Save(s);
                Stamp(zip);
            }
            File.Move(temporary,destination,true);return true;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static void NormalizeSignatures(XDocument xml,IssueDocumentInfo document)
    {
        var body=xml.Root!.Element(W+"body")!;var section=body.Element(W+"sectPr");
        var page=section?.Element(W+"pgSz");var margins=section?.Element(W+"pgMar");
        int Number(XElement? e,string a,int fallback)=>int.TryParse((string?)e?.Attribute(W+a),out var value)?value:fallback;
        var width=Number(page,"w",11906)-Number(margins,"left",1134)-Number(margins,"right",1134);
        if(document.Kind==IssueDocumentKinds.Contract)AlignContractDate(xml,width);
        var signature= document.Kind==IssueDocumentKinds.Contract?body.Elements(W+"tbl").LastOrDefault():null;
        if(signature is not null)signature.Remove();
        else
        {
            var start=body.Descendants(W+"tr").FirstOrDefault(row=>{
                var text=string.Concat(row.Descendants(W+"t").Select(t=>t.Value));
                return document.Kind switch{IssueDocumentKinds.Act=>text.Contains("Исполнитель")&&text.Contains("Заказчик"),IssueDocumentKinds.Bill=>text.Contains("Руководитель предприятия"),IssueDocumentKinds.Waybill=>text.Contains("Отпуск груза разрешил"),_=>false};
            });
            if(start is not null)
            {
                var previous=start.PreviousNode as XElement;
                while(previous?.Name==W+"tr"&&!previous.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value)))
                {var prior=previous.PreviousNode as XElement;previous.Remove();previous=prior;}
                foreach(var row in start.ElementsAfterSelf(W+"tr").ToArray())row.Remove();start.Remove();
            }
        }
        // Exactly two blank paragraphs at the last text, with equal outer offsets.
        var last=section?.PreviousNode as XElement??body.Elements().LastOrDefault();
        while(last?.Name==W+"p"&&!last.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value)))
        {var previous=last.PreviousNode as XElement;last.Remove();last=previous;}
        var labels=BusinessDocumentTemplates.PartyLabels(document.Kind);
        var table=InvoiceDocuments.SignaturesTable(document.Invoice,labels.Seller,labels.Buyer,width);
        if(document.Kind==IssueDocumentKinds.Contract)foreach(var font in table.Descendants(W+"rFonts")){font.SetAttributeValue(W+"ascii","Times New Roman");font.SetAttributeValue(W+"hAnsi","Times New Roman");}
        if(document.Kind==IssueDocumentKinds.Bill)
        {
            var cells=table.Descendants(W+"tc").ToArray();
            SetParagraph(cells[0].Element(W+"p")!,"Руководитель предприятия: "+document.Invoice.SenderDetails?.Position+"\n________________ / "+document.Invoice.SenderDetails?.Signatory+" /");
            SetParagraph(cells[1].Element(W+"p")!,"Получил: "+document.Invoice.RecipientDetails?.Position+"\n________________ / "+document.Invoice.RecipientDetails?.Signatory+" /");
        }
        else if(document.Kind==IssueDocumentKinds.Waybill)
        {
            var cells=table.Descendants(W+"tc").ToArray();
            SetParagraph(cells[0].Element(W+"p")!,"Отпуск груза разрешил: "+document.Invoice.SenderDetails?.Position+"\n________________ / "+document.Invoice.SenderDetails?.Signatory+" /\nГлавный (старший) бухгалтер: ________________\nОтпуск груза произвел: ________________\nМ.П.");
            SetParagraph(cells[1].Element(W+"p")!,"Груз принял: ________________\nГруз получил грузополучатель: "+document.Invoice.RecipientDetails?.Position+"\n________________ / "+document.Invoice.RecipientDetails?.Signatory+" /\nМ.П.");
        }
        if(section is null)body.Add(InvoiceDocuments.Paragraph(""),InvoiceDocuments.Paragraph(""),table);
        else section.AddBeforeSelf(InvoiceDocuments.Paragraph(""),InvoiceDocuments.Paragraph(""),table);
    }
    // The manifest hashes every package part other than itself. External Word edits
    // alter it, so refreshing a preview cannot silently destroy an edited DOCX.
    private static string Digest(ZipArchive zip)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var entry in zip.Entries.Where(e=>e.FullName!=StampPath).OrderBy(e=>e.FullName,StringComparer.Ordinal))
        {hash.AppendData(Encoding.UTF8.GetBytes(entry.FullName));using var stream=entry.Open();var buffer=new byte[8192];int count;while((count=stream.Read(buffer))>0)hash.AppendData(buffer.AsSpan(0,count));}
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    internal static void Stamp(ZipArchive zip)
    {
        zip.GetEntry(StampPath)?.Delete();
        // A standard custom XML relationship makes Word retain the provenance
        // part when the user edits and saves the document.
        XNamespace rel="http://schemas.openxmlformats.org/package/2006/relationships";
        const string path="word/_rels/document.xml.rels";
        var relationships=zip.GetEntry(path) is null?new XDocument(new XElement(rel+"Relationships")):Xml(zip,path);
        if(!relationships.Root!.Elements().Any(e=>(string?)e.Attribute("Id")=="ticketsGeneratedManifest"))
        {
            relationships.Root.Add(new XElement(rel+"Relationship",new XAttribute("Id","ticketsGeneratedManifest"),new XAttribute("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXml"),new XAttribute("Target","../"+StampPath)));
            zip.GetEntry(path)?.Delete();using(var stream=zip.CreateEntry(path).Open())relationships.Save(stream);
        }
        var digest=Digest(zip);using var writer=new StreamWriter(zip.CreateEntry(StampPath).Open(),Encoding.ASCII);writer.Write(new XElement("generated",new XAttribute("sha256",digest)).ToString(SaveOptions.DisableFormatting));
    }
    internal static void CheckExternalEdits(string destination)
    {
        if(!File.Exists(destination))return;
        try
        {
            using var zip=ZipFile.OpenRead(destination);var entry=zip.GetEntry(StampPath);
            if(entry is not null){using var reader=new StreamReader(entry.Open());if((string?)XDocument.Parse(reader.ReadToEnd()).Root?.Attribute("sha256")==Digest(zip))return;}
            else
            {
                // Pre-manifest exports may include manual Word work. Preserve the
                // complete old file before replacing its canonical generated path.
                var backup=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination)!,System.IO.Path.GetFileNameWithoutExtension(destination)+".до-0.12-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".docx");
                File.Copy(destination,backup,false);return;
            }
        }
        catch(Exception ex) when(ex is InvalidDataException or System.Xml.XmlException){}
        throw new TicketException("Файл Word изменён вне программы и сохранён без перезаписи: "+destination+". Откройте его для продолжения работы. Чтобы создать новый бланк, сохраните изменённый файл под другим именем или переместите его.");
    }
}
