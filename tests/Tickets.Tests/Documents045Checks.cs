using System.IO.Compression;
using System.Xml.Linq;
using Tickets.Core;

internal static class Documents045Checks
{
    private static readonly string[] Kinds=["invoice","contract","act","bill","ttn","specification"];
    public static void Register(Action<string,Action<Store>> test)
    {
        test("045 historical v044 TTN snapshot stays unchanged while future issues use approved form",store=>
        {
            using var source=typeof(Store).Assembly.GetManifestResourceStream("Tickets.Core.Resources.UniversalForms.ttn-v044.docx")!;
            using var bytes=new MemoryStream();source.CopyTo(bytes);var legacy=bytes.ToArray();
            var issue=Issue(store,"История ТТН 045");store.SaveCustomIssueForm(issue.EventId,issue.Id,"ttn",legacy);
            Require(store.CustomIssueTemplate(issue.EventId,issue.Id,"ttn")!.AsSpan().SequenceEqual(legacy),"historical TTN template upgraded on reopen");
            using(var filled=new ZipArchive(new MemoryStream(store.CustomIssueDocument(issue.EventId,issue.Id,"ttn")!.SourceDocument!)))
                Require(filled.GetEntry("customXml/tickets-ttn-045.xml") is null,"historical filled TTN upgraded on reopen");
            store.SaveRecipientForm(issue.Recipient,"ttn",legacy,"ttn.docx");
            var next=Issue(store,issue.Recipient);
            using var future=new ZipArchive(new MemoryStream(store.CustomIssueTemplate(next.EventId,next.Id,"ttn")!));
            Require(future.GetEntry("customXml/tickets-ttn-045.xml") is not null,"future TTN retains obsolete starter");
        });
        test("045 contract tail excludes and preserves a following appendix",store=>
        {
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var bytes=Doc("Last clause");
            using var output=new MemoryStream();output.Write(bytes);output.Position=0;
            using(var zip=new ZipArchive(output,ZipArchiveMode.Update,true))
            {
                var xml=Xml(bytes);var body=xml.Root!.Element(w+"body")!;
                XElement P(string text)=>new(w+"p",new XElement(w+"r",new XElement(w+"t",text)));
                body.Add(P("7. Реквизиты сторон"),new XElement(w+"tbl",new XElement(w+"tr",new XElement(w+"tc",P("Sender signature")),new XElement(w+"tc",P("Recipient signature")))));
                body.Add(new XElement(w+"p",new XElement(w+"r",new XElement(w+"br",new XAttribute(w+"type","page")))),P("Приложение № 1"),P("Appendix contents"));
                zip.GetEntry("word/document.xml")!.Delete();using var target=zip.CreateEntry("word/document.xml").Open();xml.Save(target);
            }
            var prepared=Xml(ContractPagination.Prepare(output.ToArray()));
            Require(prepared.Descendants(w+"br").Any(b=>(string?)b.Attribute(w+"type")=="page"),"appendix page break removed");
            var signature=prepared.Descendants(w+"bookmarkStart").Single(b=>(string?)b.Attribute(w+"name")==ContractPagination.SignatureBookmark).Parent!;
            Require(string.Concat(signature.Descendants(w+"t").Select(t=>t.Value))=="Recipient signature","appendix captured in signature range");
            var appendix=prepared.Descendants(w+"p").Single(p=>string.Concat(p.Descendants(w+"t").Select(t=>t.Value))=="Appendix contents");
            Require(appendix.Element(w+"pPr")?.Element(w+"keepNext") is null,"appendix keep properties changed");
        });
        test("045 approved TTN preserves landscape grid typography and contains data only inside source fields",store=>
        {
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var template=Store.DefaultRecipientForm("ttn")!.Content;
            using(var zip=new ZipArchive(new MemoryStream(template)))Require(zip.GetEntry("customXml/tickets-ttn-045.xml") is not null,"approved TTN template is missing");
            var source=Xml(template);var tables=source.Descendants(w+"body").Single().Elements(w+"tbl").ToArray();
            Require(tables.Length==2&&tables[0].Elements(w+"tr").Count()==34&&tables[1].Elements(w+"tr").Count()==37,"source two-sided grid changed");
            Require(source.Descendants(w+"sectPr").All(s=>(string?)s.Element(w+"pgSz")?.Attribute(w+"orient")=="landscape"&&(int?)s.Element(w+"pgMar")?.Attribute(w+"left")==907),"source paper or margins changed");
            var original=Issue(store,"Получатель ТТН 045");
            foreach(var count in new[]{1,3,7,12})
            {
                var invoice=original with {Tickets=Enumerable.Range(1,count).Select(n=>original.Tickets[0] with {Id=n,Amount=1000*n}).ToArray()};
                var filled=CustomDocumentForms.Fill(template,invoice,"ttn",new DateTime(2026,11,1));var xml=Xml(filled);
                Require(!CustomDocumentForms.Text(filled).Contains('{'),"unfilled TTN fields for "+count+" prices");
                Require(!xml.Root!.Element(w+"body")!.Elements(w+"p").Any(p=>p.Descendants(w+"t").Any(t=>t.Value.Contains(invoice.Title))),"extra event heading outside approved cells");
                var originalRows=source.Descendants(w+"trHeight").Select(x=>x.ToString()).ToArray();
                Require(xml.Descendants(w+"trHeight").Take(originalRows.Length).Select(x=>x.ToString()).SequenceEqual(originalRows),"TTN row heights removed or resized");
                var fonts=source.Descendants(w+"rFonts").Select(x=>x.ToString()).Distinct().Order().ToArray();
                Require(xml.Descendants(w+"rFonts").Select(x=>x.ToString()).Distinct().Order().SequenceEqual(fonts),"TTN fonts changed");
                var sizes=source.Descendants(w+"sz").Select(x=>(string?)x.Attribute(w+"val")).Distinct().Order().ToArray();
                Require(xml.Descendants(w+"sz").Select(x=>(string?)x.Attribute(w+"val")).Distinct().Order().SequenceEqual(sizes),"TTN font sizes changed");
                foreach(var amount in invoice.Tickets.Select(t=>t.Amount))Require(CustomDocumentForms.Text(filled).Contains(amount.ToString("F2",System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))),"price lost from TTN continuation");
                Require(CustomDocumentForms.Text(filled).Contains(invoice.Total.ToString("F2",System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))),"TTN grand total missing");
                Require(CustomDocumentForms.Fill(filled,invoice,"ttn",DateTime.Today).AsSpan().SequenceEqual(filled),"reopening a finished TTN regenerated or duplicated its pages");
            }
        });
        test("045 recipient document edits keep other recipients and historic issued packages independent",store=>
        {
            const string bank="Сбер проверка 045",other="Другая организация 045";
            foreach(var recipient in new[]{bank,other})
            {
                store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000045"));
                foreach(var kind in Kinds)store.SaveRecipientForm(recipient,kind,Doc(recipient+" original "+kind+" {recipient} {count}"),kind+".docx");
            }
            var oldBank=Issue(store,bank);var oldOther=Issue(store,other);
            var package=store.CreateRecipientPackage(bank,[(oldBank.EventId,oldBank.Id)]);
            var oldPackage=Kinds.ToDictionary(k=>k,k=>store.GetRecipientPackageDocument(package.Id,k).SourceDocument!);
            foreach(var kind in Kinds)store.SaveRecipientForm(bank,kind,Doc("Sber revised "+kind+" {recipient} {count}"),kind+".docx");
            var futureBank=Issue(store,bank);var futureOther=Issue(store,other);
            var reopened=new Store(store.Path);
            foreach(var kind in Kinds)
            {
                Require(reopened.CustomIssueDocument(futureBank.EventId,futureBank.Id,kind)!.Body.Contains("Sber revised"),"recipient edit not applied to its future issue: "+kind);
                Require(reopened.CustomIssueDocument(futureOther.EventId,futureOther.Id,kind)!.Body.StartsWith(other+" original"),"recipient edit leaked to another organization: "+kind);
                Require(reopened.CustomIssueDocument(oldBank.EventId,oldBank.Id,kind)!.Body.StartsWith(bank+" original"),"recipient edit rewrote historical issue: "+kind);
                Require(reopened.GetRecipientPackageDocument(package.Id,kind).SourceDocument!.AsSpan().SequenceEqual(oldPackage[kind]),"recipient edit rewrote saved package: "+kind);
                reopened.SaveCustomIssueForm(futureBank.EventId,futureBank.Id,kind,Doc("Only this issue "+kind+" {count}"));
                Require(CustomDocumentForms.Text(reopened.RecipientForm(bank,kind)!.Content).StartsWith("Sber revised"),"issue edit rewrote recipient form: "+kind);
                Require(reopened.CustomIssueDocument(oldOther.EventId,oldOther.Id,kind)!.Body.StartsWith(other+" original"),"issue edit leaked across recipients: "+kind);
            }
        });
        test("045 saved package keeps editable automatic fields separately from filled document",store=>
        {
            const string recipient="Комплект с полями 045";
            store.SaveOrganizationDetails(recipient,false,new(Name:recipient,Inn:"7700000045"));
            foreach(var kind in Kinds)store.SaveRecipientForm(recipient,kind,Doc("Template "+kind+" {recipient} {count}"),kind+".docx");
            var issue=Issue(store,recipient);var package=store.CreateRecipientPackage(recipient,[(issue.EventId,issue.Id)]);
            foreach(var kind in Kinds)
            {
                var before=store.GetRecipientPackageDocument(package.Id,kind);
                var template=store.GetRecipientPackageTemplate(package.Id,kind);
                Require(CustomDocumentForms.Text(template).Contains("{recipient}")&&!before.Body.Contains("{recipient}"),"filled data overwrote editable fields: "+kind);
                store.SaveRecipientPackageTemplate(package.Id,kind,Doc("Changed package "+kind+" {recipient} {count}"));
                var reopened=new Store(store.Path);
                Require(reopened.GetRecipientPackageDocument(package.Id,kind).Body=="Changed package "+kind+" "+recipient+" 1","package edit lost frozen invoice data: "+kind);
                Require(CustomDocumentForms.Text(reopened.GetRecipientPackageTemplate(package.Id,kind)).Contains("{count}"),"automatic fields lost after reopening: "+kind);
                Require(CustomDocumentForms.Text(reopened.RecipientForm(recipient,kind)!.Content).StartsWith("Template "),"package edit changed recipient's reusable form: "+kind);
                var filled=reopened.GetRecipientPackageDocument(package.Id,kind).SourceDocument!;
                try{reopened.SaveRecipientPackageTemplate(package.Id,kind,[1,2,3]);throw new Exception("invalid package template accepted");}catch(TicketException){}
                Require(reopened.GetRecipientPackageDocument(package.Id,kind).SourceDocument!.AsSpan().SequenceEqual(filled),"invalid template replaced the saved document: "+kind);
                var legacy=Doc("Previously saved finished document "+kind);reopened.SaveRecipientPackageDocument(package.Id,kind,legacy);
                Require(reopened.GetRecipientPackageTemplate(package.Id,kind).AsSpan().SequenceEqual(legacy),"legacy finished document regenerated from a newer template: "+kind);
            }
        });
        test("045 explicit event deletion also removes its package template and keeps a recoverable backup",store=>
        {
            var issue=Issue(store,"Удаление комплекта 045");var package=store.CreateRecipientPackage(issue.Recipient,[(issue.EventId,issue.Id)]);
            var before=store.GetRecipientPackageTemplate(package.Id,"invoice");
            store.DeleteEventsPermanently([issue.EventId]);
            Require(store.RecipientPackages(issue.Recipient).Count==0,"deleted event left its shared package");
            var backup=Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(store.Path)!,"Backups"),"tickets.db",SearchOption.AllDirectories).Single();
            Require(new Store(backup).GetRecipientPackageTemplate(package.Id,"invoice").AsSpan().SequenceEqual(before),"deletion backup lost automatic fields");
        });
    }
    private static InvoiceInfo Issue(Store store,string recipient)
    {
        var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
        return store.Invoice(show.Id,store.Issue(show.Id,[seat.Id],recipient,"DOC045-"+Guid.NewGuid().ToString("N")[..12],1));
    }
    private static byte[] Doc(string text)
    {
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){using var stream=zip.CreateEntry("word/document.xml").Open();new XDocument(new XElement(w+"document",new XElement(w+"body",new XElement(w+"p",new XElement(w+"r",new XElement(w+"t",text)))))).Save(stream);}
        return output.ToArray();
    }
    private static XDocument Xml(byte[] bytes){using var zip=new ZipArchive(new MemoryStream(bytes));using var input=zip.GetEntry("word/document.xml")!.Open();return XDocument.Load(input);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
}
