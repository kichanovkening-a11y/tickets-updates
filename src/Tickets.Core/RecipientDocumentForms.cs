using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace Tickets.Core;

public sealed record RecipientDocumentForm(string Kind,string FileName,byte[] Content);

// Uploaded forms belong to a stable directory ID; display aliases never rewrite legal text.
public sealed partial class Store
{
    private static void EnsureRecipientForms(SqliteConnection c)
    {
        using var cmd=Command(c,"""
            CREATE TABLE IF NOT EXISTS recipient_document_form(recipient_id INTEGER NOT NULL REFERENCES recipient(id),kind TEXT NOT NULL,file_name TEXT NOT NULL,content BLOB NOT NULL,PRIMARY KEY(recipient_id,kind));
            CREATE TABLE IF NOT EXISTS issue_custom_form(issue_id INTEGER NOT NULL REFERENCES issue_batch(id),kind TEXT NOT NULL,file_name TEXT NOT NULL,content BLOB NOT NULL,PRIMARY KEY(issue_id,kind));
            CREATE TABLE IF NOT EXISTS issue_contract_number(issue_id INTEGER PRIMARY KEY REFERENCES issue_batch(id),value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS issue_contract_date(issue_id INTEGER PRIMARY KEY REFERENCES issue_batch(id),value TEXT NOT NULL);
            """);cmd.ExecuteNonQuery();
    }
    public static string RecipientDocumentName(string kind)=>kind=="invoice"?"Накладная":kind=="specification"?"Спецификация":IssueDocumentKinds.Name(kind);
    public void SaveRecipientForm(string recipient,string kind,byte[] content,string fileName)
    {
        RecipientDocumentName(kind);CustomDocumentForms.Validate(content);recipient=SaveRecipient(recipient);
        using var c=Open();EnsureRecipientForms(c);
        using var cmd=Command(c,"""
            INSERT INTO recipient_document_form(recipient_id,kind,file_name,content) SELECT id,$k,$f,$b FROM recipient WHERE name_key=$n
            ON CONFLICT(recipient_id,kind) DO UPDATE SET file_name=excluded.file_name,content=excluded.content
            ""","$n",CatalogText.Key(recipient),"$k",kind,"$f",System.IO.Path.GetFileName(fileName),"$b",content);cmd.ExecuteNonQuery();
    }
    public static RecipientDocumentForm? DefaultRecipientForm(string kind)
    { RecipientDocumentName(kind);var bytes=StandardDocumentForms.Resolve(kind,null);return bytes is null?null:new(kind,RecipientDocumentName(kind)+".docx",bytes); }
    public RecipientDocumentForm? RecipientForm(string recipient,string kind)
    {
        RecipientDocumentName(kind);using var c=Open();EnsureRecipientForms(c);
        using var cmd=Command(c,"SELECT f.file_name,f.content FROM recipient_document_form f JOIN recipient r ON r.id=f.recipient_id WHERE r.name_key=$n AND f.kind=$k","$n",CatalogText.Key(recipient),"$k",kind);
        using var r=cmd.ExecuteReader();return r.Read()?new(kind,r.GetString(0),(byte[])r[1]):null;
    }
    public void DeleteRecipientForm(string recipient,string kind)
    {using var c=Open();EnsureRecipientForms(c);using var cmd=Command(c,"DELETE FROM recipient_document_form WHERE recipient_id=(SELECT id FROM recipient WHERE name_key=$n) AND kind=$k","$n",CatalogText.Key(recipient),"$k",kind);cmd.ExecuteNonQuery();}
    public void RenameRecipient(string oldName,string newName)
    {
        newName=Regex.Replace(Required(newName,"Получатель").Normalize(),@"\s+"," ");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var oldKey=CatalogText.Key(oldName);var newKey=CatalogText.Key(newName);
        using(var check=Command(c,"SELECT id FROM recipient WHERE name_key=$k AND name_key<>$old","$k",newKey,"$old",oldKey)){check.Transaction=tx;if(check.ExecuteScalar() is not null)throw new TicketException("Получатель с таким названием уже существует.");}
        using(var cmd=Command(c,"UPDATE recipient SET name=$n,name_key=$k WHERE name_key=$old","$n",newName,"$k",newKey,"$old",oldKey)){cmd.Transaction=tx;if(cmd.ExecuteNonQuery()!=1)throw new TicketException("Получатель не найден.");}
        foreach(var table in new[]{"issue_batch","ticket"}){using var cmd=Command(c,$"UPDATE {table} SET recipient=$new WHERE recipient=$old","$new",newName,"$old",oldName);cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        using(var cmd=Command(c,"UPDATE specification SET recipient=$n,recipient_key=$k WHERE recipient_key=$old","$n",newName,"$k",newKey,"$old",oldKey)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        using(var cmd=Command(c,"UPDATE organization_details SET name_key=$k WHERE role='recipient' AND name_key=$old","$k",newKey,"$old",oldKey)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        using(var cmd=Command(c,"UPDATE catalog_hidden SET entity_key=$k WHERE kind='recipient' AND entity_key=$old","$k",newKey,"$old",oldKey)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        tx.Commit();
    }
    public bool CanUseBusinessDocuments(long eventId,long issueId)=>Invoice(eventId,issueId).RecipientDetails?.HasRequisites==true;
    public DateTime ContractDate(long eventId,long issueId)
    {
        var invoice=Invoice(eventId,issueId);using var c=Open();EnsureRecipientForms(c);using var cmd=Command(c,"SELECT value FROM issue_contract_date WHERE issue_id=$i","$i",issueId);
        return DateTime.TryParse(cmd.ExecuteScalar() as string,out var date)?date:DateTime.Parse(invoice.Date,System.Globalization.CultureInfo.GetCultureInfo("ru-RU")).Date;
    }
    public void SetContractDate(long eventId,long issueId,DateTime date)
    {
        Invoice(eventId,issueId);using var c=Open();EnsureRecipientForms(c);
        using var cmd=Command(c,"INSERT INTO issue_contract_date(issue_id,value) VALUES($i,$d) ON CONFLICT(issue_id) DO UPDATE SET value=excluded.value","$i",issueId,"$d",date.ToString("yyyy-MM-dd"));cmd.ExecuteNonQuery();
    }
    private RecipientDocumentForm? SnapshotRecipientForm(InvoiceInfo invoice,string kind)
    {
        using var c=Open();EnsureRecipientForms(c);
        using var read=Command(c,"SELECT file_name,content FROM issue_custom_form WHERE issue_id=$i AND kind=$k","$i",invoice.Id,"$k",kind);
        using var rows=read.ExecuteReader();return rows.Read()?new(kind,rows.GetString(0),(byte[])rows[1]):null;
    }
    private static void CaptureRecipientForms(SqliteConnection c,SqliteTransaction tx,long issueId,string recipient)
    {
        using var cmd=Command(c,"INSERT OR IGNORE INTO issue_custom_form(issue_id,kind,file_name,content) SELECT $i,f.kind,f.file_name,f.content FROM recipient_document_form f JOIN recipient r ON r.id=f.recipient_id WHERE r.name_key=$n","$i",issueId,"$n",CatalogText.Key(recipient));cmd.Transaction=tx;cmd.ExecuteNonQuery();
    }
    private static void CopyCustomIssueForms(SqliteConnection c,SqliteTransaction tx,long source,long target)
    {
        using var copy=Command(c,"INSERT INTO issue_custom_form(issue_id,kind,file_name,content) SELECT $t,kind,file_name,content FROM issue_custom_form WHERE issue_id=$s; INSERT INTO issue_contract_date(issue_id,value) SELECT $t,value FROM issue_contract_date WHERE issue_id=$s; INSERT INTO issue_contract_number(issue_id,value) SELECT $t,value FROM issue_contract_number WHERE issue_id=$s","$t",target,"$s",source);copy.Transaction=tx;copy.ExecuteNonQuery();
    }
    public byte[]? CustomIssueTemplate(long eventId,long issueId,string kind)
    {
        var invoice=Invoice(eventId,issueId);
        if(IssueDocumentKinds.All.Contains(kind)&&CustomIssueDocument(eventId,issueId,kind) is null)
        {
            var text=GetIssueDocument(eventId,issueId,kind).Template;
            if(!SourceBusinessForms.CanUseSource(kind,text))
            {
                if(!Regex.IsMatch(text,@"\{\{?summary\}\}?",RegexOptions.IgnoreCase))text+="\n\n{summary}";
                if(!Regex.IsMatch(text,@"\{\{?total\}\}?",RegexOptions.IgnoreCase))text+="\n{total}";
            }
            return InvoiceDocuments.TextEditorTemplate(kind,text);
        }
        return StandardDocumentForms.Resolve(kind,SnapshotRecipientForm(invoice,kind)?.Content)??StandardDocumentForms.EditorTemplate(kind);
    }
    public IssueDocumentInfo? CustomIssueDocument(long eventId,long issueId,string kind)
    {
        var invoice=Invoice(eventId,issueId);var form=SnapshotRecipientForm(invoice,kind);
        // Explicit saved text takes precedence over the standard starter.
        if(form is null&&kind!="specification"&&kind!="invoice") { using var c=Open();using var read=Command(c,"SELECT template FROM issue_document WHERE event_id=$e AND issue_id=$i AND kind=$k","$e",eventId,"$i",issueId,"$k",kind);var selected=read.ExecuteScalar() as string;if(selected is null){using var global=Command(c,"SELECT template FROM document_template WHERE kind=$k","$k",kind);selected=global.ExecuteScalar() as string;}if(selected is not null&&selected!=IssueDocumentKinds.DefaultTemplate(kind))return null; }
        var template=StandardDocumentForms.Resolve(kind,form?.Content);if(template is null)return null;
        var bytes=CustomDocumentForms.Fill(template,invoice,kind,ContractDate(eventId,issueId));
        var templateText=form is null&&IssueDocumentKinds.All.Contains(kind)?IssueDocumentKinds.DefaultTemplate(kind):CustomDocumentForms.Text(template);
        return new(kind,invoice,templateText,CustomDocumentForms.Text(bytes)){SourceDocument=bytes};
    }
    public void SaveCustomIssueForm(long eventId,long issueId,string kind,byte[] content)
    {
        CustomDocumentForms.Validate(content);Invoice(eventId,issueId);using var c=Open();EnsureRecipientForms(c);
        using var cmd=Command(c,"INSERT INTO issue_custom_form(issue_id,kind,file_name,content) VALUES($i,$k,$f,$b) ON CONFLICT(issue_id,kind) DO UPDATE SET content=excluded.content","$i",issueId,"$k",kind,"$f",RecipientDocumentName(kind)+".docx","$b",content);cmd.ExecuteNonQuery();
        if(kind=="contract")SynchronizeContractRepresentative(eventId,issueId,CustomDocumentForms.Text(content));
    }
}

public static class CustomDocumentForms
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static bool Part(string name)=>name is "word/document.xml" or "word/styles.xml" or "word/numbering.xml"||Regex.IsMatch(name,@"^word/(header|footer)\d+\.xml$");
    public static void Validate(byte[] content)
    {
        if(content is null||content.Length==0||content.Length>20*1024*1024)throw new TicketException("Загрузите документ Word размером до 20 МБ.");
        try{using var ms=new MemoryStream(content);using var z=new ZipArchive(ms,ZipArchiveMode.Read);var entry=z.GetEntry("word/document.xml")??throw new InvalidDataException();if(entry.Length>30*1024*1024)throw new InvalidDataException();using var s=entry.Open();var xml=XDocument.Load(s);if(xml.Root?.Name!=W+"document")throw new InvalidDataException();}
        catch(Exception ex) when(ex is InvalidDataException or System.Xml.XmlException or IOException){throw new TicketException("Не удалось прочитать DOCX. Выберите исправный документ Word.");}
    }
    public static string Text(byte[] content){using var ms=new MemoryStream(content);using var z=new ZipArchive(ms,ZipArchiveMode.Read);using var s=z.GetEntry("word/document.xml")!.Open();return string.Join("\n",XDocument.Load(s).Descendants(W+"p").Select(p=>string.Concat(p.Descendants(W+"t").Select(t=>t.Value))));}
    // Replace from the end across runs, retaining original surrounding run formatting.
    private static void Replace(XElement paragraph,IReadOnlyDictionary<string,string> values)
    {
        var nodes=paragraph.Descendants(W+"t").ToArray();var text=string.Concat(nodes.Select(n=>n.Value));
        var matches=Regex.Matches(text,@"\{\{?(\w+)\}\}?").Cast<Match>().Where(m=>values.ContainsKey(m.Groups[1].Value)).Reverse();
        foreach(var match in matches)
        {
            var start=match.Index;var end=start+match.Length;var offset=0;
            for(var i=0;i<nodes.Length;i++)
            {
                var n=nodes[i];var len=n.Value.Length;var next=offset+len;
                if(next>start&&offset<end){var from=Math.Max(0,start-offset);var to=Math.Min(len,end-offset);n.Value=n.Value[..from]+(offset<=start?values[match.Groups[1].Value]:"")+n.Value[to..];n.SetAttributeValue(XNamespace.Xml+"space","preserve");}
                offset=next;
            }
        }
    }
    public static byte[] Map(byte[] content,string existingText,string token)
    {
        Validate(content);if(string.IsNullOrWhiteSpace(existingText))throw new TicketException("Укажите заменяемый текст.");
        return Rewrite(content,xml=>{foreach(var p in xml.Descendants(W+"p")){var nodes=p.Descendants(W+"t").ToArray();var text=string.Concat(nodes.Select(n=>n.Value));if(!text.Contains(existingText,StringComparison.Ordinal))continue; // Keep styling by using a temporary token across original runs.
            var marked=text.Replace(existingText,"{"+token+"}",StringComparison.Ordinal);if(nodes.Length>0){nodes[0].Value=marked;foreach(var n in nodes.Skip(1))n.Value="";}}});
    }
    private static byte[] Rewrite(byte[] content,Action<XDocument> change)
    {
        using var output=new MemoryStream();output.Write(content);output.Position=0;
        using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))foreach(var entry in zip.Entries.Where(e=>Part(e.FullName)).ToArray())
        {XDocument xml;using(var s=entry.Open())xml=XDocument.Load(s);change(xml);WordDocumentStructure.Normalize(xml);WordDocumentStructure.Validate(xml);var name=entry.FullName;entry.Delete();using var target=zip.CreateEntry(name).Open();xml.Save(target);}
        return output.ToArray();
    }
    public static byte[] Fill(byte[] content,InvoiceInfo invoice,string kind,DateTime contractDate)
    {
        content=StandardDocumentForms.Resolve(kind,content)??content;
        var standard=StandardDocumentForms.IsStandard(content,kind);
        var values=SourceBusinessForms.Values(invoice,kind);
        values["items"]=BusinessDocumentTemplates.Items(invoice,kind);values["title"]=invoice.Title;
        if(standard&&kind=="ttn"){values["senderDetails"]=values["senderDetails"].Replace("\n",", ");values["recipientDetails"]=values["recipientDetails"].Replace("\n",", ");}
        var calendar=SourceBusinessForms.EventCalendarDate(invoice);
        values["contractDate"]=contractDate.ToString("dd.MM.yyyy");values["eventCalendarDate"]=calendar;
        if(kind=="act"){values["date"]=calendar;}else if(kind=="contract")values["date"]=values["contractDate"];
        var seatGroups=InvoiceDocuments.Lines(invoice);
        var priceGroups=invoice.Tickets.GroupBy(t=>t.Amount).OrderByDescending(g=>g.Key).Select(g=>new InvoiceLine("","","",g.Count(),g.Key,g.Sum(t=>(decimal)t.Amount),"")).ToArray();
        values["date"]=kind=="act"?calendar:kind=="contract"?values["contractDate"]:DateTimeOffset.Parse(invoice.IssuedAt).ToString("dd.MM.yyyy");
        values["lineCountWords"]=RussianWords.Number(kind=="bill"?1:priceGroups.Length);
        return Rewrite(content,xml=>{
            DocumentFormCorrections.UpgradeVenueFields(xml,kind);
            if(kind is "invoice" or "specification")DocumentFormCorrections.RestoreInvoiceBorders(xml);
            foreach(var row in xml.Descendants(W+"tr").Where(r=>Regex.IsMatch(string.Concat(r.Descendants(W+"t").Select(t=>t.Value)),@"\{item(Section|DescriptionShort|Description|Row|Count)\}")).ToArray())
            {
                var rowText=string.Concat(row.Descendants(W+"t").Select(t=>t.Value));
                var groups=kind=="bill"?new[]{new InvoiceLine("","","",invoice.Tickets.Count,invoice.Tickets.Select(t=>t.Amount).Distinct().Count()==1?invoice.Tickets[0].Amount:0,invoice.Total,"")}:rowText.Contains("{itemRow}")||rowText.Contains("{itemSection}")||rowText.Contains("{itemSeat}")?seatGroups:priceGroups;
                var index=0;
                var packageSeats=invoice.PackageShows.Count>0&&(kind is "invoice" or "specification")&&groups==seatGroups;
                var showLines=packageSeats?invoice.PackageShows.SelectMany(show=>InvoiceDocuments.Lines(show).Select((line,n)=>(Line:line,Show:n==0?show:null))).ToArray():groups.Select(line=>(Line:line,Show:(InvoiceInfo?)null)).ToArray();
                foreach(var item in showLines)
                {
                    var line=item.Line;
                    if(item.Show is {} show)
                    {
                        var cells=row.Elements(W+"tc").ToArray();var span=cells.Sum(c=>(int?)c.Element(W+"tcPr")?.Element(W+"gridSpan")?.Attribute(W+"val")??1);
                        var width=cells.Sum(c=>(int?)c.Element(W+"tcPr")?.Element(W+"tcW")?.Attribute(W+"w")??0);
                        var props=cells[0].Element(W+"tcPr") is {} cp?new XElement(cp):new XElement(W+"tcPr");props.Elements(W+"gridSpan").Remove();props.Elements(W+"tcW").Remove();props.Add(new XElement(W+"tcW",new XAttribute(W+"w",width),new XAttribute(W+"type","dxa")),new XElement(W+"gridSpan",new XAttribute(W+"val",span)));
                        var paragraph=InvoiceDocuments.Paragraph($"{show.Title} · {show.EventDate} · {show.VenueWithAddress}",true,20);paragraph.Element(W+"pPr")!.Add(new XElement(W+"keepNext"));
                        row.AddBeforeSelf(new XElement(W+"tr",new XElement(W+"trPr",new XElement(W+"cantSplit")),new XElement(W+"tc",props,paragraph)));
                    }
                    var copy=new XElement(row);copy.Element(W+"trPr")?.Elements(W+"trHeight").Remove();if(standard&&kind=="ttn"){var props=copy.Element(W+"trPr");if(props is null){props=new XElement(W+"trPr");copy.AddFirst(props);}props.Add(new XElement(W+"cantSplit"));}var rowValues=new Dictionary<string,string>(values){["itemNumber"]=(++index).ToString(),["itemSection"]=line.Sector,["itemRow"]=line.Row,["itemSeat"]=line.Seats,["itemCount"]=line.Count.ToString(),["itemPrice"]=kind=="bill"&&invoice.Tickets.Select(t=>t.Amount).Distinct().Count()>1?"—":DocumentTotals.Amount(line.Price),["itemTotal"]=DocumentTotals.Amount(line.Total),["itemDescriptionShort"]="Театральные билеты",["itemDescription"]=standard&&kind=="ttn"?"Театральные билеты":$"Театральные билеты на мероприятие «{invoice.Title}». {invoice.VenueWithAddress}. {invoice.EventDate}."};
                    foreach(var p in copy.Descendants(W+"p"))Replace(p,rowValues);row.AddBeforeSelf(copy);
                }
                row.Remove();
            }
            // Older built-in specifications omitted the event venue. Upgrade the
            // canonical event line even in saved issue templates with ordinary edits.
            if(kind=="specification")foreach(var paragraph in xml.Descendants(W+"p"))
            {
                var nodes=paragraph.Descendants(W+"t").ToArray();
                if(string.Concat(nodes.Select(t=>t.Value))=="Спектакль «{event}» {eventDate}"&&nodes.Length>0)nodes[^1].Value+=". {venue}.";
            }
            if(standard)DocumentFormCorrections.SpaceSignatures(xml);
            if(kind=="bill")SignatureBlocks.KeepRightBillSignature(xml,invoice);
            SignatureBlocks.FillTemplateSignatures(xml,invoice);
            foreach(var p in xml.Descendants(W+"p"))Replace(p,values);
            foreach(var text in xml.Descendants(W+"t").Where(t=>t.Value.Contains('\n')).ToArray())
            {
                var lines=text.Value.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
                text.ReplaceWith(lines.SelectMany((line,index)=>index==0?new XElement[]{new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}:new XElement[]{new(W+"br"),new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}));
            }
            if(standard&&kind=="ttn"&&xml.Root?.Name==W+"document")
            {
                xml.Descendants(W+"trHeight").Remove();
                foreach(var row in xml.Descendants(W+"tr").Where(r=>!r.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value))).ToArray())row.Remove();
                foreach(var size in xml.Descendants(W+"sz"))size.SetAttributeValue(W+"val",16);
                if(standard)DocumentFormCorrections.SpaceSignatures(xml);
            }
            if(kind=="contract"&&xml.Root?.Name==W+"ftr")foreach(var p in xml.Descendants(W+"p"))
            {
                if(!p.Descendants(W+"instrText").Any(t=>t.Value.Contains("PAGE",StringComparison.OrdinalIgnoreCase)))continue;
                var pr=p.Element(W+"pPr");if(pr is null){pr=new XElement(W+"pPr");p.AddFirst(pr);}pr.Elements(W+"jc").Remove();pr.Add(new XElement(W+"jc",new XAttribute(W+"val","left")));
            }
        });
    }
}
