using Tickets.Core;
using System.IO.Compression;
using System.Xml.Linq;

internal static class RecipientPackage023Checks
{
    public static void Register(Action<string,Action<Store>> test)
    {
        test("023 custom bill preserves distinct identities legal text borders and image",s=>{
            var choice=s.Events().Select(e=>(Event:e,Seat:s.Seats(e.Id).FirstOrDefault(x=>x.Available))).First(x=>x.Seat is not null);
            var issue=s.Issue(choice.Event.Id,[choice.Seat!.Id],"Custom bill recipient","CUSTOM023",1);
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";XNamespace r="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var original=Store.DefaultRecipientForm("bill")!.Content;using var output=new MemoryStream();output.Write(original);output.Position=0;XElement expected;
            var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
            using(var z=new ZipArchive(output,ZipArchiveMode.Update,true))
            {
                var entry=z.GetEntry("word/document.xml")!;XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
                var table=xml.Root!.Element(w+"body")!.Elements(w+"tbl").Last();var rows=table.Elements(w+"tr").ToArray();
                rows[0].Elements(w+"tc").Last().Descendants(w+"t").First().Value="Director Distinct";
                rows[2].Elements(w+"tc").Last().Descendants(w+"t").First().Value="Accountant Different";
                table.Elements(w+"tblPr").First().Add(new XElement(w+"tblBorders",new XElement(w+"bottom",new XAttribute(w+"val","double"),new XAttribute(w+"sz",12))));
                var cell=rows[0].Elements(w+"tc").First();cell.Add(new XElement(w+"p",new XElement(w+"r",new XElement(w+"t","Additional binding legal text"),new XElement(w+"drawing",new XElement("{http://schemas.openxmlformats.org/drawingml/2006/main}blip",new XAttribute(r+"embed","customStamp023"))))));
                expected=new XElement(table);entry.Delete();using(var stream=z.CreateEntry("word/document.xml").Open())xml.Save(stream);
                using(var stream=z.CreateEntry("word/media/stamp023.png").Open())stream.Write(image);
                XNamespace rel="http://schemas.openxmlformats.org/package/2006/relationships";var relEntry=z.GetEntry("word/_rels/document.xml.rels");XDocument relationships;
                if(relEntry is not null){using(var stream=relEntry.Open())relationships=XDocument.Load(stream);relEntry.Delete();}else relationships=new XDocument(new XElement(rel+"Relationships"));
                relationships.Root!.Add(new XElement(rel+"Relationship",new XAttribute("Id","customStamp023"),new XAttribute("Type",r.NamespaceName+"/image"),new XAttribute("Target","media/stamp023.png")));
                using(var stream=z.CreateEntry("word/_rels/document.xml.rels").Open())relationships.Save(stream);
            }
            var filled=CustomDocumentForms.Fill(output.ToArray(),s.Invoice(choice.Event.Id,issue),"bill",DateTime.Today);
            using var result=new ZipArchive(new MemoryStream(filled),ZipArchiveMode.Read);using var document=result.GetEntry("word/document.xml")!.Open();var final=XDocument.Load(document).Root!.Element(w+"body")!.Elements(w+"tbl").Last();
            Check(expected.Descendants(w+"t").Select(x=>x.Value).SequenceEqual(final.Descendants(w+"t").Select(x=>x.Value)),"custom signature names or legal text were rewritten");
            Check(expected.Elements(w+"tr").Select(x=>x.Elements(w+"tc").Count()).SequenceEqual(final.Elements(w+"tr").Select(x=>x.Elements(w+"tc").Count())),"custom signature cells were removed");
            Check(XNode.DeepEquals(expected.Element(w+"tblPr")!.Element(w+"tblBorders"),final.Element(w+"tblPr")!.Element(w+"tblBorders")),"custom borders changed");
            Check(final.Descendants(w+"drawing").Single().Descendants().Any(x=>(string?)x.Attribute(r+"embed")=="customStamp023"),"custom drawing changed");
            using var stamp=new MemoryStream();using(var stream=result.GetEntry("word/media/stamp023.png")!.Open())stream.CopyTo(stamp);Check(stamp.ToArray().SequenceEqual(image),"custom image changed");
            using var relStream=result.GetEntry("word/_rels/document.xml.rels")!.Open();Check(XDocument.Load(relStream).Descendants().Any(x=>(string?)x.Attribute("Id")=="customStamp023"),"image relationship lost");
        });
        test("023 packages use active tickets reject returned-only and preserve return history",s=>{
            var choice=s.Events().Select(e=>(Event:e,Seats:s.Seats(e.Id).Where(x=>x.Available).Take(2).ToArray())).First(x=>x.Seats.Length==2);
            var issue=s.Issue(choice.Event.Id,choice.Seats.Select(x=>x.Id).ToArray(),"Return package recipient","RETURN023",1);
            var original=s.Invoice(choice.Event.Id,issue);s.Return([original.Tickets[0].Id]);var before=s.RecipientHistory("Return package recipient");
            var p=s.CreateRecipientPackage("Return package recipient",[(choice.Event.Id,issue)]);
            Check(p.Invoice.Tickets.Count==1&&p.Invoice.Total==original.Tickets[1].Amount,"returned ticket included in aggregate");
            Check(p.Invoice.Tickets.All(x=>x.ReturnedAt is null),"returned ticket in snapshot");
            Check(s.Invoice(choice.Event.Id,issue).Tickets.Count==2&&s.RecipientHistory("Return package recipient").SequenceEqual(before),"return history mutated");
            s.Return([original.Tickets[1].Id]);Reject(()=>s.CreateRecipientPackage("Return package recipient",[(choice.Event.Id,issue)]));
            Check(s.RecipientPackage(p.Id).Invoice.Tickets.Count==1,"later returns rewrote saved package");
        });
        test("023 package consolidates multiple issues of same show into one places group",s=>{
            var choices=s.Events().Select(e=>(Event:e,Seats:s.Seats(e.Id).Where(x=>x.Available).GroupBy(x=>new{x.Sector,x.Row,x.Amount}).FirstOrDefault(g=>g.Count()>=2)?.Take(2).ToArray())).Where(x=>x.Seats is not null).Take(2).ToArray();
            Check(choices.Length==2,"need two shows");var a=choices[0];var b=choices[1];
            s.SaveOrganizationDetails("Same show recipient",false,new(Name:"Same show recipient",Inn:"7701234567"));
            var one=s.Issue(a.Event.Id,[a.Seats![0].Id],"Same show recipient","SAMESHOW023",1);
            var two=s.Issue(a.Event.Id,[a.Seats![1].Id],"Same show recipient","SAMESHOW023",2);
            var three=s.Issue(b.Event.Id,[b.Seats![0].Id],"Same show recipient","SAMESHOW023",3);
            var before=s.RecipientHistory("Same show recipient");
            var package=s.CreateRecipientPackage("Same show recipient",[(a.Event.Id,one),(a.Event.Id,two),(b.Event.Id,three)]);
            Check(package.Issues.Count==3&&package.Invoice.PackageShows.Count==2,"issue membership or show grouping");
            var first=package.Invoice.PackageShows.Single(x=>x.EventId==a.Event.Id);var line=InvoiceDocuments.Lines(first).Single();
            Check(line.Count==2&&line.Seats.Contains(a.Seats[0].Number)&&line.Seats.Contains(a.Seats[1].Number),"same show grouped row places not combined");
            Check(InvoiceDocuments.Lines(package.Invoice).Count==2,"same show duplicated item groups");
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            foreach(var kind in new[]{"specification","invoice"})
            {
                var info=s.GetRecipientPackageDocument(package.Id,kind);using var z=new ZipArchive(new MemoryStream(info.SourceDocument!),ZipArchiveMode.Read);using var stream=z.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
                var table=xml.Root!.Element(w+"body")!.Elements(w+"tbl").First(t=>t.Descendants(w+"t").Any(x=>x.Value is "место" or "Места"));
                Check(table.Elements(w+"tr").Count(row=>row.Elements(w+"tc").Count()==1)==2,"same show duplicated heading "+kind);
                Check(table.Elements(w+"tr").Count(row=>row.Elements(w+"tc").Any(c=>string.Concat(c.Descendants(w+"t").Select(t=>t.Value))==line.Seats))==1,"merged places row missing "+kind);
            }
            Check(s.RecipientHistory("Same show recipient").SequenceEqual(before),"issues merged instead of document rows");
        });
        test("023 all forms signatures names gap line and package specification by show",s=>{
            var choices=s.Events().Select(e=>(Event:e,Seats:s.Seats(e.Id).Where(x=>x.Available).Take(2).ToArray())).Where(x=>x.Seats.Length==2).Take(2).ToArray();
            s.SaveOrganizationDetails("Package filled",false,new(Name:"Package filled",Inn:"7701234567",Signatory:"Buyer Name"));
            var members=choices.Select((x,i)=>(x.Event.Id,s.Issue(x.Event.Id,[x.Seats[0].Id,x.Seats[1].Id],"Package filled","PKF",i*2+1))).ToArray();
            var p=s.CreateRecipientPackage("Package filled",members);
            var specification=s.GetRecipientPackageDocument(p.Id,"specification");
            foreach(var show in choices)Check(specification.Body.Contains(show.Event.Title),"specification show missing");
            Check(InvoiceDocuments.Lines(p.Invoice).Sum(x=>x.Count)==4,"grouped places quantity");
            var latest=choices.OrderByDescending(x=>s.RecipientHistory("Package filled").First(h=>h.Issue.EventId==x.Event.Id).StartsAt).First();
            Check(p.Invoice.EventDate==s.Invoice(members.First(x=>x.Id==latest.Event.Id).Id,members.First(x=>x.Id==latest.Event.Id).Item2).EventDate,"latest act date");
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            foreach(var kind in new[]{"contract","bill","act","specification","ttn","invoice"})
            {
                var info=s.GetRecipientPackageDocument(p.Id,kind);using var z=new ZipArchive(new MemoryStream(info.SourceDocument!),ZipArchiveMode.Read);using var stream=z.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
                if(kind is "invoice" or "specification")
                {
                    var places=xml.Root!.Element(w+"body")!.Elements(w+"tbl").First(t=>t.Descendants(w+"t").Any(x=>x.Value is "Места" or "место"));
                    var headings=places.Elements(w+"tr").Where(row=>row.Elements(w+"tc").Count()==1).ToArray();
                    Check(headings.Length==choices.Length,"missing full-width show headings "+kind);
                    var sectors=p.Invoice.PackageShows.SelectMany(show=>show.Tickets.Select(t=>t.Sector)).ToHashSet();
                    var data=places.Elements(w+"tr").Where(row=>row.Elements(w+"tc").Count()==6&&row.Elements(w+"tc").ElementAt(kind=="specification"?2:1).Descendants(w+"t").Any(t=>int.TryParse(t.Value,out _))).ToArray();
                    Check(data.Length==InvoiceDocuments.Lines(p.Invoice).Count,"show lines merged "+kind);
                    Check(data.All(row=>sectors.Contains(string.Concat(row.Elements(w+"tc").First().Descendants(w+"t").Select(t=>t.Value)))),"show title repeated in narrow sector column "+kind);
                }
                var table=xml.Root!.Element(w+"body")!.Elements(w+"tbl").Last();
                var line=table.Elements(w+"tr").First(r=>r.Elements(w+"tc").All(c=>string.Concat(c.Descendants(w+"t").Select(t=>t.Value)).StartsWith("___")));
                Check(line.PreviousNode is XElement gap&&!gap.Descendants(w+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value)),"missing blank below names "+kind);
                Check(line.Elements(w+"tc").Count()==2,"signature baseline "+kind);
            }
        });
        test("recipient shared package snapshots issues without merging",s=>{
            var choices=s.Events().Select(e=>(Event:e,Seats:s.Seats(e.Id).Where(x=>x.Available).Take(2).ToArray())).Where(x=>x.Seats.Length==2).Take(2).ToArray();
            if(choices.Length<2)throw new Exception("Need two shows");
            var a=s.Issue(choices[0].Event.Id,[choices[0].Seats[0].Id],"Package recipient","PKG",1);
            var b=s.Issue(choices[1].Event.Id,[choices[1].Seats[0].Id],"Package recipient","PKG",2);
            var before=s.RecipientHistory("Package recipient");
            var package=s.CreateRecipientPackage("Package recipient",[(choices[0].Event.Id,a),(choices[1].Event.Id,b)]);
            Check(package.Invoice.Tickets.Count==2,"quantity");
            Check(package.Invoice.Total==before.Sum(x=>x.Issue.Total),"amount");
            Check(s.RecipientHistory("Package recipient").SequenceEqual(before),"issue history changed");
            var invoice=s.GetRecipientPackageDocument(package.Id,"invoice");
            Check(invoice.SourceDocument is not null,"invoice missing without requisites");
            Reject(()=>s.GetRecipientPackageDocument(package.Id,"contract"));
            var copy=new Store(s.Path).RecipientPackage(package.Id);
            Check(copy.Invoice.Tickets.Count==2&&copy.Issues.Count==2,"snapshot not persisted");
            s.UpdateInvoiceSender(choices[1].Event.Id,b,"Other sender");
            Reject(()=>s.CreateRecipientPackage("Package recipient",[(choices[0].Event.Id,a),(choices[1].Event.Id,b)]));
            Check(s.RecipientPackage(package.Id).Invoice.Sender==package.Invoice.Sender,"snapshot changed");
            Reject(()=>s.CreateRecipientPackage("Different recipient",[(choices[0].Event.Id,a)]));
            Reject(()=>s.CreateRecipientPackage("Package recipient",[]));
        });
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(TicketException){return;}throw new Exception("Expected rejection");}
}
