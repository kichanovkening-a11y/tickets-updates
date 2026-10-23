using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Tickets.Core;

public sealed record InvoiceLine(string Sector,string Row,string Seats,int Count,decimal Price,decimal Total,string Status);

public static class InvoiceDocuments
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly CultureInfo Ru=CultureInfo.GetCultureInfo("ru-RU");
    public static string Money(decimal amount)=>amount.ToString("N0",Ru)+" ₽";
    public static string[] Headers=>["Сектор","Ряд","Места","Кол-во","Цена, ₽","Сумма, ₽"];
    public static IReadOnlyList<InvoiceLine> Lines(InvoiceInfo invoice)=>invoice.PackageShows.Count>0?invoice.PackageShows.SelectMany(Lines).ToArray():invoice.Tickets
        .GroupBy(t=>new{t.Sector,t.Row,t.Amount})
        .Select(group=>new InvoiceLine(group.Key.Sector,group.Key.Row,
            string.Join(", ",group.Select(t=>t.Seat).OrderBy(s=>s,NaturalComparer.Instance)),group.Count(),group.Key.Amount,
            group.Sum(t=>(decimal)t.Amount),string.Join(", ",group.Select(t=>t.Status).Distinct())))
        .OrderBy(line=>line.Sector,NaturalComparer.Instance).ThenBy(line=>line.Row,NaturalComparer.Instance)
        .ThenBy(line=>line.Price).ThenBy(line=>line.Status,StringComparer.Ordinal).ToArray();
    public static string[] Row(InvoiceLine line)=>[line.Sector,line.Row,line.Seats,line.Count.ToString(Ru),line.Price.ToString("N0",Ru),line.Total.ToString("N0",Ru)];
    public static string[][] Rows(InvoiceInfo invoice)=>Lines(invoice).Select(Row).ToArray();
    public static void SaveWord(InvoiceInfo invoice,string destination)
    {
        var body=new XElement(W+"body",
            Paragraph($"НАКЛАДНАЯ № {invoice.Id}",true,32),
            Paragraph("на передачу билетов",false,24),Paragraph("Дата выдачи: "+invoice.Date),
            Paragraph("Мероприятие: "+invoice.Title,true),Paragraph("Дата и время: "+invoice.EventDate),
            Paragraph("Площадка: "+invoice.VenueWithAddress),Paragraph("Отправитель: "+invoice.Sender),
            Paragraph("Получатель: "+invoice.Recipient),Paragraph(""));

        var widths=new[]{1500,800,2700,1000,1300,1300};
        var table=new XElement(W+"tbl",new XElement(W+"tblPr",
            new XElement(W+"tblW",new XAttribute(W+"w",widths.Sum()),new XAttribute(W+"type","dxa")),
            new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),
            new XElement(W+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","B8C1CC"))))),
            new XElement(W+"tblGrid",widths.Select(width=>new XElement(W+"gridCol",new XAttribute(W+"w",width)))));
        XElement TableRow(string[] values,bool header)=>new(W+"tr",new XElement(W+"trPr",header?new XElement(W+"tblHeader"):null,values.All(v=>v.Length<100)?new XElement(W+"cantSplit"):null),
            values.Select((text,i)=>new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",widths[i]),new XAttribute(W+"type","dxa")),
                new XElement(W+"vAlign",new XAttribute(W+"val","top")),header?new XElement(W+"shd",new XAttribute(W+"fill","E8EDF3")):null),Paragraph(text,header,20))));
        table.Add(TableRow(Headers,true));foreach(var row in Rows(invoice))table.Add(TableRow(row,false));body.Add(table);
        body.Add(Paragraph(DocumentTotals.Summary(invoice.Tickets.Count,invoice.Total)),
            Paragraph(DocumentTotals.Total(invoice.Tickets.Count,invoice.Total),true));
        if(invoice.ActiveCount!=invoice.Tickets.Count)
            body.Add(Paragraph($"Возвращено: {invoice.Tickets.Count-invoice.ActiveCount} ({RussianWords.Quantity(invoice.Tickets.Count-invoice.ActiveCount)})."),
                Paragraph($"Осталось на руках: {invoice.ActiveCount} ({RussianWords.Quantity(invoice.ActiveCount)}), на сумму {Money(invoice.ActiveTotal)} ({RussianWords.Money(invoice.ActiveTotal)})."));
        body.Add(Paragraph(""),Paragraph(""),SignaturesTable(invoice,"Передал","Получил"));
        SaveDocument(body,destination);
    }
    public static void SaveIssueDocumentWord(IssueDocumentInfo document,string destination)
    {
        if(document.SourceDocument is {} custom)
        {
            SourceBusinessForms.CheckExternalEdits(destination);
            var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temporary,custom);using(var z=ZipFile.Open(temporary,ZipArchiveMode.Update))if(z.GetEntry("[Content_Types].xml") is not null)SourceBusinessForms.Stamp(z);File.Move(temporary,destination,true);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}return;
        }
        if(SourceBusinessForms.TrySave(document,destination))return;
        var body=new XElement(W+"body");
        var total=DocumentTotals.Total(document.Invoice.Tickets.Count,document.Invoice.Total);
        var firstLine=true;
        var lines=BusinessDocumentTemplates.Layout(document);
        for(var index=0;index<lines.Count;index++)
        {
            if(lines[index].Signatures){var labels=BusinessDocumentTemplates.PartyLabels(document.Kind);body.Add(Paragraph(""),Paragraph(""),SignaturesTable(document.Invoice,labels.Seller,labels.Buyer,document.Kind==IssueDocumentKinds.Waybill?14050:9600));continue;}
            if(lines[index].Requisites){var labels=BusinessDocumentTemplates.PartyLabels(document.Kind);body.Add(RequisitesTable(document.Invoice,labels.Seller,labels.Buyer,document.Kind is IssueDocumentKinds.Contract or IssueDocumentKinds.Act,document.Kind==IssueDocumentKinds.Waybill?14050:9600));continue;}
            var line=lines[index].Text;
            if(line.StartsWith("№ | Билеты и места |",StringComparison.Ordinal))
            {
                var rows=new List<string[]>();
                var columns=line.Split('|').Length;
                do{rows.Add(lines[index].Text.Split('|').Select(value=>value.Trim()).ToArray());index++;}
                while(index<lines.Count&&!lines[index].Requisites&&lines[index].Text.Split('|').Length==columns);
                index--;body.Add(BusinessTable(rows,document.Kind));continue;
            }
            var formNote=document.Kind==IssueDocumentKinds.Waybill&&(line.StartsWith("Унифицированная форма",StringComparison.Ordinal)||line.StartsWith("Утверждена постановлением",StringComparison.Ordinal)||line.StartsWith("Форма по ОКУД",StringComparison.Ordinal));
            var title=IsDocumentTitle(line);
            var heading=(!formNote&&firstLine)||title||IsBusinessHeading(line);
            var paragraph=Paragraph(line,heading||line.Contains(total,StringComparison.Ordinal),title?30:formNote?16:20);
            if(title||formNote)paragraph.Element(W+"pPr")!.Add(new XElement(W+"jc",new XAttribute(W+"val",title?"center":"right")));
            body.Add(paragraph);firstLine=false;
        }
        if(document.Kind==IssueDocumentKinds.Contract)
        {
            foreach(var font in body.Descendants(W+"rFonts")){font.SetAttributeValue(W+"ascii","Times New Roman");font.SetAttributeValue(W+"hAnsi","Times New Roman");}
            foreach(var size in body.Descendants(W+"sz"))
            {var value=(string?)size.Attribute(W+"val");size.SetAttributeValue(W+"val",value=="30"?26:size.Ancestors(W+"tbl").Any()?22:24);}
        }
        SaveDocument(body,destination,document.Kind==IssueDocumentKinds.Waybill,document.Kind==IssueDocumentKinds.Contract);
    }
    public static bool UsesSourceForm(IssueDocumentInfo document)=>document.SourceDocument is not null||SourceBusinessForms.CanUseSource(document.Kind,document.Template);
    public static XDocument IssueDocumentXml(IssueDocumentInfo document)
    {
        var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Tickets-preview-"+Guid.NewGuid().ToString("N")+".docx");
        try
        {
            SaveIssueDocumentWord(document,path);using var archive=ZipFile.OpenRead(path);using var stream=archive.GetEntry("word/document.xml")!.Open();return XDocument.Load(stream);
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
    public static bool IsDocumentTitle(string line)=>line.StartsWith("ДОГОВОР №",StringComparison.Ordinal)||line.StartsWith("АКТ №",StringComparison.Ordinal)||line.StartsWith("ТОВАРНАЯ НАКЛАДНАЯ №",StringComparison.Ordinal)||line.StartsWith("СЧЁТ НА ОПЛАТУ №",StringComparison.Ordinal);
    internal static XElement RequisitesTable(InvoiceInfo invoice,string seller,string buyer,bool signatures=false,int width=9600)
    {
        var details=new[]{invoice.SenderDetails??new OrganizationDetails(Name:invoice.Sender),invoice.RecipientDetails??new OrganizationDetails(Name:invoice.Recipient)};
        var labels=new[]{seller,buyer};
        var table=new XElement(W+"tbl",new XElement(W+"tblPr",
            new XElement(W+"tblW",new XAttribute(W+"w",width),new XAttribute(W+"type","dxa")),
            new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),
            new XElement(W+"tblCellMar",new[]{"top","left","bottom","right"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"w",100),new XAttribute(W+"type","dxa"))))),
            new XElement(W+"tblGrid",new XElement(W+"gridCol",new XAttribute(W+"w",width/2)),new XElement(W+"gridCol",new XAttribute(W+"w",width/2))),
            new XElement(W+"tr",new XElement(W+"trPr",new XElement(W+"cantSplit")),details.Select((detail,index)=>
                new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",width/2),new XAttribute(W+"type","dxa")),new XElement(W+"vAlign",new XAttribute(W+"val","top"))),
                    Paragraph(labels[index],true,20),BusinessDocumentTemplates.Requisites(detail).Split('\n').Select(line=>Paragraph(line,false,19)),
                    null))));
        if(signatures)
        {
            for(var blank=0;blank<2;blank++)table.Add(new XElement(W+"tr",Enumerable.Range(0,2).Select(_=>new XElement(W+"tc",Paragraph("")))));
            table.Add(SignaturesTable(invoice,seller,buyer,width).Elements(W+"tr"));
        }
        return table;
    }
    public static bool IsBusinessHeading(string line)=>System.Text.RegularExpressions.Regex.IsMatch(line,@"^\d+\. [^\d]")
        ||line.StartsWith("Реквизиты ",StringComparison.Ordinal)||line is "Продавец" or "Покупатель" or "Поставщик и получатель платежа" or "Покупатель и плательщик" or "Грузоотправитель и поставщик" or "Грузополучатель и плательщик";
    private static XElement BusinessTable(IReadOnlyList<string[]> rows,string kind)
    {
        var widths=BusinessDocumentTemplates.ItemWidths(kind);
        if(widths.Length!=rows[0].Length)widths=BusinessDocumentTemplates.ItemWidths(IssueDocumentKinds.Contract);
        var table=new XElement(W+"tbl",new XElement(W+"tblPr",
            new XElement(W+"tblW",new XAttribute(W+"w",widths.Sum()),new XAttribute(W+"type","dxa")),
            new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),
            new XElement(W+"tblCellMar",new[]{"top","left","bottom","right"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"w",80),new XAttribute(W+"type","dxa")))),
            new XElement(W+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(edge=>new XElement(W+edge,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","D9D9D9"))))),
            new XElement(W+"tblGrid",widths.Select(width=>new XElement(W+"gridCol",new XAttribute(W+"w",width)))));
        for(var r=0;r<rows.Count;r++)
            table.Add(new XElement(W+"tr",new XElement(W+"trPr",r==0?new XElement(W+"tblHeader"):null),
                rows[r].Select((value,c)=>new XElement(W+"tc",new XElement(W+"tcPr",
                    new XElement(W+"tcW",new XAttribute(W+"w",widths[c]),new XAttribute(W+"type","dxa")),
                    new XElement(W+"vAlign",new XAttribute(W+"val","center")),r==0?new XElement(W+"shd",new XAttribute(W+"fill","E8EDF3")):null),Paragraph(value,r==0,kind==IssueDocumentKinds.Waybill?16:19)))));
        return table;
    }
    public static void SaveIssueSpecificationWord(InvoiceInfo invoice,string destination)
    {
        // Reuse the same source of grouped places; specifications belong to an issuance.
        var temporary=destination+".invoice.tmp";
        try
        {
            SaveWord(invoice,temporary);
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Update))
            {
                var entry=archive.GetEntry("word/document.xml")!;XDocument xml;
                using(var stream=entry.Open())xml=XDocument.Load(stream);
                var text=xml.Descendants(W+"t").First();text.Value=$"СПЕЦИФИКАЦИЯ № {invoice.Id}";
                entry.Delete();Add(archive,"word/document.xml",xml);SourceBusinessForms.Stamp(archive);
            }
            SourceBusinessForms.CheckExternalEdits(destination);File.Move(temporary,destination,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal static XElement SignaturesTable(InvoiceInfo invoice,string left,string right,int width=9600)
    {
        var labels=new[]{left,right};var parties=new[]{SignatureBlocks.Sender(invoice),SignatureBlocks.Recipient(invoice)};
        var table=new XElement(W+"tbl",new XElement(W+"tblPr",new XElement(W+"tblW",new XAttribute(W+"w",width),new XAttribute(W+"type","dxa")),new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),new XElement(W+"tblCellMar",new[]{"top","left","bottom","right"}.Select(e=>new XElement(W+e,new XAttribute(W+"w",0),new XAttribute(W+"type","dxa"))))),new XElement(W+"tblGrid",Enumerable.Range(0,2).Select(_=>new XElement(W+"gridCol",new XAttribute(W+"w",width/2)))));
        foreach(var signatureRow in new[]{0,1,2})
            table.Add(new XElement(W+"tr",new XElement(W+"trPr",new XElement(W+"cantSplit")),Enumerable.Range(0,2).Select(i=>{
                var paragraph=Paragraph(signatureRow==2?"________________":signatureRow==1?"":labels[i]+": "+parties[i].Position,false,20);
                paragraph.Element(W+"pPr")!.Add(new XElement(W+"jc",new XAttribute(W+"val",i==0?"left":"right")),signatureRow<2?new XElement(W+"keepNext"):null);
                return new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",width/2),new XAttribute(W+"type","dxa"))),signatureRow==0?new[]{paragraph,SignatureBlocks.NameParagraph(parties[i].Name,paragraph)}:new[]{paragraph});
            })));
        return table;
    }
    private static void AddPageNumberParts(ZipArchive archive)
    {
        XNamespace rel="http://schemas.openxmlformats.org/package/2006/relationships";
        Add(archive,"word/_rels/document.xml.rels",new XDocument(new XElement(rel+"Relationships",new XElement(rel+"Relationship",new XAttribute("Id","footer1"),new XAttribute("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer"),new XAttribute("Target","footer1.xml")))));
        Add(archive,"word/footer1.xml",new XDocument(new XElement(W+"ftr",new XElement(W+"p",new XElement(W+"pPr",new XElement(W+"jc",new XAttribute(W+"val","left"))),new XElement(W+"fldSimple",new XAttribute(W+"instr","PAGE"))))));
        var entry=archive.GetEntry("[Content_Types].xml")!;XDocument types;using(var stream=entry.Open())types=XDocument.Load(stream);entry.Delete();
        types.Root!.Add(new XElement(types.Root.Name.Namespace+"Override",new XAttribute("PartName","/word/footer1.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml")));Add(archive,"[Content_Types].xml",types);
    }
    public static void SaveSpecificationWord(SpecificationInfo specification,string destination)
    {
        var body=new XElement(W+"body",Paragraph("СПЕЦИФИКАЦИЯ № "+specification.Id,true,32),
            Paragraph(specification.Title,true,26),Paragraph("Отправитель: "+specification.Sender),
            Paragraph("Получатель: "+specification.Recipient),Paragraph("Дата изменения: "+specification.UpdatedAt),Paragraph(""));
        foreach(var line in specification.Body.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))body.Add(Paragraph(line));
        body.Add(Paragraph(""),Paragraph("Отправитель: ____________________    Получатель: ____________________"));
        SaveDocument(body,destination);
    }
    // Legacy authored text becomes a native package with its placeholders intact.
    // The editor can therefore protect automatic fields without replacing wording.
    internal static byte[] TextEditorTemplate(string kind,string text)
    {
        var path=Path.Combine(Path.GetTempPath(),"Tickets-editor-"+Guid.NewGuid().ToString("N")+".docx");
        try{var body=new XElement(W+"body",text.Replace("\r\n","\n").Replace('\r','\n').Split('\n').Select(line=>Paragraph(line)));SaveDocument(body,path,kind==IssueDocumentKinds.Waybill,kind==IssueDocumentKinds.Contract);return File.ReadAllBytes(path);}
        finally{if(File.Exists(path))File.Delete(path);}
    }
    internal static void SaveDocument(XElement body,string destination,bool landscape=false,bool pageNumbers=false)
    {
        body.Add(new XElement(W+"sectPr",pageNumbers?new XElement(W+"footerReference",new XAttribute(W+"type","default"),new XAttribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships")+"id","footer1")):null,new XElement(W+"pgSz",new XAttribute(W+"w",landscape?16838:11906),new XAttribute(W+"h",landscape?11906:16838),landscape?new XAttribute(W+"orient","landscape"):null),
            new XElement(W+"pgMar",new XAttribute(W+"top",1134),new XAttribute(W+"bottom",1134),new XAttribute(W+"left",1134),new XAttribute(W+"right",1134))));
        var document=new XDocument(new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"w",W),body));
        WordDocumentStructure.Normalize(document);WordDocumentStructure.Validate(document);
        SourceBusinessForms.CheckExternalEdits(destination);
        var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Update))
            {
                Add(archive,"[Content_Types].xml",new XDocument(new XElement(XName.Get("Types","http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XElement(XName.Get("Default","http://schemas.openxmlformats.org/package/2006/content-types"),new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(XName.Get("Default","http://schemas.openxmlformats.org/package/2006/content-types"),new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),
                    new XElement(XName.Get("Override","http://schemas.openxmlformats.org/package/2006/content-types"),new XAttribute("PartName","/word/document.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")))));
                Add(archive,"_rels/.rels",new XDocument(new XElement(XName.Get("Relationships","http://schemas.openxmlformats.org/package/2006/relationships"),
                    new XElement(XName.Get("Relationship","http://schemas.openxmlformats.org/package/2006/relationships"),new XAttribute("Id","rId1"),new XAttribute("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),new XAttribute("Target","word/document.xml")))));
                Add(archive,"word/document.xml",document);
                if(pageNumbers)AddPageNumberParts(archive);
                SourceBusinessForms.Stamp(archive);
            }
            File.Move(temporary,destination,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal static XElement Paragraph(string text,bool bold=false,int size=22)=>new(W+"p",
        new XElement(W+"pPr",new XElement(W+"spacing",new XAttribute(W+"after",80)),bold?new XElement(W+"keepNext"):null,text.Contains('\n')?new XElement(W+"keepLines"):null,new XElement(W+"widowControl")),
        new XElement(W+"r",new XElement(W+"rPr",new XElement(W+"rFonts",new XAttribute(W+"ascii","Arial"),new XAttribute(W+"hAnsi","Arial")),bold?new XElement(W+"b"):null,new XElement(W+"sz",new XAttribute(W+"val",size))),
            text.Replace("\r\n","\n").Replace('\r','\n').Split('\n').SelectMany((line,index)=>index==0
                ?new XElement[]{new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)}
                :new XElement[]{new(W+"br"),new(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)})));
    private static void Add(ZipArchive archive,string path,XDocument xml){using var stream=archive.CreateEntry(path).Open();xml.Save(stream);}
    private sealed class NaturalComparer:IComparer<string>
    {
        public static readonly NaturalComparer Instance=new();
        public int Compare(string? left,string? right)
        {
            if(ReferenceEquals(left,right))return 0;
            if(left is null)return -1;if(right is null)return 1;
            var a=0;var b=0;
            while(a<left.Length&&b<right.Length)
            {
                if(char.IsAsciiDigit(left[a])&&char.IsAsciiDigit(right[b]))
                {
                    var endA=a;while(endA<left.Length&&char.IsAsciiDigit(left[endA]))endA++;
                    var endB=b;while(endB<right.Length&&char.IsAsciiDigit(right[endB]))endB++;
                    var digitsA=left[a..endA].TrimStart('0');var digitsB=right[b..endB].TrimStart('0');
                    var comparison=digitsA.Length.CompareTo(digitsB.Length);
                    if(comparison==0)comparison=StringComparer.Ordinal.Compare(digitsA,digitsB);
                    if(comparison!=0)return comparison;
                    a=endA;b=endB;
                }
                else
                {
                    var comparison=left[a].CompareTo(right[b]);if(comparison!=0)return comparison;a++;b++;
                }
            }
            var lengthComparison=(left.Length-a).CompareTo(right.Length-b);
            return lengthComparison!=0?lengthComparison:StringComparer.Ordinal.Compare(left,right);
        }
    }
}
