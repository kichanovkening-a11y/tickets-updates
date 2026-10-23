using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tickets.Core;

/// <summary>The same separate title and unbroken name used by Word and fallback printing.</summary>
public static class SignatureBlocks
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static (string Position,string Name) Sender(InvoiceInfo invoice)=>Identity(DocumentParties.Sender(invoice));
    public static (string Position,string Name) Recipient(InvoiceInfo invoice)=>Identity(invoice.RecipientDetails??new(),invoice.Recipient);
    private static (string Position,string Name) Identity(OrganizationDetails details,string fallback="")=>(details.Position,Name(string.IsNullOrWhiteSpace(details.Signatory)?fallback:details.Signatory));
    public static string Name(string value)
    {
        value=Regex.Replace(value.Trim(),@"\s+"," ");
        // Shorten only an unambiguous Russian full name. Other user-supplied wording is retained.
        var full=Regex.Match(value,@"^(?<surname>[А-ЯЁ][а-яё-]+) (?<first>[А-ЯЁ])[а-яё-]+ (?<middle>[А-ЯЁ])[а-яё-]+$");
        if(full.Success)value=full.Groups["surname"].Value+" "+full.Groups["first"].Value+"."+full.Groups["middle"].Value+".";
        value=Regex.Replace(value,@"(?<=[А-ЯЁ]\.)\s+(?=[А-ЯЁ]\.)","");
        return value.Replace(' ','\u00a0').Replace('-','\u2011');
    }
    internal static XElement NameParagraph(string name,XElement title)
    {
        var p=new XElement(W+"p",title.Element(W+"pPr") is {} pr?new XElement(pr):null,
            new XElement(W+"r",title.Descendants(W+"rPr").FirstOrDefault() is {} rPr?new XElement(rPr):null,
                new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),name)));
        var nameProps=p.Element(W+"pPr");if(nameProps is null){nameProps=new XElement(W+"pPr");p.AddFirst(nameProps);}
        if(nameProps.Element(W+"keepNext") is null)nameProps.Add(new XElement(W+"keepNext"));
        var titleProps=title.Element(W+"pPr");if(titleProps is null){titleProps=new XElement(W+"pPr");title.AddFirst(titleProps);}
        var spacing=titleProps.Element(W+"spacing");if(spacing is null){spacing=new XElement(W+"spacing");titleProps.Add(spacing);}spacing.SetAttributeValue(W+"after",0);
        return p;
    }
    internal static void FillTemplateSignatures(XDocument xml,InvoiceInfo invoice)
    {
        foreach(var row in xml.Descendants(W+"tr").ToArray())
        {
            // Only the identity row immediately above a signature baseline is a signature block.
            // This leaves full names in legal clauses, requisites, and bespoke bill grids intact.
            var next=row.ElementsAfterSelf(W+"tr").FirstOrDefault(r=>r.Descendants(W+"t").Any(t=>!string.IsNullOrWhiteSpace(t.Value)));
            if(next is null||!next.Elements(W+"tc").All(c=>Regex.IsMatch(string.Concat(c.Descendants(W+"t").Select(t=>t.Value)).Trim(),@"^_{8,}$")))continue;
            foreach(var cell in row.Elements(W+"tc"))foreach(var p in cell.Elements(W+"p").ToArray())
            {
                var text=string.Concat(p.Descendants(W+"t").Select(t=>t.Value));
                var token=Regex.Match(text,@"\{(?<party>sender|recipient)Signatory\}|\{receivedBy\}");
                if(!token.Success||p.Descendants(W+"drawing").Any())continue;
                var sender=token.Groups["party"].Value=="sender";var identity=sender?Sender(invoice):Recipient(invoice);
                var prefix=text[..token.Index].Replace("{senderPosition}",identity.Position).Replace("{recipientPosition}",identity.Position).TrimEnd();
                // The invoice starter contains only a name field; its position belongs above the name too.
                if(!text.Contains("Position}")&&!string.IsNullOrWhiteSpace(identity.Position))prefix+=" "+identity.Position;
                var suffix=text[(token.Index+token.Length)..];
                if(!string.IsNullOrWhiteSpace(suffix))continue; // A custom legal sentence is not an identity block.
                var name=NameParagraph(identity.Name,p);
                var props=p.Element(W+"pPr");var format=p.Descendants(W+"rPr").FirstOrDefault();
                p.ReplaceNodes(props is null?null:new XElement(props),new XElement(W+"r",format is null?null:new XElement(format),new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),prefix.Trim())));
                p.AddAfterSelf(name);
                var cellProps=cell.Element(W+"tcPr");if(cellProps is null){cellProps=new XElement(W+"tcPr");cell.AddFirst(cellProps);}cellProps.Elements(W+"vAlign").Remove();cellProps.Add(new XElement(W+"vAlign",new XAttribute(W+"val","top")));
                var pr=p.Element(W+"pPr");if(pr is null){pr=new XElement(W+"pPr");p.AddFirst(pr);}if(pr.Element(W+"keepNext") is null)pr.Add(new XElement(W+"keepNext"));
            }
        }
    }
}
