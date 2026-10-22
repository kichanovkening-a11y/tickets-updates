namespace Tickets.Core;

internal static class StandardDocumentForms
{
    public static byte[]? Resolve(string kind,byte[]? custom)
    {
        if(custom is not null)
        {
            // Only replace byte-identical built-in starters. A user's edited upload wins.
            using var original=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.SberForms.{kind}.docx");
            if(original is null)return custom;
            using var ms=new MemoryStream();original.CopyTo(ms);if(!custom.AsSpan().SequenceEqual(ms.ToArray()))return custom;
        }
        using var source=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.UniversalForms.{kind}.docx");
        if(source is null)return custom;
        using var output=new MemoryStream();source.CopyTo(output);return output.ToArray();
    }
    public static bool IsStandard(byte[] bytes)
    { foreach(var kind in new[]{"contract","act","bill","ttn","specification","invoice"}){var standard=Resolve(kind,null);if(standard is not null&&bytes.AsSpan().SequenceEqual(standard))return true;}return false; }
    public static byte[]? EditorTemplate(string kind)
    {
        using var source=typeof(Store).Assembly.GetManifestResourceStream($"Tickets.Core.Resources.BusinessForms.{kind}.docx");
        if(source is null)return null;using var output=new MemoryStream();source.CopyTo(output);return output.ToArray();
    }
}
